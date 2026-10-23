using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class LimitOncePerTurnTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(1)]
    public void Cycling_and_direct_refresh_reuse_first_result_after_json_restore(int hp)
    {
        BattleSession battle = Create(hp);
        battle.SelectPlayerCommandActor("party");
        byte crisis = battle.GetNativeLimitLevel("party");
        if (hp == 100)
        {
            Assert.Equal((byte)0, crisis);
        }
        if (hp == 1)
        {
            Assert.True(crisis > 0);
        }
        BattleSession restored = RoundTrip(battle);
        string random = JsonSerializer.Serialize(restored.Capture().Random);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            restored.SelectPlayerCommandActor("ally");
            restored.SelectPlayerCommandActor("party");
            Assert.Equal(crisis, restored.RefreshNativeLimit("party"));
        }
        Assert.Equal(random, JsonSerializer.Serialize(restored.Capture().Random));
        Assert.Equal(crisis, restored.Capture().Combatants[0].LimitTurnCrisis);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completed_attack_and_refilled_atb_allow_exactly_one_new_check(bool nativeGauge)
    {
        BattleSession battle = Create(100, nativeGauge: nativeGauge);
        battle.SelectPlayerCommandActor("party");
        battle.QueueAttack("party", "enemy");
        Assert.Null(battle.Capture().Combatants[0].LimitTurnCrisis);
        battle = RoundTrip(battle);
        for (int frame = 0; frame < 60; frame++)
        {
            battle.Tick();
        }
        battle.AdvanceTurn();
        Assert.True(battle.CanChoosePlayerCommand("party"));
        AssertOneNewDraw(battle);
    }

    [Fact]
    public void Death_and_revival_clear_failed_check_before_next_full_gauge()
    {
        BattleSession battle = Create(100, enemyReady: true);
        battle.SelectPlayerCommandActor("party");
        battle.QueueAttack("enemy", "party");
        for (int frame = 0; frame < 25; frame++)
        {
            battle.Tick();
        }
        Assert.False(battle.Combatants[0].IsAlive);
        Assert.Null(battle.Capture().Combatants[0].LimitTurnCrisis);
        battle = RoundTrip(battle);
        battle.QueueAction("ally", "party", BattleAbility.Magic, 24);
        for (int frame = 0; frame < 25; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.Combatants[0].IsAlive, JsonSerializer.Serialize(battle.Events));
        battle.AdvanceTurn();
        Assert.True(battle.CanChoosePlayerCommand("party"));
        AssertOneNewDraw(battle);
    }

    private static void AssertOneNewDraw(BattleSession battle)
    {
        NativeBattleRandomState expected = battle.Capture().Random!.Draw(out _);
        battle.SelectPlayerCommandActor("party");
        battle.SelectPlayerCommandActor("party");
        battle.RefreshNativeLimit("party");
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }

    private static BattleSession Create(int hp, bool enemyReady = false, bool nativeGauge = false)
    {
        BattleNativeLimitDefinition limit = new(3, 15, 0, 250, Enumerable.Repeat((byte)0, 32).ToImmutableArray());
        return new(new BattleDefinition
        {
            NativeClockRules = nativeGauge ? new(10) : null,
            NativeBattleSpeed = 2,
            LimitOncePerTurn = true,
            NativeLimitsDisabled = false,
            NativeSeed = Enumerable.Range(0, 256).Select(value => (byte)value).First(seed =>
            {
                NativeBattleRandomState.Initialize(seed).Draw(out byte roll);
                return NativeBattleLimitRules.CrisisLevel(limit, 1, 100, 0, 0, 0, roll) > 0;
            }),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 100, CurrentHp = hp, InitialAtb = BattleSession.AtbMaximum, Speed = 255, NativeDeathCount = 0, NativeLimit = limit },
                new() { Id = "ally", Name = "Ally", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, Speed = 1, Abilities = [BattleAbility.Magic], MagicStocks = [new(24, 1)] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, InitialAtb = enemyReady ? BattleSession.AtbMaximum : 0, Speed = 1, PhysicalPower = 255 }
            ],
            ActionDefinitions = [new(BattleAbility.Magic, 24, "Life", BattleActionEffect.Revive, 0, true)]
        }, BattleAtbMode.TurnBased);
    }
}
