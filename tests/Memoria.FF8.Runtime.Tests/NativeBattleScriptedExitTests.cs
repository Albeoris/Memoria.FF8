using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedExitTests
{
    [Fact]
    public void HitCallbackExitWaitsForTheCurrentActionAndRestoresItsPendingRequest()
    {
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [57, 0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeFieldBattleMode = 0x200, Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy", NativeAi = ai, NativeCombatantFlags = 0x10 }] }, BattleAtbMode.Normal);
        battle.QueueAttack("party", "enemy");
        for (int frame = 0; frame < 100 && !battle.NativeScriptState.ScriptedExitRequested; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeScriptState.ScriptedExitRequested);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(BattleActionPhase.Recovery, battle.ActiveAction!.Phase);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int frame = 0; frame < 100 && battle.Outcome == BattleOutcome.Active; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(BattleOutcome.ScriptedExit, battle.Outcome);
        Assert.Null(battle.ActiveAction);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0x200, true)]
    public void ScriptedExitKeepsNativeResultOneAndDoesNotBecomeVictory(ushort mode, bool canReturn)
    {
        NativeBattleAiDefinition ai = new([[57, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeFieldBattleMode = mode, Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", NativeAi = ai }] }, BattleAtbMode.Normal);
        battle.Tick();
        Assert.Equal(BattleOutcome.ScriptedExit, battle.Outcome);
        Assert.Equal((byte)1, battle.NativeResult);
        Assert.Equal(canReturn, battle.CanReturnToField);
        Assert.Empty(battle.EnemyDefeats);
        Assert.Empty(battle.QueuedActions);
        Assert.True(battle.Combatants.All(actor => actor.IsAlive));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }
}
