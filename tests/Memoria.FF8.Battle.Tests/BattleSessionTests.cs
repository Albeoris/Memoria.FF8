using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class BattleSessionTests
{
    [Fact]
    public void Detached_logical_session_rejects_debug_outcomes_without_mutation()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), Enemy()), BattleAtbMode.TurnBased);
        string before = JsonSerializer.Serialize(session.Capture());

        Assert.Throws<InvalidOperationException>(session.InstantDefeat);
        Assert.Throws<InvalidOperationException>(session.InstantVictory);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        new BattleInput(BattleInputKind.InstantDefeat).Validate();
    }

    [Fact]
    public void AdvanceTurnLetsAnotherPlayerBecomeReadyWhilePreservingWaitingPlayer()
    {
        BattleCombatantDefinition ally = Player(speed: 170) with { Id = "ally", Name = "Ally" };
        BattleSession session = BattleTurnClockTestFixture.Create(Player(initialAtb: BattleSession.AtbMaximum), ally, Enemy(speed: 1));
        BattleTurnClockTestFixture.Step(session);
        Assert.Equal(0, session.Frame);
        Assert.Equal(10, session.AdvanceTurn());
        Assert.True(session.Combatants[0].IsReady);
        Assert.True(session.Combatants[1].IsReady);
        BattleTurnClockTestFixture.Step(session);
        Assert.Equal(10, session.Frame);
    }

    [Fact]
    public void AlreadyReadyActorsArePreservedAndAllReadyAdvanceIsZero()
    {
        BattleSession session = BattleTurnClockTestFixture.Create(Player(initialAtb: BattleSession.AtbMaximum), Enemy(initialAtb: BattleSession.AtbMaximum));
        Assert.Equal(0, session.AdvanceTurn());
        Assert.Equal(0, session.Frame);
        Assert.All(session.Combatants, actor => Assert.True(actor.IsReady));
    }

    [Fact]
    public void InvalidAttackNeverConsumesGaugeOrAddsCommands()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), Enemy(), Enemy() with { Id = "fallen", CurrentHp = 0, NativeStatus0 = 1 }), BattleAtbMode.TurnBased);
        string before = JsonSerializer.Serialize(session.Capture());
        Assert.Throws<InvalidOperationException>(() => session.QueueAttack("player", "fallen"));
        Assert.Throws<ArgumentException>(() => session.QueueAttack("player", "missing"));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    [Fact]
    public void AttackRetainsAnExplicitSameSideTarget()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum), Enemy()), BattleAtbMode.TurnBased);
        session.QueueAttack("player", "player");
        BattleAttackCommand command = Assert.Single(session.QueuedActions);
        Assert.Equal("player", command.TargetId);
        Assert.Equal(BattleAbility.Attack, command.Ability);
        Assert.Equal(0, session.Combatants[0].Atb);
        Assert.True(session.Combatants[0].IsBusy);
    }

    [Fact]
    public void UnsupportedAbilityFailsBeforePublishingSession()
    {
        Assert.Throws<NotSupportedException>(() => new BattleSession(Definition(Player() with { Abilities = [BattleAbility.Attack, (BattleAbility)255] }, Enemy()), BattleAtbMode.Normal));
    }

    [Fact]
    public void FastForwardIsBoundedAndFailureLeavesActualClockUnchanged()
    {
        BattleSession session = BattleTurnClockTestFixture.Create(Player(speed: 1), Enemy(speed: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceTurn(BattleSession.MaximumAdvanceFrames + 1));
        Assert.Equal(0, session.Frame);
        Assert.Throws<TimeoutException>(() => session.AdvanceTurn(3));
        Assert.Equal(0, session.Frame);
        Assert.All(session.Combatants, actor => Assert.Equal(0, actor.Atb));
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
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { QueuedActions = [new BattleAttackCommand("player", "enemy", new BattleTargetRequest(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy")]), BattleTargetEligibility.Living, false, BattleTargetReplacement.None))] }));
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
        BattleNativeAction attack = new(1, 0, 4, 0) { Name = "Attack", MagicId = 0, AttackType = 1, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 0, DamagePresentation = 4, Element = 0, ElementPercent = 0, StatusAttack = 100, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 255, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleSession session = BattleTurnClockTestFixture.Create(Player(speed: 255, initialAtb: BattleSession.AtbMaximum) with { NativeAttack = attack }, Enemy(speed: 170));
        session.QueueAttack("player", "enemy");
        Assert.Equal(10, session.AdvanceTurn());
        Assert.False(session.Combatants[0].IsReady);
        Assert.True(session.Combatants[1].IsReady);
        Assert.Null(session.ActiveAction);
        Assert.Single(session.QueuedActions);
    }

    [Fact]
    public void ExposedCombatantCollectionsAndQueuedTargetsCannotMutateOwningState()
    {
        BattleSession session = new(Definition(Player(initialAtb: BattleSession.AtbMaximum) with { EquipmentIds = [4], NativeStatus1 = 2 }, Enemy(speed: 1)), BattleAtbMode.TurnBased);
        BattleCombatant actor = session.Combatants[0];
        Assert.Throws<NotSupportedException>(() => ((IList<BattleCombatant>)session.Combatants)[0] = session.Combatants[1]);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)actor.EquipmentIds)[0] = 42);
        Assert.Throws<NotSupportedException>(() => ((IList<BattleAbility>)actor.Abilities)[0] = BattleAbility.Magic);
        session.QueueAttack("player", "enemy");
        BattleAttackCommand[] exposedQueue = (BattleAttackCommand[])session.QueuedActions;
        exposedQueue[0] = new BattleAttackCommand("enemy", "player", new BattleTargetRequest(new BattleTargetSelection.Explicit([new BattlePartyActorReference("player")]), BattleTargetEligibility.Living, false, BattleTargetReplacement.None));
        Assert.Equal("player", session.QueuedActions[0].ActorId);
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

    private static void AssertEquivalent(BattleSession first, BattleSession second)
    {
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
    }

}
