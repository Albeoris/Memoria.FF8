using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticLiveTelemetryTests
{
    private static BattleSession Battle()
    {
        return new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }] }, BattleAtbMode.Normal);
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
