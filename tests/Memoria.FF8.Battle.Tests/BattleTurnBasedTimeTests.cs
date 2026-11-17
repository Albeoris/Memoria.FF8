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
        for (int frame = 0; frame < 1200 && !(battle.Events.Any(item => item.Kind == "Damage" && item.ActorId == "player" && item.Message.StartsWith("Poison:"))); frame++)
        {
            battle.Tick();
        }

        Assert.InRange(battle.Combatants[0].CurrentHp, 910, 950);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.Message.StartsWith("Poison:"));
        Assert.Equal(0, battle.Combatants[2].Atb);
    }

    [Fact]
    public void ReadyAllyHoldsPoisonRegenAndOtherGaugesWhileAnActionContinues()
    {
        BattleCombatantDefinition ally = Actor("ally", true, 100) with
        {
            InitialAtb = BattleSession.AtbMaximum,
            CurrentHp = 500,
            NativeStatus0 = 2,
            NativeStatus1 = 0x10
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
        Assert.Equal(before.Combatants[1].NativeStatus!.Timers, battle.Capture().Combatants[1].NativeStatus!.Timers);
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
