using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class BattleTurnBasedTimeTests
{
    [Fact]
    public void NativePoisonKeepsItsActionCompletionTickWhileTurnBasedTimeIsHeld()
    {
        BattleSession battle = Create(Actor("player", true, 100) with { InitialAtb = BattleSession.AtbMaximum, NativeStatus0 = 2 }, Actor("ally", true, 100) with { InitialAtb = BattleSession.AtbMaximum }, Actor("enemy", false, 1));
        battle.QueueAttack("player", "enemy");
        battle.Tick();
        for (int frame = 0; frame < BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }

        Assert.InRange(battle.Combatants[0].CurrentHp, 910, 950);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.Message.StartsWith("Poison:"));
        Assert.Equal(0, battle.Combatants[2].Atb);
    }

    [Fact]
    public void ExplicitPassStopsWhenAnAlreadyFullSleepingAllyWakes()
    {
        BattleCombatantDefinition sleeping = Actor("ally", true, 100) with
        {
            InitialAtb = BattleSession.AtbMaximum,
            StatusEffects = [new() { Kind = BattleStatusKind.Sleep, DurationFrames = 2 }]
        };
        BattleSession battle = Create(Actor("player", true, 100) with { InitialAtb = BattleSession.AtbMaximum }, sleeping, Actor("enemy", false, 1));

        Assert.Equal(2, battle.AdvanceTurn());

        Assert.True(battle.Combatants[0].IsReady);
        Assert.True(battle.Combatants[1].IsReady);
        Assert.Equal(2, battle.Combatants[2].Atb);
    }

    [Fact]
    public void ReadyAllyHoldsPoisonRegenAndOtherGaugesWhileAnActionContinues()
    {
        BattleCombatantDefinition ally = Actor("ally", true, 100) with
        {
            InitialAtb = BattleSession.AtbMaximum,
            CurrentHp = 500,
            StatusEffects =
            [
                new() { Kind = BattleStatusKind.Poison, Magnitude = 3, TickIntervalFrames = 1 },
                new() { Kind = BattleStatusKind.Regen, Magnitude = 1, TickIntervalFrames = 1 }
            ]
        };
        BattleSession battle = Create(Actor("caster", true, 100) with { InitialAtb = BattleSession.AtbMaximum }, ally, Actor("enemy", false, 1));
        battle.QueueAttack("caster", "enemy");
        battle.Tick();
        BattleSnapshot before = battle.Capture();
        Assert.NotNull(before.ActiveAction);
        BattleSession restored = Resume(battle);

        for (int frame = 0; frame < 5; frame++)
        {
            battle.Tick();
            restored.Tick();
        }

        Assert.Equal(before.Combatants.Select(actor => actor.Atb), battle.Combatants.Select(actor => actor.Atb));
        Assert.Equal(500, battle.Combatants[1].CurrentHp);
        Assert.Equal(before.Combatants[1].StatusEffects, battle.Capture().Combatants[1].StatusEffects);
        Assert.Equal(before.ActiveAction!.RemainingFrames - 5, battle.ActiveAction!.RemainingFrames);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void ExplicitPassDuringWindupFillsNextActorWithoutAdvancingTheAction()
    {
        BattleSession battle = Create(Actor("caster", true, 100) with { InitialAtb = BattleSession.AtbMaximum }, Actor("ally", true, 1000), Actor("enemy", false, 1));
        battle.QueueAttack("caster", "enemy");
        battle.Tick();
        BattleActionPresentation before = battle.ActiveAction!;
        BattleSession restored = Resume(battle);

        Assert.Equal(10, battle.AdvanceTurn());
        Assert.Equal(10, restored.AdvanceTurn());

        Assert.True(battle.Combatants[1].IsReady);
        Assert.False(battle.Combatants[0].IsReady);
        Assert.Equal(before, battle.ActiveAction);
        Assert.Equal(1000, battle.Combatants[2].CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        battle.Tick();
        Assert.Equal(before.RemainingFrames - 1, battle.ActiveAction!.RemainingFrames);
    }

    [Fact]
    public void LogicalSkipPresentsProducedRegenThenDispatchesPoisonAndNativeDoomBeforeNextReadiness()
    {
        BattleCombatantDefinition doomed = Actor("doomed", true, 1000) with { NativeStatus1 = 0x400, NativeDeathCount = 0 };
        BattleCombatantDefinition ally = Actor("ally", true, 500) with
        {
            CurrentHp = 500,
            NativeStatus1 = 0x10,
            StatusEffects = [new() { Kind = BattleStatusKind.Poison, Magnitude = 1, TickIntervalFrames = 2 }]
        };
        BattleSession battle = Create(doomed, ally, Actor("enemy", false, 1));
        BattleSnapshot seed = battle.Capture();
        BattleCombatantSnapshot[] actors = seed.Combatants.ToArray();
        short[] doomTimers = actors[0].NativeStatus!.Timers.ToArray();
        short[] regenTimers = actors[1].NativeStatus!.Timers.ToArray();
        doomTimers[10] = 2;
        regenTimers[4] = 60;
        actors[0] = actors[0] with { NativeStatus = actors[0].NativeStatus! with { Timers = doomTimers } };
        actors[1] = actors[1] with { NativeStatus = actors[1].NativeStatus! with { Timers = regenTimers } };
        battle = BattleSession.Restore(seed with { Combatants = actors });
        BattleSession restored = Resume(battle);

        battle.Tick();
        restored.Tick();

        Assert.Equal(1, battle.Frame);
        BattleAttackCommand regen = Assert.Single(battle.QueuedActions, command => command.NativeAction is { Command: 0, ActionId: 4 });
        Assert.Equal("ally", regen.ActorId);
        Assert.Equal("ally", regen.TargetId);
        Assert.NotNull(regen.NativeHeaderIndex);
        Assert.True(regen.NativeAction is { Command: 0, ActionId: 4 });
        // Header production commits the contact before this graphicsless command's presentation completes.
        Assert.Equal(550, battle.Combatants[1].CurrentHp);
        Assert.Equal(58, battle.Capture().Combatants[1].NativeStatus!.Timers[4]);
        Assert.False(battle.Combatants[1].IsReady);
        Assert.Contains(battle.Events, item => item.Kind == "Healing" && item.Message.StartsWith("Regen:") && item.Amount == 50);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        for (int callback = 0; callback < 100 && !battle.Combatants[1].IsReady; callback++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.True(battle.Frame > 20);
        Assert.False(battle.Combatants[0].IsAlive);
        Assert.True(battle.Combatants[1].IsReady);
        Assert.Equal(540, battle.Combatants[1].CurrentHp);
        Assert.Equal(20, battle.Capture().Combatants[1].NativeStatus!.Timers[4]);
        Assert.Contains(battle.Events, item => item.Kind == "Damage" && item.Message.StartsWith("Doom:"));
        Assert.Contains(battle.Events, item => item.Kind == "Healing" && item.Message.StartsWith("Regen:"));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        BattleSnapshot waiting = battle.Capture();
        battle.Tick();
        Assert.Equal(JsonSerializer.Serialize(waiting), JsonSerializer.Serialize(battle.Capture()));
    }

    private static BattleCombatantDefinition Actor(string id, bool player, int speed)
    {
        return new() { Id = id, Name = id, IsPlayer = player, Speed = speed, MaximumHp = 1000, CurrentHp = 1000 };
    }

    private static BattleSession Create(params BattleCombatantDefinition[] actors)
    {
        return new(new() { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, Combatants = actors }, BattleAtbMode.TurnBased);
    }

    private static BattleSession Resume(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }
}
