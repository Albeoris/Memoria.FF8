using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Bootstrap;

public static class GraphicsDispatcher
{
    public static string HostExecutable(string dataDirectory, GraphicsBackend backend)
    {
        string name = backend switch
        {
            GraphicsBackend.Direct3D11 => "dx11",
            GraphicsBackend.Vulkan => "vulkan",
            _ => throw new PlatformNotSupportedException($"No game host for {backend}. Try Auto.")
        };
        return Path.Combine(dataDirectory, "Game", name, OperatingSystem.IsWindows() ? "Memoria.FF8.exe" : "Memoria.FF8");
    }

    public static GraphicsBackend RequestedBackend(string[] args)
    {
        string? commandLine = Option(args, "--graphics-backend");
        if (commandLine is not null)
        {
            return GraphicsBackendSelection.Parse(commandLine);
        }
        string? settings = Option(args, "--settings-dir") ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_SETTINGS_DIRECTORY");
        string path = settings is null ? GraphicsBackendPreferences.DefaultPath : Path.Combine(settings, "ui-settings.json");
        return GraphicsBackendPreferences.Read(path);
    }

    public static string? Option(string[] args, string key)
    {
        string? value = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] != key)
            {
                continue;
            }
            if (++i == args.Length)
            {
                throw new ArgumentException($"Missing value for {key}.");
            }
            value = args[i];
        }
        return value;
    }

    public static ProcessStartInfo CreateStartInfo(string dataDirectory, GraphicsBackend backend, string[] args)
    {
        ProcessStartInfo start = new(HostExecutable(dataDirectory, backend))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        foreach (string argument in args)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment[ApplicationInstallation.DirectoryVariable] = Path.GetFullPath(dataDirectory);
        return start;
    }

    public static async Task<int> RunAsync(string dataDirectory, string[] args, TextWriter output, TextWriter error, CancellationToken token, Func<GraphicsBackend, ProcessStartInfo>? createStart = null, TimeSpan? initializationTimeout = null)
    {
        GraphicsBackend requested = RequestedBackend(args);
        IReadOnlyList<GraphicsBackend> order = GraphicsBackendSelection.InitializationOrder(requested, HostPlatforms.Current);
        string diagnostics = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY") ?? Path.Combine(ApplicationPaths.Current.Logs, "graphics", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(diagnostics);
        bool graphical = !args.Contains("--validate", StringComparer.Ordinal) && !(args.FirstOrDefault() is "--probe-steam-achievements" or "--import-save" or "--export-save" or "--headless-play" or "--headless-control" or "--auto-play" or "--menu-preview" or "--battle-preview");
        foreach (GraphicsBackend backend in order)
        {
            token.ThrowIfCancellationRequested();
            string attempt = Path.Combine(diagnostics, backend + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(attempt);
            string readyPath = Path.Combine(attempt, "ready.json");
            string failurePath = Path.Combine(attempt, "failure.json");
            ProcessStartInfo start = createStart?.Invoke(backend) ?? CreateStartInfo(dataDirectory, backend, args);
            start.Environment[ApplicationInstallation.DirectoryVariable] = Path.GetFullPath(dataDirectory);
            start.Environment["MEMORIA_FF8_HOST_READY_FILE"] = readyPath;
            start.Environment["MEMORIA_FF8_HOST_FAILURE_FILE"] = failurePath;
            start.Environment["MEMORIA_FF8_REQUESTED_GRAPHICS_BACKEND"] = requested.ToString();
            await error.WriteLineAsync($"Graphics initialization: requested={requested} attempt={backend}; diagnostics={attempt}");
            bool ready = false;
            bool initializationFailure = false;
            bool cancelled = false;
            int exitCode = 1;
            string? failure = null;
            using StreamWriter stdout = new(Path.Combine(attempt, "stdout.log")) { AutoFlush = true };
            using StreamWriter stderr = new(Path.Combine(attempt, "stderr.log")) { AutoFlush = true };
            using Process process = new() { StartInfo = start };
            Task? outputPump = null;
            Task? errorPump = null;
            IDisposable? owner = null;
            bool started = false;
            try
            {
                if (!process.Start())
                {
                    throw new IOException("The game host did not start.");
                }
                started = true;
                owner = OwnedChildProcess.Attach(process);
                outputPump = CopyAsync(process.StandardOutput, stdout, output);
                errorPump = CopyAsync(process.StandardError, stderr, error);
                Task exited = process.WaitForExitAsync(CancellationToken.None);
                Stopwatch timer = Stopwatch.StartNew();
                while (graphical && !ready && !exited.IsCompleted)
                {
                    token.ThrowIfCancellationRequested();
                    ready = ReadReady(readyPath, process.Id, backend);
                    if (ready)
                    {
                        break;
                    }
                    if (timer.Elapsed > (initializationTimeout ?? TimeSpan.FromSeconds(90)))
                    {
                        throw new TimeoutException($"{backend} did not complete graphics initialization and first presentation.");
                    }
                    await Task.Delay(50, token);
                }
                ready |= graphical && ReadReady(readyPath, process.Id, backend);
                if (ready)
                {
                    await output.WriteLineAsync($"Graphics ready: requested={requested} actual={backend}");
                    // After readiness this is a running game. Its exit must never trigger a fallback/restart.
                }
                await exited.WaitAsync(token);
                exitCode = process.ExitCode;
                if (graphical && !ready)
                {
                    failure = $"{backend} exited before graphics readiness (exit {exitCode}).";
                    initializationFailure = ReadInitializationFailure(failurePath, process.Id, backend);
                    if (exitCode == 0)
                    {
                        exitCode = 1;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                exitCode = 130;
                failure = "Game launch cancelled.";
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                initializationFailure = !started;
            }
            finally
            {
                owner?.Dispose();
                if (started && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
                if (outputPump is not null && errorPump is not null)
                {
                    await Task.WhenAll(outputPump, errorPump).WaitAsync(TimeSpan.FromSeconds(10));
                }
            }
            await File.WriteAllTextAsync(Path.Combine(attempt, "result.json"), JsonSerializer.Serialize(new { schemaVersion = 1, requested = requested.ToString(), actual = backend.ToString(), ready, exitCode, initializationFailure, cancelled, failure }, new JsonSerializerOptions { WriteIndented = true }));
            if (cancelled)
            {
                throw new OperationCanceledException(token);
            }
            if (ready || !graphical)
            {
                return exitCode;
            }
            await error.WriteLineAsync($"Host startup failed: {failure} Diagnostics: {attempt}");
            if (requested != GraphicsBackend.Auto || !initializationFailure)
            {
                if (initializationFailure)
                {
                    await error.WriteLineAsync("Try Auto to let the next launch choose a supported backend.");
                }
                return exitCode;
            }
        }
        await error.WriteLineAsync("No graphics host initialized. Update your GPU driver or inspect the recorded diagnostics.");
        return 1;
    }

    private static bool ReadReady(string path, int processId, GraphicsBackend backend)
    {
        if (!File.Exists(path))
        {
            return false;
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("processId").GetInt32() != processId || root.GetProperty("backend").GetString() != backend.ToString())
        {
            throw new InvalidDataException("Game host returned a readiness receipt for the wrong process or backend.");
        }
        return true;
    }

    private static bool ReadInitializationFailure(string path, int processId, GraphicsBackend backend)
    {
        if (!File.Exists(path))
        {
            return false;
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        return root.GetProperty("schemaVersion").GetInt32() == 1 && root.GetProperty("processId").GetInt32() == processId && root.GetProperty("backend").GetString() == backend.ToString() && root.GetProperty("graphicsInitialization").GetBoolean();
    }

    private static async Task CopyAsync(StreamReader source, TextWriter log, TextWriter console)
    {
        while (await source.ReadLineAsync() is string line)
        {
            await log.WriteLineAsync(line);
            await console.WriteLineAsync(line);
        }
    }
}
