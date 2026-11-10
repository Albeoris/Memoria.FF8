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

public sealed record AutoPlayTourFrontier(string Id, string Source, string Destination, string Availability, AutoPlayTourKind Kind, string Reason, AutoPlayTourStatus Status = AutoPlayTourStatus.Pending, string? BeforeWitness = null, string? EntryWitness = null, string? ReturnWitness = null, string? EntryRoute = null, string? ReturnRoute = null, string? ParentId = null);
public sealed record AutoPlayTourDirectory(string Source, string Availability, bool Complete, string Reason, string? ParentId = null);
public sealed record AutoPlayTourState(string Region, AutoPlayTourFrontier[] Frontiers, bool CatalogComplete = false, string? CatalogReason = null)
{
    public AutoPlayTourFrontier[]? CompletedPredicates { get; init; }
    public AutoPlayTourDirectory[]? Directories { get; init; }
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

    public bool Complete => state.CatalogComplete && (state.Directories?.All(directory => directory.Complete) ?? true) && state.Frontiers.All(edge => edge.Status is AutoPlayTourStatus.Completed or AutoPlayTourStatus.Excluded);
    public AutoPlayTourState Capture() => Clone(state);
    public AutoPlayTourFrontier? ActiveLeaf => state.Frontiers.SingleOrDefault(edge => edge.Status == AutoPlayTourStatus.Active && !state.Frontiers.Any(child => child.ParentId == edge.Id && child.Status == AutoPlayTourStatus.Active));

    public void PublishDirectory(AutoPlayTourDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        AutoPlayTourDirectory[] directories = (state.Directories ?? []).Where(prior => prior.Source != directory.Source || prior.ParentId != directory.ParentId).Append(directory).ToArray();
        Replace(state with { Directories = directories });
    }

    public void PublishCatalog(IEnumerable<AutoPlayTourFrontier> frontiers, bool complete, string? reason)
    {
        Dictionary<string, AutoPlayTourFrontier> entries = state.Frontiers.ToDictionary(edge => edge.Id, StringComparer.Ordinal);
        List<AutoPlayTourFrontier> completed = (state.CompletedPredicates ?? []).ToList();
        foreach (AutoPlayTourFrontier discovered in frontiers)
        {
            if (entries.TryGetValue(discovered.Id, out AutoPlayTourFrontier? identity) && (identity.ParentId != discovered.ParentId || identity.Source != discovered.Source || identity.Destination != discovered.Destination))
            {
                throw new InvalidOperationException("A native optional-area frontier cannot change its parent or endpoints.");
            }
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
                AutoPlayTourFrontier? observed = completed.SingleOrDefault(edge => edge.Id == discovered.Id && edge.Availability == discovered.Availability && edge.Source == discovered.Source && edge.Destination == discovered.Destination && edge.Kind == discovered.Kind && edge.EntryRoute == discovered.EntryRoute && edge.ReturnRoute == discovered.ReturnRoute && edge.ParentId == discovered.ParentId);
                entries[discovered.Id] = observed ?? discovered with { Status = discovered.Kind == AutoPlayTourKind.OptionalGameplay ? AutoPlayTourStatus.Pending : AutoPlayTourStatus.Excluded, BeforeWitness = null, EntryWitness = null, ReturnWitness = null };
            }
        }
        Replace(state with { Frontiers = entries.Values.OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray(), CatalogComplete = complete, CatalogReason = reason, CompletedPredicates = completed.Count == 0 ? null : completed.ToArray() });
    }

    public void Begin(string id, string witness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(witness);
        AutoPlayTourFrontier target = state.Frontiers.Single(edge => edge.Id == id);
        AutoPlayTourFrontier? leaf = ActiveLeaf;
        if (target.ParentId != leaf?.Id || target.ParentId is not null && leaf?.EntryWitness is null)
        {
            throw new InvalidOperationException("A nested optional-area visit requires its observed active parent and a unique active path.");
        }
        Change(id, edge => edge.Status == AutoPlayTourStatus.Pending && edge.Kind == AutoPlayTourKind.OptionalGameplay ? edge with { Status = AutoPlayTourStatus.Active, BeforeWitness = witness } : throw new InvalidOperationException("Only a required optional-area frontier can begin."));
    }

    public void Arrive(string id, string witness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(witness);
        Change(id, edge => edge.Status == AutoPlayTourStatus.Active && edge.EntryWitness is null ? edge with { EntryWitness = witness } : throw new InvalidOperationException("Native entry requires the active outbound visit."));
    }

    public bool CanCompleteActiveVisit
    {
        get
        {
            AutoPlayTourFrontier? active = ActiveLeaf;
            if (active?.EntryWitness is null || state.Frontiers.Any(edge => IsDescendant(state.Frontiers, edge, active.Id) && edge.Status is AutoPlayTourStatus.Pending or AutoPlayTourStatus.Active) || state.Directories?.Any(directory => directory.ParentId == active.Id && !directory.Complete) == true)
            {
                return false;
            }
            return !state.Frontiers.Any(edge => edge.ParentId == active.Id) || state.Directories?.Any(directory => directory.ParentId == active.Id && directory.Source == active.Destination && directory.Complete) == true;
        }
    }

    public void CompleteVisit(string id, string witness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(witness);
        if (ActiveLeaf?.Id != id || !CanCompleteActiveVisit)
        {
            throw new InvalidOperationException("An optional-area completion requires its observed native entry and any required child coverage.");
        }
        Change(id, edge => edge with { Status = AutoPlayTourStatus.Completed, ReturnWitness = witness });
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
        if (string.IsNullOrWhiteSpace(state.Region) || state.Region.Length > 256 || state.Frontiers is null || state.Frontiers.Length > 512 || !state.CatalogComplete && string.IsNullOrWhiteSpace(state.CatalogReason) || state.Frontiers.Any(InvalidFrontier) || state.Frontiers.Select(edge => edge.Id).Distinct(StringComparer.Ordinal).Count() != state.Frontiers.Length || state.CompletedPredicates is { Length: > 512 } || state.CompletedPredicates is { } history && (history.Any(edge => InvalidFrontier(edge) || edge.Status != AutoPlayTourStatus.Completed) || history.Select(edge => (edge.Id, edge.Availability)).Distinct().Count() != history.Length))
        {
            throw new InvalidDataException("Autoplay optional-area frontier has corrupt classification or native visit evidence.");
        }
        ValidateHierarchy(state);
    }

    private static bool InvalidFrontier(AutoPlayTourFrontier? edge) => edge is null || string.IsNullOrWhiteSpace(edge.Id) || edge.Id.Length > 128 || string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.Destination) || string.IsNullOrWhiteSpace(edge.Availability) || string.IsNullOrWhiteSpace(edge.Reason) || !Enum.IsDefined(edge.Kind) || !Enum.IsDefined(edge.Status) || edge.Status is AutoPlayTourStatus.Active or AutoPlayTourStatus.Completed && string.IsNullOrWhiteSpace(edge.BeforeWitness) || edge.Status == AutoPlayTourStatus.Completed && (string.IsNullOrWhiteSpace(edge.EntryWitness) || string.IsNullOrWhiteSpace(edge.ReturnWitness)) || edge.Kind != AutoPlayTourKind.OptionalGameplay && edge.Status != AutoPlayTourStatus.Excluded;

    private static void ValidateHierarchy(AutoPlayTourState state)
    {
        Dictionary<string, AutoPlayTourFrontier> entries = state.Frontiers.ToDictionary(edge => edge.Id, StringComparer.Ordinal);
        foreach (AutoPlayTourFrontier edge in state.Frontiers)
        {
            HashSet<string> path = [edge.Id];
            AutoPlayTourFrontier current = edge;
            while (current.ParentId is not null)
            {
                if (!entries.TryGetValue(current.ParentId, out AutoPlayTourFrontier? parent) || !path.Add(parent.Id) || path.Count > 32 || current.Source != parent.Destination || parent.Kind != AutoPlayTourKind.OptionalGameplay)
                {
                    throw new InvalidDataException("Autoplay optional-area parent links are missing, cyclic or inconsistent with native endpoints.");
                }
                if (current.Status == AutoPlayTourStatus.Active && (parent.Status != AutoPlayTourStatus.Active || parent.EntryWitness is null) || current.Status == AutoPlayTourStatus.Completed && (parent.Status is not (AutoPlayTourStatus.Active or AutoPlayTourStatus.Completed) || parent.EntryWitness is null) || parent.Status == AutoPlayTourStatus.Completed && current.Status is AutoPlayTourStatus.Pending or AutoPlayTourStatus.Active)
                {
                    throw new InvalidDataException("Autoplay nested visit status contradicts its observed parent.");
                }
                current = parent;
            }
            if (edge.Status == AutoPlayTourStatus.Completed && state.Frontiers.Any(child => child.ParentId == edge.Id) && state.Directories?.Any(directory => directory.ParentId == edge.Id && directory.Source == edge.Destination && directory.Complete) != true)
            {
                throw new InvalidDataException("A completed nested parent has no reviewed source directory.");
            }
        }
        AutoPlayTourFrontier[] active = state.Frontiers.Where(edge => edge.Status == AutoPlayTourStatus.Active).ToArray();
        if (active.Count(edge => !active.Any(child => child.ParentId == edge.Id)) > 1)
        {
            throw new InvalidDataException("Autoplay optional-area visits contain multiple active branches.");
        }
        if (state.Directories is { } directories && (directories.Length > 512 || directories.Any(directory => directory is null || string.IsNullOrWhiteSpace(directory.Source) || directory.Source.Length > 256 || string.IsNullOrWhiteSpace(directory.Availability) || directory.Availability.Length > 1024 || string.IsNullOrWhiteSpace(directory.Reason) || directory.ParentId is not null && (!entries.TryGetValue(directory.ParentId, out AutoPlayTourFrontier? parent) || parent.Destination != directory.Source || parent.Kind != AutoPlayTourKind.OptionalGameplay)) || directories.Select(directory => (directory.Source, directory.ParentId)).Distinct().Count() != directories.Length))
        {
            throw new InvalidDataException("Autoplay optional-area source directories have corrupt identity or parent bindings.");
        }
        if (state.Directories?.Any(directory => !directory.Complete && directory.ParentId is not null && entries[directory.ParentId].Status == AutoPlayTourStatus.Completed) == true)
        {
            throw new InvalidDataException("An incomplete source directory contradicts its completed parent.");
        }
    }

    private static bool IsDescendant(AutoPlayTourFrontier[] entries, AutoPlayTourFrontier edge, string parentId)
    {
        while (edge.ParentId is not null)
        {
            if (edge.ParentId == parentId)
            {
                return true;
            }
            edge = entries.Single(parent => parent.Id == edge.ParentId);
        }
        return false;
    }

    public static AutoPlayTourState Clone(AutoPlayTourState state) => state with { Frontiers = state.Frontiers.ToArray(), CompletedPredicates = state.CompletedPredicates?.ToArray(), Directories = state.Directories?.ToArray() };
}
