using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAtbTests
{
    [Theory]
    [InlineData(true, 0u, 50)]
    [InlineData(true, 2u, 75)]
    [InlineData(true, 4u, 25)]
    [InlineData(true, 8u, 0)]
    [InlineData(false, 0u, 50)]
    [InlineData(false, 2u, 75)]
    [InlineData(false, 4u, 25)]
    [InlineData(false, 8u, 0)]
    public void TimeStatusesControlPlayerAndEnemyGaugeOnEveryTickAndAfterJsonRestore(bool player, uint status, int gain)
    {
        BattleDefinition definition = new()
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeStatusRules = new(2, Enumerable.Repeat((byte)30, 14).ToImmutableArray()),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, Speed = 20, NativeStatus1 = player ? status : 0 },
                new() { Id = "enemy", Name = "Enemy", Speed = 20, NativeStatus1 = player ? 0 : status }
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int tick = 1; tick <= 12; tick++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(tick * gain, battle.Combatants.Single(actor => actor.IsPlayer == player).NativeAtb!.Current);
            Assert.Equal(tick * 50, battle.Combatants.Single(actor => actor.IsPlayer != player).NativeAtb!.Current);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
    }

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
