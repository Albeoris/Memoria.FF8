using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHitPointWordTests
{
    [Theory]
    [InlineData(100, 1, 101)]
    [InlineData(100, -1, 99)]
    [InlineData(1, -1, 0)]
    [InlineData(1, -2, -1)]
    public void SignedWordAddsToRawDwordWithoutClampingOrRecomputingFlags(int before, short change, int after)
    {
        ushort word = unchecked((ushort)change);
        NativeBattleAiDefinition ai = Program([60, (byte)word, (byte)(word >> 8), 0]);
        BattleSession battle = new(new() { Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 100, CurrentHp = before, NativeAi = ai }] }, BattleAtbMode.Normal);
        battle.Tick();
        BattleCombatant enemy = battle.Combatants[1];
        Assert.Equal(after, enemy.CurrentHp);
        Assert.True(enemy.IsAlive);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.DoesNotContain(battle.Events, item => item.Kind is "Damage" or "Defeated");
        ushort flags = battle.Capture().Combatants[1].NativeStatus!.Saved;
        Assert.Equal(before < 25 ? (ushort)0x300 : (ushort)0, flags);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Fact]
    public void HpConditionsCompareTheWholeUnsignedDwordAfterUnderflow()
    {
        NativeBattleAiDefinition ai = Program([60, 0xFE, 0xFF, 2, 0, 200, 2, 255, 255, 3, 0, 15, 96, 1, 0]);
        BattleSession battle = new(new() { Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 100, CurrentHp = 1, NativeAi = ai }] }, BattleAtbMode.Normal);
        battle.Tick();
        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
    }

    private static NativeBattleAiDefinition Program(byte[] init)
    {
        return new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
    }
}
