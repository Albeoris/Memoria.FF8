using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticReadinessHandoffTests
{
    [Fact]
    public void Handoff_changes_only_the_readiness_reference_and_existing_turn_based_logic_refills_the_selected_enemy()
    {
        BattleSession battle = Create();
        BattleCombatant[] actors = battle.Combatants.ToArray();
        Guid lifecycle = battle.DiagnosticLifecycleId;
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.True(battle.Combatants[0].IsReady);
        Assert.False(battle.Combatants[2].IsReady);
        Assert.True(battle.TryHandoffDiagnosticReadinessActor("party", "enemy"));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(lifecycle, battle.DiagnosticLifecycleId);
        Assert.Equal(0, battle.DiagnosticRestoreCount);
        Assert.Equal(0, battle.DiagnosticResetCount);
        Assert.False(battle.TryHandoffDiagnosticReadinessActor("party", "enemy"));
        battle.Tick();
        Assert.True(battle.Combatants[2].IsReady);
        Assert.True(battle.Combatants[0].IsReady);
        for (int index = 0; index < actors.Length; index++)
        {
            Assert.Same(actors[index], battle.Combatants[index]);
        }
    }

    [Theory]
    [InlineData("wrong-previous")]
    [InlineData("missing-next")]
    [InlineData("player-next")]
    [InlineData("normal")]
    [InlineData("unheld")]
    [InlineData("inactive")]
    [InlineData("suspended")]
    [InlineData("untargetable")]
    [InlineData("uninitialized")]
    [InlineData("pending-statistics")]
    [InlineData("busy")]
    public void Invalid_handoff_preserves_the_complete_native_snapshot_and_lifecycle(string reason)
    {
        BattleSession battle = Create(reason == "normal" ? BattleAtbMode.Normal : BattleAtbMode.TurnBased, reason != "unheld");
        BattleCombatant enemy = battle.Combatants[2];
        string previousId = "party";
        string nextId = "enemy";
        switch (reason)
        {
            case "wrong-previous":
                previousId = "other";
                break;
            case "missing-next":
                nextId = "missing";
                break;
            case "player-next":
                nextId = "other";
                break;
            case "inactive":
                enemy.IsActive = false;
                break;
            case "suspended":
                enemy.Suspended = true;
                break;
            case "untargetable":
                enemy.Untargetable = true;
                break;
            case "uninitialized":
                enemy.ResolvedNativeLevel = null;
                break;
            case "pending-statistics":
                enemy.NativeAllocationPendingStatistics = true;
                break;
            case "busy":
                enemy.IsBusy = true;
                break;
        }
        string before = JsonSerializer.Serialize(battle.Capture());
        Guid lifecycle = battle.DiagnosticLifecycleId;
        Assert.False(battle.TryHandoffDiagnosticReadinessActor(previousId, nextId));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(lifecycle, battle.DiagnosticLifecycleId);
        Assert.Equal(0, battle.DiagnosticRestoreCount);
        Assert.Equal(0, battle.DiagnosticResetCount);
    }

    [Fact]
    public void Pending_original_native_wait_must_drain_normally_before_readiness_can_be_handed_off()
    {
        BattleSession battle = Create(initialization: [28, 1, 0]);
        Assert.NotEmpty(battle.NativeScriptState.WaitCounters);
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.False(battle.TryHandoffDiagnosticReadinessActor("party", "enemy"));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        for (int callback = 0; callback < 64 && battle.NativeScriptState.WaitCounters.Length != 0; callback++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.NativeScriptState.WaitCounters);
        Assert.True(battle.TryHandoffDiagnosticReadinessActor("party", "enemy"));
    }

    private static BattleSession Create(BattleAtbMode mode = BattleAtbMode.TurnBased, bool hold = true, byte[]? initialization = null)
    {
        NativeBattleEnemyLevel level = new(400, 10, 10, 10, 10, 10, new int[8], [], [], 0);
        NativeBattleAiDefinition ai = new([initialization ?? [0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "other", Name = "Other", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, SceneSlot = 0, Level = 1, MaximumHp = 400, CurrentHp = 400, NativeCombatantFlags = 1, InitialAtb = 0, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()), NativeAi = ai }
            ]
        }, mode);
        battle.ConfigureDiagnosticScheduling(hold, readinessActorId: "party");
        battle.Tick();
        return battle;
    }
}
