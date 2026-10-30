using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticLiveTelemetryTests
{
    private static BattleSession Battle()
    {
        return new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }] }, BattleAtbMode.Normal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SEALED_INTEGRITY_FAILURE")]
    public void Synchronous_focal_seal_keeps_actual_afterstate_integrity_and_excludes_later_gameplay_from_separate_tail(string? sealedIntegrityError)
    {
        BattleSession battle = Battle();
        using DiagnosticLiveTelemetry telemetry = new(battle, Guid.NewGuid(), 2);
        long begin = System.Diagnostics.Stopwatch.GetTimestamp();
        telemetry.Begin(begin, 10, 0, "renderer");
        var release = DiagnosticInvocationReleaseTests.Receipt() with { LifecycleId = battle.DiagnosticLifecycleId, SimulationFrame = battle.Frame };
        typeof(DiagnosticLiveTelemetry).GetMethod("ObserveDispatch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(telemetry, [release.Dispatch]);
        NativeBattleTelemetry.Record(1001, NativeBattleTrafficKind.CpuCopyPayload, 17);
        telemetry.Presented(System.Diagnostics.Stopwatch.GetTimestamp(), 11, "Drain", 1, 1, 0);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks))!.SetValue(battle, 3L);
        release = release with { Timestamp = System.Diagnostics.Stopwatch.GetTimestamp() };
        BattleDiagnosticCombatant[] actualAtRelease = battle.ObserveDiagnosticCombatants();
        telemetry.SealInvocation(release);
        DiagnosticLiveExecution sealedWindow = Assert.Single(telemetry.Executions);
        if (sealedIntegrityError is not null)
        {
            var retained = (List<DiagnosticLiveExecution>)typeof(DiagnosticLiveTelemetry).GetField("executions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(telemetry)!;
            retained[0] = sealedWindow with { IntegrityError = sealedIntegrityError };
        }
        battle.QueueAttack("party", "enemy");
        NativeBattleTelemetry.Record(1001, NativeBattleTrafficKind.CpuCopyPayload, 999);
        telemetry.Presented(release.Timestamp + 1, 12, "Tail", 2, 2, 0);
        telemetry.Presented(release.Timestamp + 2, 13, "Tail", 3, 2, 0);
        telemetry.Complete(13, true, 2, null);
        DiagnosticLiveExecution final = Assert.Single(telemetry.Executions);
        Assert.Equal(17, final.Traffic.CpuCopyPayloadBytes);
        Assert.DoesNotContain(final.Events, value => value.Kind == "Queued");
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(actualAtRelease), System.Text.Json.JsonSerializer.Serialize(final.After));
        Assert.Contains(battle.Events, value => value.Kind == "Queued");
        Assert.Equal(sealedWindow.EndOwners, final.EndOwners);
        Assert.Equal(3, final.EndCallback);
        Assert.Equal(11, final.EndFrame);
        Assert.Equal(1, final.Frames.FrameCount);
        Assert.Equal(sealedIntegrityError is null, final.Completed);
        Assert.Equal(sealedIntegrityError is null, final.CleanupCompleted);
        Assert.Equal(sealedIntegrityError, final.IntegrityError);
        Assert.Equal(2, final.PostReleaseTail!.PresentedFrames);
        Assert.Equal(12, final.PostReleaseTail.FirstPresentedFrame);
        Assert.Equal(release, final.InvocationRelease);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SEALED_INTEGRITY_FAILURE")]
    public void Aftercare_keeps_main_sealed_and_starts_tail_only_after_its_actual_completion(string? sealedError)
    {
        BattleSession battle = Battle();
        using DiagnosticLiveTelemetry telemetry = new(battle, Guid.NewGuid(), 2);
        long begin = System.Diagnostics.Stopwatch.GetTimestamp();
        telemetry.Begin(begin, 10, 0, "renderer");
        BattleDiagnosticInvocationRelease original = DiagnosticInvocationReleaseTests.Receipt() with { LifecycleId = battle.DiagnosticLifecycleId, SimulationFrame = battle.Frame };
        var fixture = DiagnosticInvocationReleaseTests.AftercareFixture(original);
        typeof(DiagnosticLiveTelemetry).GetMethod("ObserveDispatch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(telemetry, [fixture.Main.Dispatch]);
        NativeBattleTelemetry.Record(1001, NativeBattleTrafficKind.CpuCopyPayload, 17);
        telemetry.Presented(System.Diagnostics.Stopwatch.GetTimestamp(), 11, "Drain", 1, 1, 0);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks))!.SetValue(battle, 3L);
        long mainTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        BattleDiagnosticInvocationRelease main = fixture.Main with { Timestamp = mainTicks, Aftercare = fixture.Main.Aftercare! with { MainTimestamp = mainTicks } };
        telemetry.SealInvocation(main);
        DiagnosticLiveExecution sealedMain = Assert.Single(telemetry.Executions);
        if (sealedError is not null)
        {
            var retained = (List<DiagnosticLiveExecution>)typeof(DiagnosticLiveTelemetry).GetField("executions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(telemetry)!;
            retained[0] = sealedMain with { IntegrityError = sealedError };
        }
        battle.QueueAttack("party", "enemy");
        NativeBattleTelemetry.Record(1001, NativeBattleTrafficKind.CpuCopyPayload, 999);
        telemetry.Presented(mainTicks + 1, 12, "Drain", 2, 2, 0);
        telemetry.Presented(mainTicks + 2, 13, "Drain", 3, 2, 0);
        Assert.Null(Assert.Single(telemetry.Executions).PostReleaseTail);
        BattleDiagnosticInvocationAftercareReceipt aftercare = fixture.Aftercare with { CompletedTimestamp = mainTicks + 3, CompletedFrame = battle.Frame };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks))!.SetValue(battle, aftercare.CompletedCallback);
        telemetry.ObserveInvocationAftercare(aftercare);
        telemetry.Presented(mainTicks + 4, 14, "Tail", 4, 2, 0);
        telemetry.Presented(mainTicks + 5, 15, "Tail", 5, 2, 0);
        telemetry.Complete(15, true, 2, null);
        DiagnosticLiveExecution final = Assert.Single(telemetry.Executions);
        Assert.Equal(sealedMain.EndCallback, final.EndCallback);
        Assert.Equal(sealedMain.EndFrame, final.EndFrame);
        Assert.Equal(sealedMain.EffectCompletedTicks, final.EffectCompletedTicks);
        Assert.Equal(sealedMain.EndOwners, final.EndOwners);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(sealedMain.After), System.Text.Json.JsonSerializer.Serialize(final.After));
        Assert.Equal(17, final.Traffic.CpuCopyPayloadBytes);
        Assert.Single(final.Frames.Intervals);
        Assert.DoesNotContain(final.Events, value => value.Kind == "Queued");
        Assert.Equal(13L, final.AftercareTailPrecedingFrame!.Value);
        Assert.Equal(mainTicks + 2, final.AftercareTailPrecedingTimestamp!.Value);
        Assert.Equal(14, final.PostReleaseTail!.FirstPresentedFrame);
        Assert.Equal(2, final.PostReleaseTail.PresentedFrames);
        Assert.Equal(aftercare, final.InvocationAftercare);
        Assert.Equal(sealedError, final.IntegrityError);
        Assert.Equal(sealedError is null, final.Completed);
        Assert.Equal(sealedError is null, final.CleanupCompleted);
    }

    [Fact]
    public void Window_observes_live_events_and_actual_payload_without_snapshot_polling()
    {
        BattleSession battle = Battle();
        using DiagnosticLiveTelemetry telemetry = new(battle, Guid.NewGuid());
        telemetry.Begin(100, 10, 0, "renderer");
        NativeBattleTelemetry.Record(1001, NativeBattleTrafficKind.CpuCopyPayload, 17);
        battle.QueueAttack("party", "enemy");
        telemetry.Presented(200, 11, "AwaitDispatch", 1, 2, 0);
        telemetry.Complete(11, false, 0, "TEST_BOUNDARY");
        DiagnosticLiveExecution execution = Assert.Single(telemetry.Executions);
        Assert.Contains(execution.Events, value => value.Kind == "Queued");
        Assert.Equal(17, execution.Traffic.CpuCopyPayloadBytes);
        Assert.Equal(1, execution.Frames.FrameCount);
        Assert.Equal(1, execution.RendererFrames);
        Assert.Equal("renderer", execution.StartOwners.RendererOwnerId);
        Assert.Equal(battle.DiagnosticLifecycleId.ToString(), execution.StartOwners.RetainedMemoryOwnerId);
        Assert.False(execution.Completed);
        Assert.Equal("TEST_BOUNDARY", execution.IntegrityError);
        Assert.True(execution.ProcessBefore.WorkingSetBytes > 0);
        Assert.True(execution.ProcessAfter.PrivateMemoryBytes > 0);
        Assert.True(execution.ProcessAfter.CpuMilliseconds >= execution.ProcessBefore.CpuMilliseconds);
    }

    [Fact]
    public void Shared_observers_do_not_have_an_effect_allowlist()
    {
        using DiagnosticLiveTelemetry first = new(Battle(), Guid.NewGuid(), 1);
        using DiagnosticLiveTelemetry last = new(Battle(), Guid.NewGuid(), 400);
        Assert.Equal(first.Coverage.InstrumentationVersion, last.Coverage.InstrumentationVersion);
        Assert.Equal(first.Coverage.Sites, last.Coverage.Sites);
        Assert.DoesNotContain(last.Coverage.Sites, site => !site.Enabled);
    }

    [Fact]
    public void Windows_are_disjoint_and_scopes_release_on_completion()
    {
        using DiagnosticLiveTelemetry telemetry = new(Battle(), Guid.NewGuid());
        telemetry.Begin(100, 10, 0, "renderer");
        NativeBattleTelemetry.Record(1001, NativeBattleTrafficKind.CpuCopyPayload, 17);
        telemetry.Presented(200, 11, "Tail", 1, 1, 0);
        telemetry.Complete(11, true, 2, null);
        NativeBattleTelemetry.Record(1001, NativeBattleTrafficKind.CpuCopyPayload, 999);
        telemetry.Begin(300, 12, 1, "renderer");
        NativeBattleTelemetry.Record(1001, NativeBattleTrafficKind.CpuCopyPayload, 23);
        telemetry.Presented(400, 13, "Tail", 2, 1, 0);
        telemetry.Complete(13, true, 2, null);
        Assert.Equal(new long?[] { 17, 23 }, telemetry.Executions.Select(execution => execution.Traffic.CpuCopyPayloadBytes));
        Assert.Throws<InvalidOperationException>(() => telemetry.Begin(500, 14, 2, "renderer"));
    }

    [Fact]
    public void Scheduling_is_initial_only_and_diagnostic_audio_uses_normal_route()
    {
        BattleSession battle = Battle();
        battle.ConfigureDiagnosticScheduling(true);
        battle.Tick();
        Assert.Throws<InvalidOperationException>(() => battle.ConfigureDiagnosticScheduling(false));
        LaunchOptions options = new() { BattleTestPath = "recipe.json", ControlDirectory = "control" };
        Assert.Equal(!System.Diagnostics.Debugger.IsAttached, options.AudioEnabled);
        Assert.False((options with { MuteAudio = true }).AudioEnabled);
        Assert.False((options with { BattleTestPath = null }).AudioEnabled);
    }
}
