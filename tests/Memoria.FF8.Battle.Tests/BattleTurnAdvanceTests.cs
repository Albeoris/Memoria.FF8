using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class BattleTurnAdvanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OneExplicitPassContinuesThroughEmptyEnemyTurnsUntilAnotherPlayerIsReady(bool changesVariable)
    {
        BattleSession battle = Create(changesVariable ? [19, 96, 1, 0] : [0]);

        Assert.True(battle.AdvanceTurn() > 0);
        Assert.True(battle.Combatants[2].IsReady);
        Assert.False(battle.Combatants[1].IsReady);
        Assert.Null(battle.ActiveAction);
        Assert.NotNull(battle.Capture().TurnAdvance);

        TickUntilAllyReady(battle);

        Assert.True(battle.Combatants[0].IsReady);
        Assert.Equal(BattleSession.AtbMaximum, battle.Combatants[0].Atb);
        Assert.Null(battle.ActiveAction);
        Assert.Null(battle.Capture().TurnAdvance);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind is "Windup" or "Damage");
        if (changesVariable)
        {
            Assert.True(battle.Capture().NativeAiVariables![0] >= 2);
        }
        long stoppedAt = battle.Frame;
        for (int frame = 0; frame < 5; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(stoppedAt, battle.Frame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PendingEmptyTurnContinuationSurvivesJsonRestore(int ticksBeforeRestore)
    {
        BattleSession battle = Create([19, 96, 1, 0]);
        battle.AdvanceTurn();
        for (int frame = 0; frame < ticksBeforeRestore; frame++)
        {
            battle.Tick();
        }
        Assert.NotNull(battle.Capture().TurnAdvance);
        BattleSession restored = Resume(battle);

        for (int frame = 0; frame < 100 && !battle.Combatants[1].IsReady; frame++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(Json(battle), Json(restored));
        }

        Assert.True(battle.Combatants[1].IsReady);
        Assert.Null(restored.Capture().TurnAdvance);
    }

    [Fact]
    public void RealEnemyAttackEndsThePassAndKeepsTheWaitingPlayerReady()
    {
        BattleSession battle = Create(null);
        battle.AdvanceTurn();
        battle.Tick();

        Assert.Equal("enemy", battle.ActiveAction!.ActorId);
        Assert.True(battle.Combatants[0].IsReady);
        Assert.False(battle.Combatants[1].IsReady);
        Assert.Null(battle.Capture().TurnAdvance);
        long stoppedAt = battle.Frame;
        int allyAtb = battle.Combatants[1].Atb;
        for (int frame = 0; frame < BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames + 5; frame++)
        {
            battle.Tick();
        }

        Assert.True(battle.Combatants[0].IsReady);
        Assert.Equal(allyAtb, battle.Combatants[1].Atb);
        Assert.False(battle.Combatants[1].IsReady);
        Assert.Null(battle.ActiveAction);
        Assert.InRange(battle.Frame - stoppedAt, 0, BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames + 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AiWaitOrMessageEndsTheExplicitPass(bool message)
    {
        BattleSession battle = Create(message ? [26, 0, 0] : [28, 1, 0]);
        battle.AdvanceTurn();
        for (int frame = 0; frame < 5 && battle.Capture().TurnAdvance is not null; frame++)
        {
            battle.Tick();
        }

        Assert.Null(battle.Capture().TurnAdvance);
        Assert.False(battle.Combatants[1].IsReady);
        for (int frame = 0; frame < 70; frame++)
        {
            battle.Tick();
        }
        Assert.False(battle.Combatants[1].IsReady);
        Assert.True(battle.Combatants[0].IsReady);
        if (message)
        {
            Assert.Contains(battle.Events, entry => entry.Kind == "NativeText" && entry.Message == "Enemy message");
        }
    }

    [Fact]
    public void AcceptedPlayerAttackCancelsThePendingPass()
    {
        BattleSession battle = Create([0]);
        battle.AdvanceTurn();
        Assert.NotNull(battle.Capture().TurnAdvance);

        battle.QueueAttack("player", "enemy");

        Assert.Null(battle.Capture().TurnAdvance);
        Assert.False(battle.Combatants[0].IsReady);
        Assert.Contains(battle.QueuedActions, command => command.ActorId == "player");
    }

    [Fact]
    public void LeavingTurnBasedModeCancelsThePassEvenWhenReturningBeforeTheNextTick()
    {
        BattleSession battle = Create([0]);
        battle.AdvanceTurn();
        Assert.NotNull(battle.Capture().TurnAdvance);

        battle.SetMode(BattleAtbMode.Normal);
        battle.SetMode(BattleAtbMode.TurnBased);

        Assert.Null(battle.Capture().TurnAdvance);
        for (int frame = 0; frame < 8; frame++)
        {
            battle.Tick();
        }
        Assert.False(battle.Combatants[1].IsReady);
    }

    [Fact]
    public void ANewlyReadyPlayerEndsThePassAtItsFirstBoundary()
    {
        BattleSession battle = Create([0], allySpeed: 2000);

        Assert.True(battle.AdvanceTurn() > 0);

        Assert.True(battle.Combatants[1].IsReady);
        Assert.False(battle.Combatants[2].IsReady);
        Assert.Null(battle.Capture().TurnAdvance);
        string before = Json(battle);
        battle.Tick();
        Assert.Equal(before, Json(battle));
    }

    [Fact]
    public void BoundedExplicitPassFailurePreservesTheSnapshot()
    {
        BattleSession battle = Create([0]);
        string before = Json(battle);

        Assert.Throws<TimeoutException>(() => battle.AdvanceTurn(1));

        Assert.Equal(before, Json(battle));
    }

    [Fact]
    public void RestoreRejectsANegativePendingPassBudget()
    {
        BattleSession battle = Create([0]);
        battle.AdvanceTurn();
        BattleSnapshot snapshot = battle.Capture();
        Assert.NotNull(snapshot.TurnAdvance);

        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { TurnAdvance = snapshot.TurnAdvance! with { RemainingFrames = -1 } }));
    }

    [Fact]
    public void EmptyTurnsCannotExtendTheOriginalLogicalBudget()
    {
        BattleSession battle = Create([0]);
        Assert.Equal(10, battle.AdvanceTurn(10));
        battle.Tick();
        string before = Json(battle);

        Assert.Throws<TimeoutException>(() => battle.Tick());

        Assert.Equal(before, Json(battle));
    }

    [Fact]
    public void PostActionEffectFromAnEmptyDecisionStopsContinuation()
    {
        BattleSession battle = Create([0], enemyPoisoned: true);
        battle.AdvanceTurn();
        int allyAtb = battle.Combatants[1].Atb;
        for (int frame = 0; frame < 100 && !battle.Events.Any(entry => entry.Kind == "Damage"); frame++)
        {
            battle.Tick();
        }

        Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.Message.StartsWith("Poison:"));
        Assert.Null(battle.Capture().TurnAdvance);
        Assert.Equal(allyAtb, battle.Combatants[1].Atb);
        Assert.True(battle.Combatants[0].IsReady);
    }

    private static BattleSession Create(byte[]? turnScript, int allySpeed = 100, bool enemyPoisoned = false)
    {
        NativeBattleAiDefinition? ai = turnScript is null ? null : new([[0], turnScript, [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, ["Enemy message"]);
        BattleSession battle = new(new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                Actor("player", true, 1) with { InitialAtb = BattleSession.AtbMaximum },
                Actor("ally", true, allySpeed),
                Actor("enemy", false, 1000) with { NativeAi = ai, NativeStatus0 = enemyPoisoned ? (ushort)2 : (ushort)0 }
            ]
        }, BattleAtbMode.TurnBased);
        if (ai is not null)
        {
            battle.Tick();
        }
        return battle;
    }

    private static BattleCombatantDefinition Actor(string id, bool player, int speed)
    {
        return new() { Id = id, Name = id, IsPlayer = player, Speed = speed, MaximumHp = 1000, CurrentHp = 1000 };
    }

    private static void TickUntilAllyReady(BattleSession battle)
    {
        for (int frame = 0; frame < 100 && !battle.Combatants[1].IsReady; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.Combatants[1].IsReady);
    }

    private static string Json(BattleSession battle)
    {
        return JsonSerializer.Serialize(battle.Capture());
    }

    private static BattleSession Resume(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(Json(battle))!);
    }
}
