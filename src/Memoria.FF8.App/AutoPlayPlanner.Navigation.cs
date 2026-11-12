namespace Memoria.FF8;

internal sealed record AutoPlayNavigationVisit(string Key, string VisitKey, IReadOnlyList<AutoPlayEvent> Events);
internal sealed record AutoPlayNavigationEventSource(string NavigationKey, string EventId, string? ConditionSignature, string TargetId);
internal sealed record AutoPlayNavigationRoute(string SourceKey, string DestinationKey, string EventId, AutoPlayEventRoute Route);
internal sealed record AutoPlayRetiredNavigationRoute(AutoPlayNavigationRoute Route, string ActualDestinationKey);

internal sealed partial class AutoPlayPlanner
{
    public void ObserveNavigation(string visitKey, string navigationKey, IReadOnlyList<AutoPlayEvent> events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(navigationKey);
        ArgumentNullException.ThrowIfNull(events);
        AutoPlayVisitCoverage aggregate = FindVisit(visitKey);
        if (events.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != events.Count || events.Any(item => !aggregate.Events.Any(known => known.Event.Id == item.Id)))
        {
            throw new InvalidOperationException("A navigation observation requires distinct events already observed in its aggregate visit.");
        }
        AutoPlayNavigationVisit? previous = State.NavigationVisits.SingleOrDefault(visit => visit.Key == navigationKey);
        if (previous is not null && previous.VisitKey != visitKey)
        {
            throw new InvalidOperationException("A navigation identity cannot change its aggregate visit.");
        }
        AutoPlayNavigationVisit observation = new(navigationKey, visitKey, events.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item => item with { Variants = item.Variants?.ToArray() }).ToArray());
        if (previous is null)
        {
            State.NavigationVisits.Add(observation);
        }
        else
        {
            State.NavigationVisits[State.NavigationVisits.IndexOf(previous)] = observation;
        }
    }

    public void RecordNavigationRoute(string sourceKey, string destinationKey, string eventId, AutoPlayEventRoute route, AutoPlayNavigationEventSource? executedSource = null)
    {
        ArgumentNullException.ThrowIfNull(route);
        AutoPlayNavigationVisit source = FindNavigationVisit(sourceKey);
        AutoPlayNavigationVisit destination = FindNavigationVisit(destinationKey);
        AutoPlayEvent? observed = source.Events.SingleOrDefault(item => item.Id == eventId);
        AutoPlayEventCoverage aggregate = FindEvent(source.VisitKey, eventId);
        bool observedSource = observed is not null && observed.ConditionSignature == route.ConditionSignature && (route.TargetId is not string target || (observed.Variants is { Count: > 0 } variants ? variants : [observed.Id]).Contains(target, StringComparer.Ordinal));
        bool recordedSource = State.NavigationRoutes.Any(known => known.SourceKey == sourceKey && known.DestinationKey == destinationKey && known.EventId == eventId && SameTravelRoute(known.Route, route));
        bool exactExecution = executedSource is not null && executedSource.NavigationKey == sourceKey && executedSource.EventId == eventId && executedSource.ConditionSignature == route.ConditionSignature && executedSource.TargetId == route.TargetId;
        bool recordedAggregate = aggregate.Routes.Any(known => SameTravelRoute(known, route));
        if (!observedSource && !recordedSource && !exactExecution || destination.VisitKey != route.DestinationVisitKey || !recordedAggregate)
        {
            throw new InvalidOperationException($"A navigation edge requires its exact observed source and actual recorded aggregate route and destination. Source {sourceKey}, event {eventId}, variant {route.TargetId}, entry {route.ConditionSignature}, observed entry {observed?.ConditionSignature}, known variants [{string.Join(",", observed?.Variants ?? [])}], destination {destinationKey}, expected visit {route.DestinationVisitKey}, aggregate recorded {recordedAggregate}.");
        }
        foreach (AutoPlayNavigationRoute previous in State.NavigationRoutes.Where(known => known.SourceKey == sourceKey && known.DestinationKey != destinationKey && known.EventId == eventId && SameTravelRoute(known.Route, route)).ToArray())
        {
            if (!State.NavigationRetirements.Any(retired => retired.ActualDestinationKey == destinationKey && retired.Route.SourceKey == previous.SourceKey && retired.Route.DestinationKey == previous.DestinationKey && retired.Route.EventId == previous.EventId && SameTravelRoute(retired.Route.Route, previous.Route)))
            {
                State.NavigationRetirements.Add(new(previous, destinationKey));
            }
            State.NavigationRoutes.Remove(previous);
        }
        if (!State.NavigationRoutes.Any(known => known.SourceKey == sourceKey && known.DestinationKey == destinationKey && known.EventId == eventId && SameTravelRoute(known.Route, route)))
        {
            State.NavigationRoutes.Add(new(sourceKey, destinationKey, eventId, route with { Selections = route.Selections?.ToArray() }));
        }
    }

    internal void RetireObservedGatewayDestination(string sourceNavigationKey, string eventId, AutoPlayEventRoute route, string actualDestinationNavigationKey)
    {
        AutoPlayNavigationVisit source = FindNavigationVisit(sourceNavigationKey);
        AutoPlayNavigationVisit destination = FindNavigationVisit(actualDestinationNavigationKey);
        AutoPlayEventCoverage item = FindEvent(source.VisitKey, eventId);
        AutoPlayEventRoute? recorded = item.Routes.SingleOrDefault(candidate => SameTravelRoute(candidate, route));
        if (recorded is null || recorded.DestinationVisitKey == destination.VisitKey)
        {
            throw new InvalidOperationException("A gateway destination correction requires an exact recorded route to a different destination.");
        }
        foreach (AutoPlayNavigationRoute previous in State.NavigationRoutes.Where(edge => edge.EventId == eventId && FindNavigationVisit(edge.SourceKey).VisitKey == source.VisitKey && SameTravelRoute(edge.Route, recorded)).ToArray())
        {
            AutoPlayRetiredNavigationRoute retirement = new(previous, actualDestinationNavigationKey);
            if (!State.NavigationRetirements.Contains(retirement))
            {
                State.NavigationRetirements.Add(retirement);
            }
            State.NavigationRoutes.Remove(previous);
        }
        item.Routes.Remove(recorded);
    }

    public AutoPlayPlanStep? PlanNavigation(string currentNavigationKey, Func<AutoPlayVisit, bool>? needsRefresh = null, string? refreshNavigationKey = null, Func<AutoPlayNavigationVisit, bool>? isCurrent = null, bool allowRemoteConditionRoutes = false, IReadOnlySet<string>? avoidFirstEvents = null, Func<AutoPlayPlanStep, bool>? canTravel = null, AutoPlayTravelGoal? preferredGoal = null)
    {
        FindNavigationVisit(currentNavigationKey);
        AutoPlayPlanner shadow = CreateNavigationView(isCurrent, allowRemoteConditionRoutes ? currentNavigationKey : null);
        AutoPlayVisitCoverage? current = shadow.State.Visits.SingleOrDefault(visit => visit.Visit.Key == currentNavigationKey);
        if (current is null)
        {
            return null;
        }
        if (avoidFirstEvents is { Count: > 0 })
        {
            current.Events.RemoveAll(item => avoidFirstEvents.Contains(item.Event.Id));
        }
        HashSet<string> rejectedFirstEvents = new(StringComparer.Ordinal);
        Dictionary<string, string>? visitByNavigation = canTravel is null ? null : State.NavigationVisits.ToDictionary(visit => visit.Key, visit => visit.VisitKey, StringComparer.Ordinal);
        bool CheckTravel(AutoPlayPlanStep step)
        {
            AutoPlayEventRoute route = step.Route!;
            if (canTravel!(step with { Route = route with { DestinationVisitKey = visitByNavigation![route.DestinationVisitKey] } }))
            {
                return true;
            }
            rejectedFirstEvents.Add(step.Event.Id);
            return false;
        }
        // Genuine unconsumed events precede reobserving the topology of already completed travel.
        AutoPlayPlanStep? planned;
        do
        {
            rejectedFirstEvents.Clear();
            planned = shadow.Plan(currentNavigationKey, needsRefresh, refreshNavigationKey, (visit, item) => NeedsExecution(FindEvent(FindNavigationVisit(visit.Key).VisitKey, item.Id)) ? 0 : 1, canTravel is null ? null : CheckTravel, preferredGoal);
            if (planned is { Route: null })
            {
                AutoPlayNavigationVisit localSource = FindNavigationVisit(planned.VisitKey);
                AutoPlayEvent localEvent = localSource.Events.Single(item => item.Id == planned.Event.Id);
                AutoPlayEventCoverage shared = FindEvent(localSource.VisitKey, localEvent.Id);
                AutoPlayEventRoute? probe = NeedsExecution(shared) ? null : UnconfirmedNavigationRoutes(localSource, shadow.FindEvent(localSource.Key, localEvent.Id), shadow.State.Visits.Select(visit => visit.Visit.Key).ToHashSet(StringComparer.Ordinal)).FirstOrDefault();
                if (probe is null)
                {
                    return planned with { VisitKey = localSource.VisitKey, Event = localEvent };
                }
                AutoPlayPlanStep probePlan = planned with { VisitKey = localSource.VisitKey, Event = localEvent with { DestinationVisitKey = probe.DestinationVisitKey }, IsTravel = true, Route = probe, Goal = new(localSource.Key, localEvent.Id, localEvent.ConditionSignature) };
                if (canTravel?.Invoke(probePlan) != false)
                {
                    return probePlan;
                }
                current.Events.RemoveAll(item => item.Event.Id == localEvent.Id);
                continue;
            }
            if (planned is not null || current.Events.RemoveAll(item => rejectedFirstEvents.Contains(item.Event.Id)) == 0)
            {
                break;
            }
        }
        while (true);
        if (planned is null)
        {
            return null;
        }
        AutoPlayNavigationVisit source = FindNavigationVisit(planned.VisitKey);
        AutoPlayEvent observed = source.Events.Single(item => item.Id == planned.Event.Id);
        AutoPlayNavigationVisit destination = FindNavigationVisit(planned.Route!.DestinationVisitKey);
        AutoPlayEventRoute actual = planned.Route with { DestinationVisitKey = destination.VisitKey };
        return planned with { VisitKey = source.VisitKey, Event = observed with { DestinationVisitKey = destination.VisitKey }, Route = actual };
    }

    /// <summary>Counts genuine gameplay frontiers, excluding navigation probes that only confirm map connectivity.</summary>
    public int ReachableNavigationFrontierCount(string currentNavigationKey, string? excludedVisit = null, string? excludedEvent = null, Func<AutoPlayVisit, bool>? needsRefresh = null, Func<AutoPlayNavigationVisit, bool>? isCurrent = null)
    {
        FindNavigationVisit(currentNavigationKey);
        AutoPlayPlanner shadow = CreateNavigationView(isCurrent);
        if (!shadow.State.Visits.Any(visit => visit.Visit.Key == currentNavigationKey))
        {
            return 0;
        }
        HashSet<string> reached = [];
        HashSet<(string Visit, string Event)> countedEvents = [];
        Queue<string> pending = new();
        pending.Enqueue(currentNavigationKey);
        int count = 0;
        while (pending.TryDequeue(out string? key))
        {
            if (!reached.Add(key))
            {
                continue;
            }
            AutoPlayVisitCoverage visit = shadow.FindVisit(key);
            string aggregateKey = FindNavigationVisit(key).VisitKey;
            if (key != currentNavigationKey && aggregateKey != excludedVisit && needsRefresh?.Invoke(visit.Visit) == true)
            {
                count++;
            }
            foreach (AutoPlayEventCoverage item in OrderedEvents(visit))
            {
                if ((aggregateKey != excludedVisit || item.Event.Id != excludedEvent) && NeedsExecution(FindEvent(aggregateKey, item.Event.Id)) && countedEvents.Add((aggregateKey, item.Event.Id)))
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

    private AutoPlayPlanner CreateNavigationView(Func<AutoPlayNavigationVisit, bool>? isCurrent, string? hintOrigin = null)
    {
        AutoPlayPlannerState view = new();
        Dictionary<string, AutoPlayNavigationVisit> navigationByKey = IndexVisits(State.NavigationVisits, visit => visit.Key);
        Dictionary<string, AutoPlayVisitCoverage> aggregateByKey = IndexVisits(State.Visits, visit => visit.Visit.Key);
        ILookup<(string Source, string Event), AutoPlayNavigationRoute> routesByEvent = State.NavigationRoutes.ToLookup(edge => (edge.SourceKey, edge.EventId));
        Dictionary<(string Source, string Event), (AutoPlayEventCoverage Scoped, AutoPlayEventRoute[] Travel)> travelByEvent = [];
        HashSet<string> valid = State.NavigationVisits.Where(visit => isCurrent?.Invoke(visit) != false).Select(visit => visit.Key).ToHashSet(StringComparer.Ordinal);
        ILookup<(string Visit, string Event), AutoPlayNavigationRoute> connectionsByEvent = State.NavigationRoutes.Where(edge => valid.Contains(edge.SourceKey) && valid.Contains(edge.DestinationKey)).ToLookup(edge => (navigationByKey[edge.SourceKey].VisitKey, edge.EventId));
        foreach (AutoPlayNavigationVisit observation in State.NavigationVisits.Where(visit => valid.Contains(visit.Key)))
        {
            AutoPlayVisitCoverage aggregate = aggregateByKey[observation.VisitKey];
            AutoPlayVisitCoverage scoped = new() { Visit = new(observation.Key, aggregate.Visit.Region) };
            foreach (AutoPlayEvent observed in observation.Events)
            {
                AutoPlayEventCoverage shared = aggregate.Events.Single(item => item.Event.Id == observed.Id);
                var key = (observation.Key, observed.Id);
                IEnumerable<AutoPlayNavigationRoute> recorded = routesByEvent[key];
                bool sameCondition = observed.ConditionSignature == shared.Event.ConditionSignature;
                // A previously executed transition can guide travel to its
                // remote source. It cannot authorize an action at the current vertex.
                // Retain all recorded remote predicates, including alternatives
                // replaced by a later observation of this same component.
                bool historicalTravel = hintOrigin is not null && observation.Key != hintOrigin;
                bool hint = !sameCondition && historicalTravel && recorded.Any(edge => valid.Contains(edge.DestinationKey));
                bool missingConnection = !recorded.Any(edge => edge.Route.ConditionSignature == observed.ConditionSignature && valid.Contains(edge.DestinationKey)) && connectionsByEvent[(observation.VisitKey, observed.Id)].Any(edge => edge.DestinationKey != edge.SourceKey && !(edge.Route.ObservedStoryChanged ?? observed.ChangesStory));
                bool connectionProbe = missingConnection && (sameCondition || historicalTravel);
                AutoPlayEventCoverage local = new()
                {
                    Event = observed,
                    Available = sameCondition || hint || connectionProbe,
                    Completed = shared.Completed,
                    BlockedReason = shared.BlockedReason,
                    Conditions = hint ? null : shared.Conditions,
                    ObservedStoryChanged = shared.ObservedStoryChanged,
                    ObservedConditionSignature = shared.ObservedConditionSignature,
                    Unavailable = shared.Unavailable,
                    UnavailableVariants = shared.UnavailableVariants,
                    AvailabilityFingerprint = hint ? null : shared.AvailabilityFingerprint,
                    CompletedAvailabilityFingerprints = shared.CompletedAvailabilityFingerprints,
                    PolicyDeferred = shared.PolicyDeferred,
                    PolicyDeferredVariants = shared.PolicyDeferredVariants,
                    RouteNeedsRediscovery = !hint && shared.RouteNeedsRediscovery,
                    AllowHistoricalTravelConditions = historicalTravel
                };
                if (shared.Completed && shared.Routes.Count != 0)
                {
                    AutoPlayEventRoute[] travel = NavigationTravelRoutes(observed, shared, aggregateByKey.ContainsKey, historicalTravel).ToArray();
                    // Completion alone does not prove connectivity under a new
                    // source predicate. Reobserve a previously real connection
                    // without replaying choices authenticated for the old predicate.
                    // A native return to this same component also bounds the probe.
                    if (connectionProbe && CanProbeNavigation(local))
                    {
                        local.RouteNeedsRediscovery = true;
                        local.Event = observed with { ChangesStory = false };
                    }
                    if (travel.Length != 0)
                    {
                        travelByEvent.Add(key, (local, travel));
                        if (!hint && CanProbeNavigation(local) && travel.Any(route => !recorded.Any(edge => valid.Contains(edge.DestinationKey) && SameTravelRoute(edge.Route, route))))
                        {
                            local.RouteNeedsRediscovery = true;
                            if (!NeedsExecution(shared))
                            {
                                local.Event = observed with { ChangesStory = false };
                            }
                        }
                    }
                }
                scoped.Events.Add(local);
            }
            view.Visits.Add(scoped);
        }
        foreach (AutoPlayNavigationRoute edge in State.NavigationRoutes)
        {
            if (!travelByEvent.TryGetValue((edge.SourceKey, edge.EventId), out var candidate) || !valid.Contains(edge.DestinationKey) || edge.Route.DestinationVisitKey != navigationByKey[edge.DestinationKey].VisitKey)
            {
                continue;
            }
            if (candidate.Travel.Any(route => SameTravelRoute(route, edge.Route)))
            {
                candidate.Scoped.Routes.Add(edge.Route with { DestinationVisitKey = edge.DestinationKey });
            }
        }
        AutoPlayVisit AggregateVisit(AutoPlayVisit visit)
        {
            return aggregateByKey[navigationByKey[visit.Key].VisitKey].Visit;
        }
        return new(view, (visit, item) => phaseOrder(AggregateVisit(visit), item), (visit, item) => progressionOrder(AggregateVisit(visit), item), normalizeLegacyRoutes: false);
    }

    private IEnumerable<AutoPlayEventRoute> UnconfirmedNavigationRoutes(AutoPlayNavigationVisit observation, AutoPlayEventCoverage scoped, IReadOnlySet<string> valid)
    {
        if (!CanProbeNavigation(scoped))
        {
            return [];
        }
        AutoPlayEvent observed = observation.Events.Single(item => item.Id == scoped.Event.Id);
        AutoPlayEventCoverage shared = FindEvent(observation.VisitKey, observed.Id);
        return NavigationTravelRoutes(observed, shared, key => State.Visits.Any(visit => visit.Visit.Key == key)).Where(route => !State.NavigationRoutes.Any(edge => valid.Contains(edge.SourceKey) && valid.Contains(edge.DestinationKey) && edge.SourceKey == observation.Key && edge.EventId == scoped.Event.Id && SameTravelRoute(edge.Route, route)));
    }

    private static bool CanProbeNavigation(AutoPlayEventCoverage scoped)
    {
        return scoped.Available && !scoped.Unavailable && !scoped.PolicyDeferred && scoped.BlockedReason is null;
    }

    private static IEnumerable<AutoPlayEventRoute> NavigationTravelRoutes(AutoPlayEvent observed, AutoPlayEventCoverage shared, Func<string, bool> visitExists, bool allowHistoricalConditions = false)
    {
        AutoPlayEventCoverage candidates = new()
        {
            Event = observed,
            Completed = shared.Completed,
            Routes = shared.Routes,
            UnavailableVariants = shared.UnavailableVariants,
            PolicyDeferredVariants = shared.PolicyDeferredVariants,
            AllowHistoricalTravelConditions = allowHistoricalConditions
        };
        return TravelRoutes(candidates).Where(route => visitExists(route.DestinationVisitKey));
    }

    private AutoPlayNavigationVisit FindNavigationVisit(string key)
    {
        return State.NavigationVisits.SingleOrDefault(visit => visit.Key == key) ?? throw new InvalidOperationException($"Unknown navigation visit '{key}'.");
    }
}
