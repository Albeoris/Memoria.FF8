namespace Memoria.FF8;

internal sealed record AutoPlayNavigationVisit(string Key, string VisitKey, IReadOnlyList<AutoPlayEvent> Events);
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

    public void RecordNavigationRoute(string sourceKey, string destinationKey, string eventId, AutoPlayEventRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        AutoPlayNavigationVisit source = FindNavigationVisit(sourceKey);
        AutoPlayNavigationVisit destination = FindNavigationVisit(destinationKey);
        AutoPlayEvent? observed = source.Events.SingleOrDefault(item => item.Id == eventId);
        AutoPlayEventCoverage aggregate = FindEvent(source.VisitKey, eventId);
        bool observedSource = observed is not null && observed.ConditionSignature == route.ConditionSignature && (route.TargetId is not string target || (observed.Variants is { Count: > 0 } variants ? variants : [observed.Id]).Contains(target, StringComparer.Ordinal));
        bool recordedSource = State.NavigationRoutes.Any(known => known.SourceKey == sourceKey && known.DestinationKey == destinationKey && known.EventId == eventId && SameTravelRoute(known.Route, route));
        if (!observedSource && !recordedSource || destination.VisitKey != route.DestinationVisitKey || !aggregate.Routes.Any(known => SameTravelRoute(known, route)))
        {
            throw new InvalidOperationException("A navigation edge requires its exact observed source and actual recorded aggregate route and destination.");
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

    public AutoPlayPlanStep? PlanNavigation(string currentNavigationKey, Func<AutoPlayVisit, bool>? needsRefresh = null, string? refreshNavigationKey = null, Func<AutoPlayNavigationVisit, bool>? isCurrent = null)
    {
        FindNavigationVisit(currentNavigationKey);
        AutoPlayPlanner shadow = CreateNavigationView(isCurrent);
        if (!shadow.State.Visits.Any(visit => visit.Visit.Key == currentNavigationKey))
        {
            return null;
        }
        // Genuine unconsumed events precede reobserving the topology of already completed travel.
        AutoPlayPlanStep? planned = shadow.Plan(currentNavigationKey, needsRefresh, refreshNavigationKey, (visit, item) => NeedsExecution(FindEvent(FindNavigationVisit(visit.Key).VisitKey, item.Id)) ? 0 : 1);
        if (planned is null)
        {
            return null;
        }
        AutoPlayNavigationVisit source = FindNavigationVisit(planned.VisitKey);
        AutoPlayEvent observed = source.Events.Single(item => item.Id == planned.Event.Id);
        if (planned.Route is null)
        {
            AutoPlayEventCoverage shared = FindEvent(source.VisitKey, observed.Id);
            AutoPlayEventRoute? probe = NeedsExecution(shared) ? null : UnconfirmedNavigationRoutes(source, shadow.FindEvent(source.Key, observed.Id), shadow.State.Visits.Select(visit => visit.Visit.Key).ToHashSet(StringComparer.Ordinal)).FirstOrDefault();
            if (probe is not null)
            {
                return planned with { VisitKey = source.VisitKey, Event = observed with { DestinationVisitKey = probe.DestinationVisitKey }, IsTravel = true, Route = probe };
            }
            return planned with { VisitKey = source.VisitKey, Event = observed };
        }
        AutoPlayNavigationVisit destination = FindNavigationVisit(planned.Route.DestinationVisitKey);
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

    private AutoPlayPlanner CreateNavigationView(Func<AutoPlayNavigationVisit, bool>? isCurrent)
    {
        AutoPlayPlannerState view = new();
        HashSet<string> valid = State.NavigationVisits.Where(visit => isCurrent?.Invoke(visit) != false).Select(visit => visit.Key).ToHashSet(StringComparer.Ordinal);
        foreach (AutoPlayNavigationVisit observation in State.NavigationVisits.Where(visit => valid.Contains(visit.Key)))
        {
            AutoPlayVisitCoverage aggregate = FindVisit(observation.VisitKey);
            AutoPlayVisitCoverage scoped = new() { Visit = new(observation.Key, aggregate.Visit.Region) };
            foreach (AutoPlayEvent observed in observation.Events)
            {
                AutoPlayEventCoverage shared = FindEvent(observation.VisitKey, observed.Id);
                AutoPlayEventCoverage local = new()
                {
                    Event = observed,
                    Available = observed.ConditionSignature == shared.Event.ConditionSignature,
                    Completed = shared.Completed,
                    BlockedReason = shared.BlockedReason,
                    Conditions = shared.Conditions,
                    ObservedStoryChanged = shared.ObservedStoryChanged,
                    ObservedConditionSignature = shared.ObservedConditionSignature,
                    Unavailable = shared.Unavailable,
                    UnavailableVariants = shared.UnavailableVariants,
                    AvailabilityFingerprint = shared.AvailabilityFingerprint,
                    CompletedAvailabilityFingerprints = shared.CompletedAvailabilityFingerprints,
                    PolicyDeferred = shared.PolicyDeferred,
                    PolicyDeferredVariants = shared.PolicyDeferredVariants,
                    RouteNeedsRediscovery = shared.RouteNeedsRediscovery
                };
                if (UnconfirmedNavigationRoutes(observation, local, valid).Any())
                {
                    local.RouteNeedsRediscovery = true;
                    if (!NeedsExecution(shared))
                    {
                        local.Event = observed with { ChangesStory = false };
                    }
                }
                scoped.Events.Add(local);
            }
            view.Visits.Add(scoped);
        }
        foreach (AutoPlayNavigationRoute edge in State.NavigationRoutes)
        {
            AutoPlayNavigationVisit? source = State.NavigationVisits.SingleOrDefault(visit => visit.Key == edge.SourceKey);
            AutoPlayNavigationVisit? destination = State.NavigationVisits.SingleOrDefault(visit => visit.Key == edge.DestinationKey);
            if (source is null || destination is null || !valid.Contains(source.Key) || !valid.Contains(destination.Key) || edge.Route.DestinationVisitKey != destination.VisitKey)
            {
                continue;
            }
            AutoPlayEventCoverage? scoped = view.Visits.Single(visit => visit.Visit.Key == edge.SourceKey).Events.SingleOrDefault(item => item.Event.Id == edge.EventId);
            AutoPlayEventCoverage shared = FindEvent(source.VisitKey, edge.EventId);
            if (scoped is null || !shared.Routes.Any(route => SameTravelRoute(route, edge.Route)) || !NavigationTravelRoutes(source, scoped).Any(route => SameTravelRoute(route, edge.Route)))
            {
                continue;
            }
            scoped.Routes.Add(edge.Route with { DestinationVisitKey = destination.Key });
        }
        return new(view, (visit, item) => phaseOrder(FindVisit(FindNavigationVisit(visit.Key).VisitKey).Visit, item), (visit, item) => progressionOrder(FindVisit(FindNavigationVisit(visit.Key).VisitKey).Visit, item), normalizeLegacyRoutes: false);
    }

    private IEnumerable<AutoPlayEventRoute> UnconfirmedNavigationRoutes(AutoPlayNavigationVisit observation, AutoPlayEventCoverage scoped, IReadOnlySet<string> valid)
    {
        if (!scoped.Available || scoped.Unavailable || scoped.PolicyDeferred || scoped.BlockedReason is not null)
        {
            return [];
        }
        return NavigationTravelRoutes(observation, scoped).Where(route => !State.NavigationRoutes.Any(edge => valid.Contains(edge.SourceKey) && valid.Contains(edge.DestinationKey) && edge.SourceKey == observation.Key && edge.EventId == scoped.Event.Id && SameTravelRoute(edge.Route, route)));
    }

    private IEnumerable<AutoPlayEventRoute> NavigationTravelRoutes(AutoPlayNavigationVisit observation, AutoPlayEventCoverage scoped)
    {
        AutoPlayEventCoverage shared = FindEvent(observation.VisitKey, scoped.Event.Id);
        AutoPlayEventCoverage candidates = new()
        {
            Event = observation.Events.Single(item => item.Id == scoped.Event.Id),
            Completed = shared.Completed,
            Routes = shared.Routes,
            UnavailableVariants = shared.UnavailableVariants,
            PolicyDeferredVariants = shared.PolicyDeferredVariants
        };
        return TravelRoutes(candidates).Where(route => State.Visits.Any(visit => visit.Visit.Key == route.DestinationVisitKey));
    }

    private AutoPlayNavigationVisit FindNavigationVisit(string key)
    {
        return State.NavigationVisits.SingleOrDefault(visit => visit.Key == key) ?? throw new InvalidOperationException($"Unknown navigation visit '{key}'.");
    }
}
