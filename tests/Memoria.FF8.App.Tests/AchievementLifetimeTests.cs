using System.Reflection;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class AchievementLifetimeTests
{
    [Fact]
    public void InWindowDiagnosticsSwitchToASeparateLocalProfileAfterFlushingPlayerReceipts()
    {
        GameSession session = new(new GameState { PendingAchievementObservations = [new(new("kills/1"), new("battle-results/diagnostic-switch"), 1, BattleOutcome.Victory, BattleRewardPolicy.NativeCombat, 0)] });
        using RecreateGame game = new(new LaunchOptions { MuteAudio = true, AchievementsLocalOnly = true }, session);
        AchievementService playerService = Get<AchievementService>(game, "achievements");
        string playerDirectory = Get<string>(game, "isolatedAchievementDirectory");
        // Model a player host while retaining this test's original isolated storage and explicit no-SDK setting.
        Set(game, "achievementToolSession", false);

        typeof(RecreateGame).GetMethod("EnterAchievementToolSession", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(game, null);

        Assert.Empty(session.State.PendingAchievementObservations);
        Assert.True(Get<bool>(game, "achievementToolSession"));
        Assert.NotEqual(playerDirectory, Get<string>(game, "isolatedAchievementDirectory"));
        Assert.NotSame(playerService, Get<AchievementService>(game, "achievements"));
        Assert.Equal(AchievementBackendState.LocalOnly, Get<AchievementService>(game, "achievements").BackendState);
    }

    [Fact]
    public void PausedHostPumpsCallbacksEvenWhileProfileWritesAreRetrying()
    {
        GameSession session = new(new GameState { PendingAchievementObservations = [new(new("kills/1"), new("battle-results/lifetime"), 1, BattleOutcome.Victory, BattleRewardPolicy.NativeCombat, 0)] });
        BattleEncounterOccurrence encounter = new("lifetime", 0, new string('A', 64), SceneKind.Field, 74, 0, new string('B', 64), null);
        session.SetPendingBattleEncounter(encounter);
        session.AcceptBattleAchievementFacts([new(0, 1, BattleDefeatCredit.Character, 0, null, false, -1) { AllocationId = "enemy" }]);
        using RecreateGame game = new(new LaunchOptions { MuteAudio = true }, session);
        Get<AchievementService>(game, "achievements").Dispose();
        CountingBackend backend = new();
        AchievementRegistry registry = new();
        registry.Register<EnemyDefeatedEvent>(new(AchievementRegistry.EnemiesDefeated100, "Test", "Test", 100, AchievementScope.LocalProfile), _ => 1);
        AchievementService service = new(registry, new FailingRepository(), backend);
        Set(game, "achievements", service);
        game.SetWindowActive(false);

        Update(game);
        Update(game);

        Assert.Equal(2, backend.Pumps);
        Assert.Single(session.State.PendingAchievementFacts);
        Assert.Single(session.State.PendingAchievementObservations);
        session.Restore(new GameState());
        Assert.Same(service, Get<AchievementService>(game, "achievements"));
        Assert.Empty(session.State.PendingAchievementFacts);
        Assert.Single(session.State.PendingAchievementObservations);
    }

    [Fact]
    public void CorruptProfileKeepsGameplayUsableAndRetainsTheOutbox()
    {
        GameSession session = new(new GameState());
        using RecreateGame game = new(new LaunchOptions { MuteAudio = true }, session);
        Get<AchievementService>(game, "achievements").Dispose();
        Set(game, "achievements", null);
        string directory = Get<string>(game, "isolatedAchievementDirectory");
        foreach (string writerLock in Directory.EnumerateFiles(directory, "*.json.lock"))
        {
            File.WriteAllText(writerLock[..^5], "{\"SchemaVersion\":999}");
        }
        typeof(RecreateGame).GetMethod("UpdateAchievements", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(game, [true]);
        Assert.Null(Get<object?>(game, "achievements"));
        Assert.Contains("Unsupported or invalid", Get<string>(game, "achievementFailure"));
    }

    private static void Update(RecreateGame game) => typeof(RecreateGame).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(game, [new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16))]);
    private static T Get<T>(RecreateGame game, string name) => (T)typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game)!;
    private static void Set(RecreateGame game, string name, object? value) => typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(game, value);

    private sealed class FailingRepository : IAchievementRepository
    {
        public AchievementProfile Load() => new();
        public void Save(AchievementProfile profile) => throw new IOException("Profile is temporarily unwritable.");
        public void Dispose()
        {
        }
    }

    private sealed class CountingBackend : IAchievementBackend
    {
        public int Pumps { get; private set; }
        public AchievementBackendState State => AchievementBackendState.LocalOnly;
        public AchievementOwner? Identity => null;
        public string? Diagnostic => null;
        public event Action<AchievementStoreCompletion>? StoreCompleted
        {
            add
            {
            }
            remove
            {
            }
        }
        public void Pump()
        {
            Pumps++;
        }
        public AchievementRemoteState? Read(IReadOnlyList<AchievementBinding> bindings) => null;
        public void Submit(AchievementDeliveryBatch batch, IReadOnlyList<AchievementBinding> bindings) => throw new InvalidOperationException();
        public void Dispose()
        {
        }
    }
}
