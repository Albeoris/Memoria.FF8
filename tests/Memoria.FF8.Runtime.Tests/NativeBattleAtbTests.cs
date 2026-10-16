using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAtbTests
{
    [Theory]
    [InlineData(0u, 50)]
    [InlineData(2u, 75)]
    [InlineData(4u, 25)]
    [InlineData(6u, 25)]
    public void NativeGaugeUsesSpeedPlusThirtyAndSlowOverridesHaste(uint statuses, int expected)
    {
        NativeBattleAtbState state = new(12000, 0);
        Assert.Equal(expected, state.Advance(20, 10, 1, 0, statuses).Current);
        Assert.Equal(30, state.Advance(0, 10, 1, 0, 0).Current);
    }

    [Theory]
    [InlineData(0u, 0, 0u)]
    [InlineData(0x81u, 0, 0u)]
    [InlineData(1u, 1, 0u)]
    [InlineData(1u, 4, 0u)]
    [InlineData(1u, 0, 1u)]
    [InlineData(1u, 0, 8u)]
    public void OriginalClockGuardsFreezeOnlyTheirOwnParticipant(uint flags, ushort saved, uint statuses)
    {
        NativeBattleAtbState state = new(12000, 137);
        Assert.Same(state, state.Advance(20, 10, flags, saved, statuses));
    }

    [Fact]
    public void BusyBitsDoNotStopGaugeGrowthAndRawPrecisionSurvivesStandaloneSerialization()
    {
        NativeBattleAtbState state = new(12000, 137);
        state = state.Advance(1, 1, 0xD, 0, 0);
        Assert.Equal(140, state.Current);
        Assert.Equal(116, state.Display);
        NativeBattleAtbState restored = JsonSerializer.Deserialize<NativeBattleAtbState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(state, restored);
        Assert.Equal(state.Advance(1, 1, 1, 0, 0), restored.Advance(1, 1, 1, 0, 0));
        Assert.Equal(12000, (state with { Current = 11999 }).Advance(255, 255, 1, 0, 0).Current);
    }

    [Fact]
    public void StandaloneBattleKeepsTheRawGaugeRemainderAndRejectsMissingOrContradictoryOwners()
    {
        BattleDefinition definition = new()
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            Combatants =
            [
                new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.Tick();
        BattleSnapshot saved = battle.Capture();
        Assert.Equal(new(12000, 31), saved.Combatants[0].NativeAtb);
        Assert.Equal(25, saved.Combatants[0].Atb);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        BattleCombatantSnapshot[] missing = saved.Combatants.ToArray();
        missing[0] = missing[0] with { NativeAtb = null };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { Combatants = missing }));
        BattleCombatantSnapshot[] contradictory = saved.Combatants.ToArray();
        contradictory[0] = contradictory[0] with { NativeAtb = new(8000, 20) };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { Combatants = contradictory }));
    }
}
