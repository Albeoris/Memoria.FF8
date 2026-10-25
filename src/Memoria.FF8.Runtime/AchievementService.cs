using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8.Runtime;

/// <summary>Durable local rule evaluation and serialized, revision-correlated platform delivery.</summary>
public sealed class AchievementService : IAchievementService, IDisposable
{
    private readonly AchievementRegistry registry;
    private readonly IAchievementRepository repository;
    private readonly IAchievementBackend backend;
    private readonly IReadOnlyList<AchievementBinding> bindings;
    private AchievementProfile profile;
    private AchievementOwner? activeOwner;
    private FileStream? destinationLock;
    private long? submittedRevision;
    private DateTime nextAttempt;
    private int failures;
    private bool disposed;
    public event Action<AchievementChange>? Committed;
    public AchievementBackendState BackendState => backend.State;
    public string? Diagnostic { get; private set; }
    public int PendingDeliveryCount => profile.Destinations.Values.Count(state => state.Pending is not null);

    public AchievementService(AchievementRegistry registry, IAchievementRepository repository, IAchievementBackend? backend = null, IReadOnlyList<AchievementBinding>? bindings = null)
    {
        this.registry = registry;
        this.repository = repository;
        this.backend = backend ?? new NoAchievementBackend();
        this.bindings = bindings?.ToArray() ?? [];
        if (this.bindings.Any(binding => string.IsNullOrWhiteSpace(binding.NativeAchievement) || binding.NativeCounter is not null && string.IsNullOrWhiteSpace(binding.NativeCounter)) || this.bindings.Select(binding => binding.Id).Distinct().Count() != this.bindings.Count || this.bindings.Select(binding => binding.NativeAchievement).Distinct(StringComparer.Ordinal).Count() != this.bindings.Count || this.bindings.Where(binding => binding.NativeCounter is not null).Select(binding => binding.NativeCounter).Distinct(StringComparer.Ordinal).Count() != this.bindings.Count(binding => binding.NativeCounter is not null))
        {
            throw new ArgumentException("Achievement bindings conflict or contain empty native keys.", nameof(bindings));
        }
        foreach (AchievementBinding binding in this.bindings)
        {
            if (registry.Definition(binding.Id).Scope == AchievementScope.LocalProfile)
            {
                throw new ArgumentException("External bindings require an explicit account or save-derived rule.", nameof(bindings));
            }
        }
        registry.Seal();
        try
        {
            profile = repository.Load();
            profile.Validate();
        }
        catch
        {
            try
            {
                this.backend.Dispose();
            }
            finally
            {
                repository.Dispose();
            }
            throw;
        }
        this.backend.StoreCompleted += OnStoreCompleted;
    }

    public AchievementOwner? CaptureOwner()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        AchievementOwner? identity = backend.Identity;
        identity?.Validate();
        return identity;
    }

    public AchievementSnapshot Get(AchievementId id)
    {
        AchievementDefinition definition = registry.Definition(id);
        return ViewSnapshot(profile, definition, CaptureOwner());
    }

    public void Record<TEvent>(in TEvent fact) where TEvent : struct, IAchievementEvent => RecordBatch<TEvent>([fact]);

    /// <summary>Atomically commits a bounded gameplay outbox batch before callers may acknowledge any of its facts.</summary>
    public void RecordBatch<TEvent>(IReadOnlyList<TEvent> facts) where TEvent : struct, IAchievementEvent
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(facts);
        AchievementProfile next = profile.Copy();
        HashSet<string> copiedContexts = new(StringComparer.Ordinal);
        Dictionary<string, AchievementChange> changes = new(StringComparer.Ordinal);
        foreach (TEvent fact in facts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fact.EventId.Value);
            ArgumentException.ThrowIfNullOrWhiteSpace(fact.ContextId.Value);
            fact.Owner?.Validate();
            if (next.Receipts.TryGetValue(fact.ContextId.Value, out var receipts) && receipts.Contains(fact.EventId.Value))
            {
                continue;
            }
            var contributions = registry.Evaluate(fact).ToArray();
            if (copiedContexts.Add(fact.ContextId.Value))
            {
                receipts = receipts is null ? new(StringComparer.Ordinal) : new(receipts, StringComparer.Ordinal);
                next.Receipts[fact.ContextId.Value] = receipts;
            }
            next.Receipts[fact.ContextId.Value].Add(fact.EventId.Value);
            foreach (var contribution in contributions)
            {
                AchievementDefinition definition = registry.Definition(contribution.Id);
                AchievementSnapshot previous = RawSnapshot(next, definition, fact.Owner);
                AchievementSnapshot previousView = ViewSnapshot(next, definition, fact.Owner);
                string counterKey = CounterKey(definition, fact.Owner);
                next.Counters[counterKey] = definition.Scope == AchievementScope.SaveHighWater ? Math.Max(previous.Counter, contribution.Contribution) : checked(previous.Counter + contribution.Contribution);
                if (next.Counters[counterKey] >= definition.Target)
                {
                    next.Unlocks.Add(counterKey);
                }
                if (fact.Owner is { } owner && definition.Scope != AchievementScope.LocalProfile && bindings.Any(binding => binding.Id == definition.Id))
                {
                    AchievementDestinationState destination = Destination(next, owner);
                    destination.Contributions[definition.Id.Value] = definition.Scope == AchievementScope.SaveHighWater ? Math.Max(destination.Contributions.GetValueOrDefault(definition.Id.Value), contribution.Contribution) : checked(destination.Contributions.GetValueOrDefault(definition.Id.Value) + contribution.Contribution);
                }
                AchievementSnapshot current = ViewSnapshot(next, definition, fact.Owner);
                if (current != previousView)
                {
                    changes[counterKey] = new(changes.TryGetValue(counterKey, out var existing) ? existing.Previous : previousView, current);
                }
            }
        }
        if (copiedContexts.Count == 0)
        {
            return;
        }
        Commit(next);
        foreach (AchievementChange change in changes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value))
        {
            // Subscriber failure cannot undo a committed receipt or cause duplicate evaluation.
            if (Committed is { } subscribers)
            {
                foreach (Action<AchievementChange> subscriber in subscribers.GetInvocationList().Cast<Action<AchievementChange>>())
                {
                    try
                    {
                        subscriber(change);
                    }
                    catch (Exception exception)
                    {
                        Diagnostic = $"Achievement presentation failed: {exception.Message}";
                    }
                }
            }
        }
        if (changes.Values.Any(change => change.NewlyUnlocked))
        {
            nextAttempt = DateTime.MinValue;
        }
    }

    public void Pump()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            backend.Pump();
            AchievementOwner? owner = CaptureOwner();
            if (owner != activeOwner)
            {
                destinationLock?.Dispose();
                destinationLock = null;
                activeOwner = owner;
                submittedRevision = null;
                failures = 0;
                nextAttempt = DateTime.MinValue;
            }
            if (owner is null || bindings.Count == 0 || backend.State is not (AchievementBackendState.Ready or AchievementBackendState.OfflineReady))
            {
                Diagnostic = backend.Diagnostic;
                return;
            }
            if (destinationLock is null)
            {
                string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new[] { owner.Provider, owner.Product, owner.User }))));
                string directory = Path.Combine(Path.GetTempPath(), "Memoria", "AchievementWriters");
                Directory.CreateDirectory(directory);
                destinationLock = new FileStream(Path.Combine(directory, digest + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            if (DateTime.UtcNow < nextAttempt)
            {
                return;
            }
            Deliver(owner);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Retry(exception.Message);
        }
    }

    private void Deliver(AchievementOwner owner)
    {
        profile.Destinations.TryGetValue(owner.Key, out var state);
        if (submittedRevision is not null)
        {
            // Missing callbacks are ambiguous: re-read/re-submit the same durable absolute batch.
            submittedRevision = null;
        }
        AchievementRemoteState? remote = backend.Read(bindings);
        if (remote is null)
        {
            Retry(backend.Diagnostic ?? "Achievement state read is not ready; progress remains queued.");
            return;
        }
        foreach (AchievementBinding binding in bindings.Where(binding => binding.NativeCounter is not null))
        {
            if (!remote.Counters.TryGetValue(binding.Id.Value, out long counter) || counter < 0)
            {
                Retry("Achievement counter read is incomplete; no baseline was assumed.");
                return;
            }
        }
        if (remote.PersistenceConfirmed)
        {
            ImportConfirmedRemote(owner, remote);
            state = profile.Destinations.GetValueOrDefault(owner.Key);
            failures = 0;
            Diagnostic = null;
        }
        if (state is null || !remote.PersistenceConfirmed && state.Pending is null)
        {
            Retry("Achievement state is only locally cached; waiting for a confirmed baseline.");
            return;
        }
        if (state.Pending is { } pending)
        {
            bool observed = remote.PersistenceConfirmed && pending.Targets.All(pair => bindings.Single(binding => binding.Id.Value == pair.Key).NativeCounter is null || remote.Counters[pair.Key] >= pair.Value) && pending.Unlocks.All(remote.Unlocks.Contains);
            if (observed)
            {
                Acknowledge(pending, remote);
                state = profile.Destinations[owner.Key];
            }
            else
            {
                submittedRevision = pending.Revision;
                nextAttempt = DateTime.UtcNow.AddSeconds(5);
                backend.Submit(pending, bindings);
                return;
            }
        }
        Dictionary<string, long> targets = new(StringComparer.Ordinal);
        Dictionary<string, long> covered = new(StringComparer.Ordinal);
        Dictionary<string, long> baselines = new(StringComparer.Ordinal);
        HashSet<string> unlocks = new(StringComparer.Ordinal);
        foreach (AchievementBinding binding in bindings)
        {
            string id = binding.Id.Value;
            long total = state.Contributions.GetValueOrDefault(id);
            long delivered = state.DeliveredContributions.GetValueOrDefault(id);
            long baseline = state.ConfirmedRemote.GetValueOrDefault(id);
            bool confirmedThresholdNeedsUnlock = baseline >= registry.Definition(binding.Id).Target && !state.ConfirmedUnlocks.Contains(id);
            if (total == delivered && !confirmedThresholdNeedsUnlock)
            {
                continue;
            }
            long target = registry.Definition(binding.Id).Scope == AchievementScope.SaveHighWater ? Math.Max(baseline, total) : checked(baseline + total - delivered);
            if (target >= registry.Definition(binding.Id).Target && !state.ConfirmedUnlocks.Contains(id))
            {
                unlocks.Add(id);
            }
            if (binding.NativeCounter is not null || unlocks.Contains(id))
            {
                targets[id] = target;
                covered[id] = total;
                baselines[id] = baseline;
            }
        }
        if (targets.Count == 0)
        {
            nextAttempt = DateTime.UtcNow.AddSeconds(5);
            return;
        }
        AchievementProfile next = profile.Copy();
        AchievementDeliveryBatch batch = new(checked(profile.Revision + 1), owner, targets, covered, unlocks) { Baselines = baselines };
        Destination(next, owner).Pending = batch;
        Commit(next);
        submittedRevision = batch.Revision;
        nextAttempt = DateTime.UtcNow.AddSeconds(5);
        backend.Submit(batch, bindings);
    }

    private void OnStoreCompleted(AchievementStoreCompletion completion)
    {
        if (completion.Owner != activeOwner || submittedRevision != completion.Revision || !profile.Destinations.TryGetValue(completion.Owner.Key, out var state) || state.Pending?.Revision != completion.Revision)
        {
            return;
        }
        submittedRevision = null;
        if (!completion.Success)
        {
            Retry(completion.Diagnostic ?? "Achievement store failed; durable delivery remains queued.");
            return;
        }
        // SDK success may only mean cached/offline acceptance. Confirm with a later read before retiring the batch.
        nextAttempt = DateTime.MinValue;
        Diagnostic = "Achievement batch accepted by SDK; awaiting observed state.";
    }

    private void ImportConfirmedRemote(AchievementOwner owner, AchievementRemoteState remote)
    {
        AchievementProfile next = profile.Copy();
        bool changed = !next.Destinations.ContainsKey(owner.Key);
        AchievementDestinationState state = Destination(next, owner);
        foreach (AchievementBinding binding in bindings)
        {
            string id = binding.Id.Value;
            bool remoteUnlocked = remote.Unlocks.Contains(id) || state.ConfirmedUnlocks.Contains(id);
            if (remoteUnlocked)
            {
                changed |= state.ConfirmedUnlocks.Add(id);
            }
            if (binding.NativeCounter is not null)
            {
                long observed = remote.Counters[id];
                if (!state.ConfirmedRemote.TryGetValue(id, out long previous) || observed > previous)
                {
                    state.ConfirmedRemote[id] = observed;
                    changed = true;
                }
            }
            long contribution = state.Contributions.GetValueOrDefault(id);
            bool covered = binding.NativeCounter is null ? remoteUnlocked : registry.Definition(binding.Id).Scope == AchievementScope.SaveHighWater && remote.Counters[id] >= contribution && (contribution < registry.Definition(binding.Id).Target || remoteUnlocked);
            if (covered && contribution > state.DeliveredContributions.GetValueOrDefault(id))
            {
                state.DeliveredContributions[id] = contribution;
                changed = true;
            }
        }
        if (changed)
        {
            Commit(next);
        }
    }

    private void Acknowledge(AchievementDeliveryBatch batch, AchievementRemoteState remote)
    {
        AchievementProfile next = profile.Copy();
        AchievementDestinationState state = Destination(next, batch.Owner);
        foreach (var pair in batch.CoveredContributions)
        {
            state.DeliveredContributions[pair.Key] = Math.Max(state.DeliveredContributions.GetValueOrDefault(pair.Key), pair.Value);
            if (bindings.Single(binding => binding.Id.Value == pair.Key).NativeCounter is not null)
            {
                state.ConfirmedRemote[pair.Key] = Math.Max(state.ConfirmedRemote.GetValueOrDefault(pair.Key), remote.Counters[pair.Key]);
            }
        }
        state.Pending = null;
        Commit(next);
        submittedRevision = null;
        failures = 0;
        Diagnostic = null;
    }

    private void Retry(string diagnostic)
    {
        Diagnostic = diagnostic;
        failures = Math.Min(failures + 1, 6);
        nextAttempt = DateTime.UtcNow.AddSeconds(1 << failures);
    }

    public void Flush()
    {
        nextAttempt = DateTime.MinValue;
        Pump();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        try
        {
            Flush();
        }
        finally
        {
            disposed = true;
            backend.StoreCompleted -= OnStoreCompleted;
            destinationLock?.Dispose();
            try
            {
                backend.Dispose();
            }
            finally
            {
                repository.Dispose();
            }
        }
    }

    private void Commit(AchievementProfile next)
    {
        next.Revision = checked(profile.Revision + 1);
        repository.Save(next);
        profile = next;
    }

    private static AchievementDestinationState Destination(AchievementProfile profile, AchievementOwner owner)
    {
        if (!profile.Destinations.TryGetValue(owner.Key, out var destination))
        {
            destination = new();
            profile.Destinations.Add(owner.Key, destination);
        }
        return destination;
    }

    private static string CounterKey(AchievementDefinition definition, AchievementOwner? owner) => definition.Scope == AchievementScope.LocalProfile ? definition.Id.Value : System.Text.Json.JsonSerializer.Serialize(new[] { definition.Id.Value, owner?.Key });
    private static AchievementSnapshot RawSnapshot(AchievementProfile profile, AchievementDefinition definition, AchievementOwner? owner) => new(definition.Id, profile.Counters.GetValueOrDefault(CounterKey(definition, owner)), definition.Target, profile.Unlocks.Contains(CounterKey(definition, owner)), definition.Scope != AchievementScope.SaveHighWater || profile.Counters.ContainsKey(CounterKey(definition, owner)), definition.Name);

    private static AchievementSnapshot ViewSnapshot(AchievementProfile profile, AchievementDefinition definition, AchievementOwner? owner)
    {
        AchievementSnapshot snapshot = RawSnapshot(profile, definition, owner);
        if (owner is null || definition.Scope == AchievementScope.LocalProfile || !profile.Destinations.TryGetValue(owner.Key, out var state))
        {
            return snapshot;
        }
        string key = definition.Id.Value;
        long confirmed = state.ConfirmedRemote.GetValueOrDefault(key);
        long baseline = state.Pending?.Baselines.GetValueOrDefault(key) ?? confirmed;
        long counter = definition.Scope == AchievementScope.SaveHighWater ? Math.Max(snapshot.Counter, baseline) : checked(baseline + state.Contributions.GetValueOrDefault(key) - state.DeliveredContributions.GetValueOrDefault(key));
        return snapshot with { Counter = counter, Unlocked = snapshot.Unlocked || state.ConfirmedUnlocks.Contains(key) || counter >= definition.Target, ProgressKnown = snapshot.ProgressKnown || state.ConfirmedRemote.ContainsKey(key) || baseline > 0 };
    }
}
