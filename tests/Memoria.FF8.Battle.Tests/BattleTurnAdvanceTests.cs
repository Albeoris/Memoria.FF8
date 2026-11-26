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
            BattleTurnClockTestFixture.Step(battle);
        }
        Assert.Equal(stoppedAt, battle.Frame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PendingEmptyTurnContinuationSurvivesPrivateFork(int ticksBeforeRestore)
    {
        BattleSession battle = Create([19, 96, 1, 0]);
        battle.AdvanceTurn();
        for (int frame = 0; frame < ticksBeforeRestore; frame++)
        {
            BattleTurnClockTestFixture.Step(battle);
        }
        Assert.NotNull(battle.Capture().TurnAdvance);
        BattleSession restored = Resume(battle);

        for (int frame = 0; frame < 100 && !battle.Combatants[1].IsReady; frame++)
        {
            BattleTurnClockTestFixture.Step(battle);
            BattleTurnClockTestFixture.Step(restored);
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
        int allyAtb = battle.Combatants[1].Atb;
        for (int frame = 0; frame < 1200 && !battle.Events.Any(item => item.Kind == "ActionComplete" && item.ActorId == "enemy"); frame++)
        {
            battle.Tick();
        }

        Assert.True(battle.Combatants[0].IsReady);
        Assert.Equal(allyAtb, battle.Combatants[1].Atb);
        Assert.False(battle.Combatants[1].IsReady);
        Assert.Null(battle.ActiveAction);
        Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "enemy");
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
            BattleTurnClockTestFixture.Step(battle);
        }

        Assert.Null(battle.Capture().TurnAdvance);
        Assert.False(battle.Combatants[1].IsReady);
        if (!message)
        {
            Assert.NotEmpty(battle.NativeScriptState.WaitCounters);
        }
        for (int frame = 0; frame < 70; frame++)
        {
            BattleTurnClockTestFixture.Step(battle);
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
        BattleSession battle = RestorePendingPass();
        Assert.NotNull(battle.Capture().TurnAdvance);

        battle.QueueAttack("player", "enemy");

        Assert.Null(battle.Capture().TurnAdvance);
        Assert.False(battle.Combatants[0].IsReady);
        Assert.Contains(battle.QueuedActions, command => command.ActorId == "player");
    }

    [Fact]
    public void NativeEnemyContactAdmissionCancelsThePassBeforeGraphics()
    {
        BattleNativeAction attack = new(8, 1, 3, 1) { Name = "Native attack", MagicId = 0, AttackType = 1, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [.. new byte[20]], SupplementalRecord = [] };
        NativeBattleAiDefinition ai = new([[0], [4, 0, 12, 0, 0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 12, 1), 48).ToArray(), 0, []);
        BattleSession battle = BattleTurnClockTestFixture.Create([attack],
        [
            Actor("player", true, 1) with { InitialAtb = BattleSession.AtbMaximum },
            Actor("ally", true, 20),
            Actor("enemy", false, 170) with { NativeAi = ai }
        ]);
        BattleTurnClockTestFixture.Step(battle);
        battle.AdvanceTurn();
        Assert.NotNull(battle.Capture().TurnAdvance);
        int waitingGauge = battle.Combatants[1].Atb;
        BattleTurnClockTestFixture.DispatchTurnBasedFrameCore(battle);

        Assert.Null(battle.Capture().TurnAdvance);
        Assert.Equal("enemy", battle.ActiveAction?.ActorId);
        Assert.NotNull(battle.Presentation!.ActiveActionToken);
        Assert.Equal(waitingGauge, battle.Combatants[1].Atb);
        Assert.True(battle.Combatants[0].IsReady);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "EffectContact");
    }

    [Fact]
    public void LeavingTurnBasedModeCancelsThePassEvenWhenReturningBeforeTheNextTick()
    {
        BattleSession battle = RestorePendingPass();
        Assert.NotNull(battle.Capture().TurnAdvance);

        battle.SetMode(BattleAtbMode.Normal);
        battle.SetMode(BattleAtbMode.TurnBased);

        Assert.Null(battle.Capture().TurnAdvance);
        Assert.False(battle.Combatants[1].IsReady);
    }

    [Fact]
    public void ANewlyReadyPlayerEndsThePassAtItsFirstBoundary()
    {
        BattleSession battle = Create([0], allySpeed: 250);

        Assert.True(battle.AdvanceTurn() > 0);

        Assert.True(battle.Combatants[1].IsReady);
        Assert.False(battle.Combatants[2].IsReady);
        Assert.Null(battle.Capture().TurnAdvance);
        string before = Json(battle);
        BattleTurnClockTestFixture.Step(battle);
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
        BattleSession battle = RestorePendingPass();
        BattleSnapshot snapshot = battle.Capture();
        Assert.NotNull(snapshot.TurnAdvance);

        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { TurnAdvance = snapshot.TurnAdvance! with { RemainingFrames = -1 } }));
    }

    [Fact]
    public void EmptyTurnsCannotExtendTheOriginalLogicalBudget()
    {
        BattleSession battle = Create([0]);
        Assert.Equal(10, battle.AdvanceTurn(10));
        BattleTurnClockTestFixture.Step(battle);
        string before = Json(battle);

        Assert.Throws<TimeoutException>(() => BattleTurnClockTestFixture.Step(battle));

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

    private static BattleSession RestorePendingPass()
    {
        BattleSession battle = Create(null);
        battle.Combatants[2].Atb = BattleSession.AtbMaximum;
        BattleSnapshot snapshot = battle.Capture() with { TurnAdvance = new(["enemy"], ["player"], 100) };
        return BattleSession.Restore(snapshot);
    }

    private static BattleSession Create(byte[]? turnScript, int allySpeed = 20, bool enemyPoisoned = false)
    {
        NativeBattleAiDefinition? ai = turnScript is null ? null : new([[0], turnScript, [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, ["Enemy message"]);
        BattleDefinition definition = new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                Actor("player", true, 1) with { InitialAtb = BattleSession.AtbMaximum },
                Actor("ally", true, allySpeed),
                Actor("enemy", false, 170) with { NativeAi = ai, NativeStatus0 = enemyPoisoned ? (ushort)2 : (ushort)0 }
            ]
        };
        if (ai is null || enemyPoisoned)
        {
            return new BattleSession(definition, BattleAtbMode.TurnBased);
        }

        BattleSession battle = BattleTurnClockTestFixture.Create(definition.Combatants);
        BattleTurnClockTestFixture.Step(battle);
        return battle;
    }

    private static BattleCombatantDefinition Actor(string id, bool player, int speed)
    {
        return new() { Id = id, Name = id, IsPlayer = player, Speed = speed, MaximumHp = 1000, CurrentHp = 1000, NativeCombatantFlags = player ? 0u : 0x10u };
    }

    private static void TickUntilAllyReady(BattleSession battle)
    {
        for (int frame = 0; frame < 100 && !battle.Combatants[1].IsReady; frame++)
        {
            BattleTurnClockTestFixture.Step(battle);
        }
        Assert.True(battle.Combatants[1].IsReady);
    }

    private static string Json(BattleSession battle)
    {
        return JsonSerializer.Serialize(battle.Capture());
    }

    private static BattleSession Resume(BattleSession battle)
    {
        return battle.ForkForContinuation();
    }
}
