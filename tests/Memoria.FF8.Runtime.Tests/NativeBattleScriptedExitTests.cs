using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedExitTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0x200, true)]
    public void ScriptedExitKeepsNativeResultOneAndDoesNotBecomeVictory(ushort mode, bool canReturn)
    {
        NativeBattleAiDefinition ai = new([[57, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new() { NativeFieldBattleMode = mode, Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", NativeAi = ai }] }, BattleAtbMode.Normal);
        Assert.Equal(BattleOutcome.ScriptedExit, battle.Outcome);
        Assert.Equal((byte)1, battle.NativeResult);
        Assert.Equal(canReturn, battle.CanReturnToField);
        Assert.Empty(battle.EnemyDefeats);
        Assert.Empty(battle.QueuedActions);
        Assert.True(battle.Combatants.All(actor => actor.IsAlive));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }
}
