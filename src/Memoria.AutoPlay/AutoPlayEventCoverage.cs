namespace Memoria.AutoPlay;

public enum AutoPlayEventCoverageStatus
{
    Available,
    Active,
    Completed,
    Deferred
}

public sealed record AutoPlayEventCoverageEntry(string Id, string Availability, AutoPlayEventCoverageStatus Status = AutoPlayEventCoverageStatus.Available, string? BeforeWitness = null, string? AfterWitness = null, string? Outcome = null, string? Reason = null);
public sealed record AutoPlayEventCoverageScope(string Id, AutoPlayEventCoverageEntry[] Events, bool CatalogComplete = false, string? CatalogReason = null);
public sealed record AutoPlayEventCoverageState(AutoPlayEventCoverageScope? Current = null, AutoPlayEventCoverageScope[]? History = null);

/// <summary>Tracks observed native events independently from a game's progression and interaction policy.</summary>
public sealed class AutoPlayEventCoverageLedger
{
    private AutoPlayEventCoverageState state;

    public AutoPlayEventCoverageLedger(AutoPlayEventCoverageState? restored = null)
    {
        Validate(restored ?? new());
        state = Clone(restored ?? new());
    }

    public AutoPlayEventCoverageScope? Current => state.Current is null ? null : Clone(state.Current);
    public bool CanLeave => state.Current is { CatalogComplete: true } current && current.Events.All(entry => entry.Status is AutoPlayEventCoverageStatus.Completed or AutoPlayEventCoverageStatus.Deferred);
    public bool AllEventsCompleted => CanLeave && state.Current!.Events.All(entry => entry.Status == AutoPlayEventCoverageStatus.Completed);

    public void Enter(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (state.Current?.Id == scope)
        {
            return;
        }
        AutoPlayEventCoverageScope[] history = state.History ?? [];
        if (state.Current is { } previous)
        {
            history = [.. history, previous];
        }
        AutoPlayEventCoverageState next = new(new(scope, []), history);
        Validate(next);
        state = next;
    }

    public void PublishCatalog(IEnumerable<AutoPlayEventCoverageEntry> available, bool complete, string? reason = null)
    {
        AutoPlayEventCoverageScope current = state.Current ?? throw new InvalidOperationException("Coverage requires an active native scene.");
        Dictionary<string, AutoPlayEventCoverageEntry> entries = current.Events.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        foreach (AutoPlayEventCoverageEntry discovered in available)
        {
            if (!entries.TryGetValue(discovered.Id, out AutoPlayEventCoverageEntry? prior) || prior.Availability != discovered.Availability)
            {
                if (prior?.Status == AutoPlayEventCoverageStatus.Active)
                {
                    throw new InvalidOperationException("An active native event changed availability before its completion was observed.");
                }
                entries[discovered.Id] = discovered.Status == AutoPlayEventCoverageStatus.Deferred ? discovered with { BeforeWitness = null, AfterWitness = null, Outcome = null } : discovered with { Status = AutoPlayEventCoverageStatus.Available, BeforeWitness = null, AfterWitness = null, Outcome = null, Reason = null };
            }
        }
        Replace(current with { Events = entries.Values.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray(), CatalogComplete = complete, CatalogReason = reason });
    }

    public void Begin(string id, string witness)
    {
        AutoPlayEventCoverageScope current = state.Current ?? throw new InvalidOperationException("Coverage requires an active native scene.");
        if (current.Events.Any(entry => entry.Status == AutoPlayEventCoverageStatus.Active))
        {
            throw new InvalidOperationException("Coverage already has an active native interaction.");
        }
        Change(id, entry => entry.Status == AutoPlayEventCoverageStatus.Available ? entry with { Status = AutoPlayEventCoverageStatus.Active, BeforeWitness = witness } : throw new InvalidOperationException("Only an available native event can begin."));
    }

    public void Complete(string id, string witness, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        Change(id, entry => entry.Status == AutoPlayEventCoverageStatus.Active ? entry with { Status = AutoPlayEventCoverageStatus.Completed, AfterWitness = witness, Outcome = outcome } : throw new InvalidOperationException("Native completion requires an active event owner."));
    }

    public void Defer(string id, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Change(id, entry => entry.Status != AutoPlayEventCoverageStatus.Completed ? entry with { Status = AutoPlayEventCoverageStatus.Deferred, Reason = reason } : throw new InvalidOperationException("Completed native evidence cannot be replaced by a deferral."));
    }

    public AutoPlayEventCoverageState Capture() => Clone(state);

    private void Change(string id, Func<AutoPlayEventCoverageEntry, AutoPlayEventCoverageEntry> update)
    {
        AutoPlayEventCoverageScope current = state.Current ?? throw new InvalidOperationException("Coverage requires an active native scene.");
        if (!current.Events.Any(entry => entry.Id == id))
        {
            throw new InvalidOperationException("The native event is absent from the current coverage catalog.");
        }
        Replace(current with { Events = current.Events.Select(entry => entry.Id == id ? update(entry) : entry).ToArray() });
    }

    private void Replace(AutoPlayEventCoverageScope current)
    {
        AutoPlayEventCoverageState next = state with { Current = current };
        Validate(next);
        state = next;
    }

    public static void Validate(AutoPlayEventCoverageState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.History is { Length: > 4096 })
        {
            throw new InvalidDataException("Autoplay event coverage exceeds its bounded scene allocation.");
        }
        IEnumerable<AutoPlayEventCoverageScope> scopes = (state.History ?? []).Concat(state.Current is null ? [] : new[] { state.Current });
        foreach (AutoPlayEventCoverageScope scope in scopes)
        {
            if (scope is null || string.IsNullOrWhiteSpace(scope.Id) || scope.Id.Length > 256 || scope.Events is null || scope.Events.Length > 4096 || scope.Events.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.Id) || entry.Id.Length > 128 || string.IsNullOrWhiteSpace(entry.Availability) || entry.Availability.Length > 256 || !Enum.IsDefined(entry.Status) || entry.Status is AutoPlayEventCoverageStatus.Active or AutoPlayEventCoverageStatus.Completed && string.IsNullOrWhiteSpace(entry.BeforeWitness) || entry.Status == AutoPlayEventCoverageStatus.Completed && (string.IsNullOrWhiteSpace(entry.AfterWitness) || string.IsNullOrWhiteSpace(entry.Outcome)) || entry.Status == AutoPlayEventCoverageStatus.Deferred && string.IsNullOrWhiteSpace(entry.Reason)) || scope.Events.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != scope.Events.Length || scope.Events.Count(entry => entry.Status == AutoPlayEventCoverageStatus.Active) > 1 || !scope.CatalogComplete && string.IsNullOrWhiteSpace(scope.CatalogReason) && scope.Events.Length > 0)
            {
                throw new InvalidDataException("Autoplay event coverage has corrupt native ownership or completion evidence.");
            }
        }
    }

    private static AutoPlayEventCoverageScope Clone(AutoPlayEventCoverageScope scope) => scope with { Events = scope.Events.ToArray() };
    private static AutoPlayEventCoverageState Clone(AutoPlayEventCoverageState state) => state with { Current = state.Current is null ? null : Clone(state.Current), History = state.History?.Select(Clone).ToArray() };
}
