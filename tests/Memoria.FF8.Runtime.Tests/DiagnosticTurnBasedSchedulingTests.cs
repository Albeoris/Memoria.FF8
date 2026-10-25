namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticTurnBasedSchedulingTests
{
    [Fact]
    public void Selected_diagnostic_actor_refills_on_the_live_session_despite_another_ready_player()
    {
        BattleSession battle = Create();
        battle.ConfigureDiagnosticScheduling(true, readinessActorId: "selected");
        BattleCombatant selected = battle.Combatants[0];
        BattleCombatant other = battle.Combatants[1];
        BattleCombatant enemy = battle.Combatants[2];
        Guid lifecycle = battle.DiagnosticLifecycleId;
        Assert.False(selected.IsReady);
        Assert.True(other.IsReady);
        battle.Tick();
        Assert.True(selected.IsReady);
        Assert.True(other.IsReady);
        Assert.True(enemy.Atb > 0);
        Assert.Same(selected, battle.Combatants[0]);
        Assert.Same(other, battle.Combatants[1]);
        Assert.Same(enemy, battle.Combatants[2]);
        Assert.Equal(lifecycle, battle.DiagnosticLifecycleId);
        Assert.Equal(0, battle.DiagnosticRestoreCount);
        Assert.Equal(0, battle.DiagnosticResetCount);
        Assert.InRange(battle.Frame, 1, BattleSession.MaximumAdvanceFrames);
        long readyFrame = battle.Frame;
        battle.Tick();
        Assert.Equal(readyFrame, battle.Frame);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ordinary_turn_based_scheduling_keeps_waiting_for_an_already_ready_player(bool diagnostic)
    {
        BattleSession battle = Create();
        if (diagnostic)
        {
            battle.ConfigureDiagnosticScheduling(true);
        }
        battle.Tick();
        Assert.Equal(0, battle.Combatants[0].Atb);
        Assert.True(battle.Combatants[1].IsReady);
        Assert.Equal(0, battle.Frame);
    }

    [Fact]
    public void Readiness_configuration_rejects_unknown_actors_before_mutating_scheduling()
    {
        BattleSession battle = Create();
        Assert.Throws<ArgumentException>(() => battle.ConfigureDiagnosticScheduling(true, true, "missing"));
        battle.Tick();
        Assert.Equal(0, battle.Combatants[0].Atb);
        Assert.Equal(0, battle.Frame);
    }

    [Fact]
    public void Selected_suspended_actor_does_not_trigger_automatic_time_advancement()
    {
        BattleSession battle = Create();
        battle.ConfigureDiagnosticScheduling(true, readinessActorId: "selected");
        battle.Combatants[0].Suspended = true;
        battle.Tick();
        Assert.Equal(0, battle.Combatants[0].Atb);
        Assert.Equal(0, battle.Frame);
    }

    private static BattleSession Create()
    {
        return new(new BattleDefinition
        {
            Combatants = [new() { Id = "selected", Name = "Selected", IsPlayer = true, Speed = 100, InitialAtb = 0 }, new() { Id = "other", Name = "Other", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy", Speed = 1, InitialAtb = 0 }]
        }, BattleAtbMode.TurnBased);
    }
}
