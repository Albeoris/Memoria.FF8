using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Memoria.FF8.LinuxTesting;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource cancellation = new();
        ConsoleCancelEventHandler cancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;
        using PosixSignalRegistration? termination = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? Register(PosixSignal.SIGTERM, cancellation) : null;
        using PosixSignalRegistration? hangup = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? Register(PosixSignal.SIGHUP, cancellation) : null;
        try
        {
            return await Cli.RunAsync(args, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return 130;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }

    private static PosixSignalRegistration Register(PosixSignal signal, CancellationTokenSource cancellation)
    {
        return PosixSignalRegistration.Create(signal, context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        });
    }
}

public static class Cli
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (args.Length == 1 && args[0] == "probe")
        {
            Console.WriteLine(JsonSerializer.Serialize(TargetRunner.Probe()));
            return 0;
        }
        if (args.Length < 2)
        {
            throw new ArgumentException("Usage: Memoria.FF8.LinuxTesting probe | init ROOT | manifest ROOT | run REQUEST | stop RUN_DIRECTORY | plan REQUEST | sync REQUEST | commit REQUEST | accept REQUEST --archive ZIP");
        }
        if (args[0] == "accept" ? args.Length != 4 || args[2] != "--archive" : args.Length != 2)
        {
            throw new ArgumentException("Unexpected command arguments.");
        }
        string path = ExpandPath(args[1]);
        switch (args[0])
        {
            case "init":
                Deployment.Initialize(path);
                return 0;
            case "manifest":
                Console.WriteLine(JsonSerializer.Serialize(Deployment.Manifest(path)));
                return 0;
            case "run":
                return await TargetRunner.RunAsync(path, cancellationToken);
            case "stop":
                RequestStop(path);
                if (Directory.Exists(path))
                {
                    using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    deadline.CancelAfter(TimeSpan.FromSeconds(12));
                    while (!File.Exists(Path.Combine(path, "result.json")))
                    {
                        await Task.Delay(100, deadline.Token);
                    }
                }
                return 0;
            case "plan":
            case "sync":
            case "accept":
            case "commit":
                using (JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken)))
                {
                    JsonElement request = document.RootElement;
                    string root = ExpandPath(request.GetProperty("root").GetString() ?? throw new InvalidDataException("Missing deployment root."));
                    Dictionary<string, string> manifest = request.GetProperty("manifest").Deserialize<Dictionary<string, string>>() ?? throw new InvalidDataException("Missing deployment manifest.");
                    switch (args[0])
                    {
                        case "plan":
                            Console.WriteLine(JsonSerializer.Serialize(Deployment.Prepare(root, manifest)));
                            break;
                        case "sync":
                            Deployment.Sync(root, manifest, ExpandPath(request.GetProperty("source").GetString() ?? throw new InvalidDataException("Missing deployment source.")));
                            break;
                        case "accept":
                            Deployment.Accept(root, manifest, ExpandPath(args[3]));
                            break;
                        case "commit":
                            Deployment.Commit(root, manifest);
                            break;
                    }
                }
                return 0;
            default:
                throw new ArgumentException("Unknown operation: " + args[0]);
        }
    }

    public static void RequestStop(string runDirectory)
    {
        string path = Path.GetFullPath(runDirectory);
        DirectoryInfo run = new(path);
        DirectoryInfo? runs = run.Parent;
        DirectoryInfo? root = runs?.Parent;
        if (!Regex.IsMatch(run.Name, "^[a-zA-Z0-9-]+$") || runs?.Name != "runs" || root == null)
        {
            throw new ArgumentException("Stop requires a concrete run directory below runs.");
        }
        if (!run.Exists)
        {
            return;
        }
        string marker = Path.Combine(root.FullName, ".memoria-linux-test-owner");
        foreach (FileSystemInfo entry in new FileSystemInfo[] { run, runs, root, new FileInfo(marker), new FileInfo(Path.Combine(path, "stop.request")) })
        {
            if (entry.LinkTarget != null || (entry.Attributes & FileAttributes.ReparsePoint) != 0 && entry.Exists)
            {
                throw new InvalidDataException("Run control paths cannot be symlinks.");
            }
        }
        if (!File.Exists(marker) || File.ReadAllText(marker) != "memoria-linux-test-v1")
        {
            throw new InvalidDataException("Run directory has no recognized staging owner.");
        }
        File.WriteAllText(Path.Combine(path, "stop.request"), "stop");
    }

    private static string ExpandPath(string path)
    {
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal))
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Length > 2 ? path[2..] : "");
        }
        return Path.GetFullPath(path);
    }
}
