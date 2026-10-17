namespace Memoria.FF8;

internal enum AutoPlayEventKind
{
    Interaction,
    Transition
}

internal sealed record AutoPlayVisit(string Key, string Region);

/// <summary>ChangesStory is supplied by script-write analysis; Order permits explicit policy overrides.</summary>
internal sealed record AutoPlayEvent(string Id, AutoPlayEventKind Kind, string? DestinationVisitKey = null, bool ChangesStory = false, int Order = 0, string? ConditionSignature = null, IReadOnlyList<string>? Variants = null);

internal sealed class AutoPlayConditionCoverage
{
    public HashSet<string> ConsumedEntries { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> ConsumedExits { get; set; } = new(StringComparer.Ordinal);
    public string? ActiveEntry { get; set; }

    public bool Contains(string signature) => ConsumedEntries.Contains(signature) || ConsumedExits.Contains(signature);
}

internal sealed record AutoPlayChoiceSelection(string Key, int Selection);
internal sealed record AutoPlayEventRoute(string DestinationVisitKey, string? ConditionSignature, bool? ObservedStoryChanged, IReadOnlyList<AutoPlayChoiceSelection>? Selections, string? TargetId = null);
internal sealed record AutoPlayRetiredRoute(AutoPlayEventRoute Route, string ActualChoiceKey, int FirstChoice, int LastChoice, IReadOnlyList<AutoPlayChoiceSelection> ExecutedPrefix);
internal sealed record AutoPlayPlanStep(string VisitKey, AutoPlayEvent Event, bool IsTravel, AutoPlayEventRoute? Route = null, string? RefreshVisitKey = null);
internal sealed record AutoPlayUnavailableAttempt(string SourceConditionSignature, string DestinationVisitKey, string GuardKey, string GuardFingerprint, string? TargetId = null);

internal sealed class AutoPlayEventCoverage
{
    public AutoPlayEvent Event { get; set; } = new(string.Empty, AutoPlayEventKind.Interaction);
    public bool Available { get; set; }
    public bool Completed { get; set; }
    public string? BlockedReason { get; set; }
    public AutoPlayConditionCoverage? Conditions { get; set; }
    public bool? ObservedStoryChanged { get; set; }
    public string? ObservedConditionSignature { get; set; }
    public List<AutoPlayEventRoute> Routes { get; set; } = [];
    public List<AutoPlayRetiredRoute> RetiredRoutes { get; set; } = [];
    public bool RouteNeedsRediscovery { get; set; }
    public List<AutoPlayUnavailableAttempt> UnavailableAttempts { get; set; } = [];
    public bool Unavailable { get; set; }
    public HashSet<string> UnavailableVariants { get; set; } = new(StringComparer.Ordinal);
    public string? AvailabilityFingerprint { get; set; }
    public HashSet<string> CompletedAvailabilityFingerprints { get; set; } = new(StringComparer.Ordinal);
    public List<AutoPlayParticipantPolicyDeferral> ParticipantPolicyDeferrals { get; set; } = [];
    public HashSet<string> PolicyDeferredVariants { get; set; } = new(StringComparer.Ordinal);
    public bool PolicyDeferred { get; set; }
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
    Complete,
    PolicyDeferred
}

internal sealed record AutoPlayChoiceStep(AutoPlayChoiceStepKind Kind, int? Selection = null, string? Checkpoint = null);
internal sealed record AutoPlayPolicyChoiceCheckpoint(string? BeforeCheckpoint, bool InputCheckpointPrepared, string? ContinuationCheckpoint, IReadOnlyList<AutoPlayChoiceSelection>? BeforeSelections);

internal sealed class AutoPlayChoiceCoverage
{
    public string Key { get; set; } = string.Empty;
    public int OptionCount { get; set; }
    public int FirstOption { get; set; }
    public string? BeforeCheckpoint { get; set; }
    public bool InputCheckpointPrepared { get; set; }
    public string? ContinuationCheckpoint { get; set; }
    public List<int> CompletedOptions { get; set; } = [];
    public int? ActiveOption { get; set; }
    public bool Finished { get; set; }
    public List<AutoPlayChoiceSelection>? BeforeSelections { get; set; }
    public HashSet<int> PolicyDeferredOptions { get; set; } = [];
    public bool PolicyExhausted { get; set; }
    public string? PolicyScope { get; set; }
    public Dictionary<string, AutoPlayPolicyChoiceCheckpoint> PolicyCheckpoints { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class AutoPlayPlannerState
{
    public List<AutoPlayVisitCoverage> Visits { get; set; } = [];
    public List<AutoPlayChoiceCoverage> Choices { get; set; } = [];
    public List<AutoPlayNavigationVisit> NavigationVisits { get; set; } = [];
    public List<AutoPlayNavigationRoute> NavigationRoutes { get; set; } = [];
}

/// <summary>A deterministic frontier planner with stable visit graphs and separately consumed event conditions.</summary>
internal sealed partial class AutoPlayPlanner
{
    private readonly Func<AutoPlayVisit, AutoPlayEvent, int> phaseOrder;
    private readonly Func<AutoPlayVisit, AutoPlayEvent, int> progressionOrder;

    public AutoPlayPlannerState State { get; }

    public AutoPlayPlanner(AutoPlayPlannerState? state = null, Func<AutoPlayVisit, AutoPlayEvent, int>? phaseOrder = null, Func<AutoPlayVisit, AutoPlayEvent, int>? progressionOrder = null) : this(state, phaseOrder, progressionOrder, normalizeLegacyRoutes: true)
    {
    }

    private AutoPlayPlanner(AutoPlayPlannerState? state, Func<AutoPlayVisit, AutoPlayEvent, int>? phaseOrder, Func<AutoPlayVisit, AutoPlayEvent, int>? progressionOrder, bool normalizeLegacyRoutes)
    {
        State = state ?? new AutoPlayPlannerState();
        this.phaseOrder = phaseOrder ?? ((_, _) => 0);
        this.progressionOrder = progressionOrder ?? ((_, _) => 0);
        if (normalizeLegacyRoutes)
        {
            foreach (AutoPlayEventCoverage item in State.Visits.SelectMany(visit => visit.Events))
            {
                NormalizeLegacyRoute(item);
            }
        }
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
                coverage.Events.Add(new AutoPlayEventCoverage { Event = item, Available = true, Conditions = item.ConditionSignature is null ? null : new AutoPlayConditionCoverage() });
            }
            else
            {
                if (!StringComparer.Ordinal.Equals(item.ConditionSignature, previous.ObservedConditionSignature))
                {
                    previous.ObservedStoryChanged = null;
                    previous.ObservedConditionSignature = null;
                }
                previous.Event = item with { DestinationVisitKey = item.DestinationVisitKey ?? previous.Event.DestinationVisitKey, Kind = previous.Event.DestinationVisitKey is not null ? AutoPlayEventKind.Transition : item.Kind };
                previous.Available = true;
                if (!previous.UnavailableAttempts.Any(attempt => attempt.SourceConditionSignature == item.ConditionSignature))
                {
                    previous.Unavailable = false;
                    previous.UnavailableVariants.Clear();
                    previous.AvailabilityFingerprint = null;
                }
                else
                {
                    previous.Unavailable = ObservedVariants(previous).All(previous.UnavailableVariants.Contains);
                }
                if (item.ConditionSignature is string signature)
                {
                    if (previous.Conditions is null)
                    {
                        previous.Conditions = new AutoPlayConditionCoverage();
                        if (previous.Completed || previous.BlockedReason is not null)
                        {
                            // Legacy manifests establish a baseline instead of replaying their completed frontier.
                            previous.Conditions.ConsumedExits.Add(signature);
                        }
                    }
                    else if (!previous.Conditions.Contains(signature))
                    {
                        previous.BlockedReason = null;
                    }
                }
            }
        }
    }

    public void BeginEvent(string visitKey, string eventId)
    {
        AutoPlayEventCoverage item = FindEvent(visitKey, eventId);
        if (item.Event.ConditionSignature is string signature)
        {
            item.Conditions ??= new AutoPlayConditionCoverage();
            item.Conditions.ActiveEntry = signature;
        }
    }

    public void Completed(string visitKey, string eventId, string? destinationVisitKey = null, string? exitSignature = null, bool? observedStoryChanged = null, IReadOnlyList<AutoPlayChoiceSelection>? selections = null, string? entrySignature = null, bool entrySignatureAuthoritative = false, string? entryAvailabilityFingerprint = null, bool availabilityFingerprintAuthoritative = false, string? targetId = null)
    {
        AutoPlayEventCoverage item = FindEvent(visitKey, eventId);
        item.Completed = true;
        item.RouteNeedsRediscovery = false;
        item.BlockedReason = null;
        item.Unavailable = false;
        string? completedAvailability = availabilityFingerprintAuthoritative ? entryAvailabilityFingerprint : item.AvailabilityFingerprint;
        if (completedAvailability is string availability)
        {
            item.CompletedAvailabilityFingerprints.Add(availability);
        }
        item.ObservedStoryChanged = observedStoryChanged;
        string? entry = entrySignatureAuthoritative ? entrySignature : entrySignature ?? item.Conditions?.ActiveEntry ?? item.Event.ConditionSignature;
        item.ObservedConditionSignature = observedStoryChanged is null ? null : entry;
        ConsumeConditions(item, exitSignature, entry, entrySignatureAuthoritative: true);
        if (destinationVisitKey is not null)
        {
            AutoPlayEventRoute route = new(destinationVisitKey, entry, observedStoryChanged, selections?.ToArray(), targetId);
            item.Routes.RemoveAll(previous => previous.DestinationVisitKey == route.DestinationVisitKey && previous.ConditionSignature == route.ConditionSignature && previous.TargetId == route.TargetId && (previous.Selections is null ? route.Selections is null : route.Selections is not null && previous.Selections.SequenceEqual(route.Selections)));
            item.Routes.Add(route);
            item.Event = item.Event with { DestinationVisitKey = destinationVisitKey, Kind = AutoPlayEventKind.Transition };
        }
        if (!entrySignatureAuthoritative)
        {
            NormalizeLegacyRoute(item);
        }
    }

    public void Block(string visitKey, string eventId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        AutoPlayEventCoverage item = FindEvent(visitKey, eventId);
        item.BlockedReason = reason;
        ConsumeConditions(item, null);
    }

    public void RetireTravelRoute(string visitKey, string eventId, AutoPlayEventRoute route, string actualChoiceKey, int first, int last, IReadOnlyList<AutoPlayChoiceSelection>? executedPrefix)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualChoiceKey);
        ArgumentOutOfRangeException.ThrowIfNegative(first);
        ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
        AutoPlayEventCoverage item = FindEvent(visitKey, eventId);
        if (route.Selections is null || executedPrefix is null || executedPrefix.Count > route.Selections.Count || !route.Selections.Take(executedPrefix.Count).SequenceEqual(executedPrefix))
        {
            throw new InvalidOperationException("Route retirement requires the exact authenticated executed prefix of its recorded choices.");
        }
        if (executedPrefix.Count < route.Selections.Count)
        {
            AutoPlayChoiceSelection expected = route.Selections[executedPrefix.Count];
            if (expected.Key == actualChoiceKey && expected.Selection >= first && expected.Selection <= last)
            {
                throw new InvalidOperationException("The actual next choice still admits the recorded route selection.");
            }
        }
        if (!OrderedEvents(FindVisit(visitKey)).Contains(item) || !TravelRoutes(item).Any(candidate => SameTravelRoute(candidate, route)))
        {
            throw new InvalidOperationException("Only an exact known eligible travel route can be retired.");
        }
        AutoPlayEventRoute recorded = item.Routes.Single(candidate => SameTravelRoute(candidate, route));
        item.RetiredRoutes.Add(new(recorded with { Selections = recorded.Selections!.ToArray() }, actualChoiceKey, first, last, executedPrefix.ToArray()));
        item.Routes.Remove(recorded);
        item.RouteNeedsRediscovery = true;
    }

    private static bool SameTravelRoute(AutoPlayEventRoute left, AutoPlayEventRoute right)
    {
        return left.DestinationVisitKey == right.DestinationVisitKey && left.ConditionSignature == right.ConditionSignature && left.ObservedStoryChanged == right.ObservedStoryChanged && left.TargetId == right.TargetId && (left.Selections is null ? right.Selections is null : right.Selections is not null && left.Selections.SequenceEqual(right.Selections));
    }

    public void MarkUnavailable(string visitKey, string eventId, AutoPlayUnavailableAttempt attempt)
    {
        AutoPlayEventCoverage item = FindEvent(visitKey, eventId);
        string targetId = attempt.TargetId ?? item.Event.Id;
        if (item.Event.ConditionSignature != attempt.SourceConditionSignature || string.IsNullOrWhiteSpace(attempt.GuardKey) || string.IsNullOrWhiteSpace(attempt.GuardFingerprint) || !ObservedVariants(item).Contains(targetId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Native unavailability requires its exact current source condition and a known destination guard.");
        }
        attempt = attempt with { TargetId = targetId };
        if (!item.UnavailableAttempts.Contains(attempt))
        {
            item.UnavailableAttempts.Add(attempt);
        }
        item.UnavailableVariants.Add(targetId);
        item.Unavailable = ObservedVariants(item).All(item.UnavailableVariants.Contains);
        if (item.Conditions is not null)
        {
            item.Conditions.ActiveEntry = null;
        }
    }

    public void RefreshUnavailable(string visitKey, IReadOnlyDictionary<string, string> guardFingerprints)
    {
        AutoPlayVisitCoverage visit = FindVisit(visitKey);
        List<(AutoPlayEventCoverage Event, HashSet<string> Variants, string? Fingerprint)> updates = [];
        foreach (AutoPlayEventCoverage item in visit.Events.Where(item => item.Available))
        {
            AutoPlayUnavailableAttempt[] attempts = item.UnavailableAttempts.Where(attempt => attempt.SourceConditionSignature == item.Event.ConditionSignature && ObservedVariants(item).Contains(attempt.TargetId ?? item.Event.Id, StringComparer.Ordinal)).ToArray();
            if (attempts.Length == 0)
            {
                updates.Add((item, [], null));
                continue;
            }
            foreach (string key in attempts.Select(attempt => attempt.GuardKey).Distinct(StringComparer.Ordinal))
            {
                if (!guardFingerprints.TryGetValue(key, out string? fingerprint) || string.IsNullOrWhiteSpace(fingerprint))
                {
                    throw new InvalidOperationException("A destination eligibility dependency is unknown; native unavailability cannot be refreshed.");
                }
            }
            HashSet<string> unavailable = attempts.Where(attempt => guardFingerprints[attempt.GuardKey] == attempt.GuardFingerprint).Select(attempt => attempt.TargetId ?? item.Event.Id).Where(target => ObservedVariants(item).Contains(target, StringComparer.Ordinal)).ToHashSet(StringComparer.Ordinal);
            string signature = item.Event.ConditionSignature + "|" + string.Join('|', attempts.Select(attempt => attempt.GuardKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(key => key + "=" + guardFingerprints[key]));
            updates.Add((item, unavailable, signature));
        }
        foreach (var update in updates)
        {
            update.Event.UnavailableVariants = update.Variants;
            update.Event.Unavailable = ObservedVariants(update.Event).All(update.Variants.Contains);
            update.Event.AvailabilityFingerprint = update.Fingerprint;
        }
    }

    private static IReadOnlyList<string> ObservedVariants(AutoPlayEventCoverage item)
    {
        return item.Event.Variants is { Count: > 0 } variants ? variants : [item.Event.Id];
    }

    /// <summary>Uses recorded routes for frontiers and optional remote refreshes; returns null when neither is reachable.</summary>
    public AutoPlayPlanStep? Plan(string currentVisitKey, Func<AutoPlayVisit, bool>? needsRefresh = null, string? refreshVisitKey = null, Func<AutoPlayVisit, AutoPlayEvent, int>? frontierPriority = null)
    {
        AutoPlayVisitCoverage current = FindVisit(currentVisitKey);
        Dictionary<string, AutoPlayPlanStep?> routes = new(StringComparer.Ordinal) { [currentVisitKey] = null };
        Dictionary<string, int> distances = new(StringComparer.Ordinal) { [currentVisitKey] = 0 };
        Queue<string> pending = new();
        pending.Enqueue(currentVisitKey);
        while (pending.TryDequeue(out string? key))
        {
            AutoPlayVisitCoverage visit = FindVisit(key);
            foreach (AutoPlayEventCoverage edge in OrderedEvents(visit))
            {
                foreach (AutoPlayEventRoute route in TravelRoutes(edge))
                {
                    string destination = route.DestinationVisitKey;
                    if (routes.ContainsKey(destination) || !State.Visits.Any(item => item.Visit.Key == destination))
                    {
                        continue;
                    }
                    routes.Add(destination, routes[key] ?? new AutoPlayPlanStep(key, edge.Event with { DestinationVisitKey = destination }, true, route));
                    distances.Add(destination, distances[key] + 1);
                    pending.Enqueue(destination);
                }
            }
        }
        var candidates = routes.Keys.SelectMany(key => OrderedEvents(FindVisit(key)).Where(NeedsExecution).Select(item => new { Visit = FindVisit(key), Coverage = item }));
        var ordered = candidates.OrderBy(item => progressionOrder(item.Visit.Visit, item.Coverage.Event))
            .ThenBy(item => item.Coverage.Event.ChangesStory)
            .ThenBy(item => frontierPriority?.Invoke(item.Visit.Visit, item.Coverage.Event) ?? 0)
            .ThenBy(item => phaseOrder(item.Visit.Visit, item.Coverage.Event))
            .ThenBy(item => item.Visit.Visit.Region != current.Visit.Region)
            .ThenBy(item => item.Coverage.Event.Order)
            .ThenBy(item => item.Coverage.Event.Kind)
            .ThenBy(item => distances[item.Visit.Visit.Key])
            .ThenBy(item => item.Visit.Visit.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Coverage.Event.Id, StringComparer.Ordinal);
        var selected = ordered.FirstOrDefault();
        if (needsRefresh is not null && (selected is null || progressionOrder(selected.Visit.Visit, selected.Coverage.Event) >= 0))
        {
            var ordinary = ordered.FirstOrDefault(item => !item.Coverage.Event.ChangesStory && progressionOrder(item.Visit.Visit, item.Coverage.Event) <= 0);
            if (ordinary is not null)
            {
                selected = ordinary;
            }
            else
            {
                string? refresh = refreshVisitKey is not null && refreshVisitKey != currentVisitKey && routes.ContainsKey(refreshVisitKey) ? refreshVisitKey : routes.Keys.Where(key => key != currentVisitKey && needsRefresh(FindVisit(key).Visit))
                    .OrderBy(key => phaseOrder(FindVisit(key).Visit, routes[key]!.Event))
                    .ThenBy(key => FindVisit(key).Visit.Region != current.Visit.Region)
                    .ThenBy(key => distances[key])
                    .ThenBy(key => key, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (refresh is not null)
                {
                    return routes[refresh]! with { RefreshVisitKey = refresh };
                }
            }
        }
        if (selected is null)
        {
            return null;
        }
        return routes[selected.Visit.Visit.Key] ?? new AutoPlayPlanStep(selected.Visit.Visit.Key, selected.Coverage.Event, false);
    }

    public int ReachableFrontierCount(string currentVisitKey, string? excludedVisit = null, string? excludedEvent = null, Func<AutoPlayVisit, bool>? needsRefresh = null)
    {
        HashSet<string> reached = [];
        Queue<string> pending = new();
        pending.Enqueue(currentVisitKey);
        int count = 0;
        while (pending.TryDequeue(out string? key))
        {
            if (!reached.Add(key))
            {
                continue;
            }
            AutoPlayVisitCoverage? visit = State.Visits.SingleOrDefault(item => item.Visit.Key == key);
            if (visit is null)
            {
                continue;
            }
            if (key != currentVisitKey && key != excludedVisit && needsRefresh?.Invoke(visit.Visit) == true)
            {
                count++;
            }
            foreach (AutoPlayEventCoverage item in OrderedEvents(visit))
            {
                if ((key != excludedVisit || item.Event.Id != excludedEvent) && NeedsExecution(item))
                {
                    count++;
                }
                foreach (AutoPlayEventRoute route in TravelRoutes(item))
                {
                    pending.Enqueue(route.DestinationVisitKey);
                }
            }
        }
        return count;
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
        if (choice.Finished || choice.PolicyExhausted)
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
        int selection = Enumerable.Range(choice.FirstOption, choice.OptionCount).FirstOrDefault(index => !choice.CompletedOptions.Contains(index) && !choice.PolicyDeferredOptions.Contains(index), -1);
        if (selection < 0)
        {
            return choice.CompletedOptions.Count == 0 ? new AutoPlayChoiceStep(AutoPlayChoiceStepKind.PolicyDeferred) : new AutoPlayChoiceStep(AutoPlayChoiceStepKind.RestoreContinuation, Checkpoint: choice.ContinuationCheckpoint);
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
        if (choice.ContinuationCheckpoint is null)
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
        AutoPlayChoiceCoverage choice = FindChoice(key);
        choice.PolicyExhausted = choice.PolicyDeferredOptions.Count > 0;
        choice.Finished = !choice.PolicyExhausted;
    }

    public void FinishPolicyDeferredChoice(string key)
    {
        if (PlanChoice(key).Kind != AutoPlayChoiceStepKind.PolicyDeferred)
        {
            throw new InvalidOperationException("Every remaining option must have an explicit participant-policy deferral.");
        }
        FindChoice(key).PolicyExhausted = true;
    }

    public void RefreshParticipantChoice(string key, string scope, IReadOnlyCollection<int> deferred)
    {
        AutoPlayChoiceCoverage choice = FindChoice(key);
        if (choice.ActiveOption is not null)
        {
            throw new InvalidOperationException("An active alternative cannot change its policy scope.");
        }
        if (choice.PolicyScope is not null && choice.PolicyScope != scope)
        {
            choice.PolicyCheckpoints[choice.PolicyScope] = new(choice.BeforeCheckpoint, choice.InputCheckpointPrepared, choice.ContinuationCheckpoint, choice.BeforeSelections?.ToArray());
            choice.PolicyCheckpoints.TryGetValue(scope, out AutoPlayPolicyChoiceCheckpoint? checkpoint);
            choice.BeforeCheckpoint = checkpoint?.BeforeCheckpoint;
            choice.ContinuationCheckpoint = checkpoint?.ContinuationCheckpoint;
            choice.BeforeSelections = checkpoint?.BeforeSelections?.ToList();
            choice.InputCheckpointPrepared = checkpoint?.InputCheckpointPrepared ?? false;
        }
        choice.PolicyScope = scope;
        choice.PolicyDeferredOptions = deferred.ToHashSet();
        choice.PolicyExhausted = !choice.Finished && deferred.Count > 0 && Enumerable.Range(choice.FirstOption, choice.OptionCount).All(option => choice.CompletedOptions.Contains(option) || deferred.Contains(option));
    }

    public bool DeferParticipantAction(string visitKey, string eventId, AutoPlayParticipantPolicyDeferral deferral)
    {
        AutoPlayEventCoverage item = FindEvent(visitKey, eventId);
        if (item.ParticipantPolicyDeferrals.Any(previous => previous.TargetId == deferral.TargetId && previous.SourceConditionSignature == deferral.SourceConditionSignature && previous.Guard.Fingerprint == deferral.Guard.Fingerprint && previous.Selections.SequenceEqual(deferral.Selections)))
        {
            return false;
        }
        item.ParticipantPolicyDeferrals.Add(deferral);
        return true;
    }

    public void RefreshParticipantPolicy(string visitKey, Func<AutoPlayParticipantPolicyDeferral, bool> matches)
    {
        AutoPlayVisitCoverage visit = State.Visits.Single(item => item.Visit.Key == visitKey);
        foreach (AutoPlayEventCoverage item in visit.Events)
        {
            AutoPlayParticipantPolicyDeferral[] current = item.ParticipantPolicyDeferrals.Where(attempt => attempt.SourceConditionSignature == item.Event.ConditionSignature && matches(attempt)).ToArray();
            item.PolicyDeferredVariants = current.GroupBy(attempt => attempt.TargetId).Where(group => group.Any(attempt => attempt.Selections.Count == 0) || group.Where(attempt => attempt.Selections.Count == 1 && attempt.ChoiceKey is not null).GroupBy(attempt => (attempt.ChoiceKey, attempt.FirstOption, attempt.OptionCount)).Any(options => Enumerable.Range(options.Key.FirstOption, options.Key.OptionCount).All(option => options.Any(attempt => attempt.Selections[0].Selection == option)))).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            item.PolicyDeferred = ObservedVariants(item).All(item.PolicyDeferredVariants.Contains);
        }
    }

    private static IOrderedEnumerable<AutoPlayEventCoverage> OrderedEvents(AutoPlayVisitCoverage visit)
    {
        return visit.Events.Where(item => item.Available && !item.Unavailable && !item.PolicyDeferred && item.BlockedReason is null).OrderBy(item => item.Event.Order).ThenBy(item => item.Event.Kind).ThenBy(item => item.Event.Id, StringComparer.Ordinal);
    }

    private static bool NeedsExecution(AutoPlayEventCoverage item)
    {
        bool conditionsChanged = item.Event.ConditionSignature is string signature && item.Conditions is not null ? !item.Conditions.Contains(signature) : !item.Completed;
        return item.RouteNeedsRediscovery || conditionsChanged || item.AvailabilityFingerprint is string availability && !item.CompletedAvailabilityFingerprints.Contains(availability);
    }

    private static IEnumerable<AutoPlayEventRoute> TravelRoutes(AutoPlayEventCoverage item)
    {
        return item.Routes.Where(route => item.Completed && StringComparer.Ordinal.Equals(route.ConditionSignature, item.Event.ConditionSignature) && !(route.ObservedStoryChanged ?? item.Event.ChangesStory) && (route.TargetId is null ? item.UnavailableVariants.Count == 0 && item.PolicyDeferredVariants.Count == 0 : ObservedVariants(item).Contains(route.TargetId, StringComparer.Ordinal) && !item.UnavailableVariants.Contains(route.TargetId) && !item.PolicyDeferredVariants.Contains(route.TargetId))).OrderBy(route => route.DestinationVisitKey, StringComparer.Ordinal).ThenBy(route => route.TargetId, StringComparer.Ordinal).ThenBy(route => route.Selections is null ? string.Empty : string.Join('|', route.Selections.Select(selection => $"{selection.Key}={selection.Selection}")), StringComparer.Ordinal);
    }

    private static void NormalizeLegacyRoute(AutoPlayEventCoverage item)
    {
        if (item.Completed && item.Routes.Count == 0 && item.RetiredRoutes.Count == 0 && item.Event.DestinationVisitKey is string destination)
        {
            bool? observed = StringComparer.Ordinal.Equals(item.ObservedConditionSignature, item.Event.ConditionSignature) ? item.ObservedStoryChanged : null;
            item.Routes.Add(new(destination, item.Event.ConditionSignature, observed, null));
        }
    }

    private static void ConsumeConditions(AutoPlayEventCoverage item, string? exitSignature, string? entrySignature = null, bool entrySignatureAuthoritative = false)
    {
        if (item.Event.ConditionSignature is not string && !entrySignatureAuthoritative)
        {
            return;
        }
        item.Conditions ??= new AutoPlayConditionCoverage();
        string? entry = entrySignatureAuthoritative ? entrySignature : item.Conditions.ActiveEntry ?? item.Event.ConditionSignature;
        if (entry is not null)
        {
            item.Conditions.ConsumedEntries.Add(entry);
        }
        if (exitSignature is not null)
        {
            // Exit states suppress scheduler self-repetition; they are not executed branch coverage.
            item.Conditions.ConsumedExits.Add(exitSignature);
        }
        item.Conditions.ActiveEntry = null;
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
