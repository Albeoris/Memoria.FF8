namespace Memoria.FF8;

internal enum AutoPlayEventKind
{
    Interaction,
    Transition
}

internal sealed record AutoPlayVisit(string Key, string Region);

/// <summary>ChangesStory is supplied by script-write analysis; Order permits explicit policy overrides.</summary>
internal sealed record AutoPlayEvent(string Id, AutoPlayEventKind Kind, string? DestinationVisitKey = null, bool ChangesStory = false, int Order = 0);

internal sealed record AutoPlayPlanStep(string VisitKey, AutoPlayEvent Event, bool IsTravel);

internal sealed class AutoPlayEventCoverage
{
    public AutoPlayEvent Event { get; set; } = new(string.Empty, AutoPlayEventKind.Interaction);
    public bool Available { get; set; }
    public bool Completed { get; set; }
    public string? BlockedReason { get; set; }
}

internal sealed class AutoPlayVisitCoverage
{
    public AutoPlayVisit Visit { get; set; } = new(string.Empty, string.Empty);
    public List<AutoPlayEventCoverage> Events { get; set; } = [];
}

internal enum AutoPlayChoiceStepKind
{
    SaveBeforeChoice,
    Select,
    Replay,
    RestoreContinuation,
    Complete
}

internal sealed record AutoPlayChoiceStep(AutoPlayChoiceStepKind Kind, int? Selection = null, string? Checkpoint = null);

internal sealed class AutoPlayChoiceCoverage
{
    public string Key { get; set; } = string.Empty;
    public int OptionCount { get; set; }
    public int FirstOption { get; set; }
    public string? BeforeCheckpoint { get; set; }
    public string? ContinuationCheckpoint { get; set; }
    public List<int> CompletedOptions { get; set; } = [];
    public int? ActiveOption { get; set; }
    public bool Finished { get; set; }
}

internal sealed class AutoPlayPlannerState
{
    public List<AutoPlayVisitCoverage> Visits { get; set; } = [];
    public List<AutoPlayChoiceCoverage> Choices { get; set; } = [];
}

/// <summary>A deterministic frontier planner. Visit keys include the adapter's relevant story-state fingerprint.</summary>
internal sealed class AutoPlayPlanner
{
    private readonly Func<AutoPlayVisit, AutoPlayEvent, int> phaseOrder;

    public AutoPlayPlannerState State { get; }

    public AutoPlayPlanner(AutoPlayPlannerState? state = null, Func<AutoPlayVisit, AutoPlayEvent, int>? phaseOrder = null)
    {
        State = state ?? new AutoPlayPlannerState();
        this.phaseOrder = phaseOrder ?? ((_, _) => 0);
    }

    public void Observe(AutoPlayVisit visit, IReadOnlyList<AutoPlayEvent> events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(visit.Key);
        if (events.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != events.Count)
        {
            throw new ArgumentException("Event identifiers must be unique within a visit.", nameof(events));
        }
        AutoPlayVisitCoverage? coverage = State.Visits.SingleOrDefault(item => item.Visit.Key == visit.Key);
        if (coverage is null)
        {
            coverage = new AutoPlayVisitCoverage { Visit = visit };
            State.Visits.Add(coverage);
        }
        coverage.Visit = visit;
        foreach (AutoPlayEventCoverage item in coverage.Events)
        {
            item.Available = false;
        }
        foreach (AutoPlayEvent item in events.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);
            AutoPlayEventCoverage? previous = coverage.Events.SingleOrDefault(candidate => candidate.Event.Id == item.Id);
            if (previous is null)
            {
                coverage.Events.Add(new AutoPlayEventCoverage { Event = item, Available = true });
            }
            else
            {
                previous.Event = item with { DestinationVisitKey = item.DestinationVisitKey ?? previous.Event.DestinationVisitKey, Kind = previous.Event.DestinationVisitKey is not null ? AutoPlayEventKind.Transition : item.Kind };
                previous.Available = true;
            }
        }
    }

    public void Completed(string visitKey, string eventId, string? destinationVisitKey = null)
    {
        AutoPlayEventCoverage item = FindEvent(visitKey, eventId);
        item.Completed = true;
        if (destinationVisitKey is not null)
        {
            item.Event = item.Event with { DestinationVisitKey = destinationVisitKey, Kind = AutoPlayEventKind.Transition };
        }
    }

    public void Block(string visitKey, string eventId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        FindEvent(visitKey, eventId).BlockedReason = reason;
    }

    /// <summary>Returns null when no observed frontier is reachable without crossing a completed story edge.</summary>
    public AutoPlayPlanStep? Plan(string currentVisitKey)
    {
        AutoPlayVisitCoverage current = FindVisit(currentVisitKey);
        Dictionary<string, AutoPlayPlanStep?> routes = new(StringComparer.Ordinal) { [currentVisitKey] = null };
        Dictionary<string, int> distances = new(StringComparer.Ordinal) { [currentVisitKey] = 0 };
        Queue<string> pending = new();
        pending.Enqueue(currentVisitKey);
        while (pending.TryDequeue(out string? key))
        {
            AutoPlayVisitCoverage visit = FindVisit(key);
            foreach (AutoPlayEventCoverage edge in OrderedEvents(visit).Where(item => item.Completed && !item.Event.ChangesStory && item.Event.Kind == AutoPlayEventKind.Transition))
            {
                string? destination = edge.Event.DestinationVisitKey;
                if (destination is null || routes.ContainsKey(destination) || !State.Visits.Any(item => item.Visit.Key == destination))
                {
                    continue;
                }
                routes.Add(destination, routes[key] ?? new AutoPlayPlanStep(key, edge.Event, true));
                distances.Add(destination, distances[key] + 1);
                pending.Enqueue(destination);
            }
        }
        var candidates = routes.Keys.SelectMany(key => OrderedEvents(FindVisit(key)).Where(item => !item.Completed).Select(item => new { Visit = FindVisit(key), Coverage = item }));
        var selected = candidates.OrderBy(item => item.Coverage.Event.ChangesStory)
            .ThenBy(item => phaseOrder(item.Visit.Visit, item.Coverage.Event))
            .ThenBy(item => item.Visit.Visit.Region != current.Visit.Region)
            .ThenBy(item => item.Coverage.Event.Order)
            .ThenBy(item => item.Coverage.Event.Kind)
            .ThenBy(item => distances[item.Visit.Visit.Key])
            .ThenBy(item => item.Visit.Visit.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Coverage.Event.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected is null)
        {
            return null;
        }
        return routes[selected.Visit.Visit.Key] ?? new AutoPlayPlanStep(selected.Visit.Visit.Key, selected.Coverage.Event, false);
    }

    /// <summary>Keys identify the choice and relevant story state, never the history of alternative selections.</summary>
    public void ObserveChoice(string key, int optionCount, int firstOption = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfLessThan(optionCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(firstOption);
        AutoPlayChoiceCoverage? choice = State.Choices.SingleOrDefault(item => item.Key == key);
        if (choice is null)
        {
            State.Choices.Add(new AutoPlayChoiceCoverage { Key = key, OptionCount = optionCount, FirstOption = firstOption });
        }
        else if (choice.OptionCount != optionCount || choice.FirstOption != firstOption)
        {
            throw new InvalidOperationException("A changed choice requires a distinct state key.");
        }
    }

    public AutoPlayChoiceStep PlanChoice(string key)
    {
        AutoPlayChoiceCoverage choice = FindChoice(key);
        if (choice.Finished)
        {
            return new AutoPlayChoiceStep(AutoPlayChoiceStepKind.Complete);
        }
        if (choice.ActiveOption is not null)
        {
            throw new InvalidOperationException("Wait for the active choice to return control before planning another option.");
        }
        if (choice.BeforeCheckpoint is null)
        {
            return new AutoPlayChoiceStep(AutoPlayChoiceStepKind.SaveBeforeChoice);
        }
        int selection = Enumerable.Range(choice.FirstOption, choice.OptionCount).FirstOrDefault(index => !choice.CompletedOptions.Contains(index), -1);
        if (selection < 0)
        {
            return new AutoPlayChoiceStep(AutoPlayChoiceStepKind.RestoreContinuation, Checkpoint: choice.ContinuationCheckpoint);
        }
        return new AutoPlayChoiceStep(selection == choice.FirstOption ? AutoPlayChoiceStepKind.Select : AutoPlayChoiceStepKind.Replay, selection, selection == choice.FirstOption ? null : choice.BeforeCheckpoint);
    }

    public void RegisterChoiceCheckpoint(string key, string checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint);
        AutoPlayChoiceCoverage choice = FindChoice(key);
        if (choice.BeforeCheckpoint is not null)
        {
            throw new InvalidOperationException("The original choice checkpoint must be preserved.");
        }
        choice.BeforeCheckpoint = checkpoint;
    }

    public void BeginChoiceOption(string key, int selection)
    {
        AutoPlayChoiceStep next = PlanChoice(key);
        if (next.Selection != selection)
        {
            throw new InvalidOperationException("The option does not match the next planned choice.");
        }
        FindChoice(key).ActiveOption = selection;
    }

    public void CompleteChoiceAtControlReturn(string key, string? continuationCheckpoint = null)
    {
        AutoPlayChoiceCoverage choice = FindChoice(key);
        int selection = choice.ActiveOption ?? throw new InvalidOperationException("No choice option is active.");
        if (selection == choice.FirstOption)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(continuationCheckpoint);
            choice.ContinuationCheckpoint = continuationCheckpoint;
        }
        choice.CompletedOptions.Add(selection);
        choice.ActiveOption = null;
    }

    public void FinishChoice(string key)
    {
        if (PlanChoice(key).Kind != AutoPlayChoiceStepKind.RestoreContinuation)
        {
            throw new InvalidOperationException("Every option must return control before restoring the continuation.");
        }
        FindChoice(key).Finished = true;
    }

    private static IOrderedEnumerable<AutoPlayEventCoverage> OrderedEvents(AutoPlayVisitCoverage visit)
    {
        return visit.Events.Where(item => item.Available && item.BlockedReason is null).OrderBy(item => item.Event.Order).ThenBy(item => item.Event.Kind).ThenBy(item => item.Event.Id, StringComparer.Ordinal);
    }

    private AutoPlayVisitCoverage FindVisit(string key)
    {
        return State.Visits.SingleOrDefault(item => item.Visit.Key == key) ?? throw new InvalidOperationException($"Unknown visit '{key}'.");
    }

    private AutoPlayEventCoverage FindEvent(string visitKey, string eventId)
    {
        return FindVisit(visitKey).Events.SingleOrDefault(item => item.Event.Id == eventId) ?? throw new InvalidOperationException($"Unknown event '{eventId}'.");
    }

    private AutoPlayChoiceCoverage FindChoice(string key)
    {
        return State.Choices.SingleOrDefault(item => item.Key == key) ?? throw new InvalidOperationException($"Unknown choice '{key}'.");
    }
}
