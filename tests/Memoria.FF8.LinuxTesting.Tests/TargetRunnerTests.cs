using System.Text.Json;
using Memoria.FF8.LinuxTesting;

namespace Memoria.FF8.LinuxTesting.Tests;

public sealed class TargetRunnerTests
{
    [Theory]
    [InlineData("deviceType = PHYSICAL_DEVICE_TYPE_CPU llvmpipe", "software")]
    [InlineData("PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU", "hardware")]
    [InlineData("GPU0: PHYSICAL_DEVICE_TYPE_CPU\nGPU1: PHYSICAL_DEVICE_TYPE_DISCRETE_GPU", "hardware")]
    [InlineData("unknown implementation", "unverified")]
    public void VulkanClassificationIdentifiesMixedAndSoftwareDevices(string summary, string expected)
    {
        Assert.Equal(expected, TargetRunner.ClassifyVulkan(summary));
    }

    [Fact]
    public async Task RejectsRunIdentifierTraversalBeforeCreatingOutput()
    {
        string directory = CreateDirectory();
        try
        {
            string request = Path.Combine(directory, "request.json");
            await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new { root = directory, runId = "../escape", mode = "Smoke", component = "Launcher" }), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<ArgumentException>(() => TargetRunner.RunAsync(request, TestContext.Current.CancellationToken));
            Assert.False(Directory.Exists(Path.Combine(directory, "runs")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [Trait("Platform", "Linux")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringDiagnosticCaptureWritesDurableResult(bool stopFile)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Requires a Linux executable fixture.");
        string directory = CreateDirectory();
        try
        {
            Deployment.Initialize(directory);
            string uname = Path.Combine(directory, "uname");
            string started = Path.Combine(directory, "started");
            await File.WriteAllTextAsync(uname, "#!/bin/sh\nprintf ready > \"$MEMORIA_TEST_STARTED\"\nexec /bin/sleep 60\n", TestContext.Current.CancellationToken);
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(uname, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            string request = Path.Combine(directory, "request.json");
            await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new { root = directory, runId = "cancel-test", mode = "Smoke", component = "Launcher", vulkan = "None", graphicalEnvironment = new Dictionary<string, string> { ["PATH"] = directory, ["MEMORIA_TEST_STARTED"] = started } }), TestContext.Current.CancellationToken);
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            Task<int> run = TargetRunner.RunAsync(request, cancellation.Token);
            string output = Path.Combine(directory, "runs", "cancel-test");
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            while (!File.Exists(started) && !run.IsCompleted)
            {
                await Task.Delay(10, deadline.Token);
            }
            Assert.True(File.Exists(started));
            if (stopFile)
            {
                await File.WriteAllTextAsync(Path.Combine(output, "stop.request"), "stop", TestContext.Current.CancellationToken);
            }
            else
            {
                cancellation.Cancel();
            }
            Assert.Equal(130, await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            using JsonDocument result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "result.json"), TestContext.Current.CancellationToken));
            Assert.Equal("cancelled", result.RootElement.GetProperty("termination").GetString());
            Assert.False(File.Exists(Path.Combine(output, "process.log")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task UnownedNonemptyRootIsRejectedBeforeCreatingRunOrState()
    {
        string directory = CreateDirectory();
        try
        {
            string request = Path.Combine(directory, "request.json");
            await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new { root = directory, runId = "unowned", mode = "Smoke", component = "Launcher" }), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => TargetRunner.RunAsync(request, TestContext.Current.CancellationToken));
            Assert.False(Directory.Exists(Path.Combine(directory, "runs")));
            Assert.False(Directory.Exists(Path.Combine(directory, "state")));
            Assert.True(File.Exists(request));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ProbeReportsMachineAndRuntimeWithoutAcceptingGameplay()
    {
        JsonElement probe = JsonSerializer.SerializeToElement(TargetRunner.Probe());
        Assert.False(string.IsNullOrWhiteSpace(probe.GetProperty("machine").GetString()));
        Assert.Contains(".NET", probe.GetProperty("dotnet").GetString());
    }

    [Fact]
    [Trait("Platform", "Linux")]
    public async Task PublishedModulesUseRootDirectoryAndFilePatternFromPayloadWorkingDirectory()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Requires Linux executable fixtures.");
        string directory = CreateDirectory();
        CancellationToken token = TestContext.Current.CancellationToken;
        try
        {
            Deployment.Initialize(directory);
            string payload = Path.Combine(directory, "payload");
            string tools = Path.Combine(directory, "tools");
            string commands = Path.Combine(directory, "commands");
            Directory.CreateDirectory(tools);
            Directory.CreateDirectory(commands);
            string[] projects = ["Memoria.FF8.Platform.Tests", "Memoria.FF8.Audio.Tests", "Memoria.Launcher.Tests", "Memoria.FF8.Presentation.Tests", "Memoria.FF8.LinuxTesting.Tests"];
            foreach (string project in projects)
            {
                string moduleDirectory = Path.Combine(payload, "tests", project);
                Directory.CreateDirectory(moduleDirectory);
                await File.WriteAllTextAsync(Path.Combine(moduleDirectory, project + ".dll"), "fixture", token);
            }
            await File.WriteAllTextAsync(Path.Combine(directory, "payload-manifest.json"), JsonSerializer.Serialize(Deployment.Manifest(payload)), token);
            string dotnet = Path.Combine(tools, "dotnet");
            string uname = Path.Combine(tools, "uname");
            await File.WriteAllTextAsync(dotnet, "#!/bin/sh\nprintf '%s\\n' \"$PWD\" \"$@\" > \"$MEMORIA_TEST_COMMANDS/$5\"\n", token);
            await File.WriteAllTextAsync(uname, "#!/bin/sh\nexit 0\n", token);
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(dotnet, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                File.SetUnixFileMode(uname, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            string request = Path.Combine(directory, "request.json");
            await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new { root = directory, runId = "module-contract", mode = "Smoke", component = "Game", vulkan = "None", fingerprint = "module-fixture", graphicalEnvironment = new Dictionary<string, string> { ["PATH"] = tools, ["MEMORIA_TEST_COMMANDS"] = commands } }), token);
            Assert.Equal(1, await TargetRunner.RunAsync(request, token));
            foreach (string project in projects)
            {
                string[] arguments = await File.ReadAllLinesAsync(Path.Combine(commands, project + ".dll"), token);
                Assert.Equal([payload, "test", "--root-directory", Path.Combine(payload, "tests", project), "--test-modules", project + ".dll", "--results-directory", Path.Combine(directory, "runs", "module-contract", project), "--no-progress"], arguments);
            }
            using JsonDocument result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "runs", "module-contract", "result.json"), token));
            Assert.Equal("passed", result.RootElement.GetProperty("portableTests").GetProperty("status").GetString());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [Trait("Platform", "Linux")]
    [InlineData(false, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false)]
    [InlineData(true, false, false, false, false, false, false)]
    [InlineData(true, true, true, false, false, false, false)]
    [InlineData(true, true, false, false, true, false, false)]
    [InlineData(true, true, false, false, true, true, false)]
    [InlineData(true, true, false, false, false, true, true)]
    [InlineData(true, true, false, true, false, false, false)]
    public async Task GameSmokeRequiresValidatedFirstPresentation(bool receipt, bool readinessLine, bool malformed, bool earlyExit, bool expectedPass, bool configureMediaTools, bool missingMediaTools)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Requires Linux executable fixtures.");
        string directory = CreateDirectory();
        CancellationToken token = TestContext.Current.CancellationToken;
        try
        {
            Deployment.Initialize(directory);
            string payload = Path.Combine(directory, "payload");
            string tools = Path.Combine(directory, "tools");
            string game = Path.Combine(directory, "game");
            Directory.CreateDirectory(payload);
            Directory.CreateDirectory(tools);
            Directory.CreateDirectory(game);
            string mediaTools = Path.Combine(directory, "media tools ' fixture");
            if (configureMediaTools && !missingMediaTools)
            {
                Directory.CreateDirectory(mediaTools);
            }
            Directory.CreateDirectory(Path.Combine(directory, "test-results"));
            await File.WriteAllTextAsync(Path.Combine(directory, "test-results", "game-fixture.json"), "{\"passed\":true}", token);
            string script = "#!/bin/sh\nprintf 'argument=<%s>\\n' \"$@\"\n";
            if (receipt)
            {
                string json = malformed ? "{}" : "{\"schemaVersion\":1,\"backend\":\"Vulkan\",\"processId\":42,\"capabilities\":{\"device\":\"fixture Vulkan device\"}}";
                script += "/bin/mkdir -p \"$MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY/Vulkan-fixture\"\nprintf '%s' '" + json + "' > \"$MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY/Vulkan-fixture/ready.json\"\n";
            }
            if (readinessLine)
            {
                script += "printf 'Graphics ready: requested=Auto actual=Vulkan\\n'\n";
            }
            script += earlyExit ? "exit 0\n" : "exec /bin/sleep 60\n";
            string executable = Path.Combine(payload, "game-fixture");
            string uname = Path.Combine(tools, "uname");
            await File.WriteAllTextAsync(executable, script, token);
            await File.WriteAllTextAsync(uname, "#!/bin/sh\nexit 0\n", token);
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(uname, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            await File.WriteAllTextAsync(Path.Combine(directory, "payload-manifest.json"), JsonSerializer.Serialize(Deployment.Manifest(payload)), token);
            string request = Path.Combine(directory, "request.json");
            await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new { root = directory, runId = "graphics-readiness", mode = "Smoke", smoke = true, timeoutSeconds = 1, component = "Game", gameHost = true, gameDirectory = game, gameExecutable = "game-fixture", mediaToolsDirectory = configureMediaTools ? mediaTools : null, vulkan = "None", fingerprint = "game-fixture", graphicalEnvironment = new Dictionary<string, string> { ["PATH"] = tools, ["DISPLAY"] = ":fixture" } }), token);
            Assert.Equal(expectedPass ? 0 : 1, await TargetRunner.RunAsync(request, token));
            using JsonDocument result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "runs", "graphics-readiness", "result.json"), token));
            JsonElement value = result.RootElement;
            if (missingMediaTools)
            {
                Assert.Contains("Configured media tools directory", value.GetProperty("error").GetString());
                Assert.False(File.Exists(Path.Combine(directory, "runs", "graphics-readiness", "leader-pid")));
                return;
            }
            string log = await File.ReadAllTextAsync(Path.Combine(directory, "runs", "graphics-readiness", "process.log"), token);
            string expectedArguments = "argument=<--game-dir>\nargument=<" + game + ">\n";
            if (configureMediaTools)
            {
                expectedArguments += "argument=<--media-tools>\nargument=<" + mediaTools + ">\n";
            }
            Assert.StartsWith(expectedArguments, log);
            Assert.Equal(configureMediaTools, log.Contains("argument=<--media-tools>", StringComparison.Ordinal));
            Assert.Equal(!earlyExit, value.GetProperty("processSurvivedSmoke").GetBoolean());
            Assert.Equal(receipt && readinessLine && !malformed, value.GetProperty("gameGraphicsReady").GetBoolean());
            Assert.False(value.GetProperty("gameplayAccepted").GetBoolean());
            Assert.False(value.GetProperty("nativeGpuAcceptance").GetBoolean());
            if (expectedPass)
            {
                Assert.Equal("fixture Vulkan device", value.GetProperty("gameGraphics").GetProperty("capabilities").GetProperty("device").GetString());
            }
            else
            {
                Assert.True(value.TryGetProperty("error", out _));
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-target-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
