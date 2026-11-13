using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Bootstrap;

/// <summary>Keeps dispatcher ownership while each graphics process exits and its already-started successor takes over.</summary>
internal static class GraphicsHostSuccessor
{
    internal const string DirectoryVariable = "MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY";
    internal const string TokenVariable = "MEMORIA_FF8_HOST_SUCCESSOR_TOKEN";

    internal sealed record HostExit(int ProcessId, int ExitCode);
    private sealed record Receipt(int SchemaVersion, int ProcessId, long ProcessStarted, string Token, int SuccessorProcessId, long SuccessorStarted);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal static async Task<HostExit> WaitAsync(Process initial, string directory, string token, TextWriter stdout, TextWriter stderr, TextWriter output, TextWriter error, CancellationToken cancellation)
    {
        Process current = initial;
        HashSet<int> visited = [initial.Id];
        Dictionary<int, SuccessorOutput> relays = [];
        Process? pending = null;
        try
        {
            while (true)
            {
                Task exited = current.WaitForExitAsync(CancellationToken.None);
                while (!exited.IsCompleted)
                {
                    cancellation.ThrowIfCancellationRequested();
                    Process? previousPending = pending;
                    int? previousPendingId = pending?.Id;
                    pending = ReadSuccessor(current, directory, token, pending, visited, predecessorRunning: true);
                    if (previousPendingId is int retired && !ReferenceEquals(previousPending, pending))
                    {
                        Retire(retired, directory, relays);
                    }
                    if (pending is not null && !relays.ContainsKey(pending.Id))
                    {
                        relays.Add(pending.Id, new SuccessorOutput(directory, pending.Id));
                    }
                    foreach (SuccessorOutput relay in relays.Values)
                    {
                        await relay.DrainAsync(stdout, stderr, output, error);
                    }
                    await Task.WhenAny(exited, Task.Delay(50, cancellation));
                }
                await exited.WaitAsync(cancellation);
                pending = ReadSuccessor(current, directory, token, pending, visited, predecessorRunning: false);
                foreach (SuccessorOutput relay in relays.Values)
                {
                    await relay.DrainAsync(stdout, stderr, output, error);
                }
                int exitCode = ReadExitCode(current, ReferenceEquals(current, initial), directory, token);
                Retire(current.Id, directory, relays);
                if (pending is null || exitCode != 0)
                {
                    return new HostExit(current.Id, exitCode);
                }
                visited.Add(pending.Id);
                await output.WriteLineAsync($"World transfer: following game host {current.Id} -> {pending.Id}.");
                if (!ReferenceEquals(current, initial))
                {
                    current.Dispose();
                }
                current = pending;
                pending = null;
            }
        }
        finally
        {
            pending?.Dispose();
            if (!ReferenceEquals(current, initial))
            {
                current.Dispose();
            }
            foreach (SuccessorOutput relay in relays.Values)
            {
                relay.Dispose();
            }
        }
    }

    private static void Retire(int processId, string directory, Dictionary<int, SuccessorOutput> relays)
    {
        if (relays.Remove(processId, out SuccessorOutput? relay))
        {
            relay.Dispose();
        }
        File.Delete(Path.Combine(directory, processId + ".json"));
        File.Delete(Path.Combine(directory, processId + ".accepted.json"));
        File.Delete(Path.Combine(directory, processId + ".exit.json"));
    }

    private sealed class SuccessorOutput(string directory, int processId) : IDisposable
    {
        private FileStream? stdout;
        private FileStream? stderr;
        private readonly Decoder outputDecoder = Encoding.UTF8.GetDecoder();
        private readonly Decoder errorDecoder = Encoding.UTF8.GetDecoder();
        private readonly byte[] bytes = new byte[4096];
        private readonly char[] characters = new char[Encoding.UTF8.GetMaxCharCount(4096)];

        internal async Task DrainAsync(TextWriter outputLog, TextWriter errorLog, TextWriter output, TextWriter error)
        {
            stdout ??= Open("stdout");
            stderr ??= Open("stderr");
            await DrainAsync(stdout, outputDecoder, outputLog, output);
            await DrainAsync(stderr, errorDecoder, errorLog, error);
        }

        private FileStream? Open(string name)
        {
            string path = Path.Combine(directory, processId + "." + name + ".log");
            return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete) : null;
        }

        private async Task DrainAsync(FileStream? source, Decoder decoder, TextWriter log, TextWriter console)
        {
            if (source is null)
            {
                return;
            }
            int length;
            while ((length = await source.ReadAsync(bytes.AsMemory())) > 0)
            {
                int decoded = decoder.GetChars(bytes, 0, length, characters, 0, flush: false);
                await log.WriteAsync(characters.AsMemory(0, decoded));
                await console.WriteAsync(characters.AsMemory(0, decoded));
            }
        }

        public void Dispose()
        {
            stdout?.Dispose();
            stderr?.Dispose();
            File.Delete(Path.Combine(directory, processId + ".stdout.log"));
            File.Delete(Path.Combine(directory, processId + ".stderr.log"));
        }
    }

    private static int ReadExitCode(Process process, bool initial, string directory, string token)
    {
        if (initial || OperatingSystem.IsWindows())
        {
            return process.ExitCode;
        }
        // Unix cannot reap a grandchild. Its host publishes its result before leaving the owned session.
        string path = Path.Combine(directory, process.Id + ".exit.json");
        if (!File.Exists(path))
        {
            throw new InvalidDataException("The replacement game host exited without a completion receipt.");
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("processId").GetInt32() != process.Id || root.GetProperty("processStarted").GetInt64() != process.StartTime.ToUniversalTime().Ticks || root.GetProperty("token").GetString() != token)
        {
            throw new InvalidDataException("The replacement game host completion receipt does not belong to this process.");
        }
        return root.GetProperty("exitCode").GetInt32();
    }

    private static Process? ReadSuccessor(Process process, string directory, string token, Process? pending, HashSet<int> visited, bool predecessorRunning)
    {
        int processId = process.Id;
        string path = Path.Combine(directory, processId + ".json");
        if (!File.Exists(path))
        {
            pending?.Dispose();
            return null;
        }
        Receipt receipt;
        try
        {
            FileInfo file = new(path);
            if (file.LinkTarget is not null || file.Length > 16384)
            {
                throw new InvalidDataException("The game host successor receipt is invalid.");
            }
            receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("The game host successor receipt is empty.");
        }
        catch (FileNotFoundException)
        {
            // A failed transfer may withdraw its request while the running host continues.
            pending?.Dispose();
            return null;
        }
        // Unix may already have reaped an initial host that never requested a transfer.
        // Query its start time only when a receipt requires process identity validation.
        if (receipt.SchemaVersion != 1 || receipt.ProcessId != processId || receipt.ProcessStarted != process.StartTime.ToUniversalTime().Ticks || receipt.Token != token || receipt.SuccessorProcessId <= 0 || visited.Contains(receipt.SuccessorProcessId))
        {
            throw new InvalidDataException("The game host successor receipt does not belong to this game process.");
        }
        if (pending is not null && pending.Id == receipt.SuccessorProcessId && pending.StartTime.ToUniversalTime().Ticks == receipt.SuccessorStarted)
        {
            return pending;
        }
        pending?.Dispose();
        Process? successor = null;
        try
        {
            successor = Process.GetProcessById(receipt.SuccessorProcessId);
            // Acquire a durable operating-system handle before querying process facts or acknowledging.
            _ = successor.SafeHandle;
            if (successor.HasExited && predecessorRunning)
            {
                successor.Dispose();
                return null;
            }
            if (successor.StartTime.ToUniversalTime().Ticks != receipt.SuccessorStarted)
            {
                throw new InvalidDataException("The replacement game host process identity has changed.");
            }
            string accepted = Path.Combine(directory, processId + ".accepted.json");
            string temporary = accepted + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { schemaVersion = 1, processId, successorProcessId = successor.Id, token }));
            File.Move(temporary, accepted, overwrite: true);
            return successor;
        }
        catch (ArgumentException) when (predecessorRunning && successor is null)
        {
            return null;
        }
        catch (Exception exception) when (predecessorRunning && (exception is InvalidOperationException or Win32Exception) && SuccessorHasExited(receipt.SuccessorProcessId))
        {
            successor?.Dispose();
            return null;
        }
        catch
        {
            successor?.Dispose();
            throw;
        }
    }

    private static bool SuccessorHasExited(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
