namespace Memoria.FF8.Runtime;

/// <summary>Semantic destination verified by an adapter, never an unauthenticated wrapper identity.</summary>
public sealed record AchievementOwner(string Provider, string Product, string User, string Profile)
{
    public string Key => System.Text.Json.JsonSerializer.Serialize(new[] { Provider, Product, User, Profile });
    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(Product);
        ArgumentException.ThrowIfNullOrWhiteSpace(User);
        ArgumentException.ThrowIfNullOrWhiteSpace(Profile);
    }
}

public enum AchievementBackendState
{
    LocalOnly,
    Initializing,
    AwaitingIdentity,
    AwaitingStats,
    Ready,
    OfflineReady,
    TransientFailure,
    Unsupported
}

public sealed record AchievementBinding(AchievementId Id, string NativeAchievement, string? NativeCounter = null);
public sealed record AchievementRemoteState(IReadOnlyDictionary<string, long> Counters, IReadOnlySet<string> Unlocks, bool PersistenceConfirmed = true);
public sealed record AchievementDeliveryBatch(long Revision, AchievementOwner Owner, Dictionary<string, long> Targets, Dictionary<string, long> CoveredContributions, HashSet<string> Unlocks)
{
    public Dictionary<string, long> Baselines { get; init; } = new(StringComparer.Ordinal);
}
public sealed record AchievementStoreCompletion(long Revision, AchievementOwner Owner, bool Success, string? Diagnostic = null);

/// <summary>All methods and completion callbacks execute on the owning application thread. Read failures return null.</summary>
public interface IAchievementBackend : IDisposable
{
    AchievementBackendState State { get; }
    AchievementOwner? Identity { get; }
    string? Diagnostic { get; }
    event Action<AchievementStoreCompletion>? StoreCompleted;
    void Pump();
    /// <summary>Return null if any requested counter or achievement read fails; an absent unlock means successfully read and locked.</summary>
    AchievementRemoteState? Read(IReadOnlyList<AchievementBinding> bindings);
    void Submit(AchievementDeliveryBatch batch, IReadOnlyList<AchievementBinding> bindings);
}

public sealed class NoAchievementBackend(string? diagnostic = null) : IAchievementBackend
{
    public AchievementBackendState State => AchievementBackendState.LocalOnly;
    public AchievementOwner? Identity => null;
    public string? Diagnostic => diagnostic;
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
    }
    public AchievementRemoteState? Read(IReadOnlyList<AchievementBinding> bindings) => null;
    public void Submit(AchievementDeliveryBatch batch, IReadOnlyList<AchievementBinding> bindings) => throw new InvalidOperationException("No external achievement destination is active.");
    public void Dispose()
    {
    }
}
