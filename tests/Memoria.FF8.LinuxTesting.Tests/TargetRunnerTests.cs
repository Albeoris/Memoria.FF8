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

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-target-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
