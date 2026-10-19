using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class BattleSessionTests
{
    [Fact]
    public void InstantDefeatClearsQueuedAndActiveActionsAndSurvivesInMemorySnapshotRestore()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), Enemy(initialAtb: BattleSession.AtbMaximum)), BattleAtbMode.TurnBased);
        session.QueueAttack("player", "enemy");
        session.Tick();
        Assert.NotNull(session.ActiveAction);
        session.InstantDefeat();
        Assert.Equal(BattleOutcome.Defeat, session.Outcome);
        Assert.All(session.Combatants.Where(actor => actor.IsPlayer), actor =>
        {
            Assert.Equal(0, actor.CurrentHp);
            Assert.Equal(0, actor.Atb);
        });
        Assert.All(session.Combatants, actor => Assert.False(actor.IsBusy));
        Assert.Empty(session.QueuedActions);
        Assert.Null(session.ActiveAction);
        Assert.Equal(BattleOutcome.Defeat, BattleSession.Restore(session.Capture()).Outcome);
        Assert.Throws<InvalidOperationException>(session.InstantDefeat);
        new BattleInput(BattleInputKind.InstantDefeat).Validate();
    }

    [Fact]
    public void NormalClockFillsGaugesAndEnemyAiQueuesItsOwnReadyTransition()
    {
        BattleSession session = new(Definition(Player(speed: 100), Enemy(speed: 200)), BattleAtbMode.Normal);
        Dispatch(session, 49);
        Assert.Equal(4_900, session.Combatants[0].Atb);
        Assert.Equal(9_800, session.Combatants[1].Atb);
        session.Tick();
        Assert.Equal(50, session.Frame);
        Assert.True(session.Combatants[1].IsBusy);
        Assert.Equal("enemy", session.ActiveAction!.ActorId);
        Assert.Contains(session.Events, item => item.Kind == "Ready" && item.ActorId == "enemy" && item.Frame == 50);
    }

    [Fact]
    public void TurnBasedClockAutomaticallyReachesPlayerReadinessAndPausesForCommand()
    {
        BattleSession session = new(Definition(Player(speed: 1_000), Enemy(speed: 1)), BattleAtbMode.TurnBased);
        session.Tick();
        Assert.True(session.Combatants[0].IsReady);
        Assert.Equal(10, session.Frame);
        session.Tick();
        Assert.Equal(10, session.Frame);
        Assert.Equal(10, session.Combatants[1].Atb);
    }

    [Fact]
    public void TurnBasedEnemiesBecomeReadyAndResolveWithoutManualAdvance()
    {
        BattleSession session = new(Definition(Player(speed: 1), Enemy(speed: 1_000)), BattleAtbMode.TurnBased);
        session.Tick();
        Assert.Equal("enemy", session.ActiveAction!.ActorId);
        Dispatch(session, BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames);
        Assert.Null(session.ActiveAction);
        Assert.Equal(46, session.Combatants[0].CurrentHp);
        Assert.Equal(34, session.Frame);
        session.Tick();
        Assert.Equal(35, session.Frame);
        Assert.Empty(session.QueuedActions);
        session.Tick();
        Assert.Equal(45, session.Frame);
        Assert.Equal("enemy", session.ActiveAction!.ActorId);
    }

    [Fact]
    public void TurnBasedClockResumesGaugeGrowthAfterPlayerActionCompletes()
    {
        BattleSession session = new(Definition(Player(speed: 1_000, initialAtb: BattleSession.AtbMaximum), Enemy(speed: 1)), BattleAtbMode.TurnBased);
        session.QueueAttack("player", "enemy");
        Dispatch(session, 1 + BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames);
        Assert.Null(session.ActiveAction);
        Assert.False(session.Combatants[0].IsReady);
        session.Tick();
        Assert.True(session.Combatants[0].IsReady);
        Assert.Equal(35, session.Frame);
        session.Tick();
        Assert.Equal(35, session.Frame);
    }

    [Fact]
    public void TurnBasedReadySleepingPlayerDoesNotPauseStatusExpiry()
    {
        BattleCombatantDefinition sleeping = Player(initialAtb: BattleSession.AtbMaximum) with { StatusEffects = [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Sleep, DurationFrames = 2 }] };
        BattleSession session = new(Definition(sleeping, Enemy(speed: 1)), BattleAtbMode.TurnBased);
        Assert.Throws<InvalidOperationException>(() => session.QueueAttack("player", "enemy"));
        session.Tick();
        Assert.Equal(2, session.Frame);
        Assert.Empty(session.Combatants[0].StatusEffects);
        Assert.True(session.Combatants[0].IsReady);
        session.Tick();
        Assert.Equal(2, session.Frame);
        session.QueueAttack("player", "enemy");
        Assert.Single(session.QueuedActions);
    }

    [Fact]
    public void TurnBasedReadyEnemyDispatchesWhilePlayerWaitsForCommand()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), Enemy(initialAtb: BattleSession.AtbMaximum)), BattleAtbMode.TurnBased);
        session.Tick();
        Assert.Equal(1, session.Frame);
        Assert.Equal("enemy", session.ActiveAction!.ActorId);
        Assert.True(session.Combatants[0].IsReady);
        Dispatch(session, BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames);
        Assert.Equal(46, session.Combatants[0].CurrentHp);
        Assert.Null(session.ActiveAction);
        Assert.Equal((ushort)2, Assert.Single(session.QueuedActions).NativeCallback!.Stage);
        long beforeCallback = session.Frame;
        session.Tick();
        Assert.Empty(session.QueuedActions);
        Assert.Equal(beforeCallback + 1, session.Frame);
        session.Tick();
        Assert.Equal(beforeCallback + 1, session.Frame);
    }

    [Fact]
    public void TurnBasedCommandWaitPausesAndAdvanceIncludesEnemiesWithoutResettingReadyPlayers()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), Enemy(speed: 1_000)), BattleAtbMode.TurnBased);
        session.Tick();
        Assert.Equal(0, session.Frame);
        Assert.Equal(10, session.AdvanceTurn());
        Assert.True(session.Combatants[0].IsReady);
        Assert.Equal(BattleSession.AtbMaximum, session.Combatants[0].Atb);
        Assert.True(session.Combatants[1].IsReady);
        Assert.Null(session.ActiveAction);
        session.Tick();
        Assert.True(session.Combatants[1].IsBusy);
        Assert.Equal("enemy", session.ActiveAction!.ActorId);
    }

    [Fact]
    public void AdvanceTurnLetsAnotherPlayerBecomeReadyWhilePreservingWaitingPlayer()
    {
        BattleCombatantDefinition ally = Player(speed: 1_000) with { Id = "ally", Name = "Ally" };
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), ally, Enemy(speed: 1)), BattleAtbMode.TurnBased);
        session.Tick();
        Assert.Equal(0, session.Frame);
        Assert.Equal(10, session.AdvanceTurn());
        Assert.True(session.Combatants[0].IsReady);
        Assert.True(session.Combatants[1].IsReady);
        session.Tick();
        Assert.Equal(10, session.Frame);
    }

    [Fact]
    public void AdvanceTurnMatchesNormalGaugeAndEffectTimeWithoutPresentingTheNextAction()
    {
        BattleCombatantDefinition player = Player(speed: 100) with
        {
            CurrentHp = 80,
            StatusEffects = [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Regen, Magnitude = 3, TickIntervalFrames = 2, DurationFrames = 90 }]
        };
        BattleDefinition definition = Definition(player, Enemy(speed: 400));
        BattleSession turn = new(definition, BattleAtbMode.TurnBased);
        BattleSession normal = new(definition, BattleAtbMode.Normal);
        int frames = turn.AdvanceTurn();
        Dispatch(normal, frames);
        normal.SetMode(BattleAtbMode.TurnBased);
        Assert.Equal(normal.Frame, turn.Frame);
        Assert.Equal(normal.Combatants[0].Atb, turn.Combatants[0].Atb);
        Assert.Equal(normal.Combatants[0].CurrentHp, turn.Combatants[0].CurrentHp);
        Assert.Equal(normal.Combatants[0].StatusEffects, turn.Combatants[0].StatusEffects);
        Assert.True(turn.Combatants[1].IsReady);
        Assert.Null(turn.ActiveAction);
        Assert.Empty(turn.QueuedActions);
        Assert.Equal(25, frames);
        Assert.Equal(100, turn.Combatants[0].CurrentHp);
    }

    [Fact]
    public void AlreadyReadyActorsArePreservedAndAllReadyAdvanceIsZero()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), Enemy(initialAtb: BattleSession.AtbMaximum)), BattleAtbMode.TurnBased);
        Assert.Equal(0, session.AdvanceTurn());
        Assert.Equal(0, session.Frame);
        Assert.All(session.Combatants, actor => Assert.True(actor.IsReady));
    }

    [Fact]
    public void AttackUsesQueueWindupAndRecoveryAndOtherReadyActorsRemainReady()
    {
        BattleCombatantDefinition ally = Player(initialAtb: BattleSession.AtbMaximum) with { Id = "ally", Name = "Ally" };
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), ally, Enemy(speed: 1)), BattleAtbMode.TurnBased);
        session.QueueAttack("player", "enemy");
        Assert.False(session.Combatants[0].IsReady);
        Assert.True(session.Combatants[0].IsBusy);
        session.Tick();
        Assert.Equal(BattleActionPhase.Windup, session.ActiveAction!.Phase);
        Dispatch(session, BattleSession.AttackWindupFrames - 1);
        Assert.Equal(100, session.Combatants[2].CurrentHp);
        session.Tick();
        Assert.Equal(46, session.Combatants[2].CurrentHp);
        Assert.Equal(BattleActionPhase.Recovery, session.ActiveAction!.Phase);
        Dispatch(session, BattleSession.AttackRecoveryFrames);
        Assert.False(session.Combatants[0].IsBusy);
        Assert.Null(session.ActiveAction);
        Assert.True(session.Combatants[1].IsReady);
        long completedFrame = session.Frame;
        session.Tick();
        Assert.Equal(completedFrame, session.Frame);
    }

    [Fact]
    public void InvalidAttackNeverConsumesGaugeOrAddsCommands()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), Enemy()), BattleAtbMode.TurnBased);
        string before = JsonSerializer.Serialize(session.Capture());
        Assert.Throws<InvalidOperationException>(() => session.QueueAttack("player", "player"));
        Assert.Throws<ArgumentException>(() => session.QueueAttack("player", "missing"));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    [Fact]
    public void DefeatedActorCannotResolveQueuedAttackAndPoisonUsesSharedClock()
    {
        BattleCombatantDefinition doomed = Player(initialAtb: BattleSession.AtbMaximum) with
        {
            CurrentHp = 1,
            StatusEffects = [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Poison, Magnitude = 1, TickIntervalFrames = 1 }]
        };
        BattleCombatantDefinition ally = Player() with { Id = "ally", Name = "Ally" };
        BattleSession session = new(Definition(doomed, ally, Enemy(speed: 1)), BattleAtbMode.Normal);
        session.QueueAttack("player", "enemy");
        session.Tick();
        Assert.False(session.Combatants[0].IsAlive);
        Assert.Equal(0, session.Combatants[0].Atb);
        Assert.False(session.Combatants[0].IsBusy);
        Assert.Empty(session.QueuedActions);
        Assert.Null(session.ActiveAction);
        Assert.Equal(100, session.Combatants[2].CurrentHp);
        Assert.Throws<InvalidOperationException>(() => session.QueueAttack("player", "enemy"));
        AssertEquivalent(session, BattleSession.Restore(session.Capture()));
    }

    [Theory]
    [InlineData(true, BattleOutcome.Victory)]
    [InlineData(false, BattleOutcome.Defeat)]
    public void TerminalOutcomesStopClockAndClearActions(bool playerWins, BattleOutcome outcome)
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum) with { Attack = playerWins ? 1_000 : 20 }, Enemy(initialAtb: BattleSession.AtbMaximum) with { Attack = playerWins ? 20 : 1_000 }), BattleAtbMode.Normal);
        if (playerWins)
        {
            session.QueueAttack("player", "enemy");
        }
        for (int dispatch = 0; dispatch < 2 * (BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames + 2) && session.Outcome == BattleOutcome.Active; dispatch++)
        {
            session.Tick();
        }
        Assert.Equal(outcome, session.Outcome);
        Assert.Equal(playerWins ? new[] { "enemy", "player" } : new[] { "enemy" }, session.Events.Where(item => item.Kind == "Windup").Select(item => item.ActorId));
        Assert.Empty(session.QueuedActions);
        Assert.Null(session.ActiveAction);
        Assert.All(session.Combatants, actor => Assert.False(actor.IsBusy));
        long frame = session.Frame;
        session.Tick();
        Assert.Equal(frame, session.Frame);
        AssertEquivalent(session, BattleSession.Restore(session.Capture()));
    }

    [Fact]
    public void StatusExpiryAndAtbModifiersAreDispatchedPerFrame()
    {
        BattleCombatantDefinition player = Player(speed: 100) with { StatusEffects = [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Haste, DurationFrames = 2 }] };
        BattleSession session = new(Definition(player, Enemy(speed: 1)), BattleAtbMode.Normal);
        Dispatch(session, 2);
        Assert.Equal(400, session.Combatants[0].Atb);
        Assert.Empty(session.Combatants[0].StatusEffects);
        session.Tick();
        Assert.Equal(500, session.Combatants[0].Atb);
    }

    [Fact]
    public void UnsupportedAbilityAndStatusFailBeforePublishingSession()
    {
        Assert.Throws<NotSupportedException>(() => new BattleSession(Definition(Player() with { Abilities = [BattleAbility.Attack, (BattleAbility)255] }, Enemy()), BattleAtbMode.Normal));
        Assert.Throws<NotSupportedException>(() => new BattleSession(Definition(Player() with { StatusEffects = [new BattleStatusEffectDefinition { Kind = (BattleStatusKind)255 }] }, Enemy()), BattleAtbMode.Normal));
    }

    [Fact]
    public void FastForwardIsBoundedAndFailureLeavesActualClockUnchanged()
    {
        BattleSession session = new(Definition(Player(speed: 1), Enemy(speed: 1)), BattleAtbMode.TurnBased);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceTurn(BattleSession.MaximumAdvanceFrames + 1));
        Assert.Equal(0, session.Frame);
        Assert.Throws<TimeoutException>(() => session.AdvanceTurn(3));
        Assert.Equal(0, session.Frame);
        Assert.All(session.Combatants, actor => Assert.Equal(0, actor.Atb));
    }

    [Fact]
    public void JsonRoundTripPreservesWindupRecoveryQueueAndFutureDispatch()
    {
        BattleCombatantDefinition ally = Player(initialAtb: BattleSession.AtbMaximum) with { Id = "ally", Name = "Ally" };
        BattleSession original = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), ally, Enemy(speed: 1)), BattleAtbMode.Normal);
        original.QueueAttack("player", "enemy");
        original.QueueAttack("ally", "enemy");
        original.Tick();
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(original.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        AssertEquivalent(original, restored);
        for (int frame = 0; frame < 60; frame++)
        {
            original.Tick();
            restored.Tick();
            AssertEquivalent(original, restored);
            BattleSession secondRestore = BattleSession.Restore(original.Capture());
            AssertEquivalent(original, secondRestore);
        }
    }

    [Fact]
    public void InvalidSnapshotsFailWithoutChangingSourceSession()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), Enemy()), BattleAtbMode.TurnBased);
        BattleSnapshot snapshot = session.Capture();
        string before = JsonSerializer.Serialize(snapshot);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Frame = -1 }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Version = 2 }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Outcome = BattleOutcome.Victory }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Combatants = [snapshot.Combatants[0] with { Atb = BattleSession.AtbMaximum + 1 }, snapshot.Combatants[1]] }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { QueuedActions = [new BattleAttackCommand("player", "enemy")] }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Events = [new BattleEvent(1, "Damage", "player", "enemy", 1, "Invalid future event")] }));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    [Fact]
    public void DefinitionAndSnapshotArraysDoNotAliasCallerState()
    {
        BattleDefinition definition = Definition(Player(), Enemy());
        BattleSession session = new(definition, BattleAtbMode.Normal);
        definition.Combatants[0] = Player() with { Name = "Mutated" };
        BattleSnapshot snapshot = session.Capture();
        snapshot.Definition.Combatants[0] = Player() with { Name = "Mutated again" };
        Assert.Equal("Player", session.Combatants[0].Name);
        Assert.Equal("Player", session.Capture().Definition.Combatants[0].Name);
    }

    [Fact]
    public void TypedEffectHandlerUsesSameClockAndRestoresWithExplicitHandler()
    {
        BattleDefinition definition = Definition(Player() with { CurrentHp = 80, StatusEffects = [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Protect, TickIntervalFrames = 1, DurationFrames = 2 }] }, Enemy(speed: 1));
        BattleSession session = new(definition, BattleAtbMode.Normal, [new ProtectDemoHandler()]);
        session.Tick();
        Assert.Equal(81, session.Combatants[0].CurrentHp);
        BattleSession restored = BattleSession.Restore(session.Capture(), [new ProtectDemoHandler()]);
        restored.Tick();
        session.Tick();
        AssertEquivalent(session, restored);
        Assert.Throws<NotSupportedException>(() => BattleSession.Restore(session.Capture()));
    }

    [Fact]
    public void DebugVictoryClearsActionsPreservesPartyHpAndRestores()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum) with { CurrentHp = 73 }, Enemy()), BattleAtbMode.TurnBased);
        session.QueueAttack("player", "enemy");
        session.Tick();
        session.InstantVictory();
        Assert.Equal(BattleOutcome.Victory, session.Outcome);
        Assert.Equal(73, session.Combatants[0].CurrentHp);
        Assert.Equal(0, session.Combatants[1].CurrentHp);
        Assert.Empty(session.QueuedActions);
        Assert.Null(session.ActiveAction);
        Assert.Contains(session.Events, item => item.Kind == "debug-victory");
        AssertEquivalent(session, BattleSession.Restore(session.Capture()));
    }

    [Fact]
    public void DefeatedTargetQueuedComponentAttackCancelsAtItsOwnDispatchAndRestoresRecovery()
    {
        BattleCombatantDefinition ally = Player(initialAtb: BattleSession.AtbMaximum) with { Id = "ally", Name = "Ally" };
        BattleCombatantDefinition secondEnemy = Enemy(speed: 1) with { Id = "second", Name = "Second enemy" };
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum) with { Attack = 1_000 }, ally, Enemy(speed: 1), secondEnemy), BattleAtbMode.TurnBased);
        session.QueueAttack("player", "enemy");
        session.QueueAttack("ally", "enemy");
        Dispatch(session, BattleSession.AttackWindupFrames + 1);
        Assert.False(session.Combatants[2].IsAlive);
        Assert.Equal(BattleOutcome.Active, session.Outcome);
        Assert.Equal(BattleActionPhase.Recovery, session.ActiveAction!.Phase);
        Assert.Equal("ally", Assert.Single(session.QueuedActions).ActorId);
        Assert.True(session.Combatants[1].IsBusy);
        BattleSession restored = BattleSession.Restore(session.Capture());
        Dispatch(session, BattleSession.AttackRecoveryFrames);
        Dispatch(restored, BattleSession.AttackRecoveryFrames);
        AssertEquivalent(session, restored);
        session.Tick();
        restored.Tick();
        Assert.Empty(session.QueuedActions);
        Assert.False(session.Combatants[1].IsBusy);
        AssertEquivalent(session, restored);
    }

    [Fact]
    public void SnapshotRejectsInconsistentPeriodicStatusTimer()
    {
        BattleSession session = new(Definition(Player() with { StatusEffects = [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Poison, TickIntervalFrames = 5 }] }, Enemy()), BattleAtbMode.Normal);
        session.Tick();
        BattleSnapshot snapshot = session.Capture();
        BattleCombatantSnapshot actor = snapshot.Combatants[0];
        BattleCombatantSnapshot invalidActor = actor with { StatusEffects = [actor.StatusEffects[0] with { UntilTickFrames = 1 }] };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Combatants = [invalidActor, snapshot.Combatants[1]] }));
        AssertEquivalent(session, BattleSession.Restore(snapshot));
    }

    [Fact]
    public void CostumeIsValidatedExposedAndRoundTrips()
    {
        BattleSession session = new(Definition(Player() with { Costume = 2 }, Enemy()), BattleAtbMode.Normal);
        Assert.Equal(2, session.Combatants[0].Costume);
        Assert.Equal(2, BattleSession.Restore(session.Capture()).Combatants[0].Costume);
        Assert.Throws<ArgumentException>(() => new BattleSession(Definition(Player() with { Costume = -1 }, Enemy()), BattleAtbMode.Normal));
    }

    [Fact]
    public void ExplicitPassDoesNotCompleteTheBusyActorsQueuedAction()
    {
        BattleSession session = new(Definition(Player(speed: BattleSession.AtbMaximum, initialAtb: BattleSession.AtbMaximum), Enemy(speed: 1000)), BattleAtbMode.TurnBased);
        session.QueueAttack("player", "enemy");
        Assert.Equal(10, session.AdvanceTurn());
        Assert.False(session.Combatants[0].IsReady);
        Assert.True(session.Combatants[1].IsReady);
        Assert.Null(session.ActiveAction);
        Assert.Single(session.QueuedActions);
    }

    [Fact]
    public void ExposedCombatantCollectionsAndPresentationCannotMutateOwningState()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum) with { EquipmentIds = [4], StatusEffects = [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Haste }] }, Enemy(speed: 1)), BattleAtbMode.TurnBased);
        BattleCombatant actor = session.Combatants[0];
        Assert.Throws<NotSupportedException>(() => ((IList<BattleCombatant>)session.Combatants)[0] = session.Combatants[1]);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)actor.EquipmentIds)[0] = 42);
        Assert.Throws<NotSupportedException>(() => ((IList<BattleAbility>)actor.Abilities)[0] = BattleAbility.Magic);
        BattleStatusSnapshot[] exposedStatuses = (BattleStatusSnapshot[])actor.StatusEffects;
        exposedStatuses[0] = exposedStatuses[0] with { RemainingFrames = 1 };
        Assert.Equal(300, actor.StatusEffects[0].RemainingFrames);
        session.QueueAttack("player", "enemy");
        BattleAttackCommand[] exposedQueue = (BattleAttackCommand[])session.QueuedActions;
        exposedQueue[0] = new BattleAttackCommand("enemy", "player");
        Assert.Equal("player", session.QueuedActions[0].ActorId);
        session.Tick();
        BattleActionPresentation altered = session.ActiveAction! with { RemainingFrames = 1 };
        Assert.Equal(1, altered.RemainingFrames);
        Assert.Equal(BattleSession.AttackWindupFrames, session.ActiveAction!.RemainingFrames);
    }

    [Fact]
    public void RejectedRestoreCannotDispatchHandlersOrChangeOriginalSession()
    {
        ProtectDispatchSpy handler = new();
        BattleSession original = new(Definition(Player() with { StatusEffects = [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Protect }] }, Enemy(speed: 1)), BattleAtbMode.Normal, [handler]);
        original.Tick();
        string before = JsonSerializer.Serialize(original.Capture());
        BattleSnapshot snapshot = original.Capture();
        BattleCombatantSnapshot invalid = snapshot.Combatants[0] with { Atb = -1 };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Combatants = [invalid, snapshot.Combatants[1]] }, [handler]));
        Assert.Equal(1, handler.DispatchCalls);
        Assert.Equal(before, JsonSerializer.Serialize(original.Capture()));
    }

    [Fact]
    public void SetBattleOwnsValidatedSnapshotAndRejectsInvalidReplacementAtomically()
    {
        BattleSession battle = new(Definition(Player() with { Costume = 1 }, Enemy()), BattleAtbMode.Normal);
        GameSession game = new(new GameState());
        BattleSnapshot source = battle.Capture();
        game.SetBattle(source);
        string before = JsonSerializer.Serialize(game.State.Battle);
        source.Combatants[0] = source.Combatants[0] with { CurrentHp = 0 };
        source.Definition.Combatants[0] = Player() with { Costume = 3 };
        Assert.Equal(before, JsonSerializer.Serialize(game.State.Battle));
        Assert.Throws<InvalidDataException>(() => game.SetBattle(battle.Capture() with { Outcome = BattleOutcome.Victory }));
        Assert.Equal(before, JsonSerializer.Serialize(game.State.Battle));
        game.SetBattle(null);
        Assert.Null(game.State.Battle);
    }

    private static BattleCombatantDefinition Player(int speed = 100, int initialAtb = 0)
    {
        return new BattleCombatantDefinition { Id = "player", Name = "Player", IsPlayer = true, Speed = speed, InitialAtb = initialAtb, NativeDeathCount = 0 };
    }

    private static BattleCombatantDefinition Enemy(int speed = 100, int initialAtb = 0)
    {
        return new BattleCombatantDefinition { Id = "enemy", Name = "Enemy", Speed = speed, InitialAtb = initialAtb };
    }

    private static BattleDefinition Definition(params BattleCombatantDefinition[] actors)
    {
        return new BattleDefinition { Combatants = actors };
    }

    private static void Dispatch(BattleSession session, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            session.Tick();
        }
    }

    private static void AssertEquivalent(BattleSession first, BattleSession second)
    {
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
    }

    private sealed class ProtectDemoHandler : IBattleStatusEffectHandler
    {
        public BattleStatusKind Kind => BattleStatusKind.Protect;
        public int AtbRatePercent => 100;

        public void Validate(BattleStatusEffectDefinition definition)
        {
            Assert.True(definition.DurationFrames > 0 && definition.TickIntervalFrames > 0);
        }

        public void Dispatch(BattleEffectContext context)
        {
            if (context.IsPeriodicTick)
            {
                context.Heal(1);
            }
        }
    }

    private sealed class ProtectDispatchSpy : IBattleStatusEffectHandler
    {
        public BattleStatusKind Kind => BattleStatusKind.Protect;
        public int AtbRatePercent => 100;
        public int DispatchCalls { get; private set; }

        public void Validate(BattleStatusEffectDefinition definition)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(definition.DurationFrames);
        }

        public void Dispatch(BattleEffectContext context)
        {
            // Observation only: no counter influences simulation state or future dispatch behavior.
            DispatchCalls++;
        }
    }
}
