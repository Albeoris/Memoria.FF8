using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SteamAchievementBackendTests
{
    [Theory]
    [InlineData("39150", "TOTAL_KILLS_100", "nmy_kill")]
    [InlineData("1026680", "NEW_ACHIEVEMENT_1_10", null)]
    public void ProductUsesOnlyItsVerifiedMappingAndTypedReads(string product, string key, string? stat)
    {
        FakeApi api = new(uint.Parse(product));
        using SteamAchievementBackend backend = new(api, product, "profile");
        AchievementBinding binding = Binding(product);
        Assert.Equal(AchievementBackendState.Ready, backend.State);
        Assert.Equal(product, backend.Identity!.Product);
        Assert.Equal(key, binding.NativeAchievement);
        Assert.Equal(stat, binding.NativeCounter);
        Assert.Contains("read-achievement:" + key, api.Calls);
        Assert.Equal(stat is not null, api.Calls.Contains("read-stat:nmy_kill"));
        Assert.NotNull(backend.Read([binding]));
        Assert.DoesNotContain(api.Calls, call => call.StartsWith("write-", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadinessDelayAndFailedReadsNeverBecomeAZeroBaseline()
    {
        FakeApi api = new(39150) { ReadsSucceed = false };
        using SteamAchievementBackend backend = new(api, "39150", "profile");
        Assert.Equal(AchievementBackendState.AwaitingStats, backend.State);
        Assert.Null(backend.Read([Binding("39150")]));
        api.ReadsSucceed = true;
        api.Counter = 40;
        backend.Pump();
        Assert.Equal(40, backend.Read([Binding("39150")])!.Counters[AchievementRegistry.EnemiesDefeated100.Value]);
        api.ReadsSucceed = false;
        Assert.Null(backend.Read([Binding("39150")]));
        Assert.Equal(AchievementBackendState.AwaitingStats, backend.State);
    }

    [Fact]
    public void RuntimeProductMismatchPreventsEveryWrite()
    {
        FakeApi api = new(39150);
        using SteamAchievementBackend backend = new(api, "1026680", "profile");
        Assert.Equal(AchievementBackendState.Unsupported, backend.State);
        Assert.Null(backend.Identity);
        Assert.Null(backend.Read([Binding("1026680")]));
        Assert.DoesNotContain(api.Calls, call => call.StartsWith("write-", StringComparison.Ordinal));
    }

    [Fact]
    public void AccountChangesInvalidatePendingCallbacksInsteadOfRebindingProgress()
    {
        FakeApi api = new(1026680);
        using SteamAchievementBackend backend = new(api, "1026680", "profile");
        List<AchievementStoreCompletion> completed = [];
        backend.StoreCompleted += completed.Add;
        backend.Submit(Batch(backend, 1), [Binding("1026680")]);
        api.UserId++;
        api.Callbacks.Add(new(SteamAchievementCallbackKind.StatsStored, 1026680, 1));
        backend.Pump();
        Assert.Equal(AchievementBackendState.Unsupported, backend.State);
        Assert.Null(backend.Identity);
        Assert.Empty(completed);
    }

    [Fact]
    public void ProgressCallbacksAndOtherProductsDoNotAcknowledgeAStore()
    {
        FakeApi api = new(1026680);
        using SteamAchievementBackend backend = new(api, "1026680", "profile");
        List<AchievementStoreCompletion> completed = [];
        backend.StoreCompleted += completed.Add;
        backend.Submit(Batch(backend, 7), [Binding("1026680")]);
        api.Callbacks.Add(new(SteamAchievementCallbackKind.AchievementProgress, 1026680, 1));
        api.Callbacks.Add(new(SteamAchievementCallbackKind.StatsStored, 39150, 1));
        backend.Pump();
        Assert.Empty(completed);
        Assert.False(backend.Read([Binding("1026680")])!.PersistenceConfirmed);
        api.Callbacks.Add(new(SteamAchievementCallbackKind.StatsStored, 1026680, 1));
        backend.Pump();
        AchievementStoreCompletion completion = Assert.Single(completed);
        Assert.Equal(7, completion.Revision);
        Assert.True(completion.Success);
        Assert.True(backend.Read([Binding("1026680")])!.PersistenceConfirmed);
    }

    [Fact]
    public void FailedStoreDoesNotPromoteSdkCachedWritesToDurableConfirmation()
    {
        FakeApi api = new(1026680) { StoreSucceeds = false };
        using SteamAchievementBackend backend = new(api, "1026680", "profile");
        Assert.Throws<IOException>(() => backend.Submit(Batch(backend, 3), [Binding("1026680")]));
        AchievementRemoteState remote = backend.Read([Binding("1026680")])!;
        Assert.Contains(AchievementRegistry.EnemiesDefeated100.Value, remote.Unlocks);
        Assert.False(remote.PersistenceConfirmed);
        api.StoreSucceeds = true;
        backend.Submit(Batch(backend, 3), [Binding("1026680")]);
    }

    [Fact]
    public void FailedStoreCallbackKeepsPersistenceUnconfirmedAndAllowsSameRevisionRetry()
    {
        FakeApi api = new(1026680);
        using SteamAchievementBackend backend = new(api, "1026680", "profile");
        List<AchievementStoreCompletion> completed = [];
        backend.StoreCompleted += completed.Add;
        backend.Submit(Batch(backend, 3), [Binding("1026680")]);
        api.Callbacks.Add(new(SteamAchievementCallbackKind.StatsStored, 1026680, 2));
        backend.Pump();
        Assert.False(Assert.Single(completed).Success);
        Assert.False(backend.Read([Binding("1026680")])!.PersistenceConfirmed);
        backend.Submit(Batch(backend, 3), [Binding("1026680")]);
    }

    [Fact]
    public void OriginalCounterWritesAbsoluteMaximumAndCannotStoreConcurrently()
    {
        FakeApi api = new(39150) { Counter = 90 };
        using SteamAchievementBackend backend = new(api, "39150", "profile");
        AchievementDeliveryBatch batch = Batch(backend, 9) with { Targets = new() { [AchievementRegistry.EnemiesDefeated100.Value] = 80 }, Unlocks = [] };
        backend.Submit(batch, [Binding("39150")]);
        Assert.Equal(90, api.Counter);
        Assert.Contains("write-stat:nmy_kill:90", api.Calls);
        Assert.DoesNotContain(api.Calls, call => call.StartsWith("write-achievement:", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => backend.Submit(batch with { Revision = batch.Revision + 1 }, [Binding("39150")]));
    }

    [Fact]
    public void MissingStoreCallbackCoalescesIdenticalBatchWithoutProducingDuplicateCallbacks()
    {
        FakeApi api = new(39150) { Counter = 90 };
        using SteamAchievementBackend backend = new(api, "39150", "profile");
        AchievementDeliveryBatch batch = Batch(backend, 9) with { Baselines = new() { [AchievementRegistry.EnemiesDefeated100.Value] = 90 } };
        backend.Submit(batch, [Binding("39150")]);
        AchievementDeliveryBatch equalCopy = batch with { Targets = new(batch.Targets), CoveredContributions = new(batch.CoveredContributions), Baselines = new(batch.Baselines), Unlocks = new(batch.Unlocks) };

        backend.Submit(equalCopy, [Binding("39150")]);

        Assert.Equal(1, api.Calls.Count(call => call == "store"));
        Assert.Equal(100, api.Counter);
        foreach (AchievementDeliveryBatch changed in new[]
        {
            batch with { Revision = batch.Revision + 1 },
            batch with { Targets = new() { [AchievementRegistry.EnemiesDefeated100.Value] = 101 } },
            batch with { CoveredContributions = new() { [AchievementRegistry.EnemiesDefeated100.Value] = 99 } },
            batch with { Baselines = new() { [AchievementRegistry.EnemiesDefeated100.Value] = 89 } },
            batch with { Unlocks = [] }
        })
        {
            Assert.Throws<InvalidOperationException>(() => backend.Submit(changed, [Binding("39150")]));
        }
        Assert.Equal(1, api.Calls.Count(call => call == "store"));
        List<AchievementStoreCompletion> completed = [];
        backend.StoreCompleted += completed.Add;
        api.Callbacks.Add(new(SteamAchievementCallbackKind.StatsStored, 39150, 1));
        backend.Pump();
        Assert.Equal(9, Assert.Single(completed).Revision);
    }

    [Fact]
    public void MissingStoreCallbackStopsTheSessionAfterBoundedWaitAndCannotAcknowledgeLaterWork()
    {
        FakeApi api = new(1026680);
        FakeClock clock = new();
        using SteamAchievementBackend backend = new(api, "1026680", "profile", clock);
        List<AchievementStoreCompletion> completions = [];
        backend.StoreCompleted += completions.Add;
        AchievementDeliveryBatch batch = Batch(backend, 5);
        backend.Submit(batch, [Binding("1026680")]);
        clock.Ticks = TimeSpan.FromSeconds(31).Ticks;

        backend.Pump();

        Assert.Equal(AchievementBackendState.Unsupported, backend.State);
        Assert.Contains("restart", backend.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, api.DisposeCount);
        Assert.Empty(completions);
        api.Callbacks.Add(new(SteamAchievementCallbackKind.StatsStored, 1026680, 1));
        backend.Pump();
        Assert.Empty(completions);
        Assert.Throws<InvalidOperationException>(() => backend.Submit(batch with { Revision = 6 }, [Binding("1026680")]));
        Assert.Equal(1, api.Calls.Count(call => call == "store"));
    }

    [Fact]
    public void SignedInOfflineUserKeepsVerifiedCachedOperationsAvailable()
    {
        FakeApi api = new(1026680) { LoggedOn = false };
        using SteamAchievementBackend backend = new(api, "1026680", "profile");
        Assert.Equal(AchievementBackendState.OfflineReady, backend.State);
        Assert.NotNull(backend.Read([Binding("1026680")]));
        backend.Submit(Batch(backend, 1), [Binding("1026680")]);
    }

    [Fact]
    public void UnknownMappingIsRejectedBeforeNativeCalls()
    {
        FakeApi api = new(1026680);
        using SteamAchievementBackend backend = new(api, "1026680", "profile");
        int calls = api.Calls.Count;
        Assert.Throws<ArgumentException>(() => backend.Read([Binding("39150")]));
        Assert.Equal(calls, api.Calls.Count);
    }

    [Fact]
    public void InvalidPlatformMappingFailureStopsDeliveryAndReleasesSdkLifetime()
    {
        FakeApi api = new(1026680);
        using (SteamAchievementBackend backend = new(api, "1026680", "profile"))
        {
            backend.Submit(Batch(backend, 1), [Binding("1026680")]);
            api.Callbacks.Add(new(SteamAchievementCallbackKind.StatsStored, 1026680, 8));
            backend.Pump();
            Assert.Equal(AchievementBackendState.Unsupported, backend.State);
            Assert.Contains("InvalidParam", backend.Diagnostic);
            Assert.Null(backend.Identity);
            Assert.Equal(1, api.DisposeCount);
        }
        Assert.Equal(1, api.DisposeCount);
    }

    [Fact]
    public void PartialInitializationFailureReleasesSdkLifetimeOnce()
    {
        FakeApi api = new(1026680) { InitializeSucceeds = false };
        using (SteamAchievementBackend backend = new(api, "1026680", "profile"))
        {
            Assert.Equal(AchievementBackendState.Unsupported, backend.State);
            Assert.Equal(1, api.DisposeCount);
        }
        Assert.Equal(1, api.DisposeCount);
    }

    [Fact]
    public void MissingAndUnverifiedImagesRemainUnsupportedWithoutLoadingThem()
    {
        string path = Path.Combine(Path.GetTempPath(), "memoria-sdk-test-" + Guid.NewGuid().ToString("N") + ".dll");
        using (SteamAchievementBackend missing = new(path, "1026680", "profile"))
        {
            Assert.Equal(AchievementBackendState.Unsupported, missing.State);
        }
        try
        {
            File.WriteAllText(path, "Unverified SDK image");
            using SteamAchievementBackend invalid = new(path, "1026680", "profile");
            Assert.Equal(AchievementBackendState.Unsupported, invalid.State);
            Assert.Null(invalid.Identity);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static AchievementBinding Binding(string product) => Assert.Single(SteamAchievementMappings.ForProduct(product));
    private static AchievementDeliveryBatch Batch(SteamAchievementBackend backend, long revision) => new(revision, backend.Identity!, new() { [AchievementRegistry.EnemiesDefeated100.Value] = 100 }, new() { [AchievementRegistry.EnemiesDefeated100.Value] = 100 }, [AchievementRegistry.EnemiesDefeated100.Value]);

    private sealed class FakeApi(uint product) : ISteamAchievementApi
    {
        public uint AppId => product;
        public ulong UserId { get; set; } = 76561198000000000;
        public bool LoggedOn { get; set; } = true;
        public bool ReadsSucceed { get; set; } = true;
        public bool StoreSucceeds { get; set; } = true;
        public bool InitializeSucceeds { get; set; } = true;
        public int DisposeCount { get; private set; }
        public int Counter { get; set; }
        public bool Unlocked { get; set; }
        public List<string> Calls { get; } = [];
        public List<SteamAchievementCallback> Callbacks { get; } = [];
        public bool Initialize(string selectedProduct, out string? diagnostic)
        {
            diagnostic = null;
            return InitializeSucceeds;
        }
        public IReadOnlyList<SteamAchievementCallback> Pump()
        {
            SteamAchievementCallback[] result = Callbacks.ToArray();
            Callbacks.Clear();
            return result;
        }
        public bool GetAchievement(string key, out bool unlocked)
        {
            Calls.Add("read-achievement:" + key);
            unlocked = Unlocked;
            return ReadsSucceed;
        }
        public bool GetStat(string key, out int value)
        {
            Calls.Add("read-stat:" + key);
            value = Counter;
            return ReadsSucceed;
        }
        public bool SetAchievement(string key)
        {
            Calls.Add("write-achievement:" + key);
            Unlocked = true;
            return true;
        }
        public bool SetStat(string key, int value)
        {
            Calls.Add("write-stat:" + key + ":" + value);
            Counter = value;
            return true;
        }
        public bool StoreStats()
        {
            Calls.Add("store");
            return StoreSucceeds;
        }
        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class FakeClock : TimeProvider
    {
        public long Ticks { get; set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Ticks;
    }
}
