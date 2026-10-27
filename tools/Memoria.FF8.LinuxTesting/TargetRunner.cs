using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Memoria.FF8.LinuxTesting;

public static class TargetRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] TestProjects = ["Memoria.FF8.Platform.Tests", "Memoria.FF8.Audio.Tests", "Memoria.Launcher.Tests", "Memoria.FF8.Presentation.Tests", "Memoria.FF8.LinuxTesting.Tests"];

    public static object Probe() => new
    {
        machine = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        release = Environment.OSVersion.VersionString,
        home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        display = Environment.GetEnvironmentVariable("DISPLAY"),
        wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
        wslg = Directory.Exists("/mnt/wslg"),
        system = new { sysname = OperatingSystem.IsLinux() ? "Linux" : RuntimeInformation.OSDescription, nodename = Environment.MachineName, release = Environment.OSVersion.VersionString, version = RuntimeInformation.OSDescription, machine = RuntimeInformation.OSArchitecture.ToString() },
        dotnet = RuntimeInformation.FrameworkDescription
    };

    public static async Task<int> RunAsync(string requestPath, CancellationToken cancellationToken = default)
    {
        JsonObject request = JsonNode.Parse(await File.ReadAllTextAsync(requestPath, cancellationToken))!.AsObject();
        string root = Path.GetFullPath(Required(request, "root"));
        CheckOwnedPath(root, root);
        string runId = Required(request, "runId");
        if (!Regex.IsMatch(runId, "^[a-zA-Z0-9-]+$"))
        {
            throw new ArgumentException("Invalid run identifier.");
        }
        Deployment.Initialize(root);
        string output = Path.Combine(root, "runs", runId);
        CheckOwnedPath(root, output);
        if (Path.Exists(output))
        {
            throw new IOException("Run directory already exists.");
        }
        Directory.CreateDirectory(output);
        string payload = Path.Combine(root, "payload");
        string component = Required(request, "component");
        Dictionary<string, object?> result = new()
        {
            ["schemaVersion"] = 1, ["mode"] = Required(request, "mode"), ["component"] = component,
            ["gameplayAccepted"] = false, ["nativeGpuAcceptance"] = false, ["exitCode"] = 1,
            ["boundary"] = "Component startup validation only; gameplay and native GPU acceptance require inspection.", ["runId"] = runId
        };
        using CancellationTokenSource runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using CancellationTokenSource watcherCancellation = new();
        Task watcher = ObserveStopAsync(output, runCancellation, watcherCancellation.Token);
        CancellationToken token = runCancellation.Token;
        try
        {
            Dictionary<string, string> environment = CreateEnvironment(root, request, runId);
            await WriteJsonAsync(Path.Combine(output, "system.json"), new { probe = Probe(), runtime = environment.GetValueOrDefault("XDG_RUNTIME_DIR"), display = environment.GetValueOrDefault("DISPLAY"), wayland = environment.GetValueOrDefault("WAYLAND_DISPLAY") });
            string vulkan = request["vulkan"]?.GetValue<string>() ?? "Auto";
            Dictionary<string, object?> graphics = await GraphicsDiagnosticsAsync(output, environment, vulkan, token);
            result["graphics"] = graphics;
            await CaptureAsync("uname", ["-a"], output, environment, "kernel.txt", 15, token);
            if ((vulkan is "Software" or "Hardware") && graphics.TryGetValue("error", out object? graphicsError))
            {
                throw new InvalidOperationException(graphicsError?.ToString());
            }
            if (Flag(request, "diagnosticsOnly"))
            {
                result["portableTests"] = new { status = "not requested" };
                result["exitCode"] = 0;
            }
            else
            {
                Dictionary<string, string> expected = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(Path.Combine(root, "payload-manifest.json"), token))!;
                Dictionary<string, string> actual = Deployment.Manifest(payload);
                if (actual.Count != expected.Count || actual.Any(item => !expected.TryGetValue(item.Key, out string? hash) || hash != item.Value))
                {
                    throw new InvalidOperationException("Staged payload hashes do not match the verified build manifest.");
                }
                Dictionary<string, object?> portableTests = await PortableTestsAsync(payload, root, output, environment, Required(request, "fingerprint"), token);
                result["portableTests"] = portableTests;
                if (Equals(portableTests["status"], "failed"))
                {
                    throw new InvalidOperationException("Portable tests failed; inspect per-project test logs.");
                }
                await RunComponentAsync(request, result, payload, output, environment, component, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            result["exitCode"] = 130;
            result["termination"] = "cancelled";
        }
        catch (Exception error)
        {
            result["exitCode"] = 1;
            result["error"] = error.Message;
        }
        finally
        {
            await watcherCancellation.CancelAsync();
            await watcher;
            await WriteJsonAsync(Path.Combine(output, "result.json"), result);
            Console.WriteLine(JsonSerializer.Serialize(new { output, result }));
        }
        return (int)result["exitCode"]!;
    }

    private static async Task ObserveStopAsync(string output, CancellationTokenSource runCancellation, CancellationToken token)
    {
        try
        {
            while (true)
            {
                if (File.Exists(Path.Combine(output, "stop.request")))
                {
                    await runCancellation.CancelAsync();
                    return;
                }
                await Task.Delay(100, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private static Dictionary<string, string> CreateEnvironment(string root, JsonObject request, string runId)
    {
        Dictionary<string, string> environment = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(item => (string)item.Key, item => (string)item.Value!);
        environment["MEMORIA_LINUX_TEST_RUN_ID"] = runId;
        if (request["graphicalEnvironment"] is JsonObject graphical)
        {
            foreach ((string name, JsonNode? value) in graphical)
            {
                environment[name] = value!.GetValue<string>();
            }
        }
        if (!environment.TryGetValue("XDG_RUNTIME_DIR", out string? runtime))
        {
            runtime = "/run/user/" + ReadUserId();
            environment["XDG_RUNTIME_DIR"] = runtime;
        }
        environment.TryAdd("DBUS_SESSION_BUS_ADDRESS", "unix:path=" + runtime + "/bus");
        if (!HasDisplay(environment))
        {
            string? socket = Directory.Exists(runtime) ? Directory.EnumerateFileSystemEntries(runtime, "wayland-*").FirstOrDefault(path => !path.EndsWith(".lock", StringComparison.Ordinal)) : null;
            if (socket != null)
            {
                environment["WAYLAND_DISPLAY"] = Path.GetFileName(socket);
            }
            else if (Path.Exists("/tmp/.X11-unix/X0"))
            {
                environment["DISPLAY"] = ":0";
            }
        }
        foreach ((string name, string directory) in new[] { ("XDG_CONFIG_HOME", "config"), ("XDG_DATA_HOME", "data"), ("XDG_STATE_HOME", "state"), ("XDG_CACHE_HOME", "cache") })
        {
            environment[name] = Path.Combine(root, "state", directory);
            CheckOwnedPath(root, environment[name]);
            Directory.CreateDirectory(environment[name]);
        }
        return environment;
    }

    private static string ReadUserId()
    {
        string user = Environment.UserName;
        if (File.Exists("/etc/passwd"))
        {
            string[]? entry = File.ReadLines("/etc/passwd").Select(line => line.Split(':')).FirstOrDefault(parts => parts.Length > 2 && parts[0] == user);
            if (entry != null)
            {
                return entry[2];
            }
        }
        return "unknown";
    }

    public static string ClassifyVulkan(string summary)
    {
        string text = summary.ToLowerInvariant();
        if (text.Contains("physical_device_type_discrete_gpu") || text.Contains("physical_device_type_integrated_gpu"))
        {
            return "hardware";
        }
        return text.Contains("llvmpipe") || text.Contains("lavapipe") || text.Contains("physical_device_type_cpu") ? "software" : "unverified";
    }

    private static async Task<Dictionary<string, object?>> GraphicsDiagnosticsAsync(string output, Dictionary<string, string> environment, string mode, CancellationToken token)
    {
        List<Dictionary<string, object?>> inventory = [];
        List<string> software = [];
        foreach (string directory in new[] { "/usr/share/vulkan/icd.d", "/etc/vulkan/icd.d" })
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }
            foreach (string path in Directory.EnumerateFiles(directory, "*.json").Order())
            {
                try
                {
                    JsonNode? descriptor = JsonNode.Parse(await File.ReadAllTextAsync(path, token));
                    inventory.Add(new() { ["path"] = path, ["descriptor"] = descriptor });
                    if ((descriptor?["ICD"]?["library_path"]?.GetValue<string>() ?? "").Contains("lvp", StringComparison.OrdinalIgnoreCase))
                    {
                        software.Add(path);
                    }
                }
                catch (Exception error) when (error is IOException or JsonException)
                {
                    inventory.Add(new() { ["path"] = path, ["error"] = error.Message });
                }
            }
        }
        await WriteJsonAsync(Path.Combine(output, "vulkan-icds.json"), inventory);
        Dictionary<string, object?> result = new() { ["requested"] = mode, ["classification"] = "not tested", ["presentation"] = "not tested", ["memoriaVulkanAccepted"] = false };
        if (mode == "None")
        {
            return result;
        }
        if (mode == "Software")
        {
            if (software.Count != 1)
            {
                result["error"] = "Software Vulkan needs exactly one installed Lavapipe ICD descriptor; use explicit Ubuntu dependency setup.";
                return result;
            }
            environment["VK_ICD_FILENAMES"] = software[0];
            environment["VK_DRIVER_FILES"] = software[0];
            result["forcedIcd"] = software[0];
        }
        if (!Available("vulkaninfo", environment))
        {
            result["error"] = "vulkaninfo is unavailable; install normal distro vulkan-tools explicitly.";
            return result;
        }
        result["summaryExitCode"] = await CaptureAsync("vulkaninfo", ["--summary"], output, environment, "vulkan-summary.txt", 15, token);
        await CaptureAsync("vulkaninfo", [], output, environment, "vulkan-full.txt", 25, token);
        string summary = (await File.ReadAllTextAsync(Path.Combine(output, "vulkan-summary.txt"), token)).ToLowerInvariant();
        result["classification"] = ClassifyVulkan(summary);
        var devices = Regex.Matches(summary, @"gpu(\d+):([\s\S]*?)(?=gpu\d+:|\z)").Select(match => new { index = int.Parse(match.Groups[1].Value), hardware = ClassifyVulkan(match.Groups[2].Value) == "hardware" }).ToArray();
        result["devices"] = devices;
        if (mode == "Hardware" && !Equals(result["classification"], "hardware"))
        {
            result["error"] = "Hardware Vulkan requested, but no hardware device was identified.";
            return result;
        }
        if (Available("vkcube", environment) && HasDisplay(environment))
        {
            List<string> arguments = ["--c", "30"];
            var hardware = devices.FirstOrDefault(device => device.hardware);
            if (mode == "Hardware" && hardware == null)
            {
                result["error"] = "Hardware device index could not be established for the presentation probe.";
                return result;
            }
            if (hardware != null && mode != "Software")
            {
                result["probeGpuIndex"] = hardware.index;
                arguments.AddRange(["--gpu_number", hardware.index.ToString()]);
            }
            int? code = await CaptureAsync("vkcube", arguments, output, environment, "vulkan-presentation.txt", 25, token);
            result["presentationExitCode"] = code;
            result["presentation"] = code == 0 ? "passed" : "failed";
        }
        else
        {
            result["presentation"] = "unavailable: vkcube or graphical session missing";
        }
        return result;
    }

    private static async Task<Dictionary<string, object?>> PortableTestsAsync(string payload, string root, string output, Dictionary<string, string> environment, string fingerprint, CancellationToken token)
    {
        if (!Regex.IsMatch(fingerprint, "^[a-zA-Z0-9-]+$"))
        {
            throw new ArgumentException("Invalid build fingerprint.");
        }
        string receipt = Path.Combine(root, "test-results", fingerprint + ".json");
        CheckOwnedPath(root, receipt);
        if (File.Exists(receipt) && JsonNode.Parse(await File.ReadAllTextAsync(receipt, token))?["passed"]?.GetValue<bool>() == true)
        {
            return new() { ["status"] = "cached", ["fingerprint"] = fingerprint };
        }
        Dictionary<string, int?> results = [];
        foreach (string project in TestProjects)
        {
            string module = Path.Combine(payload, "tests", project, project + ".dll");
            if (!File.Exists(module))
            {
                throw new FileNotFoundException("Portable test module missing: " + project);
            }
            results[project] = await ProcessRunner.CaptureAsync("dotnet", ["test", "--root-directory", Path.GetDirectoryName(module)!, "--test-modules", Path.GetFileName(module), "--results-directory", Path.Combine(output, project), "--no-progress"], payload, environment, Path.Combine(output, project + ".log"), TimeSpan.FromSeconds(180), token);
        }
        bool passed = results.Values.All(code => code == 0);
        Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
        await WriteJsonAsync(receipt, new { passed, results, fingerprint });
        return new() { ["status"] = passed ? "passed" : "failed", ["results"] = results, ["fingerprint"] = fingerprint };
    }

    private static async Task RunComponentAsync(JsonObject request, Dictionary<string, object?> result, string payload, string output, Dictionary<string, string> environment, string component, CancellationToken token)
    {
        string? gameDirectory = request["gameDirectory"]?.GetValue<string>();
        if (gameDirectory != null && !Directory.Exists(gameDirectory))
        {
            throw new DirectoryNotFoundException("Configured game directory is not accessible. Explicitly expose the game data to this Linux target.");
        }
        List<string> arguments = [];
        string executable;
        if (component == "Audio")
        {
            string dls = Required(request, "dlsPath");
            string sgt = Required(request, "sgtPath");
            if (!File.Exists(dls) || !File.Exists(sgt))
            {
                throw new FileNotFoundException("Audio requires explicitly configured existing dlsPath and sgtPath fixtures.");
            }
            executable = Path.Combine(payload, "audio", "Memoria.FF8.Music");
            arguments.AddRange(["--play", dls, sgt, (request["audioSeconds"]?.GetValue<int>() ?? 10).ToString()]);
        }
        else if (component == "Game")
        {
            if (!Flag(request, "gameHost"))
            {
                throw new InvalidOperationException("Linux gameplay is unavailable until a supported game distribution recipe is committed.");
            }
            if (gameDirectory == null)
            {
                throw new ArgumentException("Select an existing legal game data directory.");
            }
            executable = Path.GetFullPath(Path.Combine(payload, Required(request, "gameExecutable")));
            if (!executable.StartsWith(payload + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new ArgumentException("Game executable must be inside the verified payload.");
            }
            arguments.AddRange(["--game-dir", gameDirectory]);
            string? mediaToolsDirectory = request["mediaToolsDirectory"]?.GetValue<string>();
            if (mediaToolsDirectory != null)
            {
                if (!Directory.Exists(mediaToolsDirectory))
                {
                    throw new DirectoryNotFoundException("Configured media tools directory is not accessible. Explicitly expose the media tools to this Linux target.");
                }
                arguments.AddRange(["--media-tools", mediaToolsDirectory]);
            }
            environment["MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY"] = Path.Combine(output, "game-graphics");
            result["boundary"] = "Game graphics startup smoke requires first presentation readiness; gameplay/native GPU acceptance requires human inspection.";
        }
        else
        {
            executable = Path.Combine(payload, "Memoria.Launcher");
            result["boundary"] = "Launcher process smoke; browse/dialog/render acceptance requires human inspection.";
            if (gameDirectory != null)
            {
                string settings = Path.Combine(environment["XDG_CONFIG_HOME"], "Memoria", "FF8", "launcher.json");
                CheckOwnedPath(environment["XDG_CONFIG_HOME"], settings);
                Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
                await WriteJsonAsync(settings, new { LastGameDirectory = gameDirectory, AchievementsLocalOnly = true });
            }
        }
        if (component != "Audio" && !HasDisplay(environment))
        {
            throw new InvalidOperationException("No existing graphical session found. Start WSLg or configure the hardware desktop session environment.");
        }
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(executable, File.GetUnixFileMode(executable) | UnixFileMode.UserExecute);
        }
        string[] screenshot = request["screenshotCommand"]?.Deserialize<string[]>() ?? [];
        if (screenshot.Length == 0)
        {
            screenshot = !string.IsNullOrEmpty(environment.GetValueOrDefault("WAYLAND_DISPLAY")) && Available("grim", environment) ? ["grim", "{output}"] : Available("spectacle", environment) ? ["spectacle", "-b", "-n", "-o", "{output}"] : !string.IsNullOrEmpty(environment.GetValueOrDefault("DISPLAY")) && Available("gnome-screenshot", environment) ? ["gnome-screenshot", "-f", "{output}"] : [];
        }
        result["screenshot"] = screenshot.Length > 0 ? "pending" : "unavailable: configure an existing ScreenshotCommand";
        int seconds = request["timeoutSeconds"]?.GetValue<int>() ?? 30;
        if (seconds <= 0)
        {
            throw new ArgumentException("Timeout must be positive.");
        }
        bool smoke = Flag(request, "smoke");
        Stopwatch elapsed = Stopwatch.StartNew();
        await using RunningProcess process = ProcessRunner.Start(executable, arguments, payload, environment, Path.Combine(output, "process.log"));
        await File.WriteAllTextAsync(Path.Combine(output, "leader-pid"), process.Id.ToString(), token);
        bool attempted = false;
        while (!process.HasExited && elapsed.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            token.ThrowIfCancellationRequested();
            if (screenshot.Length > 0 && elapsed.Elapsed.TotalSeconds > 3 && !attempted)
            {
                attempted = true;
                await File.WriteAllTextAsync(Path.Combine(output, "screenshot-attempt.txt"), "Configured screenshot command invoked; inspect screenshot.png manually.", token);
                string[] command = screenshot.Select(part => part.Replace("{output}", Path.Combine(output, "screenshot.png"), StringComparison.Ordinal)).ToArray();
                int? code = await CaptureAsync(command[0], command.Skip(1), output, environment, "screenshot.log", 15, token);
                result["screenshot"] = code == 0 && File.Exists(Path.Combine(output, "screenshot.png")) ? "captured" : "failed: inspect screenshot.log";
            }
            await Task.Delay(200, token);
        }
        result["elapsedSeconds"] = Math.Round(elapsed.Elapsed.TotalSeconds, 2);
        bool survived = !process.HasExited;
        result["processSurvivedSmoke"] = survived;
        int? processExitCode = survived ? null : await process.WaitAsync(token);
        bool gameReady = component != "Game" || ReadGameGraphicsReadiness(output, result);
        if (component == "Game")
        {
            result["gameGraphicsReady"] = gameReady;
        }
        result["termination"] = survived ? smoke ? "smoke deadline" : "launch timeout" : "process exit";
        if (survived)
        {
            result["exitCode"] = smoke ? gameReady ? 0 : 1 : 124;
            if (smoke && !gameReady)
            {
                result["error"] = "Game process survived without completing graphics initialization and first presentation; inspect process.log and game-graphics for startup prompts or failures.";
            }
        }
        else
        {
            int code = processExitCode!.Value;
            result["exitCode"] = component == "Audio" || !smoke ? code : code == 0 ? 1 : code;
            if (component != "Audio" && smoke && code == 0)
            {
                result["error"] = "Launcher/game exited before the smoke interval; sustained startup was not established.";
            }
            if ((int)result["exitCode"]! != 0)
            {
                string text = await File.ReadAllTextAsync(Path.Combine(output, "process.log"), token);
                result["errorTail"] = text.Length > 4000 ? text[^4000..] : text;
            }
        }
    }

    private static bool ReadGameGraphicsReadiness(string output, Dictionary<string, object?> result)
    {
        string diagnostics = Path.Combine(output, "game-graphics");
        if (!Directory.Exists(diagnostics))
        {
            return false;
        }
        string log = File.ReadAllText(Path.Combine(output, "process.log"));
        if (!Regex.IsMatch(log, @"(?m)^Graphics ready: requested=(Auto|Vulkan) actual=Vulkan\r?$"))
        {
            return false;
        }
        foreach (string attempt in Directory.EnumerateDirectories(diagnostics, "Vulkan-*"))
        {
            string path = Path.Combine(attempt, "ready.json");
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement receipt = document.RootElement;
                if (receipt.GetProperty("schemaVersion").GetInt32() != 1 || receipt.GetProperty("backend").GetString() != "Vulkan" || receipt.GetProperty("processId").GetInt32() <= 0 || receipt.GetProperty("capabilities").ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                result["gameGraphics"] = new { receipt = Path.GetRelativePath(output, path), backend = "Vulkan", processId = receipt.GetProperty("processId").GetInt32(), capabilities = receipt.GetProperty("capabilities").Clone() };
                return true;
            }
            catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                result["gameGraphicsReceiptError"] = error.Message;
            }
        }
        return false;
    }

    private static string Required(JsonObject request, string name) => request[name]?.GetValue<string>() ?? throw new ArgumentException("Missing request property: " + name);
    private static void CheckOwnedPath(string root, string path)
    {
        string? current = path;
        while (current != null)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Staging symlinks are unsupported; select a fresh target directory.");
            }
            if (current == root)
            {
                return;
            }
            current = Path.GetDirectoryName(current);
        }
        throw new ArgumentException("Output must be inside the staging directory.");
    }
    private static bool Flag(JsonObject request, string name) => request[name]?.GetValue<bool>() ?? false;
    private static bool HasDisplay(Dictionary<string, string> environment) => !string.IsNullOrEmpty(environment.GetValueOrDefault("DISPLAY")) || !string.IsNullOrEmpty(environment.GetValueOrDefault("WAYLAND_DISPLAY"));
    private static bool Available(string name, Dictionary<string, string> environment) => (environment.GetValueOrDefault("PATH") ?? "").Split(Path.PathSeparator).Any(directory => File.Exists(Path.Combine(directory, name)));
    private static Task<int?> CaptureAsync(string executable, IEnumerable<string> arguments, string output, Dictionary<string, string> environment, string log, int seconds, CancellationToken token) => ProcessRunner.CaptureAsync(executable, arguments, output, environment, Path.Combine(output, log), TimeSpan.FromSeconds(seconds), token);
    private static async Task WriteJsonAsync(string path, object value)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
