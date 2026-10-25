using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class AchievementTests
{
    private static readonly AchievementOwner OwnerA = new("test", "product-one", "user-a", "profile");
    private static readonly AchievementOwner OwnerB = OwnerA with { User = "user-b" };
    private static readonly AchievementId Official = AchievementRegistry.EnemiesDefeated100;
    private static readonly AchievementBinding[] Bindings = [new(Official, "verified-test-key", "verified-test-counter")];

    [Theory]
    [InlineData(false, BattleOutcome.Victory, 0, true)]
    [InlineData(false, BattleOutcome.Victory, 16, true)]
    [InlineData(false, BattleOutcome.Defeat, 0, true)]
    [InlineData(false, BattleOutcome.Defeat, 16, true)]
    [InlineData(false, BattleOutcome.ScriptedExit, 0, true)]
    [InlineData(false, BattleOutcome.ScriptedExit, 16, true)]
    [InlineData(false, BattleOutcome.Escaped, 16, true)]
    [InlineData(false, BattleOutcome.Active, 0, false)]
    [InlineData(true, BattleOutcome.Victory, 0, true)]
    [InlineData(true, BattleOutcome.Victory, 16, false)]
    [InlineData(true, BattleOutcome.Defeat, 0, false)]
    [InlineData(true, BattleOutcome.ScriptedExit, 0, false)]
    [InlineData(true, BattleOutcome.Escaped, 0, true)]
    [InlineData(true, BattleOutcome.Escaped, 16, true)]
    [InlineData(true, BattleOutcome.Active, 0, false)]
    public void OfficialRuleUsesEditionSpecificCompletedBattleBoundary(bool remastered, BattleOutcome outcome, byte formationFlags, bool eligible)
    {
        using AchievementService service = new(AchievementRegistry.CreateCombatRegistry(remastered), new MemoryRepository());
        service.Record(new BattleKillTotalObservedEvent(new("kill-total"), new("completed-encounter"), 100, outcome, BattleRewardPolicy.NativeCombat, formationFlags));
        Assert.Equal(eligible, service.Get(Official).Unlocked);
        Assert.Equal(eligible, service.Get(Official).ProgressKnown);
        Assert.Equal(eligible ? 100 : 0, service.Get(Official).Counter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OfficialRuleObservesImportedTotalWithoutNewEnemyDefeat(bool remastered)
    {
        using AchievementService service = new(AchievementRegistry.CreateCombatRegistry(remastered), new MemoryRepository());
        service.Record(new BattleKillTotalObservedEvent(new("kill-total"), new("zero-kill-encounter"), 100, BattleOutcome.Escaped, BattleRewardPolicy.NativeCombat, 0));
        Assert.True(service.Get(Official).Unlocked);
        Assert.Equal(100, service.Get(Official).Counter);
    }

    [Fact]
    public void OfficialRuleDoesNotConsumeEnemyEventsOrInventMissingHistoricalCounters()
    {
        using AchievementService service = new(AchievementRegistry.CreateCombatRegistry(remastered: true), new MemoryRepository());
        service.Record(Fact("live-enemy", 100));
        Assert.False(service.Get(Official).ProgressKnown);
        service.Record(new BattleKillTotalObservedEvent(new("unknown-kill-total"), new("encounter"), null, BattleOutcome.Escaped, BattleRewardPolicy.NativeCombat, 0));
        Assert.False(service.Get(Official).ProgressKnown);
        service.Record(new BattleKillTotalObservedEvent(new("known-kill-total"), new("next-encounter"), 99, BattleOutcome.Escaped, BattleRewardPolicy.NativeCombat, 0));
        Assert.Equal(99, service.Get(Official).Counter);
    }

    [Theory]
    [InlineData(BattleRewardPolicy.DebugNoRewards)]
    [InlineData(BattleRewardPolicy.ResearchAutomaticApOnly)]
    public void OfficialRuleKeepsDebugAndResearchCompletionsIneligible(BattleRewardPolicy policy)
    {
        using AchievementService service = new(AchievementRegistry.CreateCombatRegistry(remastered: false), new MemoryRepository());
        service.Record(new BattleKillTotalObservedEvent(new("kill-total"), new("encounter"), 100, BattleOutcome.Victory, policy, 0));
        Assert.False(service.Get(Official).ProgressKnown);
        Assert.False(service.Get(Official).Unlocked);
    }

    [Fact]
    public void SaveObservationHasUnknownHistoricalProgressUntilObservedAndOneUnlock()
    {
        using AchievementService service = new(Registry(), new MemoryRepository());
        Assert.False(service.Get(Official).ProgressKnown);
        int unlocks = 0;
        service.Committed += change => unlocks += change.NewlyUnlocked ? 1 : 0;
        foreach (long total in new long[] { 0, 1, 99, 100, 101, 12 })
        {
            service.Record(Fact($"allocation-{total}", total));
            Assert.True(service.Get(Official).ProgressKnown);
        }
        Assert.Equal(101, service.Get(Official).Counter);
        Assert.Equal(100, service.Get(Official).DisplayProgress);
        Assert.True(service.Get(Official).Unlocked);
        Assert.Equal(1, unlocks);
    }

    [Fact]
    public void DuplicateReceiptAndOlderSaveObservationDoNotAddProgress()
    {
        using AchievementService service = new(Registry(), new MemoryRepository());
        EnemyDefeatedEvent fact = Fact("allocation", 60);
        service.Record(fact);
        service.Record(fact);
        service.Record(Fact("other", 20));
        Assert.Equal(60, service.Get(Official).Counter);
    }

    [Fact]
    public void CustomRuleRunsOnceAndPresentationFailureDoesNotReevaluate()
    {
        AchievementRegistry registry = Registry();
        AchievementId custom = new("memoria.test.first");
        registry.Register<EnemyDefeatedEvent>(new(custom, "Test", "Test definition", 1, AchievementScope.LocalProfile), _ => 1);
        using AchievementService service = new(registry, new MemoryRepository(), new NoAchievementBackend("Unavailable"));
        int changes = 0;
        service.Committed += _ => throw new InvalidOperationException("Test presentation failure");
        service.Committed += _ => changes++;
        EnemyDefeatedEvent fact = Fact("a", 1);
        service.Record(fact);
        service.Record(fact);
        Assert.Equal(1, service.Get(custom).Counter);
        Assert.True(service.Get(custom).Unlocked);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void ConfiguredPolicyControlsEligibilityWithoutRewardRollGuessing()
    {
        AchievementRegistry registry = new();
        registry.Register<EnemyDefeatedEvent>(new(Official, "Test", "", 100, AchievementScope.SaveHighWater), fact => fact.Facts.NativeCommand == 7 && fact.Facts.Credit == BattleDefeatCredit.GuardianForce ? fact.Facts.NativeKillTotal : null);
        using AchievementService service = new(registry, new MemoryRepository());
        EnemyDefeatedEvent eligible = Fact("gf", 10) with { Facts = Fact("gf", 10).Facts with { NativeCommand = 7, Credit = BattleDefeatCredit.GuardianForce, GuardianForceId = 0, KillerRow = 0, CardSelected = true } };
        service.Record(Fact("ordinary", 99));
        Assert.False(service.Get(Official).ProgressKnown);
        service.Record(eligible);
        Assert.Equal(10, service.Get(Official).Counter);
    }

    [Fact]
    public void DiskFailureDoesNotCommitCounterReceiptOrNotification()
    {
        MemoryRepository repository = new() { FailSave = true };
        using AchievementService service = new(Registry(), repository);
        int changes = 0;
        service.Committed += _ => changes++;
        EnemyDefeatedEvent fact = Fact("a", 1);
        Assert.Throws<IOException>(() => service.Record(fact));
        Assert.Equal(0, service.Get(Official).Counter);
        Assert.Equal(0, changes);
        repository.FailSave = false;
        service.Record(fact);
        Assert.Equal(1, service.Get(Official).Counter);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void AtomicProfileRestartsAndIgnoresInterruptedTemporaryWrite()
    {
        string directory = Path.Combine(Path.GetTempPath(), "MemoriaAchievementTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "profile.json");
        try
        {
            using (AchievementService service = new(Registry(), new FileAchievementRepository(path)))
            {
                service.Record(Fact("a", 99));
            }
            File.WriteAllText(path + ".pending", "interrupted");
            using (AchievementService service = new(Registry(), new FileAchievementRepository(path)))
            {
                service.Record(Fact("a", 99));
                Assert.Equal(99, service.Get(Official).Counter);
                service.Record(Fact("b", 100));
                Assert.True(service.Get(Official).Unlocked);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CorruptCommittedProfileFailsClosedRatherThanResettingReplayProtection()
    {
        string directory = Path.Combine(Path.GetTempPath(), "MemoriaAchievementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "profile.json");
        try
        {
            File.WriteAllText(path, "{\"SchemaVersion\":2}");
            using FileAchievementRepository repository = new(path);
            Assert.Throws<InvalidDataException>(() => repository.Load());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void InitialReadBaselineIncludesContributionsAcceptedWhileReadWasPending()
    {
        FakeBackend backend = new() { Identity = OwnerA, ReadAvailable = false, Counter = 40 };
        using AchievementService service = new(Registry(AchievementScope.AccountCumulative), new MemoryRepository(), backend, Bindings);
        service.Flush();
        for (int i = 0; i < 10; i++)
        {
            service.Record(Fact($"a-{i}", 0, OwnerA));
        }
        backend.ReadAvailable = true;
        service.Flush();
        AchievementDeliveryBatch first = Assert.Single(backend.Submitted);
        Assert.Equal(50, first.Targets[Official.Value]);
        Assert.Equal(40, first.Baselines[Official.Value]);
        backend.Complete(first.Revision, false);
        service.Flush();
        Assert.Equal(50, backend.Submitted.Last().Targets[Official.Value]);
    }

    [Fact]
    public void SaveHighWaterReconcilesAbsoluteObservationsWithoutAddingBaseline()
    {
        FakeBackend backend = new() { Identity = OwnerA, Counter = 40 };
        using AchievementService service = new(Registry(), new MemoryRepository(), backend, Bindings);
        service.Record(Fact("a", 50, OwnerA));
        service.Flush();
        Assert.Equal(50, Assert.Single(backend.Submitted).Targets[Official.Value]);
    }

    [Fact]
    public void UnboundAndDifferentAccountsNeverPoolOfficialProgress()
    {
        FakeBackend backend = new() { Identity = OwnerA };
        using AchievementService service = new(Registry(AchievementScope.AccountCumulative), new MemoryRepository(), backend, Bindings);
        for (int i = 0; i < 90; i++)
        {
            service.Record(Fact($"unbound-{i}", 0));
            service.Record(Fact($"a-{i}", 0, OwnerA));
        }
        Assert.Equal(90, service.Get(Official).Counter);
        backend.Identity = OwnerB;
        for (int i = 0; i < 10; i++)
        {
            service.Record(Fact($"b-{i}", 0, OwnerB));
        }
        service.Flush();
        AchievementDeliveryBatch batch = Assert.Single(backend.Submitted);
        Assert.Equal(10, batch.Targets[Official.Value]);
        Assert.Empty(batch.Unlocks);
        Assert.False(service.Get(Official).Unlocked);
    }

    [Fact]
    public void CrashAfterRemoteAcceptanceReconcilesDurableBatchBeforeAddingNewContributions()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA, Counter = 40 };
        using (AchievementService service = new(Registry(AchievementScope.AccountCumulative), repository, backend, Bindings))
        {
            for (int i = 0; i < 10; i++)
            {
                service.Record(Fact($"a-{i}", 0, OwnerA));
            }
            service.Flush();
            backend.Counter = 50;
            // Prevent shutdown reconciliation, representing process termination after remote acceptance.
            backend.ReadAvailable = false;
        }
        FakeBackend restored = new() { Identity = OwnerA, Counter = 50 };
        using AchievementService restarted = new(Registry(AchievementScope.AccountCumulative), repository, restored, Bindings);
        restarted.Record(Fact("new", 0, OwnerA));
        restarted.Flush();
        Assert.Equal(51, Assert.Single(restored.Submitted).Targets[Official.Value]);
    }

    [Fact]
    public void FailedReadNeverAssumesZeroAndStoreCallbackNeverAcknowledgesNewerContributions()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA, Counter = 40, ReadAvailable = false };
        using AchievementService service = new(Registry(AchievementScope.AccountCumulative), repository, backend, Bindings);
        service.Record(Fact("a", 0, OwnerA));
        service.Flush();
        Assert.Empty(backend.Submitted);
        backend.ReadAvailable = true;
        service.Flush();
        AchievementDeliveryBatch first = backend.Submitted.Last();
        service.Record(Fact("b", 0, OwnerA));
        backend.Counter = 41;
        backend.Complete(first.Revision, true);
        service.Flush();
        AchievementDeliveryBatch second = backend.Submitted.Last();
        Assert.Equal(42, second.Targets[Official.Value]);
        backend.Complete(first.Revision, true);
        Assert.Equal(second.Revision, repository.Profile.Destinations[OwnerA.Key].Pending!.Revision);
    }

    [Fact]
    public void PersistenceFailurePreventsSubmissionAndRetainsAmbiguousAcknowledgement()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA };
        using AchievementService service = new(Registry(), repository, backend, Bindings);
        service.Record(Fact("a", 100, OwnerA));
        repository.FailSave = true;
        service.Flush();
        Assert.Empty(backend.Submitted);
        repository.FailSave = false;
        service.Flush();
        AchievementDeliveryBatch batch = Assert.Single(backend.Submitted);
        backend.Counter = 100;
        backend.Unlocked = true;
        repository.FailSave = true;
        service.Flush();
        Assert.Equal(batch.Revision, repository.Profile.Destinations[OwnerA.Key].Pending!.Revision);
        repository.FailSave = false;
        service.Flush();
        Assert.Null(repository.Profile.Destinations[OwnerA.Key].Pending);
    }

    [Fact]
    public void OldAccountCallbacksCannotRetireAnotherAccountsBatch()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA };
        using AchievementService service = new(Registry(), repository, backend, Bindings);
        service.Record(Fact("a", 90, OwnerA));
        service.Flush();
        AchievementDeliveryBatch first = Assert.Single(backend.Submitted);
        backend.Identity = OwnerB;
        service.Record(Fact("b", 10, OwnerB));
        service.Flush();
        AchievementDeliveryBatch second = backend.Submitted.Last();
        backend.Signal(new(first.Revision, OwnerA, true));
        Assert.Equal(second.Revision, repository.Profile.Destinations[OwnerB.Key].Pending!.Revision);
    }

    [Fact]
    public void ConflictingRegistrationsBindingsAndInvalidFactsFailFast()
    {
        AchievementRegistry registry = Registry();
        Assert.Throws<ArgumentException>(() => registry.Register<EnemyDefeatedEvent>(new(Official, "Duplicate", "", 1, AchievementScope.LocalProfile), _ => 1));
        Assert.Throws<ArgumentException>(() => new AchievementService(registry, new MemoryRepository(), bindings: [Bindings[0], Bindings[0]]));
        using AchievementService service = new(registry, new MemoryRepository());
        Assert.ThrowsAny<ArgumentException>(() => service.Record(default(EnemyDefeatedEvent)));
        Assert.Throws<InvalidOperationException>(() => registry.Register<EnemyDefeatedEvent>(new(new("memoria.test.late"), "Late", "", 1, AchievementScope.LocalProfile), _ => 1));
    }

    [Fact]
    public void WrappingSaveCounterAndRollbackRetainHighWaterAndRejectInvalidObservation()
    {
        using AchievementService service = new(Registry(), new MemoryRepository());
        service.Record(Fact("before-wrap", ushort.MaxValue));
        service.Record(Fact("after-wrap", 0));
        service.Record(Fact("older-save", 3));
        Assert.Equal(ushort.MaxValue, service.Get(Official).Counter);
        Assert.True(service.Get(Official).Unlocked);
        Assert.Throws<InvalidOperationException>(() => service.Record(Fact("invalid", -1)));
        Assert.Equal(ushort.MaxValue, service.Get(Official).Counter);
    }

    [Fact]
    public void BatchCoalescesPersistenceAndPresentationWhileDeduplicatingEachFact()
    {
        MemoryRepository repository = new();
        using AchievementService service = new(Registry(), repository);
        List<AchievementChange> notifications = [];
        service.Committed += notifications.Add;
        service.RecordBatch<EnemyDefeatedEvent>([Fact("first", 99), Fact("first", 99), Fact("second", 100), Fact("third", 101)]);
        Assert.Equal(1, repository.SaveCount);
        AchievementChange change = Assert.Single(notifications);
        Assert.Equal(0, change.Previous.Counter);
        Assert.Equal(101, change.Current.Counter);
        Assert.True(change.NewlyUnlocked);
        Assert.Equal(3, repository.Profile.Receipts["encounter"].Count);
        service.RecordBatch<EnemyDefeatedEvent>([Fact("first", 99), Fact("second", 100)]);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public void InvalidFactOrPersistenceFailureRollsBackWholeBatch()
    {
        MemoryRepository repository = new();
        using AchievementService service = new(Registry(), repository);
        int notifications = 0;
        service.Committed += _ => notifications++;
        Assert.Throws<InvalidOperationException>(() => service.RecordBatch<EnemyDefeatedEvent>([Fact("valid", 99), Fact("invalid", -1)]));
        Assert.Empty(repository.Profile.Receipts);
        Assert.Equal(0, service.Get(Official).Counter);
        repository.FailSave = true;
        Assert.Throws<IOException>(() => service.RecordBatch<EnemyDefeatedEvent>([Fact("valid", 99), Fact("threshold", 100)]));
        Assert.Empty(repository.Profile.Receipts);
        Assert.Equal(0, notifications);
        repository.FailSave = false;
        service.RecordBatch<EnemyDefeatedEvent>([Fact("valid", 99), Fact("threshold", 100)]);
        Assert.True(service.Get(Official).Unlocked);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void ConfirmedRemoteProgressImportsWithoutContributionsAndRemainsOwnerSpecific()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA, Counter = 40 };
        using AchievementService service = new(Registry(), repository, backend, Bindings);
        service.Flush();
        Assert.Equal(40, service.Get(Official).Counter);
        Assert.True(service.Get(Official).ProgressKnown);
        Assert.Empty(repository.Profile.Destinations[OwnerA.Key].Contributions);
        Assert.Empty(backend.Submitted);
        service.Record(Fact("low-save-observation", 1, OwnerA));
        service.Flush();
        Assert.Equal(40, service.Get(Official).Counter);
        Assert.Equal(1, repository.Profile.Destinations[OwnerA.Key].DeliveredContributions[Official.Value]);
        Assert.Empty(backend.Submitted);
        backend.Identity = OwnerB;
        backend.Counter = 0;
        backend.ReadAvailable = false;
        service.Flush();
        Assert.False(service.Get(Official).ProgressKnown);
        Assert.False(service.Get(Official).Unlocked);
        backend.ReadAvailable = true;
        service.Flush();
        Assert.Equal(0, service.Get(Official).Counter);
        Assert.True(service.Get(Official).ProgressKnown);
        Assert.Equal(40, repository.Profile.Destinations[OwnerA.Key].ConfirmedRemote[Official.Value]);
    }

    [Fact]
    public void UnlockOnlyExistingRemoteUnlockNeedsNoLocalFactOrRedundantWrite()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA, Unlocked = true };
        AchievementBinding[] unlockOnly = [new(Official, "verified-test-key")];
        using AchievementService service = new(Registry(), repository, backend, unlockOnly);
        service.Flush();
        Assert.True(service.Get(Official).Unlocked);
        Assert.False(service.Get(Official).ProgressKnown);
        Assert.Equal(0, service.Get(Official).Counter);
        Assert.Empty(backend.Submitted);
        int notifications = 0;
        service.Committed += change => notifications += change.NewlyUnlocked ? 1 : 0;
        service.Record(Fact("new-observation", 100, OwnerA));
        service.Flush();
        Assert.True(service.Get(Official).Unlocked);
        Assert.Equal(100, repository.Profile.Destinations[OwnerA.Key].DeliveredContributions[Official.Value]);
        Assert.Empty(backend.Submitted);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void FailedOrUnconfirmedInitialReadsDoNotImportBaselineOrUnlock()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA, Counter = 100, Unlocked = true, ReadAvailable = false };
        using AchievementService service = new(Registry(), repository, backend, Bindings);
        service.Flush();
        Assert.Empty(repository.Profile.Destinations);
        Assert.False(service.Get(Official).ProgressKnown);
        backend.ReadAvailable = true;
        backend.PersistenceConfirmed = false;
        service.Flush();
        Assert.Empty(repository.Profile.Destinations);
        Assert.False(service.Get(Official).Unlocked);
        backend.PersistenceConfirmed = true;
        service.Flush();
        Assert.True(service.Get(Official).Unlocked);
        Assert.Equal(100, service.Get(Official).Counter);
    }

    [Fact]
    public void RemoteImportDoesNotMigrateUnboundObservationsAndPersistsAcrossRestart()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA, Counter = 40 };
        using (AchievementService service = new(Registry(), repository, backend, Bindings))
        {
            service.Record(Fact("unbound", 100));
            service.Flush();
            Assert.Equal(40, service.Get(Official).Counter);
            Assert.False(service.Get(Official).Unlocked);
        }
        FakeBackend unavailable = new() { Identity = OwnerA, ReadAvailable = false };
        using AchievementService restarted = new(Registry(), repository, unavailable, Bindings);
        Assert.Equal(40, restarted.Get(Official).Counter);
        Assert.False(restarted.Get(Official).Unlocked);
    }

    [Fact]
    public void ConfirmedImportedUnlockAndProgressAreMonotonicAcrossStaleReads()
    {
        FakeBackend backend = new() { Identity = OwnerA, Counter = 100, Unlocked = true };
        using AchievementService service = new(Registry(), new MemoryRepository(), backend, Bindings);
        service.Flush();
        backend.Counter = 10;
        backend.Unlocked = false;
        service.Flush();
        Assert.Equal(100, service.Get(Official).Counter);
        Assert.True(service.Get(Official).Unlocked);
        Assert.Empty(backend.Submitted);
    }

    [Fact]
    public void ConfirmedCounterThresholdCanQueueMissingUnlockWithoutSyntheticContribution()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA, Counter = 100, Unlocked = false };
        using AchievementService service = new(Registry(), repository, backend, Bindings);
        service.Flush();
        AchievementDeliveryBatch batch = Assert.Single(backend.Submitted);
        Assert.Contains(Official.Value, batch.Unlocks);
        Assert.Equal(100, batch.Targets[Official.Value]);
        Assert.Empty(repository.Profile.Destinations[OwnerA.Key].Contributions);
        Assert.True(service.Get(Official).Unlocked);
    }

    [Fact]
    public void CommittedPresentationUsesReconciledHighWaterWithoutRegressingToLocalObservation()
    {
        FakeBackend backend = new() { Identity = OwnerA, Counter = 40 };
        using AchievementService service = new(Registry(), new MemoryRepository(), backend, Bindings);
        service.Flush();
        List<AchievementChange> changes = [];
        service.Committed += changes.Add;
        service.Record(Fact("below-remote", 1, OwnerA));
        Assert.Equal(40, service.Get(Official).Counter);
        Assert.Empty(changes);
        service.Record(Fact("above-remote", 41, OwnerA));
        AchievementChange change = Assert.Single(changes);
        Assert.Equal(40, change.Previous.Counter);
        Assert.Equal(service.Get(Official), change.Current);
        Assert.Equal(41, change.Current.Counter);
    }

    [Fact]
    public void AlreadyUnlockedRemoteObservationCannotProduceAnotherLocalUnlockToast()
    {
        FakeBackend backend = new() { Identity = OwnerA, Counter = 100, Unlocked = true };
        using AchievementService service = new(Registry(), new MemoryRepository(), backend, Bindings);
        service.Flush();
        List<AchievementChange> changes = [];
        service.Committed += changes.Add;
        service.Record(Fact("new-local-threshold", 100, OwnerA));
        Assert.Empty(changes);
        service.Record(Fact("above-threshold", 101, OwnerA));
        AchievementChange change = Assert.Single(changes);
        Assert.False(change.NewlyUnlocked);
        Assert.True(change.Previous.Unlocked);
        Assert.Equal(service.Get(Official), change.Current);
    }

    [Fact]
    public void StatsReadinessDelayKeepsCapturedContributionsAndPumpsEveryTime()
    {
        FakeBackend backend = new() { Identity = OwnerA, State = AchievementBackendState.AwaitingStats, Counter = 40 };
        using AchievementService service = new(Registry(AchievementScope.AccountCumulative), new MemoryRepository(), backend, Bindings);
        service.Record(Fact("accepted-while-pending", 0, service.CaptureOwner()));
        service.Flush();
        service.Pump();
        Assert.Empty(backend.Submitted);
        Assert.Equal(2, backend.PumpCalls);
        backend.State = AchievementBackendState.OfflineReady;
        service.Flush();
        Assert.Equal(41, Assert.Single(backend.Submitted).Targets[Official.Value]);
    }

    [Fact]
    public void CachedReadAndSuccessfulSdkCallbackCannotAcknowledgeUnpersistedState()
    {
        MemoryRepository repository = new();
        FakeBackend backend = new() { Identity = OwnerA };
        using AchievementService service = new(Registry(), repository, backend, Bindings);
        service.Record(Fact("a", 100, OwnerA));
        service.Flush();
        AchievementDeliveryBatch batch = Assert.Single(backend.Submitted);
        backend.Counter = 100;
        backend.Unlocked = true;
        backend.PersistenceConfirmed = false;
        backend.Complete(batch.Revision, true);
        service.Flush();
        Assert.NotNull(repository.Profile.Destinations[OwnerA.Key].Pending);
        backend.PersistenceConfirmed = true;
        service.Flush();
        Assert.Null(repository.Profile.Destinations[OwnerA.Key].Pending);
    }

    [Fact]
    public void CounterOverflowDoesNotPublishReceiptAndSingleWriterPreventsTwoProfiles()
    {
        MemoryRepository repository = new();
        AchievementRegistry registry = new();
        registry.Register<EnemyDefeatedEvent>(new(Official, "Test counter", "", 100, AchievementScope.AccountCumulative), fact => fact.Facts.NativeKillTotal);
        FakeBackend backend = new() { Identity = OwnerA };
        using AchievementService first = new(registry, repository, backend, Bindings);
        first.Record(Fact("maximum", long.MaxValue, OwnerA));
        Assert.Throws<OverflowException>(() => first.Record(Fact("overflow", 1, OwnerA)));
        Assert.DoesNotContain("overflow", repository.Profile.Receipts["encounter"]);
        first.Flush();
        FakeBackend otherBackend = new() { Identity = OwnerA with { Profile = "other-profile" } };
        using AchievementService second = new(Registry(), new MemoryRepository(), otherBackend, Bindings);
        second.Record(Fact("other", 20, otherBackend.Identity));
        second.Flush();
        Assert.Empty(otherBackend.Submitted);
        Assert.NotNull(second.Diagnostic);
    }

    private static AchievementRegistry Registry(AchievementScope scope = AchievementScope.SaveHighWater)
    {
        AchievementRegistry registry = new();
        registry.Register<EnemyDefeatedEvent>(new(Official, "Test defeat counter", "Test rule", 100, scope), fact => scope == AchievementScope.SaveHighWater ? fact.Facts.NativeKillTotal : 1);
        return registry;
    }
    private static EnemyDefeatedEvent Fact(string allocation, long total, AchievementOwner? owner = null) => new(new(allocation), new("encounter"), new(allocation, BattleDefeatCredit.Character, 0, null, 0, false, BattleRewardPolicy.NativeCombat) { NativeKillTotal = total }) { Owner = owner };

    private sealed class MemoryRepository : IAchievementRepository
    {
        public AchievementProfile Profile { get; private set; } = new();
        public bool FailSave { get; set; }
        public int SaveCount { get; private set; }
        public AchievementProfile Load() => System.Text.Json.JsonSerializer.Deserialize<AchievementProfile>(System.Text.Json.JsonSerializer.Serialize(Profile))!;
        public void Save(AchievementProfile profile)
        {
            if (FailSave)
            {
                throw new IOException("Test disk failure");
            }
            Profile = System.Text.Json.JsonSerializer.Deserialize<AchievementProfile>(System.Text.Json.JsonSerializer.Serialize(profile))!;
            SaveCount++;
        }
        public void Dispose() { }
    }

    private sealed class FakeBackend : IAchievementBackend
    {
        public AchievementBackendState State { get; set; } = AchievementBackendState.Ready;
        public AchievementOwner? Identity { get; set; }
        public string? Diagnostic => null;
        public bool ReadAvailable { get; set; } = true;
        public long Counter { get; set; }
        public bool Unlocked { get; set; }
        public bool PersistenceConfirmed { get; set; } = true;
        public int PumpCalls { get; private set; }
        public List<AchievementDeliveryBatch> Submitted { get; } = [];
        public event Action<AchievementStoreCompletion>? StoreCompleted;
        public void Pump() => PumpCalls++;
        public AchievementRemoteState? Read(IReadOnlyList<AchievementBinding> bindings) => ReadAvailable ? new(new Dictionary<string, long> { [Official.Value] = Counter }, Unlocked ? new HashSet<string> { Official.Value } : new HashSet<string>(), PersistenceConfirmed) : null;
        public void Submit(AchievementDeliveryBatch batch, IReadOnlyList<AchievementBinding> bindings) => Submitted.Add(batch);
        public void Complete(long revision, bool success) => Signal(new(revision, Identity!, success));
        public void Signal(AchievementStoreCompletion completion) => StoreCompleted?.Invoke(completion);
        public void Dispose() { }
    }
}
