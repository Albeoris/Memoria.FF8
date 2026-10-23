using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEscapeClockTests
{
    [Fact]
    public void Holding_escape_with_a_ready_player_runs_at_normal_pace_without_changing_mode()
    {
        BattleSession battle = Create();
        BattleSession normal = Create(mode: BattleAtbMode.Normal);
        battle.PublishNativeEscapeButtons(3);
        normal.PublishNativeEscapeButtons(3);

        Tick(battle, 12);
        Tick(normal, 12);

        Assert.Equal(BattleAtbMode.TurnBased, battle.Mode);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(normal.Frame, battle.Frame);
        AssertGaugesEqual(normal, battle);
        Assert.True(battle.Combatants.Single(actor => actor.Id == "waiting").Atb > 0);
        Assert.True(battle.Combatants.Single(actor => actor.Id == "enemy").Atb > 0);
    }

    [Fact]
    public void Holding_escape_without_a_ready_actor_suppresses_turn_based_fast_forward()
    {
        BattleSession battle = Create(readyPlayer: false);
        BattleSession normal = Create(readyPlayer: false, mode: BattleAtbMode.Normal);
        battle.PublishNativeEscapeButtons(3);

        battle.Tick();
        normal.Tick();

        Assert.Equal(1, battle.Frame);
        Assert.Equal(BattleAtbMode.TurnBased, battle.Mode);
        AssertGaugesEqual(normal, battle);
        Assert.All(battle.Combatants, actor => Assert.False(actor.IsReady));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Releasing_either_button_restores_the_ready_player_decision_hold(ushort buttons)
    {
        BattleSession battle = Create();
        battle.PublishNativeEscapeButtons(3);
        Tick(battle, 5);
        int[] gauges = battle.Combatants.Select(actor => actor.Atb).ToArray();

        battle.PublishNativeEscapeButtons(buttons);
        Tick(battle, 10);

        Assert.Equal(gauges, battle.Combatants.Select(actor => actor.Atb).ToArray());
        Assert.Equal(BattleAtbMode.TurnBased, battle.Mode);
        Assert.False(battle.Escape.Held);
        Assert.Equal(0, battle.Escape.HeldFrames);
    }

    [Fact]
    public void Json_restoration_preserves_held_clock_and_release_restores_the_decision_hold()
    {
        BattleSession battle = Create();
        battle.PublishNativeEscapeButtons(3);
        Tick(battle, 5);
        BattleSession restored = RoundTrip(battle);

        Tick(battle, 7);
        Tick(restored, 7);

        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(BattleAtbMode.TurnBased, restored.Mode);
        restored.PublishNativeEscapeButtons(1);
        restored = RoundTrip(restored);
        int[] gauges = restored.Combatants.Select(actor => actor.Atb).ToArray();
        Tick(restored, 7);

        Assert.Equal(gauges, restored.Combatants.Select(actor => actor.Atb).ToArray());
        Assert.False(restored.Escape.Held);
        Assert.Equal(BattleAtbMode.TurnBased, restored.Mode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Terminal_escape_or_debug_victory_stops_the_held_clock(bool debugVictory)
    {
        BattleSession battle = Create(flags: debugVictory ? (ushort)1 : (ushort)0);
        battle.PublishNativeEscapeButtons(3);
        if (debugVictory)
        {
            Tick(battle, 5);
            battle.InstantVictory();
        }
        else
        {
            Tick(battle, 60);
        }
        Assert.Equal(debugVictory ? BattleOutcome.Victory : BattleOutcome.Escaped, battle.Outcome);
        long frame = battle.Frame;
        int[] gauges = battle.Combatants.Select(actor => actor.Atb).ToArray();

        Tick(battle, 10);

        Assert.Equal(frame, battle.Frame);
        Assert.Equal(gauges, battle.Combatants.Select(actor => actor.Atb).ToArray());
        Assert.Equal(BattleAtbMode.TurnBased, battle.Mode);
    }

    [Fact]
    public void Holding_escape_cancels_a_pending_explicit_turn_advance()
    {
        BattleSnapshot snapshot = Create().Capture();
        BattleSession battle = BattleSession.Restore(snapshot with { TurnAdvance = new(["enemy"], ["ready"], 100) });
        Assert.NotNull(battle.Capture().TurnAdvance);
        battle.PublishNativeEscapeButtons(3);

        battle.Tick();

        Assert.Null(battle.Capture().TurnAdvance);
        Assert.Equal(1, battle.Frame);
        battle.PublishNativeEscapeButtons(0);
        int[] gauges = battle.Combatants.Select(actor => actor.Atb).ToArray();
        Tick(battle, 5);
        Assert.Equal(gauges, battle.Combatants.Select(actor => actor.Atb).ToArray());
    }

    private static BattleSession Create(bool readyPlayer = true, BattleAtbMode mode = BattleAtbMode.TurnBased, ushort flags = 1)
    {
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 1, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        return new(new BattleDefinition
        {
            NativeFlags = flags,
            InitialRandom = NativeBattleRandomState.ForecastPhase(1),
            Combatants =
            [
                new() { Id = "ready", Name = "Ready", IsPlayer = true, Speed = 1, InitialAtb = readyPlayer ? BattleSession.AtbMaximum : 0 },
                new() { Id = "waiting", Name = "Waiting", IsPlayer = true, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, Speed = 1, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 0 } }
            ]
        }, mode);
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }

    private static void AssertGaugesEqual(BattleSession expected, BattleSession actual)
    {
        Assert.Equal(expected.Combatants.Select(actor => actor.Atb).ToArray(), actual.Combatants.Select(actor => actor.Atb).ToArray());
    }

    private static void Tick(BattleSession battle, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            battle.Tick();
        }
    }
}
