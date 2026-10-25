using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameSessionAchievementTests : IDisposable
{
    private static readonly AchievementId CustomId = new("memoria.test.accepted_defeats");
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria-achievement-outbox", Guid.NewGuid().ToString("N"));

    [Fact]
    public void InvalidFactRejectsTheWholeAcceptedBatchWithoutChangingItsCursor()
    {
        GameSession session = Session();
        GameState before = session.State;

        Assert.Throws<InvalidDataException>(() => session.AcceptBattleAchievementFacts([Defeat("first"), Defeat("invalid") with { Level = 0 }]));

        Assert.Same(before, session.State);
        Assert.Empty(session.State.PendingAchievementFacts);
        session.AcceptBattleAchievementFacts([Defeat("first")]);
        Assert.Single(session.State.PendingAchievementFacts);
    }

    [Fact]
    public void MultipleAllocationsAndReplacementsInOneSlotProduceOneFactEach()
    {
        GameSession session = Session();
        BattleEnemyDefeat first = Defeat("allocation-1");
        BattleEnemyDefeat replacement = Defeat("allocation-2");
        session.AcceptBattleAchievementFacts([first, first, replacement]);
        session.AcceptBattleAchievementFacts([first, replacement]);

        Assert.Equal(new[] { "allocation-1", "allocation-2" }, session.State.PendingAchievementFacts.Select(fact => fact.EventId.Value));
        Assert.Equal(new[] { "allocation-1", "allocation-2" }, session.State.AchievementCursor!.Allocations);
        Assert.All(session.State.PendingAchievementFacts, fact => Assert.Equal("encounter-1", fact.ContextId.Value));
    }

    [Fact]
    public void LegacyDefeatsWithoutAllocationIdentityDoNotFabricateEvents()
    {
        GameSession session = Session();
        session.AcceptBattleAchievementFacts([Defeat(null)]);
        Assert.Empty(session.State.PendingAchievementFacts);
        Assert.Empty(session.State.AchievementCursor!.Allocations);
    }

    [Fact]
    public void ExistingLegacyBattleFactsSeedTheCursorWithoutBecomingNewEvents()
    {
        BattleSession battle = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 },
                new() { Id = "historical", Name = "Enemy", MaximumHp = 1000, CurrentHp = 0, NativeStatus0 = 1, Level = 1, SceneSlot = 0, RewardSlot = 0, Rewards = new(0, 0, 20, 30, 255, false, 0, [.. Enumerable.Repeat(new BattleStock(0, 0), 12)]) }
            ]
        }, BattleAtbMode.Normal);
        BattleEnemyDefeat historical = Defeat("historical");
        GameSession session = new(new GameState { Scene = SceneKind.Field, PendingBattleEncounter = Encounter("encounter-1"), Battle = battle.Capture() with { EnemyDefeats = [historical] } });
        session.AcceptBattleAchievementFacts([historical, Defeat("new")]);

        Assert.Equal("new", Assert.Single(session.State.PendingAchievementFacts).EventId.Value);
        Assert.Equal(new[] { "historical", "new" }, session.State.AchievementCursor!.Allocations);
    }

    [Fact]
    public void FreshEncounterUsesASeparateEventContextEvenWhenAllocationNameIsReused()
    {
        GameSession session = Session();
        session.AcceptBattleAchievementFacts([Defeat("allocation")]);
        session.SetPendingBattleEncounter(Encounter("encounter-2"));
        session.AcceptBattleAchievementFacts([Defeat("allocation")]);

        Assert.Equal(new[] { "encounter-1", "encounter-2" }, session.State.PendingAchievementFacts.Select(fact => fact.ContextId.Value));
        Assert.Equal("encounter-2", session.State.AchievementCursor!.OccurrenceId);
    }

    [Fact]
    public void AccountOwnerIsCapturedAtAcceptanceAndSurvivesJsonRestart()
    {
        GameSession session = Session();
        AchievementOwner first = new("steam", "39150", "account-a", "profile");
        AchievementOwner second = first with { User = "account-b" };
        AchievementOwner? current = first;
        session.CaptureAchievementOwner = () => current;
        session.AcceptBattleAchievementFacts([Defeat("first")]);
        current = second;
        session.AcceptBattleAchievementFacts([Defeat("second")]);
        current = null;
        session.AcceptBattleAchievementFacts([Defeat("unbound")]);

        GameState restoredState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restarted = new(restoredState) { CaptureAchievementOwner = () => second };
        restarted.AcceptBattleAchievementFacts([Defeat("first"), Defeat("second"), Defeat("unbound")]);
        Assert.Equal(new AchievementOwner?[] { first, second, null }, restarted.State.PendingAchievementFacts.Select(fact => fact.Owner));
        Assert.Equal(session.State.AchievementCursor!.Allocations.ToArray(), restarted.State.AchievementCursor!.Allocations.ToArray());
    }

    [Fact]
    public void AcknowledgementRequiresTheExactAcceptedFact()
    {
        GameSession session = Session();
        session.AcceptBattleAchievementFacts([Defeat("one"), Defeat("two")]);
        EnemyDefeatedEvent first = session.State.PendingAchievementFacts[0];
        session.AcknowledgeAchievementFact(first with { Owner = new("steam", "39150", "other-account", "profile") });
        Assert.Equal(2, session.State.PendingAchievementFacts.Length);
        session.AcknowledgeAchievementFact(first);
        Assert.Equal("two", Assert.Single(session.State.PendingAchievementFacts).EventId.Value);
        session.AcknowledgeAchievementFact(first);
        Assert.Single(session.State.PendingAchievementFacts);
        session.AcceptBattleAchievementFacts([Defeat("one")]);
        Assert.Single(session.State.PendingAchievementFacts);
    }

    [Fact]
    public void RestoreDropsFinalizedUnacknowledgedFactsFromTheAbandonedLiveBranch()
    {
        GameSession session = Session();
        GameState earlier = session.State;
        session.AcceptBattleAchievementFacts([Defeat("accepted")]);
        EnemyDefeatedEvent accepted = Finalized(Assert.Single(session.State.PendingAchievementFacts));
        session = new(session.State with { PendingAchievementFacts = [accepted] });
        session.Restore(earlier);
        Assert.Empty(session.State.PendingAchievementFacts);
        session.AcceptBattleAchievementFacts([Defeat("accepted")]);
        Assert.Single(session.State.PendingAchievementFacts);
    }

    [Fact]
    public void RestoreDropsUnfinalizedFactsFromTheAbandonedLiveBranch()
    {
        GameSession session = Session();
        GameState earlier = session.State;
        session.AcceptBattleAchievementFacts([Defeat("abandoned")]);
        Assert.Null(Assert.Single(session.State.PendingAchievementFacts).Facts.AcceptedOutcome);

        session.Restore(earlier);

        Assert.Empty(session.State.PendingAchievementFacts);
        session.AcceptBattleAchievementFacts([Defeat("abandoned")]);
        Assert.Equal("abandoned", Assert.Single(session.State.PendingAchievementFacts).EventId.Value);
    }

    [Fact]
    public void RestoreKeepsStagedFactsOwnedByTheSelectedSave()
    {
        GameSession selectedSave = Session();
        selectedSave.AcceptBattleAchievementFacts([Defeat("saved-staging")]);
        GameState checkpoint = selectedSave.State;
        GameSession liveBranch = Session();
        liveBranch.AcceptBattleAchievementFacts([Defeat("abandoned-live-staging")]);

        liveBranch.Restore(checkpoint);

        Assert.Equal("saved-staging", Assert.Single(liveBranch.State.PendingAchievementFacts).EventId.Value);
        Assert.Null(liveBranch.State.PendingAchievementFacts[0].Facts.AcceptedOutcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectedSaveOwnsFactFinalizationWithTheSameIdentity(bool finalizedOnLiveBranch)
    {
        GameSession stagedSession = Session();
        stagedSession.AcceptBattleAchievementFacts([Defeat("same-allocation")]);
        EnemyDefeatedEvent staged = Assert.Single(stagedSession.State.PendingAchievementFacts);
        EnemyDefeatedEvent finalized = Finalized(staged);
        GameState finalizedState = stagedSession.State with { PendingAchievementFacts = [finalized] };
        GameSession live = finalizedOnLiveBranch ? new(finalizedState) : new(stagedSession.State);

        live.Restore(finalizedOnLiveBranch ? stagedSession.State : finalizedState);

        Assert.Equal(finalizedOnLiveBranch ? staged : finalized, Assert.Single(live.State.PendingAchievementFacts));
    }

    [Fact]
    public void SelectedBattleObservationsSurviveRestartRequireExactAcknowledgementAndDoNotCrossSaves()
    {
        AchievementOwner owner = new("steam", "39150", "test-owner", "profile");
        BattleKillTotalObservedEvent observation = new(new("kills/100/1/0"), new("battle-results/encounter-1"), 100, BattleOutcome.Victory, BattleRewardPolicy.NativeCombat, 0, owner);
        GameState earlier = Session().State;
        GameState selected = earlier with { PendingAchievementObservations = [observation] };
        GameSession live = new(selected);
        live.Restore(earlier);
        Assert.Empty(live.State.PendingAchievementObservations);
        live.Restore(selected);
        GameState restartedState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(live.State))!;
        GameSession restarted = new(restartedState);

        Assert.Equal(observation, Assert.Single(restarted.State.PendingAchievementObservations));
        restarted.AcknowledgeAchievementObservations([observation with { Owner = owner with { User = "different-owner" } }]);
        Assert.Single(restarted.State.PendingAchievementObservations);
        restarted.AcknowledgeAchievementObservations([observation]);
        Assert.Empty(restarted.State.PendingAchievementObservations);
    }

    [Fact]
    public void DurableProfileCommitBeforeOutboxAcknowledgementMakesCrashReplayIdempotent()
    {
        Directory.CreateDirectory(directory);
        string save = Path.Combine(directory, "game.json");
        string profile = Path.Combine(directory, "achievements.json");
        GameSession session = Session();
        session.AcceptBattleAchievementFacts([Defeat("accepted")]);
        session.Save(save);
        using (AchievementService service = Service(new FileAchievementRepository(profile)))
        {
            EnemyDefeatedEvent fact = Assert.Single(session.State.PendingAchievementFacts);
            service.Record(in fact);
            Assert.Equal(1, service.Get(CustomId).Counter);
        }
        // Simulate interruption after profile replacement and before saving the acknowledgement.
        GameSession restarted = new(GameSession.Load(save));
        using (AchievementService service = Service(new FileAchievementRepository(profile)))
        {
            Drain(restarted, service);
            Assert.Equal(1, service.Get(CustomId).Counter);
            Assert.Empty(restarted.State.PendingAchievementFacts);
        }
        restarted.Save(save);
        Assert.Empty(GameSession.Load(save).PendingAchievementFacts);
    }

    [Fact]
    public void FailedProfileWriteLeavesGameplayOutboxRecoverable()
    {
        GameSession session = Session();
        session.AcceptBattleAchievementFacts([Defeat("accepted")]);
        EnemyDefeatedEvent accepted = Assert.Single(session.State.PendingAchievementFacts);
        using AchievementService service = Service(new FailingRepository());

        Assert.Throws<IOException>(() => Drain(session, service));

        Assert.Equal(accepted, Assert.Single(session.State.PendingAchievementFacts));
        Assert.Equal(0, service.Get(CustomId).Counter);
    }

    private static AchievementService Service(IAchievementRepository repository)
    {
        AchievementRegistry registry = new();
        registry.Register<EnemyDefeatedEvent>(new(CustomId, "Accepted defeats", "Portable transaction test", 100, AchievementScope.LocalProfile), _ => 1);
        return new(registry, repository);
    }

    private static void Drain(GameSession session, IAchievementService service)
    {
        foreach (EnemyDefeatedEvent fact in session.State.PendingAchievementFacts)
        {
            service.Record(in fact);
            session.AcknowledgeAchievementFact(fact);
        }
    }

    private static GameSession Session() => new(new GameState { Scene = SceneKind.Field, PendingBattleEncounter = Encounter("encounter-1"), AchievementCursor = new("encounter-1", []) });
    private static BattleEncounterOccurrence Encounter(string id) => new(id, 1, new string('a', 64), SceneKind.Field, 74, 0, new string('b', 64), null);
    private static BattleEnemyDefeat Defeat(string? allocation) => new(0, 1, BattleDefeatCredit.Character, 0, null, false, -1) { AllocationId = allocation, NativeCommand = 1 };
    private static EnemyDefeatedEvent Finalized(EnemyDefeatedEvent fact) => fact with { Facts = fact.Facts with { AcceptedOutcome = BattleOutcome.Victory, ResultPresentationEligible = true } };

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class FailingRepository : IAchievementRepository
    {
        public AchievementProfile Load() => new();
        public void Save(AchievementProfile profile) => throw new IOException("Simulated profile write failure.");
        public void Dispose()
        {
        }
    }
}
