using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiLastAttackerOperandTests
{
    [Theory]
    [InlineData(14, 0, 0u)]
    [InlineData(14, 1, 1u)]
    [InlineData(14, 6, 6u)]
    [InlineData(18, 0, 255u)]
    [InlineData(18, 1, 256u)]
    [InlineData(18, 6, 261u)]
    public void Operand203UsesAuthoritativeAttackerHistoryInsteadOfDiagnosticIdentity(byte opcode, byte physical, uint expected)
    {
        byte[] script = opcode == 14 ? [14, 220, 203, 0] : [14, 220, 255, 18, 220, 203, 0];
        NativeBattleAiDefinition ai = new([script, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", ModelId = 1, NativeAi = ai }]
        }, BattleAtbMode.Normal);
        BattleCombatant enemy = battle.Combatants[1];
        typeof(BattleCombatant).GetProperty("LastNativeAction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(enemy, new NativeBattleLastAction(null, null, null, null, null, physical));
        typeof(BattleCombatant).GetProperty("LastAttackerId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(enemy, "enemy");

        battle.Tick();

        Assert.Equal(expected, battle.Capture().Combatants[1].NativeParticipant!.Variables[0]);
        Assert.Equal(physical, battle.Capture().Combatants[1].NativeParticipant!.LastAction!.AttackerIndex);
    }
}
