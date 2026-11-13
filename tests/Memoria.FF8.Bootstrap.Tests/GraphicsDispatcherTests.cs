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
    private readonly List<BootstrapFailure> failures = [];

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
        string[] arguments = ["--game-dir", Path.Combine(directory, "game files"), "--ff7-game-dir", Path.Combine(directory, "FF7 resources"), "--mods-dir", Path.Combine(directory, "Mods"), "--state", Path.Combine(directory, "saved state.json"), "--media-tools", Path.Combine(directory, "media tools"), "--graphics-backend", "Vulkan", "--achievements-local-only"];
        ProcessStartInfo start = GraphicsDispatcher.CreateStartInfo(data, GraphicsBackend.Vulkan, arguments);
        Assert.Equal(Path.Combine(data, "Game", "vulkan", HostPlatforms.Current.ExecutableName("Memoria.FF8")), start.FileName);
        Assert.Equal(arguments, start.ArgumentList);
        Assert.Equal(Environment.CurrentDirectory, start.WorkingDirectory);
        Assert.Equal(Path.GetFullPath(data), start.Environment[ApplicationInstallation.DirectoryVariable]);
        Assert.False(start.UseShellExecute);
        Assert.True(start.RedirectStandardOutput);
        Assert.True(start.RedirectStandardError);
        Assert.Equal(Path.Combine(data, "Game", "dx12", HostPlatforms.Current.ExecutableName("Memoria.FF8")), GraphicsDispatcher.HostExecutable(data, GraphicsBackend.Direct3D12));
    }

    [Fact]
    public async Task AutoTriesVulkanWhenTheFirstHostIsMissing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows Auto has three graphics host candidates.");
        List<GraphicsBackend> attempts = [];
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await RunAsync(GraphicsBackend.Auto, backend =>
        {
            attempts.Add(backend);
            return backend != GraphicsBackend.Vulkan ? MissingHost() : Child(backend, ReadyScript + "\nexit 0");
        }, output, error);
        Assert.Equal(0, result);
        Assert.Equal([GraphicsBackend.Direct3D11, GraphicsBackend.Direct3D12, GraphicsBackend.Vulkan], attempts);
        Assert.Contains("Graphics ready: requested=Auto actual=Vulkan", output.ToString());
        Assert.Contains("Direct3D11", error.ToString());
        Assert.Empty(failures);
        Assert.Equal(3, Directory.GetFiles(Path.Combine(directory, "diagnostics"), "result.json", SearchOption.AllDirectories).Length);
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
        BootstrapFailure failure = Assert.Single(failures);
        GraphicsAttemptEvidence attempt = Assert.Single(failure.Attempts);
        Assert.Equal("Vulkan", attempt.Backend);
        Assert.NotNull(attempt.Exception);
        Assert.NotNull(attempt.Exception.Stack);
        Assert.Contains("missing-host", attempt.Exception.Message);
        ErrorIncident captured = failure.Capture();
        string incidentRoot = Path.Combine(directory, "reports");
        captured.Persist(incidentRoot);
        ErrorIncident discovered = Assert.IsType<ErrorIncident>(ErrorIncident.DiscoverLatest(incidentRoot, failure.LaunchId));
        Assert.Equal(captured.Id, discovered.Id);
        Assert.Equal("Graphics host", discovered.Subsystem);
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
        BootstrapFailure failure = Assert.Single(failures);
        Assert.Null(failure.Exception);
        GraphicsAttemptEvidence attempt = Assert.Single(failure.Attempts);
        Assert.Null(attempt.Exception);
        Assert.Equal(childExit == 0 ? 1 : childExit, result);
        Assert.Equal(result, attempt.ExitCode);
        Assert.False(attempt.Ready);
        Assert.False(attempt.InitializationFailure);
        Assert.Single(attempts);
        Assert.DoesNotContain("Graphics ready:", output.ToString());
        Assert.Contains($"before graphics readiness (exit {childExit})", error.ToString());
        Assert.Null(failure.Capture().Exception.Stack);
    }

    [Theory]
    [InlineData("processStarted", "0")]
    [InlineData("token", "'another-launch'")]
    public async Task InvalidSuccessorIdentityCannotTransferDispatcherOwnership(string property, string value)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        string body = ReadyScript + "\n" + """
            $receipt = @{schemaVersion=1; processId=$PID; processStarted=([Diagnostics.Process]::GetCurrentProcess().StartTime.ToUniversalTime().Ticks); token=$env:MEMORIA_FF8_HOST_SUCCESSOR_TOKEN; successorProcessId=[int]::MaxValue; successorStarted=1}
            """ + $"\n$receipt['{property}'] = {value}\n" + """
            $request = Join-Path $env:MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY "$PID.json"
            [IO.File]::WriteAllText($request + '.tmp', ($receipt | ConvertTo-Json -Compress))
            [IO.File]::Move($request + '.tmp', $request)
            Start-Sleep -Seconds 120
            """;
        int result = await RunAsync(GraphicsBackend.Vulkan, backend => Child(backend, body), output, error);
        Assert.Equal(1, result);
        InvalidDataException exception = Assert.IsType<InvalidDataException>(Assert.Single(failures).Exception);
        Assert.Contains("does not belong to this game process", exception.Message);
        Assert.DoesNotContain("World transfer: following game host", output.ToString());
    }

    [Fact]
    public async Task MalformedSuccessorReceiptCannotBeTreatedAsAnOrdinaryHostExit()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await RunAsync(GraphicsBackend.Vulkan, backend => Child(backend, ReadyScript + "\n" + """
            $request = Join-Path $env:MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY "$PID.json"
            [IO.File]::WriteAllText($request + '.tmp', '{')
            [IO.File]::Move($request + '.tmp', $request)
            Start-Sleep -Seconds 120
            """), output, error);
        Assert.Equal(1, result);
        Assert.IsAssignableFrom<JsonException>(Assert.Single(failures).Exception);
        Assert.DoesNotContain("World transfer: following game host", output.ToString());
    }

    [Fact]
    public async Task OnlyClassifiedInitializationFailureCanFallbackAfterAHostStarts()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows Auto has three graphics host candidates.");
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
        Assert.Equal([GraphicsBackend.Direct3D11, GraphicsBackend.Direct3D12], attempts);
        Assert.Contains("Graphics ready: requested=Auto actual=Direct3D12", output.ToString());
        Assert.Empty(failures);
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
        int result = await GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Vulkan", "--validate"], output, error, TestContext.Current.CancellationToken, backend => Child(backend, "[Console]::Out.WriteLine('validation output')\nexit 27"), captureFailure: failures.Add);
        Assert.Equal(27, result);
        Assert.Contains("validation output", output.ToString());
        Assert.Empty(failures);
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
        Assert.Null(Assert.Single(failures).Exception);
        Assert.Equal(37, result);
        Assert.Single(attempts);
        Assert.Contains("Graphics ready: requested=Auto actual=" + attempts[0], output.ToString());
        Assert.Contains("game stdout", output.ToString());
        Assert.Contains("game crash detail", error.ToString());
        BootstrapFailure failure = Assert.Single(failures);
        GraphicsAttemptEvidence attempt = Assert.Single(failure.Attempts);
        Assert.True(attempt.Ready);
        Assert.Equal(37, attempt.ExitCode);
        Assert.Contains("backend", attempt.ReadinessReceipt);
        Assert.Contains(attempt.LogPaths, path => File.ReadAllText(path).Contains("game crash detail", StringComparison.Ordinal));
        string receiptPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, "diagnostics"), "result.json", SearchOption.AllDirectories));
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        Assert.True(receipt.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal(37, receipt.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Fact]
    public async Task WorldSuccessorKeepsDispatcherAliveAfterItsPredecessorExitsAndPreservesOutput()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows successor process ownership regression.");
        string marker = Path.Combine(directory, "successor-running.json");
        string finish = Path.Combine(directory, "finish-successor");
        string script = WriteSuccessorScript();
        using StringWriter output = new();
        using StringWriter error = new();
        Task<int> run = RunAsync(GraphicsBackend.Vulkan, backend => WorldParent(backend, script, marker, finish), output, error);
        try
        {
            await WaitForFileAsync(marker, run);
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(marker));
            await AssertExitedAsync(state.RootElement.GetProperty("parent").GetInt32());
            Assert.False(run.IsCompleted, "The launcher must keep waiting while the replacement game is running.");
            Assert.Contains("World transfer: following game host", output.ToString());
            File.WriteAllText(finish, "finish");
            Assert.Equal(29, await run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
            Assert.Contains("successor stdout", output.ToString());
            Assert.Contains("雪", output.ToString());
            Assert.DoesNotContain("\ufffd", output.ToString());
            Assert.Contains("successor stderr", error.ToString());
            Assert.Equal(29, Assert.Single(Assert.Single(failures).Attempts).ExitCode);
            Assert.Empty(Directory.GetFiles(Path.Combine(directory, "diagnostics"), "*.stdout.log", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(Path.Combine(directory, "diagnostics"), "*.stderr.log", SearchOption.AllDirectories));
            string successors = Assert.Single(Directory.GetDirectories(Path.Combine(directory, "diagnostics"), "successors", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(successors));
        }
        finally
        {
            File.WriteAllText(finish, "finish");
            await run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task DeadUnacceptedWorldSuccessorDoesNotTerminateTheRunningPredecessor()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows successor process rollback regression.");
        string marker = Path.Combine(directory, "dead-successor-published");
        string finish = Path.Combine(directory, "finish-source-rollback");
        using StringWriter output = new();
        using StringWriter error = new();
        Task<int> run = RunAsync(GraphicsBackend.Vulkan, backend =>
        {
            ProcessStartInfo start = Child(backend, ReadyScript + "\n" + """
                $start = [Diagnostics.ProcessStartInfo]::new('pwsh')
                $start.UseShellExecute = $false
                $start.CreateNoWindow = $true
                foreach ($argument in @('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', 'exit 0'))
                {
                    $start.ArgumentList.Add($argument)
                }
                $child = [Diagnostics.Process]::Start($start)
                $childId = $child.Id
                $childStarted = $child.StartTime.ToUniversalTime().Ticks
                $child.WaitForExit()
                $child.Dispose()
                $request = Join-Path $env:MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY "$PID.json"
                $receipt = @{schemaVersion=1; processId=$PID; processStarted=([Diagnostics.Process]::GetCurrentProcess().StartTime.ToUniversalTime().Ticks); token=$env:MEMORIA_FF8_HOST_SUCCESSOR_TOKEN; successorProcessId=$childId; successorStarted=$childStarted}
                [IO.File]::WriteAllText($request + '.tmp', ($receipt | ConvertTo-Json -Compress))
                [IO.File]::Move($request + '.tmp', $request)
                [IO.File]::WriteAllText($env:MEMORIA_TEST_SUCCESSOR_MARKER, "$PID")
                while (!(Test-Path -LiteralPath $env:MEMORIA_TEST_SUCCESSOR_FINISH))
                {
                    Start-Sleep -Milliseconds 10
                }
                [IO.File]::Delete($request)
                [Console]::Out.WriteLine('source continued after rollback')
                exit 0
                """);
            start.Environment["MEMORIA_TEST_SUCCESSOR_MARKER"] = marker;
            start.Environment["MEMORIA_TEST_SUCCESSOR_FINISH"] = finish;
            return start;
        }, output, error);
        try
        {
            await WaitForFileAsync(marker, run);
            await Task.Delay(250, TestContext.Current.CancellationToken);
            Assert.False(run.IsCompleted, "A failed preload must leave the running source game alive.");
            string successors = Assert.Single(Directory.GetDirectories(Path.Combine(directory, "diagnostics"), "successors", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(successors, "*.accepted.json"));
            File.WriteAllText(finish, "finish");
            Assert.Equal(0, await run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
            Assert.Contains("source continued after rollback", output.ToString());
            Assert.DoesNotContain("World transfer: following game host", output.ToString());
            Assert.Empty(failures);
        }
        finally
        {
            File.WriteAllText(finish, "finish");
            await run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CancellationAfterWorldHandoffStopsTheSuccessor()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows successor process ownership regression.");
        string marker = Path.Combine(directory, "successor-cancel.json");
        string finish = Path.Combine(directory, "finish-cancel");
        string script = WriteSuccessorScript();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using StringWriter output = new();
        using StringWriter error = new();
        Task<int> run = GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Vulkan"], output, error, cancellation.Token, backend => WorldParent(backend, script, marker, finish), TimeSpan.FromSeconds(15), failures.Add);
        try
        {
            await WaitForFileAsync(marker, run);
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(marker));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
            await AssertExitedAsync(state.RootElement.GetProperty("child").GetInt32());
        }
        finally
        {
            cancellation.Cancel();
            File.WriteAllText(finish, "finish");
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Dispatcher cancellation is expected after owned successors have stopped.
            }
        }
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
        }, TimeSpan.FromMilliseconds(200), failures.Add);
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
        }, TimeSpan.FromSeconds(30), failures.Add);
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
            Assert.Empty(failures);
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

    [Fact]
    public async Task UnexpectedDispatchExceptionRetainsEveryNestedCauseAndStack()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        AggregateException original = new("Synthetic launch failure", new IOException("first cause"), new InvalidOperationException("second cause"));
        AggregateException thrown = await Assert.ThrowsAsync<AggregateException>(() => GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Vulkan"], output, error, TestContext.Current.CancellationToken, _ => throw original, captureFailure: failures.Add));
        Assert.Same(original, thrown);
        ErrorIncident incident = Assert.Single(failures).Capture();
        Assert.Equal(2, incident.Exception.Causes.Count);
        Assert.NotNull(incident.Exception.Stack);
        Assert.Contains("Synthetic launch failure", incident.Exception.Message);
    }

    [Fact]
    public async Task ExistingGameIncidentRemainsPrimaryAndReceivesHostLogs()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await RunAsync(GraphicsBackend.Vulkan, backend => Child(backend, ReadyScript + "\n[Console]::Out.WriteLine($env:MEMORIA_FF8_REPORT_LAUNCH_ID)\nexit 31"), output, error);
        Assert.Equal(31, result);
        BootstrapFailure failure = Assert.Single(failures);
        Assert.Contains(failure.LaunchId, output.ToString());
        string reports = Path.Combine(directory, "reports");
        ErrorIncident game = ErrorIncident.Capture(new InvalidOperationException("Original game error"), "Game", launchId: failure.LaunchId);
        string incidentDirectory = game.Persist(reports);
        failure.Persist(reports);
        ErrorIncident preserved = Assert.IsType<ErrorIncident>(ErrorIncident.DiscoverLatest(reports, failure.LaunchId));
        Assert.Equal(game.Id, preserved.Id);
        Assert.Equal("Game", preserved.Subsystem);
        Assert.Equal("Original game error", preserved.Exception.Message);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(incidentDirectory)!, "incident.json", SearchOption.AllDirectories));
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(incidentDirectory, "incident.json")));
        Assert.Contains(evidence.RootElement.GetProperty("LogPaths").EnumerateArray(), path => path.GetString()!.EndsWith("result.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BrokenReporterCannotReplaceTheHostFailure()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Vulkan"], output, error, TestContext.Current.CancellationToken, _ => MissingHost(), captureFailure: _ => throw new IOException("Synthetic reporting failure"));
        Assert.Equal(1, result);
        Assert.Contains("Try Auto", error.ToString());
    }

    [Fact]
    public async Task BackendSelectionFailureStillHasDiscoverableEvidence()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        await Assert.ThrowsAsync<ArgumentException>(() => GraphicsDispatcher.RunAsync(directory, ["--graphics-backend"], output, error, TestContext.Current.CancellationToken, captureFailure: failures.Add));
        BootstrapFailure failure = Assert.Single(failures);
        Assert.Contains("backend selection failed", failure.RequestedBackend);
        Assert.IsType<ArgumentException>(failure.Exception);
    }

    private Task<int> RunAsync(GraphicsBackend requested, Func<GraphicsBackend, ProcessStartInfo> factory, TextWriter output, TextWriter error)
    {
        return GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", requested.ToString()], output, error, TestContext.Current.CancellationToken, factory, TimeSpan.FromSeconds(15), failures.Add);
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

    private ProcessStartInfo WorldParent(GraphicsBackend backend, string successorScript, string marker, string finish)
    {
        ProcessStartInfo start = Child(backend, ReadyScript + "\n" + """
            $start = [Diagnostics.ProcessStartInfo]::new('pwsh')
            $start.UseShellExecute = $false
            $start.CreateNoWindow = $true
            foreach ($argument in @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $env:MEMORIA_TEST_SUCCESSOR_SCRIPT, '-Parent', "$PID"))
            {
                $start.ArgumentList.Add($argument)
            }
            $child = [Diagnostics.Process]::Start($start)
            $request = Join-Path $env:MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY "$PID.json"
            $accepted = Join-Path $env:MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY "$PID.accepted.json"
            $receipt = @{schemaVersion=1; processId=$PID; processStarted=([Diagnostics.Process]::GetCurrentProcess().StartTime.ToUniversalTime().Ticks); token=$env:MEMORIA_FF8_HOST_SUCCESSOR_TOKEN; successorProcessId=$child.Id; successorStarted=$child.StartTime.ToUniversalTime().Ticks}
            [IO.File]::WriteAllText($request + '.tmp', ($receipt | ConvertTo-Json -Compress))
            [IO.File]::Move($request + '.tmp', $request)
            while (!(Test-Path -LiteralPath $accepted))
            {
                Start-Sleep -Milliseconds 10
            }
            exit 0
            """);
        start.Environment["MEMORIA_TEST_SUCCESSOR_SCRIPT"] = successorScript;
        start.Environment["MEMORIA_TEST_SUCCESSOR_MARKER"] = marker;
        start.Environment["MEMORIA_TEST_SUCCESSOR_FINISH"] = finish;
        return start;
    }

    private string WriteSuccessorScript()
    {
        string path = Path.Combine(directory, "successor.ps1");
        File.WriteAllText(path, """
            param([int] $Parent)
            $ErrorActionPreference = 'Stop'
            $outputPath = Join-Path $env:MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY "$PID.stdout.log"
            $errorPath = Join-Path $env:MEMORIA_FF8_HOST_SUCCESSOR_DIRECTORY "$PID.stderr.log"
            [IO.File]::WriteAllText($outputPath, '')
            [IO.File]::WriteAllText($errorPath, '')
            try
            {
                [Diagnostics.Process]::GetProcessById($Parent).WaitForExit()
            }
            catch [ArgumentException]
            {
            }
            $state = @{parent=$Parent; child=$PID} | ConvertTo-Json -Compress
            [IO.File]::WriteAllText($env:MEMORIA_TEST_SUCCESSOR_MARKER + '.tmp', $state)
            [IO.File]::Move($env:MEMORIA_TEST_SUCCESSOR_MARKER + '.tmp', $env:MEMORIA_TEST_SUCCESSOR_MARKER)
            [IO.File]::AppendAllText($outputPath, "successor stdout`n")
            [IO.File]::AppendAllText($errorPath, "successor stderr`n")
            $unicode = [Text.Encoding]::UTF8.GetBytes('雪')
            $stream = [IO.FileStream]::new($outputPath, [IO.FileMode]::Append, [IO.FileAccess]::Write, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            $stream.Write($unicode, 0, 1)
            $stream.Flush()
            Start-Sleep -Milliseconds 150
            $stream.Write($unicode, 1, 2)
            $stream.Flush()
            $stream.Dispose()
            while (!(Test-Path -LiteralPath $env:MEMORIA_TEST_SUCCESSOR_FINISH))
            {
                Start-Sleep -Milliseconds 20
            }
            exit 29
            """);
        return path;
    }

    private static async Task WaitForFileAsync(string path, Task run)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!File.Exists(path) && !run.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
        Assert.True(File.Exists(path), "The replacement host must run after its predecessor has exited.");
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
