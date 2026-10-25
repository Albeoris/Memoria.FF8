using System.Text;
using System.Text.Json;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class SupervisionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "battle-supervision-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Failed_host_preserves_the_actual_reason_without_claiming_complete_stages()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        SupervisionRequest request = ScriptRequest("[IO.File]::WriteAllText((Join-Path $env:MEMORIA_FF8_OUTPUT_DIRECTORY 'candidate.json'), '{\"status\":\"ERROR\",\"reason\":\"UNSUPPORTED_OWNER: required model unavailable\"}')");
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("HOST_REPORTED_ERROR", outcome.ErrorCategory);
        Assert.Equal("UNSUPPORTED_OWNER: required model unavailable", outcome.Message);
        Assert.True(outcome.ProcessExited);
        AssertError(outcome, "HOST_REPORTED_ERROR");
    }

    [Fact]
    public async Task StartedRecordIsPersistedBeforeOwnedChildMayExecute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        string marker = Path.Combine(directory, "child-started.txt");
        EvidenceStore store = new(Path.Combine(directory, "store"));
        SupervisionRequest request = ScriptRequest($"Set-Content -LiteralPath '{marker.Replace("'", "''")}' -Value executed");
        bool persisted = false;
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken, persistStarted: payload =>
        {
            Assert.False(File.Exists(marker));
            store.WriteBytesNew("attempts/started.json", payload);
            persisted = true;
        });
        Assert.True(persisted);
        Assert.True(File.Exists(marker));
        Assert.Equal(File.ReadAllBytes(Path.Combine(request.OutputDirectory, "started.json")), store.ReadBytes("attempts/started.json"));
        AssertError(outcome, "MISSING_CANDIDATE");
    }

    [Fact]
    public void TransientPublicationIsIdempotentButDifferentBytesAreRejected()
    {
        string path = Path.Combine(directory, "transient.json");
        OwnedChildSupervisor.PublishTransientImmutable(path, Encoding.UTF8.GetBytes("original"));
        OwnedChildSupervisor.PublishTransientImmutable(path, Encoding.UTF8.GetBytes("original"));
        Assert.Throws<InvalidDataException>(() => OwnedChildSupervisor.PublishTransientImmutable(path, Encoding.UTF8.GetBytes("replacement")));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task DeniedPerformanceSlotKillsSuspendedChildBeforeItCanWrite()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        string marker = Path.Combine(directory, "marker.txt");
        SupervisionRequest request = ScriptRequest($"Set-Content -LiteralPath '{marker.Replace("'", "''")}' -Value executed");
        bool callbackInvoked = false;
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken, identity =>
        {
            callbackInvoked = true;
            Assert.False(new OwnedProcessProbe().HasExited(identity));
            return false;
        });
        Assert.True(callbackInvoked);
        Assert.Equal("SUPERVISION", outcome.ErrorCategory);
        Assert.False(File.Exists(marker));
        Assert.NotNull(outcome.Process);
        Assert.True(new OwnedProcessProbe().HasExited(outcome.Process));
        AssertError(outcome, "SUPERVISION");
    }

    [Fact]
    public async Task MissingCandidatePublishesErrorAfterCleanChildExit()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        SupervisionRequest request = CommandRequest("exit /b 0");
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("MISSING_CANDIDATE", outcome.ErrorCategory);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(outcome.ProcessExited);
        Assert.True(File.Exists(Path.Combine(request.OutputDirectory, "started.json")));
        Assert.False(Directory.Exists(Path.Combine(request.ControlDirectory, "records")));
        AssertError(outcome, "MISSING_CANDIDATE");
        await Assert.ThrowsAsync<InvalidOperationException>(() => OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CrashedChildPublishesErrorAndPreservesExitCode()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(CommandRequest("exit /b 7"), TestContext.Current.CancellationToken);
        Assert.Equal("CRASH", outcome.ErrorCategory);
        Assert.Equal(7, outcome.ExitCode);
        AssertError(outcome, "CRASH");
    }

    [Fact]
    public async Task StageTimeoutTerminatesOwnedTreeAndPublishesOneError()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        SupervisionRequest request = CommandRequest("ping -n 30 127.0.0.1 >nul");
        request = request with { Budgets = request.Budgets with { Initialization = TimeSpan.FromMilliseconds(300) } };
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("TIMEOUT", outcome.ErrorCategory);
        Assert.Equal("initialization", outcome.LastStage);
        Assert.True(outcome.ProcessExited);
        Assert.NotNull(outcome.Process);
        Assert.True(new OwnedProcessProbe().HasExited(outcome.Process));
        AssertError(outcome, "TIMEOUT");
    }

    [Fact]
    public async Task CancellationPublishesExplicitErrorAndKillsChild()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(300));
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(CommandRequest("ping -n 30 127.0.0.1 >nul"), cancellation.Token);
        Assert.Equal("CANCELLED", outcome.ErrorCategory);
        Assert.True(outcome.ProcessExited);
        AssertError(outcome, "CANCELLED");
    }

    [Fact]
    public async Task CandidateIsNotPassAndCannotPublishUntilValidationCompletes()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        SupervisionRequest request = ScriptRequest("""
            $ErrorActionPreference = 'Stop'
            $completed = @('initialization', 'action_one', 'cleanup_rearm', 'action_two')
            $progress = @{ stage = 'teardown'; sequence = 5; completed_stages = $completed } | ConvertTo-Json -Compress
            [IO.File]::WriteAllText((Join-Path $env:MEMORIA_FF8_CONTROL_DIRECTORY 'progress.json'), $progress)
            [IO.File]::WriteAllText((Join-Path $env:MEMORIA_FF8_OUTPUT_DIRECTORY 'candidate.json'), '{"synthetic":true}')
            exit 0
            """);
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("candidate_ready", outcome.Status);
        Assert.Null(outcome.ErrorCategory);
        Assert.False(File.Exists(outcome.ResultPath));
        OwnedChildSupervisor.PublishValidatedCandidate(outcome, payload =>
        {
            using JsonDocument candidate = JsonDocument.Parse(payload);
            Assert.True(candidate.RootElement.GetProperty("synthetic").GetBoolean());
            return Encoding.UTF8.GetBytes("{\"verdict\":\"ERROR\",\"error_category\":\"SYNTHETIC\"}");
        });
        using JsonDocument result = JsonDocument.Parse(File.ReadAllBytes(outcome.ResultPath));
        Assert.Equal("SYNTHETIC", result.RootElement.GetProperty("error_category").GetString());
        Assert.Throws<InvalidDataException>(() => OwnedChildSupervisor.PublishValidatedCandidate(outcome, _ => Encoding.UTF8.GetBytes("{\"different\":true}")));
    }

    [Fact]
    public async Task InvalidCandidateCannotEscapeAsPass()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        SupervisionRequest request = ScriptRequest("""
            $progress = @{ stage = 'teardown'; sequence = 5; completed_stages = @('initialization', 'action_one', 'cleanup_rearm', 'action_two') } | ConvertTo-Json -Compress
            [IO.File]::WriteAllText((Join-Path $env:MEMORIA_FF8_CONTROL_DIRECTORY 'progress.json'), $progress)
            [IO.File]::WriteAllText((Join-Path $env:MEMORIA_FF8_OUTPUT_DIRECTORY 'candidate.json'), '{}')
            """);
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("candidate_ready", outcome.Status);
        OwnedChildSupervisor.PublishValidatedCandidate(outcome, _ => throw new InvalidDataException("Missing mandatory telemetry."));
        AssertError(outcome, "INVALID_CANDIDATE");
    }

    [Fact]
    public async Task ProgressCannotSkipUnrecordedStages()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        SupervisionRequest request = ScriptRequest("""
            [IO.File]::WriteAllText((Join-Path $env:MEMORIA_FF8_CONTROL_DIRECTORY 'progress.json'), '{"stage":"action_two","sequence":3}')
            Start-Sleep -Seconds 30
            """);
        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("SUPERVISION", outcome.ErrorCategory);
        Assert.True(outcome.ProcessExited);
        AssertError(outcome, "SUPERVISION");
    }

    [Fact]
    public async Task InvalidBoundsAndOverlappingChannelsFailBeforeLaunch()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }
        SupervisionRequest request = CommandRequest("exit /b 0");
        await Assert.ThrowsAsync<ArgumentException>(() => OwnedChildSupervisor.RunAsync(request with { Budgets = request.Budgets with { NoProgress = TimeSpan.Zero } }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => OwnedChildSupervisor.RunAsync(request with { SettingsDirectory = request.ControlDirectory }, TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    private SupervisionRequest CommandRequest(string command)
    {
        string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        return Request(executable, ["/d", "/s", "/c", command]);
    }

    private SupervisionRequest ScriptRequest(string script)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "helper.ps1");
        File.WriteAllText(path, script);
        string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        return Request(executable, ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", path]);
    }

    private SupervisionRequest Request(string executable, IReadOnlyList<string> arguments)
    {
        SupervisionBudgets budgets = new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10));
        return new SupervisionRequest("synthetic-attempt", executable, arguments, Path.GetDirectoryName(executable)!, Path.Combine(directory, "settings"), Path.Combine(directory, "output"), Path.Combine(directory, "control"), budgets);
    }

    private static void AssertError(SupervisionOutcome outcome, string category)
    {
        using JsonDocument result = JsonDocument.Parse(File.ReadAllBytes(outcome.ResultPath));
        Assert.Equal("ERROR", result.RootElement.GetProperty("verdict").GetString());
        Assert.Equal(category, result.RootElement.GetProperty("error_category").GetString());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
