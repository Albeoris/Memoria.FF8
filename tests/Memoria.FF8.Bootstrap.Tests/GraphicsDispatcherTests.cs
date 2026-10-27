using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Bootstrap.Tests;

[CollectionDefinition("Bootstrap environment", DisableParallelization = true)]
public sealed class BootstrapEnvironmentCollection
{
}

[Collection("Bootstrap environment")]
public sealed class GraphicsDispatcherTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria bootstrap tests", Guid.NewGuid().ToString("N"));
    private readonly string? previousDiagnostics;

    public GraphicsDispatcherTests()
    {
        Directory.CreateDirectory(directory);
        previousDiagnostics = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY");
        Environment.SetEnvironmentVariable("MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY", Path.Combine(directory, "diagnostics"));
    }

    [Fact]
    public void BackendPrecedenceUsesCommandLineThenSelectedSettingsThenAuto()
    {
        string settings = Path.Combine(directory, "selected settings");
        string path = Path.Combine(settings, "ui-settings.json");
        Assert.Equal(GraphicsBackend.Auto, GraphicsDispatcher.RequestedBackend(["--settings-dir", settings]));
        GraphicsBackendPreferences.Write(path, GraphicsBackend.Vulkan);
        Assert.Equal(GraphicsBackend.Vulkan, GraphicsDispatcher.RequestedBackend(["--settings-dir", settings]));
        Assert.Equal(GraphicsBackend.Direct3D11, GraphicsDispatcher.RequestedBackend(["--settings-dir", settings, "--graphics-backend", "Direct3D11"]));
        Assert.Equal(GraphicsBackend.Auto, GraphicsDispatcher.RequestedBackend(["--settings-dir", settings, "--graphics-backend", "Auto"]));
        Assert.Equal(GraphicsBackend.Vulkan, GraphicsDispatcher.RequestedBackend(["--graphics-backend", "Auto", "--graphics-backend", "Vulkan"]));
        Assert.Throws<ArgumentException>(() => GraphicsDispatcher.RequestedBackend(["--graphics-backend"]));
        Assert.Throws<ArgumentException>(() => GraphicsDispatcher.RequestedBackend(["--graphics-backend", "OpenGL"]));
        Assert.Equal(GraphicsBackend.Direct3D12, GraphicsDispatcher.RequestedBackend(["--graphics-backend", "Direct3D12"]));
    }

    [Fact]
    public void ChildArgumentsAndPackageRootsArePreservedWithoutChangingCallerDirectory()
    {
        string data = Path.Combine(directory, "package with spaces", "Data");
        string[] arguments = ["--game-dir", Path.Combine(directory, "game files"), "--mods-dir", Path.Combine(directory, "Mods"), "--state", Path.Combine(directory, "saved state.json"), "--media-tools", Path.Combine(directory, "media tools"), "--graphics-backend", "Vulkan", "--achievements-local-only"];
        ProcessStartInfo start = GraphicsDispatcher.CreateStartInfo(data, GraphicsBackend.Vulkan, arguments);
        Assert.Equal(Path.Combine(data, "Game", "vulkan", HostPlatforms.Current.ExecutableName("Memoria.FF8")), start.FileName);
        Assert.Equal(arguments, start.ArgumentList);
        Assert.Equal(Environment.CurrentDirectory, start.WorkingDirectory);
        Assert.Equal(Path.GetFullPath(data), start.Environment[ApplicationInstallation.DirectoryVariable]);
        Assert.False(start.UseShellExecute);
        Assert.True(start.RedirectStandardOutput);
        Assert.True(start.RedirectStandardError);
        Assert.Throws<PlatformNotSupportedException>(() => GraphicsDispatcher.HostExecutable(data, GraphicsBackend.Direct3D12));
    }

    [Fact]
    public async Task AutoTriesVulkanWhenTheFirstHostIsMissing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows Auto has two graphics host candidates.");
        List<GraphicsBackend> attempts = [];
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await RunAsync(GraphicsBackend.Auto, backend =>
        {
            attempts.Add(backend);
            return backend == GraphicsBackend.Direct3D11 ? MissingHost() : Child(backend, ReadyScript + "\nexit 0");
        }, output, error);
        Assert.Equal(0, result);
        Assert.Equal([GraphicsBackend.Direct3D11, GraphicsBackend.Vulkan], attempts);
        Assert.Contains("Graphics ready: requested=Auto actual=Vulkan", output.ToString());
        Assert.Contains("Direct3D11", error.ToString());
        Assert.Equal(2, Directory.GetFiles(Path.Combine(directory, "diagnostics"), "result.json", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task ExplicitMissingHostFailsWithRecoveryAndNeverChangesBackend()
    {
        List<GraphicsBackend> attempts = [];
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await RunAsync(GraphicsBackend.Vulkan, backend =>
        {
            attempts.Add(backend);
            return MissingHost();
        }, output, error);
        Assert.NotEqual(0, result);
        Assert.Equal([GraphicsBackend.Vulkan], attempts);
        Assert.Contains("Try Auto", error.ToString());
        Assert.DoesNotContain("Graphics ready:", output.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public async Task UnclassifiedExitBeforeFirstPresentationNeverTriggersAutoFallback(int childExit)
    {
        List<GraphicsBackend> attempts = [];
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await RunAsync(GraphicsBackend.Auto, backend =>
        {
            attempts.Add(backend);
            return Child(backend, $"exit {childExit}");
        }, output, error);
        Assert.NotEqual(0, result);
        Assert.Single(attempts);
        Assert.DoesNotContain("Graphics ready:", output.ToString());
        Assert.Contains("before graphics readiness", error.ToString());
    }

    [Fact]
    public async Task OnlyClassifiedInitializationFailureCanFallbackAfterAHostStarts()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows Auto has two graphics host candidates.");
        List<GraphicsBackend> attempts = [];
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await RunAsync(GraphicsBackend.Auto, backend =>
        {
            attempts.Add(backend);
            return Child(backend, backend == GraphicsBackend.Direct3D11 ? """
                $failure = @{schemaVersion=1; processId=$PID; backend=$Backend; graphicsInitialization=$true; message='Synthetic device initialization failure'} | ConvertTo-Json -Compress
                [IO.File]::WriteAllText($env:MEMORIA_FF8_HOST_FAILURE_FILE + '.tmp', $failure)
                [IO.File]::Move($env:MEMORIA_FF8_HOST_FAILURE_FILE + '.tmp', $env:MEMORIA_FF8_HOST_FAILURE_FILE)
                exit 19
                """ : ReadyScript + "\nexit 0");
        }, output, error);
        Assert.Equal(0, result);
        Assert.Equal([GraphicsBackend.Direct3D11, GraphicsBackend.Vulkan], attempts);
        Assert.Contains("Graphics ready: requested=Auto actual=Vulkan", output.ToString());
    }

    [Theory]
    [InlineData("processId", "0")]
    [InlineData("backend", "'Direct3D12'")]
    [InlineData("graphicsInitialization", "$false")]
    public async Task AnUnmatchedFailureReceiptCannotAuthorizeFallback(string property, string value)
    {
        List<GraphicsBackend> attempts = [];
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await RunAsync(GraphicsBackend.Auto, backend =>
        {
            attempts.Add(backend);
            return Child(backend, $"$failure = @{{schemaVersion=1; processId=$PID; backend=$Backend; graphicsInitialization=$true; message='Synthetic failure'}}\n$failure['{property}'] = {value}\n" + """
                [IO.File]::WriteAllText($env:MEMORIA_FF8_HOST_FAILURE_FILE + '.tmp', ($failure | ConvertTo-Json -Compress))
                [IO.File]::Move($env:MEMORIA_FF8_HOST_FAILURE_FILE + '.tmp', $env:MEMORIA_FF8_HOST_FAILURE_FILE)
                exit 31
                """);
        }, output, error);
        Assert.NotEqual(0, result);
        Assert.Single(attempts);
        Assert.DoesNotContain("Graphics ready:", output.ToString());
    }

    [Fact]
    public async Task ValidationCommandsPreserveTheirExitCodeWithoutAGraphicsHandshake()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Vulkan", "--validate"], output, error, TestContext.Current.CancellationToken, backend => Child(backend, "[Console]::Out.WriteLine('validation output')\nexit 27"));
        Assert.Equal(27, result);
        Assert.Contains("validation output", output.ToString());
        Assert.DoesNotContain("Graphics ready:", output.ToString());
    }

    [Fact]
    public async Task GraphicsReadinessPreservesGameCrashExitCodeAndPreventsFallback()
    {
        List<GraphicsBackend> attempts = [];
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await RunAsync(GraphicsBackend.Auto, backend =>
        {
            attempts.Add(backend);
            return Child(backend, ReadyScript + "\n[Console]::Out.WriteLine('game stdout')\n[Console]::Error.WriteLine('game crash detail')\nexit 37");
        }, output, error);
        Assert.Equal(37, result);
        Assert.Single(attempts);
        Assert.Contains("Graphics ready: requested=Auto actual=" + attempts[0], output.ToString());
        Assert.Contains("game stdout", output.ToString());
        Assert.Contains("game crash detail", error.ToString());
        string receiptPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, "diagnostics"), "result.json", SearchOption.AllDirectories));
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        Assert.True(receipt.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal(37, receipt.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Theory]
    [InlineData("processId", "0")]
    [InlineData("backend", "'Direct3D12'")]
    [InlineData("schemaVersion", "2")]
    public async Task AReceiptForAnotherHostCannotConfirmGraphicsReadiness(string property, string value)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        string body = $"$receipt = @{{schemaVersion=1; processId=$PID; backend=$Backend}}\n$receipt['{property}'] = {value}\n" + WriteReadyReceiptScript + "\nStart-Sleep -Milliseconds 300\nexit 0";
        int result = await RunAsync(GraphicsBackend.Vulkan, backend => Child(backend, body), output, error);
        Assert.NotEqual(0, result);
        Assert.DoesNotContain("Graphics ready:", output.ToString());
        Assert.Contains("wrong process or backend", error.ToString());
    }

    [Fact]
    public async Task TimeoutBeforeReadinessStopsTheHostWithoutTryingAnotherBackend()
    {
        List<GraphicsBackend> attempts = [];
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Auto"], output, error, TestContext.Current.CancellationToken, backend =>
        {
            attempts.Add(backend);
            return Child(backend, "Start-Sleep -Seconds 120");
        }, TimeSpan.FromMilliseconds(200));
        Assert.NotEqual(0, result);
        Assert.Single(attempts);
        Assert.DoesNotContain("Graphics ready:", output.ToString());
    }

    [Fact]
    public async Task CancellationStopsTheGameAndItsDescendant()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows job ownership regression.");
        string marker = Path.Combine(directory, "child-processes.json");
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using StringWriter output = new();
        using StringWriter error = new();
        Task<int> run = GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Vulkan"], output, error, cancellation.Token, backend =>
        {
            ProcessStartInfo start = Child(backend, """
                $child = Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -WindowStyle Hidden -ArgumentList @('-NoProfile', '-NonInteractive', '-Command', 'Start-Sleep -Seconds 120') -PassThru
                $processes = @{parent=$PID; child=$child.Id} | ConvertTo-Json -Compress
                [IO.File]::WriteAllText($env:MEMORIA_TEST_PROCESS_MARKER + '.tmp', $processes)
                [IO.File]::Move($env:MEMORIA_TEST_PROCESS_MARKER + '.tmp', $env:MEMORIA_TEST_PROCESS_MARKER)
                Start-Sleep -Seconds 120
                """);
            start.Environment["MEMORIA_TEST_PROCESS_MARKER"] = marker;
            return start;
        }, TimeSpan.FromSeconds(30));
        try
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (!File.Exists(marker) && !run.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
            Assert.True(File.Exists(marker), "The synthetic host must start its descendant before cancellation.");
            using JsonDocument processes = JsonDocument.Parse(File.ReadAllText(marker));
            int parent = processes.RootElement.GetProperty("parent").GetInt32();
            int child = processes.RootElement.GetProperty("child").GetInt32();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
            string resultPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, "diagnostics"), "result.json", SearchOption.AllDirectories));
            using JsonDocument result = JsonDocument.Parse(File.ReadAllText(resultPath));
            Assert.Equal(130, result.RootElement.GetProperty("exitCode").GetInt32());
            Assert.True(result.RootElement.GetProperty("cancelled").GetBoolean());
            await AssertExitedAsync(parent);
            await AssertExitedAsync(child);
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is the expected dispatcher API result after ownership cleanup.
            }
        }
    }

    private Task<int> RunAsync(GraphicsBackend requested, Func<GraphicsBackend, ProcessStartInfo> factory, TextWriter output, TextWriter error)
    {
        return GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", requested.ToString()], output, error, TestContext.Current.CancellationToken, factory, TimeSpan.FromSeconds(15));
    }

    private ProcessStartInfo MissingHost() => new(Path.Combine(directory, "missing-host.exe")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };

    private ProcessStartInfo Child(GraphicsBackend backend, string body)
    {
        string script = Path.Combine(directory, backend + "-" + Guid.NewGuid().ToString("N") + ".ps1");
        File.WriteAllText(script, "param([string] $Backend)\n$ErrorActionPreference = 'Stop'\n" + body);
        ProcessStartInfo start = new("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script, backend.ToString() })
        {
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    private static async Task AssertExitedAsync(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        catch (ArgumentException)
        {
            // A reaped process is also a successful ownership cleanup.
        }
    }

    private const string WriteReadyReceiptScript = """
        [IO.File]::WriteAllText($env:MEMORIA_FF8_HOST_READY_FILE + '.tmp', ($receipt | ConvertTo-Json -Compress))
        [IO.File]::Move($env:MEMORIA_FF8_HOST_READY_FILE + '.tmp', $env:MEMORIA_FF8_HOST_READY_FILE)
        """;

    private const string ReadyScript = "$receipt = @{schemaVersion=1; processId=$PID; backend=$Backend}\n" + WriteReadyReceiptScript + "\nStart-Sleep -Milliseconds 250";

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY", previousDiagnostics);
        Directory.Delete(directory, recursive: true);
    }
}
