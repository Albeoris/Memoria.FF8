namespace Memoria.AutoPlay;

public enum AutoPlayTourKind
{
    OptionalGameplay,
    Return,
    Corridor,
    StoryExit,
    Locked
}

public enum AutoPlayTourStatus
{
    Pending,
    Active,
    Completed,
    Excluded
}

public sealed record AutoPlayTourFrontier(string Id, string Source, string Destination, string Availability, AutoPlayTourKind Kind, string Reason, AutoPlayTourStatus Status = AutoPlayTourStatus.Pending, string? BeforeWitness = null, string? EntryWitness = null, string? ReturnWitness = null, string? EntryRoute = null, string? ReturnRoute = null);
public sealed record AutoPlayTourState(string Region, AutoPlayTourFrontier[] Frontiers, bool CatalogComplete = false, string? CatalogReason = null)
{
    public AutoPlayTourFrontier[]? CompletedPredicates { get; init; }
}

/// <summary>Records bounded optional-area visits independently from native game progression.</summary>
public sealed class AutoPlayTourLedger
{
    private AutoPlayTourState state;

    public AutoPlayTourLedger(AutoPlayTourState restored)
    {
        Validate(restored);
        state = Clone(restored);
    }

    public bool Complete => state.CatalogComplete && state.Frontiers.All(edge => edge.Status is AutoPlayTourStatus.Completed or AutoPlayTourStatus.Excluded);
    public AutoPlayTourState Capture() => Clone(state);

    public void PublishCatalog(IEnumerable<AutoPlayTourFrontier> frontiers, bool complete, string reason)
    {
        Dictionary<string, AutoPlayTourFrontier> entries = state.Frontiers.ToDictionary(edge => edge.Id, StringComparer.Ordinal);
        List<AutoPlayTourFrontier> completed = (state.CompletedPredicates ?? []).ToList();
        foreach (AutoPlayTourFrontier discovered in frontiers)
        {
            if (!entries.TryGetValue(discovered.Id, out AutoPlayTourFrontier? prior) || prior.Availability != discovered.Availability)
            {
                if (prior?.Status == AutoPlayTourStatus.Active)
                {
                    throw new InvalidOperationException("An active optional-area visit changed availability before its native return.");
                }
                if (prior?.Status == AutoPlayTourStatus.Completed && !completed.Any(edge => edge.Id == prior.Id && edge.Availability == prior.Availability))
                {
                    completed.Add(prior);
                }
                AutoPlayTourFrontier? observed = completed.SingleOrDefault(edge => edge.Id == discovered.Id && edge.Availability == discovered.Availability && edge.Source == discovered.Source && edge.Destination == discovered.Destination && edge.Kind == discovered.Kind && edge.EntryRoute == discovered.EntryRoute && edge.ReturnRoute == discovered.ReturnRoute);
                entries[discovered.Id] = observed ?? discovered with { Status = discovered.Kind == AutoPlayTourKind.OptionalGameplay ? AutoPlayTourStatus.Pending : AutoPlayTourStatus.Excluded, BeforeWitness = null, EntryWitness = null, ReturnWitness = null };
            }
        }
        Replace(state with { Frontiers = entries.Values.OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray(), CatalogComplete = complete, CatalogReason = reason, CompletedPredicates = completed.Count == 0 ? null : completed.ToArray() });
    }

    public void Begin(string id, string witness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(witness);
        if (state.Frontiers.Any(edge => edge.Status == AutoPlayTourStatus.Active))
        {
            throw new InvalidOperationException("An optional-area visit is already active.");
        }
        Change(id, edge => edge.Status == AutoPlayTourStatus.Pending && edge.Kind == AutoPlayTourKind.OptionalGameplay ? edge with { Status = AutoPlayTourStatus.Active, BeforeWitness = witness } : throw new InvalidOperationException("Only a required optional-area frontier can begin."));
    }

    public void Arrive(string id, string witness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(witness);
        Change(id, edge => edge.Status == AutoPlayTourStatus.Active && edge.EntryWitness is null ? edge with { EntryWitness = witness } : throw new InvalidOperationException("Native entry requires the active outbound visit."));
    }

    public void CompleteVisit(string id, string witness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(witness);
        Change(id, edge => edge.Status == AutoPlayTourStatus.Active && edge.EntryWitness is not null ? edge with { Status = AutoPlayTourStatus.Completed, ReturnWitness = witness } : throw new InvalidOperationException("An optional-area completion requires its native entry and return."));
    }

    private void Change(string id, Func<AutoPlayTourFrontier, AutoPlayTourFrontier> update)
    {
        if (!state.Frontiers.Any(edge => edge.Id == id))
        {
            throw new InvalidOperationException("The optional-area frontier is absent from the catalog.");
        }
        Replace(state with { Frontiers = state.Frontiers.Select(edge => edge.Id == id ? update(edge) : edge).ToArray() });
    }

    private void Replace(AutoPlayTourState next)
    {
        Validate(next);
        state = Clone(next);
    }

    public static void Validate(AutoPlayTourState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(state.Region) || state.Region.Length > 256 || state.Frontiers is null || state.Frontiers.Length > 512 || !state.CatalogComplete && string.IsNullOrWhiteSpace(state.CatalogReason) || state.Frontiers.Any(InvalidFrontier) || state.Frontiers.Select(edge => edge.Id).Distinct(StringComparer.Ordinal).Count() != state.Frontiers.Length || state.Frontiers.Count(edge => edge.Status == AutoPlayTourStatus.Active) > 1 || state.CompletedPredicates is { Length: > 512 } || state.CompletedPredicates is { } history && (history.Any(edge => InvalidFrontier(edge) || edge.Status != AutoPlayTourStatus.Completed) || history.Select(edge => (edge.Id, edge.Availability)).Distinct().Count() != history.Length))
        {
            throw new InvalidDataException("Autoplay optional-area frontier has corrupt classification or native visit evidence.");
        }
    }

    private static bool InvalidFrontier(AutoPlayTourFrontier? edge) => edge is null || string.IsNullOrWhiteSpace(edge.Id) || edge.Id.Length > 128 || string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.Destination) || string.IsNullOrWhiteSpace(edge.Availability) || string.IsNullOrWhiteSpace(edge.Reason) || !Enum.IsDefined(edge.Kind) || !Enum.IsDefined(edge.Status) || edge.Status is AutoPlayTourStatus.Active or AutoPlayTourStatus.Completed && string.IsNullOrWhiteSpace(edge.BeforeWitness) || edge.Status == AutoPlayTourStatus.Completed && (string.IsNullOrWhiteSpace(edge.EntryWitness) || string.IsNullOrWhiteSpace(edge.ReturnWitness)) || edge.Kind != AutoPlayTourKind.OptionalGameplay && edge.Status != AutoPlayTourStatus.Excluded;

    public static AutoPlayTourState Clone(AutoPlayTourState state) => state with { Frontiers = state.Frontiers.ToArray(), CompletedPredicates = state.CompletedPredicates?.ToArray() };
}
