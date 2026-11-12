using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record WorldLaunchSpecification(int Version, string Pipe, int ParentProcess, long ParentStarted, LaunchOptions Options, WindowTransferState Window);

/// <summary>A fresh host may initialize Steam only after an explicit release and its predecessor's exit.</summary>
internal sealed class WorldProcessHandoff : IDisposable
{
    internal static WorldProcessHandoff? Current { get; private set; }
    private readonly string directory;
    private readonly PipeStream pipe;
    private readonly CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(120));
    private readonly Process peer;
    private readonly WorldHandoffProtocol protocol;
    private readonly bool incoming;
    private bool disposed;
    private string? successorReceipt;
    public WorldLaunchSpecification Specification { get; }
    public bool IsReleased => protocol.IsReleased;
    public bool ReleaseReceived => protocol.ReleaseReceived;
    public bool IsCovered => protocol.IsCovered;
    public bool Committed => protocol.Committed;

    private WorldProcessHandoff(string directory, PipeStream pipe, Process peer, WorldLaunchSpecification specification, bool incoming = false)
    {
        this.directory = directory;
        this.incoming = incoming;
        this.pipe = pipe;
        this.peer = peer;
        Specification = specification;
        protocol = new WorldHandoffProtocol(pipe, peer.WaitForExitAsync(cancellation.Token), cancellation.Token);
    }

    internal static WorldProcessHandoff Start(LaunchOptions options, CombinedSessionSave state, WindowTransferState window)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria", "world-transfer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        NamedPipeServerStream? server = null;
        Process? child = null;
        try
        {
            string save = Path.Combine(directory, "destination.ffsave");
            CombinedSessionCodec.Save(save, state);
            string name = "Memoria-world-" + Guid.NewGuid().ToString("N");
            server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 4096, 4096);
            using Process parent = Process.GetCurrentProcess();
            LaunchOptions destination = options with { InitialState = new(), Ff7Debug = false, Ff7StartNewGame = false, StatePath = save, CrossoverStatePath = save, ValidateOnly = false };
            WorldLaunchSpecification spec = new(1, name, parent.Id, parent.StartTime.ToUniversalTime().Ticks, destination, window);
            string specification = Path.Combine(directory, "launch.json");
            File.WriteAllText(specification, JsonSerializer.Serialize(spec));
            ProcessStartInfo start = CreateStartInfo(specification);
            child = Process.Start(start) ?? throw new IOException("Could not start the destination game host.");
            WorldProcessHandoff handoff = new(directory, server, child, spec);
            handoff.connectionReady = handoff.WaitForConnectionAsync(server);
            return handoff;
        }
        catch
        {
            server?.Dispose();
            if (child is not null)
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                    child.WaitForExit();
                }
                child.Dispose();
            }
            Directory.Delete(directory, true);
            throw;
        }
    }

    private Task? connectionReady;
    internal bool DestinationReady => connectionReady?.IsCompletedSuccessfully == true && protocol.DestinationReady;

    internal static ProcessStartInfo CreateStartInfo(string specification)
    {
        string executable = Environment.ProcessPath ?? throw new NotSupportedException("The current game host executable is unavailable.");
        ProcessStartInfo start = new(executable) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
        }
        start.ArgumentList.Add("--world-handoff");
        start.ArgumentList.Add(specification);
        // A handoff never inherits the old Steam application's identity or launcher readiness files.
        foreach (string key in new[] { "SteamAppId", "SteamGameId", "SteamOverlayGameId", "MEMORIA_FF8_HOST_FAILURE_FILE", "MEMORIA_FF8_HOST_READY_FILE" })
        {
            start.Environment.Remove(key);
        }
        return start;
    }

    internal static WorldProcessHandoff Connect(string path)
    {
        WorldLaunchSpecification spec = JsonSerializer.Deserialize<WorldLaunchSpecification>(File.ReadAllText(path)) ?? throw new InvalidDataException("The world launch specification is empty.");
        if (spec.Version != 1)
        {
            throw new NotSupportedException("Unsupported world launch specification.");
        }
        Process parent = Process.GetProcessById(spec.ParentProcess);
        if (parent.StartTime.ToUniversalTime().Ticks != spec.ParentStarted || parent.HasExited)
        {
            parent.Dispose();
            throw new IOException("The originating game host has exited.");
        }
        NamedPipeClientStream client = new(".", spec.Pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            client.Connect(10000);
            WorldProcessHandoff handoff = new(Path.GetDirectoryName(Path.GetFullPath(path))!, client, parent, spec, incoming: true);
            Current = handoff;
            return handoff;
        }
        catch
        {
            client.Dispose();
            parent.Dispose();
            throw;
        }
    }

    private async Task WaitForConnectionAsync(NamedPipeServerStream server)
    {
        await server.WaitForConnectionAsync(cancellation.Token);
        await protocol.WaitForReadyAsync();
    }

    internal void ThrowIfFailed()
    {
        cancellation.Token.ThrowIfCancellationRequested();
        if (connectionReady?.IsCompleted == true)
        {
            connectionReady.GetAwaiter().GetResult();
        }
        protocol.ThrowIfFailed();
        if (peer.HasExited && !Committed && !ReleaseReceived)
        {
            throw new IOException("The other game host exited before the world transfer completed.");
        }
    }

    internal void AcknowledgeReady() => protocol.AcknowledgeReady();
    internal void AcknowledgeCover() => protocol.AcknowledgeCover();
    internal void Release(Action shutdownSteam)
    {
        if (!RegisterSuccessor())
        {
            return;
        }
        protocol.Release(shutdownSteam);
    }

    private bool RegisterSuccessor()
    {
        string? successorDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY");
        string? token = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_SUCCESSOR_TOKEN");
        if (string.IsNullOrEmpty(successorDirectory) || string.IsNullOrEmpty(token))
        {
            return true;
        }
        if (successorReceipt is null)
        {
            successorReceipt = Path.Combine(successorDirectory, Specification.ParentProcess + ".json");
            string temporary = successorReceipt + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { schemaVersion = 1, processId = Specification.ParentProcess, processStarted = Specification.ParentStarted, token, successorProcessId = peer.Id, successorStarted = peer.StartTime.ToUniversalTime().Ticks }));
            File.Move(temporary, successorReceipt, true);
        }
        string accepted = Path.Combine(successorDirectory, Specification.ParentProcess + ".accepted.json");
        if (!File.Exists(accepted))
        {
            return false;
        }
        using JsonDocument acknowledgement = JsonDocument.Parse(File.ReadAllText(accepted));
        JsonElement root = acknowledgement.RootElement;
        return root.GetProperty("schemaVersion").GetInt32() == 1 && root.GetProperty("processId").GetInt32() == Specification.ParentProcess && root.GetProperty("token").GetString() == token && root.GetProperty("successorProcessId").GetInt32() == peer.Id;
    }

    internal static void RecordExit(int exitCode)
    {
        string? successorDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY");
        string? token = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_SUCCESSOR_TOKEN");
        if (string.IsNullOrEmpty(successorDirectory) || string.IsNullOrEmpty(token))
        {
            return;
        }
        try
        {
            using Process process = Process.GetCurrentProcess();
            string receipt = Path.Combine(successorDirectory, process.Id + ".exit.json");
            string temporary = receipt + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { schemaVersion = 1, processId = process.Id, processStarted = process.StartTime.ToUniversalTime().Ticks, token, exitCode }));
            File.Move(temporary, receipt, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Could not record the game host exit: " + exception.Message);
        }
    }

    internal static IDisposable? RedirectOutput(string[] args)
    {
        string? successorDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY");
        if (args.Length != 2 || args[0] != "--world-handoff" || string.IsNullOrEmpty(successorDirectory))
        {
            return null;
        }
        return new SuccessorOutput(successorDirectory);
    }

    private sealed class SuccessorOutput : IDisposable
    {
        private readonly TextWriter previousOutput = Console.Out;
        private readonly TextWriter previousError = Console.Error;
        private readonly StreamWriter output;
        private readonly StreamWriter error;

        internal SuccessorOutput(string successorDirectory)
        {
            output = Open(successorDirectory, ".stdout.log");
            try
            {
                error = Open(successorDirectory, ".stderr.log");
            }
            catch
            {
                output.Dispose();
                throw;
            }
            Console.SetOut(output);
            Console.SetError(error);
        }

        private static StreamWriter Open(string directory, string suffix)
        {
            return new StreamWriter(new FileStream(Path.Combine(directory, Environment.ProcessId + suffix), FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete), new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        }

        public void Dispose()
        {
            Console.SetOut(previousOutput);
            Console.SetError(previousError);
            output.Dispose();
            error.Dispose();
        }
    }

    internal void CompleteIncoming()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        cancellation.Cancel();
        pipe.Dispose();
        if (Current == this)
        {
            Current = null;
        }
        if (!incoming && !Committed && !peer.HasExited)
        {
            peer.Kill(entireProcessTree: true);
            peer.WaitForExit();
        }
        if (!incoming && !Committed && successorReceipt is not null)
        {
            File.Delete(successorReceipt);
            File.Delete(Path.ChangeExtension(successorReceipt, ".accepted.json"));
        }
        peer.Dispose();
        cancellation.Dispose();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
