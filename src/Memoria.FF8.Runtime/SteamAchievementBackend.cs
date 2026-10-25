namespace Memoria.FF8.Runtime;

/// <summary>One process-lifetime Steam SDK owner, bound to an explicitly selected product and actual user.</summary>
public sealed class SteamAchievementBackend : IAchievementBackend
{
    private readonly ISteamAchievementApi? api;
    private readonly string product;
    private readonly string profile;
    private readonly AchievementBinding[] catalog;
    private readonly int owningThread = Environment.CurrentManagedThreadId;
    private readonly TimeProvider clock = TimeProvider.System;
    private AchievementDeliveryBatch? pending;
    private long submittedAt;
    private bool persistenceConfirmed = true;
    private bool disposed;
    private bool apiDisposed;
    public AchievementBackendState State { get; private set; } = AchievementBackendState.Initializing;
    public AchievementOwner? Identity { get; private set; }
    public string? Diagnostic { get; private set; }
    public event Action<AchievementStoreCompletion>? StoreCompleted;

    public SteamAchievementBackend(string nativePath, string productId, string localProfile)
    {
        product = productId;
        profile = localProfile;
        catalog = SteamAchievementMappings.ForProduct(productId);
        try
        {
            ValidateConfiguration();
            api = new SteamAchievementNativeApi(nativePath);
            Initialize();
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            Stop("Steam SDK unavailable: " + exception.Message);
        }
    }

    internal SteamAchievementBackend(ISteamAchievementApi api, string productId, string localProfile, TimeProvider? clock = null)
    {
        product = productId;
        profile = localProfile;
        catalog = SteamAchievementMappings.ForProduct(productId);
        ValidateConfiguration();
        this.api = api;
        this.clock = clock ?? TimeProvider.System;
        Initialize();
    }

    private void ValidateConfiguration()
    {
        if (catalog.Length == 0)
        {
            throw new ArgumentException("No verified Steam achievement mapping exists for this product.");
        }
        if (catalog.Select(binding => binding.Id).Distinct().Count() != catalog.Length || catalog.Select(binding => binding.NativeAchievement).Distinct(StringComparer.Ordinal).Count() != catalog.Length || catalog.Any(binding => string.IsNullOrWhiteSpace(binding.NativeAchievement) || binding.NativeCounter is not null && string.IsNullOrWhiteSpace(binding.NativeCounter)))
        {
            throw new ArgumentException("Steam achievement compatibility mappings are invalid or conflicting.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);
    }

    private void Initialize()
    {
        if (!api!.Initialize(product, out string? error))
        {
            Stop(error ?? "Steam initialization failed; local achievements remain available.");
            return;
        }
        State = AchievementBackendState.AwaitingIdentity;
        if (RefreshIdentity())
        {
            State = AchievementBackendState.AwaitingStats;
            // UserStats013 preloads current-user data; successful typed reads establish readiness.
            ProbeReadiness();
        }
    }

    public void Pump()
    {
        CheckThread();
        if (disposed || State == AchievementBackendState.Unsupported)
        {
            return;
        }
        try
        {
            if (!RefreshIdentity())
            {
                return;
            }
            foreach (SteamAchievementCallback callback in api!.Pump())
            {
                if (callback.Product != ulong.Parse(product, System.Globalization.CultureInfo.InvariantCulture))
                {
                    continue;
                }
                if (callback.Kind == SteamAchievementCallbackKind.StatsStored && pending is { } batch && batch.Owner == Identity)
                {
                    bool success = callback.Result == 1;
                    persistenceConfirmed = success;
                    pending = null;
                    Diagnostic = success ? "Steam SDK accepted/cached the store; server synchronization is not independently observed." : "Steam stats store failed; delivery remains queued.";
                    if (callback.Result == 8)
                    {
                        Stop("Steam rejected the stat or achievement configuration (InvalidParam); correct the mapping before retrying.");
                    }
                    StoreCompleted?.Invoke(new(batch.Revision, batch.Owner, success, Diagnostic));
                    if (State == AchievementBackendState.Unsupported)
                    {
                        return;
                    }
                }
                else if (callback.Kind == SteamAchievementCallbackKind.StatsReceived && callback.User == api.UserId && callback.Result != 1)
                {
                    State = AchievementBackendState.AwaitingStats;
                    Diagnostic = "Steam current-user stats retrieval failed; no baseline was assumed.";
                }
            }
            if (pending is not null && clock.GetElapsedTime(submittedAt) >= TimeSpan.FromSeconds(30))
            {
                Stop("Steam stats-store callback did not arrive within 30 seconds. Delivery remains queued; restart Memoria to establish a fresh SDK session before retrying.");
                return;
            }
            if (State is AchievementBackendState.AwaitingStats or AchievementBackendState.TransientFailure)
            {
                ProbeReadiness();
            }
            else if (State is AchievementBackendState.Ready or AchievementBackendState.OfflineReady)
            {
                State = api.LoggedOn ? AchievementBackendState.Ready : AchievementBackendState.OfflineReady;
            }
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            State = AchievementBackendState.TransientFailure;
            Diagnostic = "Steam callback processing failed: " + exception.Message;
        }
    }

    private bool RefreshIdentity()
    {
        if (api!.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture) != product)
        {
            Stop("Steam runtime AppID does not match the selected product; external delivery is disabled.");
            return false;
        }
        ulong user = api.UserId;
        if ((user >> 56) != 1 || ((user >> 52) & 15) != 1 || (user & uint.MaxValue) == 0)
        {
            Stop("Steam has no valid individual user identity; external delivery is disabled.");
            return false;
        }
        AchievementOwner actual = new("steam", product, user.ToString(System.Globalization.CultureInfo.InvariantCulture), profile);
        if (Identity is not null && Identity != actual)
        {
            Stop("Steam account changed; external delivery requires a fresh process session.");
            return false;
        }
        Identity = actual;
        return true;
    }

    private void ProbeReadiness()
    {
        if (TryRead(catalog, out _) && Identity is not null)
        {
            State = api!.LoggedOn ? AchievementBackendState.Ready : AchievementBackendState.OfflineReady;
            Diagnostic = "Steam product, user and current achievement data verified.";
        }
        else
        {
            State = AchievementBackendState.AwaitingStats;
            Diagnostic = "Steam current-user stats are not readable yet; no locked or zero baseline was assumed.";
        }
    }

    public AchievementRemoteState? Read(IReadOnlyList<AchievementBinding> bindings)
    {
        CheckThread();
        ValidateBindings(bindings);
        if (disposed || State is not (AchievementBackendState.Ready or AchievementBackendState.OfflineReady) || !RefreshIdentity())
        {
            return null;
        }
        if (!TryRead(bindings, out AchievementRemoteState? remote))
        {
            State = AchievementBackendState.AwaitingStats;
            Diagnostic = "Steam achievement/stat read failed; no baseline was assumed.";
            return null;
        }
        return remote;
    }

    private bool TryRead(IReadOnlyList<AchievementBinding> bindings, out AchievementRemoteState? remote)
    {
        remote = null;
        Dictionary<string, long> counters = [];
        HashSet<string> unlocks = new(StringComparer.Ordinal);
        foreach (AchievementBinding binding in bindings)
        {
            if (!api!.GetAchievement(binding.NativeAchievement, out bool unlocked))
            {
                return false;
            }
            if (unlocked)
            {
                unlocks.Add(binding.Id.Value);
            }
            if (binding.NativeCounter is string stat)
            {
                if (!api.GetStat(stat, out int value) || value < 0)
                {
                    return false;
                }
                counters[binding.Id.Value] = value;
            }
        }
        remote = new(counters, unlocks, persistenceConfirmed);
        return true;
    }

    public void Submit(AchievementDeliveryBatch batch, IReadOnlyList<AchievementBinding> bindings)
    {
        CheckThread();
        ValidateBindings(bindings);
        HashSet<string> ids = bindings.Select(binding => binding.Id.Value).ToHashSet(StringComparer.Ordinal);
        if (disposed || State is not (AchievementBackendState.Ready or AchievementBackendState.OfflineReady) || !RefreshIdentity() || batch.Owner != Identity || pending is not null && !SameDurableBatch(pending, batch) || batch.Revision <= 0 || batch.Targets.Keys.Any(key => !ids.Contains(key)) || batch.Unlocks.Any(key => !ids.Contains(key)) || batch.Targets.Count == 0 && batch.Unlocks.Count == 0)
        {
            throw new InvalidOperationException("Steam has no matching ready account or serialized store slot for this batch.");
        }
        if (pending is not null)
        {
            // Store callbacks carry no request ID. Never create a second callback for an ambiguous batch.
            Diagnostic = "Steam store is already in flight; the identical durable batch remains queued pending its callback.";
            return;
        }
        if (!TryRead(bindings, out AchievementRemoteState? before))
        {
            throw new IOException("Steam baseline read failed before submission.");
        }
        persistenceConfirmed = false;
        foreach (AchievementBinding binding in bindings)
        {
            if (binding.NativeCounter is string stat && batch.Targets.TryGetValue(binding.Id.Value, out long target))
            {
                if (target is < 0 or > int.MaxValue || !api!.SetStat(stat, checked((int)Math.Max(target, before!.Counters[binding.Id.Value]))))
                {
                    throw new IOException("Steam rejected a verified absolute integer counter.");
                }
            }
            else if (binding.NativeCounter is null && batch.Targets.ContainsKey(binding.Id.Value) && !batch.Unlocks.Contains(binding.Id.Value))
            {
                throw new InvalidOperationException("This achievement has no verified persistent progress stat; only its unlock may be submitted.");
            }
            if (batch.Unlocks.Contains(binding.Id.Value) && !api!.SetAchievement(binding.NativeAchievement))
            {
                throw new IOException("Steam rejected a verified achievement unlock.");
            }
        }
        // Preserve the exact persisted batch while its one native store is in flight.
        pending = batch with { Targets = new(batch.Targets, StringComparer.Ordinal), CoveredContributions = new(batch.CoveredContributions, StringComparer.Ordinal), Baselines = new(batch.Baselines, StringComparer.Ordinal), Unlocks = new(batch.Unlocks, StringComparer.Ordinal) };
        submittedAt = clock.GetTimestamp();
        if (!api!.StoreStats())
        {
            pending = null;
            throw new IOException("Steam rejected StoreStats; cached state does not confirm persistence.");
        }
        Diagnostic = "Steam store submitted; awaiting the stats-store callback.";
    }

    private void ValidateBindings(IReadOnlyList<AchievementBinding> bindings)
    {
        if (bindings.Count == 0 || bindings.Distinct().Count() != bindings.Count || bindings.Any(binding => !catalog.Contains(binding)))
        {
            throw new ArgumentException("Steam bindings must match the selected product's verified keys and counter type.");
        }
    }

    private static bool SameDurableBatch(AchievementDeliveryBatch first, AchievementDeliveryBatch second)
    {
        return first.Revision == second.Revision && first.Owner == second.Owner && SameValues(first.Targets, second.Targets) && SameValues(first.CoveredContributions, second.CoveredContributions) && SameValues(first.Baselines, second.Baselines) && first.Unlocks.SetEquals(second.Unlocks);
    }

    private static bool SameValues(IReadOnlyDictionary<string, long> first, IReadOnlyDictionary<string, long> second)
    {
        return first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out long value) && value == pair.Value);
    }

    private void Stop(string reason)
    {
        State = AchievementBackendState.Unsupported;
        Identity = null;
        pending = null;
        Diagnostic = reason;
        DisposeApi();
    }

    private void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != owningThread)
        {
            throw new InvalidOperationException("Steam SDK operations must remain on their owning application thread.");
        }
    }

    private static bool IsNativeFailure(Exception exception) => exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or DllNotFoundException or EntryPointNotFoundException or InvalidOperationException;

    public void Dispose()
    {
        CheckThread();
        if (disposed)
        {
            return;
        }
        disposed = true;
        DisposeApi();
        Identity = null;
    }

    private void DisposeApi()
    {
        if (apiDisposed)
        {
            return;
        }
        apiDisposed = true;
        try
        {
            api?.Dispose();
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            Diagnostic = (Diagnostic ?? "Steam SDK stopped.") + " Shutdown: " + exception.Message;
        }
    }
}

internal enum SteamAchievementCallbackKind { StatsReceived, StatsStored, AchievementProgress }
internal readonly record struct SteamAchievementCallback(SteamAchievementCallbackKind Kind, ulong Product, int Result, ulong User = 0);
internal interface ISteamAchievementApi : IDisposable
{
    bool Initialize(string product, out string? diagnostic);
    uint AppId { get; }
    ulong UserId { get; }
    bool LoggedOn { get; }
    IReadOnlyList<SteamAchievementCallback> Pump();
    bool GetAchievement(string key, out bool unlocked);
    bool GetStat(string key, out int value);
    bool SetAchievement(string key);
    bool SetStat(string key, int value);
    bool StoreStats();
}
