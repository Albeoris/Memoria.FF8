using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private string? ordinaryAlternativeSource;
    private string? ordinaryAlternativeIntent;
    private Ff7AutoPlayActiveEvent? ordinaryAlternativeOwner;
    private AutoPlayTravelGoal? ordinaryAlternativeGoal;
    private readonly Dictionary<string, Ff7AutoplayDriver> ordinaryAlternativeDrivers = new(StringComparer.Ordinal);
    private readonly HashSet<string> ordinaryAlternativeFailures = new(StringComparer.Ordinal);
    private string? ordinaryNoReadySource;
    private Ff7NativePlanningHeader? ordinaryNoReadyHeader;
    private long ordinaryNoReadyGeneration;
    private readonly Dictionary<(string Navigation, string Event), (string Condition, string Physical)> ordinaryDeferredGoals = [];

    private void RecordOrdinaryGoalDeferral(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, Ff7AutoplayDriver driver)
    {
        ordinaryDeferredGoals[(observation.NavigationKey, target.Id)] = (target.ConditionFingerprint, driver.ReadPhysicalAvailabilityFingerprint(target));
    }

    private Func<AutoPlayNavigationVisit, AutoPlayEvent, AutoPlayEventAvailability?> CreateOrdinarySchedulingAvailability(Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, Func<AutoPlayNavigationVisit, AutoPlayEvent, AutoPlayEventAvailability?> underlying)
    {
        foreach (Ff7AutoPlayTarget target in observation.Events)
        {
            var key = (observation.NavigationKey, target.Id);
            if (ordinaryDeferredGoals.TryGetValue(key, out var deferred) && (deferred.Condition != target.ConditionFingerprint || deferred.Physical != driver.ReadPhysicalAvailabilityFingerprint(target)))
            {
                ordinaryDeferredGoals.Remove(key);
            }
        }
        return (navigation, target) =>
        {
            AutoPlayEventAvailability? actual = underlying(navigation, target);
            bool deferredGoal = ordinaryDeferredGoals.TryGetValue((navigation.Key, target.Id), out var deferred) && deferred.Condition == target.ConditionSignature;
            return ReadOrdinarySchedulingAvailability(actual, target.Id, deferredGoal);
        };
    }

    internal static AutoPlayEventAvailability? ReadOrdinarySchedulingAvailability(AutoPlayEventAvailability? actual, string eventId, bool deferred)
    {
        return actual?.Unavailable == true || !deferred ? actual : new(true, new HashSet<string>(StringComparer.Ordinal) { eventId }, actual?.Fingerprint);
    }

    private bool TryChooseOrdinaryAlternative(Ff7AutoplayDriver liveDriver, out Ff7AutoplayInput input)
    {
        input = default;
        if (ordinaryObservation is not { } observation || ordinaryPhysicalGuards is not { } facts || State.Active is not { Accepted: false } expected || State.NativeRoute is not null || State.PendingRestore is not null || session.Battle is not null || session.World is not null || expected.VisitId != observation.VisitId || expected.NavigationKey != observation.NavigationKey || expected.Generation != observation.Generation)
        {
            return false;
        }
        string source = Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(session.CaptureAutoPlayNavigation(), "ordinary-alternatives:1");
        string schedulingSource = Ff7AutoplayDriver.HasNativeRouteClockContract(session) ? Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) : source;
        // This cache only skips a scheduling scan; every input and proof still needs its full publication fences.
        if (ordinaryNoReadySource == schedulingSource && ordinaryNoReadyHeader == ReadNativePlanningHeader(State) && ordinaryNoReadyGeneration == NativePlanningGeneration)
        {
            return false;
        }
        string intent = ReadNativePlanningIntent();
        AutoPlayTravelGoal? expectedGoal = State.NavigationGoal;
        if (ordinaryAlternativeSource != schedulingSource || ordinaryAlternativeIntent != intent || ordinaryAlternativeOwner != expected || ordinaryAlternativeGoal != expectedGoal)
        {
            ordinaryAlternativeSource = schedulingSource;
            ordinaryAlternativeIntent = intent;
            ordinaryAlternativeOwner = expected;
            ordinaryAlternativeGoal = expectedGoal;
            ordinaryAlternativeDrivers.Clear();
            ordinaryAlternativeFailures.Clear();
        }
        var baseAvailability = CreateOrdinarySchedulingAvailability(observation, liveDriver, CreateRetainedNavigationAvailabilityReader(State));
        bool localRepair = observation.Events.SingleOrDefault(item => item.Id == expected.EventId) is { } selected && liveDriver.HasNativeGeometricContact(selected);
        bool unknownContactSearch = OwnsUnknownTalkContactSearch(expected, unknownTalkContactSearchOwner);
        bool equivalentTransportScope = expected.TravelRoute is not null && expectedGoal is not null && observation.Events.SingleOrDefault(item => item.Id == expected.EventId && item.ConditionFingerprint == expected.ConditionFingerprint)?.EffectFingerprint is string effect && Ff7AutoPlayDiscovery.TryReadDirectTransitionEffect(effect, out _);
        bool Adopt(Ff7AutoPlayTarget target, AutoPlayPlanStep plan, string[] preceding, Ff7AutoplayDriver candidate, Func<AutoPlayNavigationVisit, AutoPlayEvent, AutoPlayEventAvailability?> availability, bool ready)
        {
            AutoPlayPlanStep? fresh = planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, allowRemoteConditionRoutes: true, avoidFirstEvents: preceding.ToHashSet(StringComparer.Ordinal), preferredGoal: expectedGoal, readAvailability: availability);
            AutoPlayNavigationVisit current = State.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
            AutoPlayEventAvailability selectedAvailability = availability(current, current.Events.Single(item => item.Id == target.Id)) ?? new(false, [], null);
            bool touching = target.Kind is Ff7AutoPlayTargetKind.LineLeave or Ff7AutoPlayTargetKind.LineEnter && session.Host.Process.Lines.Single(line => line.Entity == target.Entity).Touch;
            int phase = target.Kind == Ff7AutoPlayTargetKind.LineLeave && touching || target.Kind == Ff7AutoPlayTargetKind.LineEnter && !touching ? 1 : 0;
            if (intent != ReadNativePlanningIntent() || source != Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(session.CaptureAutoPlayNavigation(), "ordinary-alternatives:1") || !TryAdoptOrdinaryAlternative(State, expected, expectedGoal, observation, target, plan, fresh, selectedAvailability, facts, Story(), session.CaptureEventProgressWitness(), phase))
            {
                return false;
            }
            CancelNativePlanning();
            liveDriver.AdoptPreparedOrdinaryNavigation(candidate);
            if (ready)
            {
                liveDriver.BeginPlanningStep();
            }
            else
            {
                liveDriver.DeferPlanning();
            }
            ordinaryAlternativeDrivers.Clear();
            ordinaryAlternativeFailures.Clear();
            ordinaryAlternativeSource = null;
            State.Status = $"{session.Field.Name}: {target.Id}";
            Publish();
            return true;
        }
        foreach (bool local in ReadOrdinaryAlternativePasses(localRepair, expected, unknownTalkContactSearchOwner))
        {
            Func<AutoPlayNavigationVisit, AutoPlayEvent, AutoPlayEventAvailability?> availability = (navigation, target) => ReadOrdinarySchedulingAvailability(baseAvailability(navigation, target), target.Id, local && !equivalentTransportScope && navigation.Key != observation.NavigationKey);
            (Ff7AutoPlayTarget Target, AutoPlayPlanStep Plan, string[] Preceding, Ff7AutoplayDriver Driver)? pending = null;
            bool pendingLocalWork = false;
            HashSet<string> avoid = new(StringComparer.Ordinal) { expected.EventId };
            while (true)
            {
                string[] preceding = avoid.ToArray();
                AutoPlayPlanStep? plan = planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, allowRemoteConditionRoutes: true, avoidFirstEvents: avoid, preferredGoal: expectedGoal, readAvailability: availability);
                if (plan is null || !avoid.Add(plan.Event.Id))
                {
                    break;
                }
                Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(item => item.Id == plan.Event.Id && item.ConditionFingerprint == plan.Event.ConditionSignature && item.AnalysisComplete);
                bool equivalentTransport = target is not null && IsCertifiedEquivalentTransport(State, expected, expectedGoal, observation, target, plan);
                if (target is null || !IsDestinationPlan(State, plan, observation.NavigationKey) || local && plan.IsTravel && !equivalentTransport || ordinaryAlternativeFailures.Contains(target.Id))
                {
                    continue;
                }
                if (!ordinaryAlternativeDrivers.TryGetValue(target.Id, out Ff7AutoplayDriver? candidate))
                {
                    candidate = new(session);
                    ordinaryAlternativeDrivers.Add(target.Id, candidate);
                }
                candidate.BeginPlanningStep();
                candidate.ConfigureSpatialBoundaries(observation.Events, target);
                candidate.SetPhysicalFailureReceipts(State.NavigationGuards.Where(entry => entry.Value.NavigationKey == observation.NavigationKey && IsUncertifiedMovementAdvice(entry.Key, entry.Value)).Select(entry => entry.Value.PhysicalFailure).OfType<Ff7AutoPlayPhysicalFailureReceipt>());
                bool repair = local && candidate.HasNativeGeometricContact(target);
                Ff7AutoplayInput prepared;
                long work = candidate.PhysicalPlanningWork;
                try
                {
                    prepared = candidate.ChooseTargetInput(target, observation.Events, run => PrepareEntryInput(target, ChooseFreshOrdinaryAlternativeInput(candidate, target, run)));
                }
                catch (Ff7AutoPlayNavigationUnavailableException)
                {
                    ordinaryAlternativeFailures.Add(target.Id);
                    if (repair && !equivalentTransport)
                    {
                        pending ??= (target, plan, preceding, candidate);
                    }
                    else if (!equivalentTransport)
                    {
                        RecordOrdinaryGoalDeferral(observation, target, candidate);
                    }
                    continue;
                }
                finally
                {
                    liveDriver.RecordOrdinaryAlternativeWork(candidate.PhysicalPlanningWork - work);
                }
                if (candidate.PlanningPending || prepared.Horizontal == 0 && prepared.Vertical == 0 && prepared.Held == 0 && !prepared.Talk || equivalentTransport && candidate.PlannedActionAdvances != true)
                {
                    pendingLocalWork |= local && candidate.PlanningPending;
                    if (repair && !equivalentTransport)
                    {
                        pending ??= (target, plan, preceding, candidate);
                    }
                    continue;
                }
                if (!Adopt(target, plan, preceding, candidate, availability, true))
                {
                    return false;
                }
                input = prepared;
                return true;
            }
            if (local && !localRepair && pending is { } reserved)
            {
                Adopt(reserved.Target, reserved.Plan, reserved.Preceding, reserved.Driver, availability, false);
                return false;
            }
            if (local && localRepair && !unknownContactSearch)
            {
                if (!pendingLocalWork)
                {
                    ordinaryNoReadySource = schedulingSource;
                    ordinaryNoReadyHeader = ReadNativePlanningHeader(State);
                    ordinaryNoReadyGeneration = NativePlanningGeneration;
                }
                return false;
            }
        }
        return false;
    }

    internal static IEnumerable<bool> ReadOrdinaryAlternativePasses(bool localRepair, Ff7AutoPlayActiveEvent owner, Ff7AutoPlayActiveEvent? unknownContactSearchOwner)
    {
        yield return true;
        if (!localRepair || OwnsUnknownTalkContactSearch(owner, unknownContactSearchOwner))
        {
            yield return false;
        }
    }

    internal static bool IsCertifiedEquivalentTransport(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent expected, AutoPlayTravelGoal? goal, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, AutoPlayPlanStep plan)
    {
        if (state.Active != expected || state.NavigationGoal != goal || expected.Accepted || expected.TravelRoute is not { } originalRoute || goal is null || plan.Goal != goal || !plan.IsTravel || plan.Route is not { } route || state.NativeRevalidation is not null || state.Choice is not null || expected.VisitId != observation.VisitId || expected.Generation != observation.Generation || expected.NavigationKey != observation.NavigationKey || target.Id == expected.EventId || target.ConditionFingerprint != plan.Event.ConditionSignature || target.Id != plan.Event.Id)
        {
            return false;
        }
        Ff7AutoPlayTarget? original = observation.Events.SingleOrDefault(item => item.Id == expected.EventId && item.ConditionFingerprint == expected.ConditionFingerprint);
        if (original?.EffectFingerprint is not string fingerprint || target.EffectFingerprint != fingerprint || !Ff7AutoPlayDiscovery.TryReadDirectTransitionEffect(fingerprint, out int destination))
        {
            return false;
        }
        bool OwnsCurrentRoute(Ff7AutoPlayTarget item, AutoPlayEventRoute candidate)
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(known => known.Event.Id == item.Id);
            return candidate.DestinationVisitKey.StartsWith($"ff7:{destination}:", StringComparison.Ordinal) && candidate.ConditionSignature == item.ConditionFingerprint && candidate.TargetId == item.Id && candidate.ObservedStoryChanged == false && (candidate.Selections is null || candidate.Selections.Count == 0) && coverage is { Completed: true } && coverage.Conditions?.HasExecutedEntry(item.ConditionFingerprint) == true && coverage.Routes.Any(known => known.DestinationVisitKey == candidate.DestinationVisitKey && known.ConditionSignature == candidate.ConditionSignature && known.TargetId == candidate.TargetId && known.ObservedStoryChanged == false && (known.Selections is null || known.Selections.Count == 0));
        }
        return originalRoute.DestinationVisitKey == route.DestinationVisitKey && OwnsCurrentRoute(original, originalRoute) && OwnsCurrentRoute(target, route);
    }

    private Ff7AutoplayInput ChooseFreshOrdinaryAlternativeInput(Ff7AutoplayDriver driver, Ff7AutoPlayTarget target, bool run)
    {
        if (target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave)
        {
            bool touching = session.Host.Process.Lines.Single(line => line.Entity == target.Entity).Touch;
            return target.Kind switch
            {
                Ff7AutoPlayTargetKind.LineTalk => driver.TalkToLine(target.Entity, run),
                Ff7AutoPlayTargetKind.LineLeave => touching ? driver.LeaveLine(target.Entity, run) : driver.ApproachLine(target.Entity, Ff7AutoPlayTargetKind.LineTouch, run),
                Ff7AutoPlayTargetKind.LineEnter when touching => driver.LeaveLine(target.Entity, run),
                _ => driver.ApproachLine(target.Entity, target.Kind, run)
            };
        }
        return ChooseOrdinaryTargetInput(driver, target, run);
    }

    internal static bool TryAdoptOrdinaryAlternative(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent expected, AutoPlayTravelGoal? expectedGoal, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, AutoPlayPlanStep plan, AutoPlayPlanStep? fresh, AutoPlayEventAvailability availability, IReadOnlyDictionary<string, string> facts, int story, string witness, int linePhase = 0)
    {
        if (state.Active != expected || expected.Accepted || expected.VisitId != observation.VisitId || expected.NavigationKey != observation.NavigationKey || expected.Generation != observation.Generation || state.NavigationGoal != expectedGoal || state.NativeRoute is not null || state.PendingRestore is not null || target.Id == expected.EventId || plan.Event.Id != target.Id || plan.Event.ConditionSignature != target.ConditionFingerprint || !MatchesNativePlan(plan, fresh) || !IsDestinationPlan(state, fresh, observation.NavigationKey) || availability.Unavailable || !observation.Events.Any(item => item == target && item.AnalysisComplete))
        {
            return false;
        }
        var old = state.Coverage.Visits.Single(visit => visit.Visit.Key == expected.VisitId).Events.Single(item => item.Event.Id == expected.EventId);
        if (old.Conditions?.ActiveEntry == expected.ConditionFingerprint)
        {
            old.Conditions.ActiveEntry = null;
        }
        ApplyNativePlanningAvailability(state, observation.VisitId, target.Id, availability);
        var selected = state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
        selected.Conditions ??= new();
        selected.Conditions.ActiveEntry = target.ConditionFingerprint;
        state.NativeRevalidation = null;
        RetainDestinationGoal(state, fresh!, observation.NavigationKey, target.ConditionFingerprint);
        state.Active = CreateNativeActiveEvent(state, observation, target, fresh!, story, witness, facts, availability.Fingerprint) with { LinePhase = linePhase };
        return true;
    }
}

public sealed partial class Ff7AutoplayDriver
{
    internal bool HasNativeGeometricContact(Ff7AutoPlayTarget target)
    {
        if (!SupportsNativeRouteTarget(target))
        {
            return false;
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        return target.Kind == Ff7AutoPlayTargetKind.ActorTalk ? DiscoverTalkContacts(session.Host.GetActor(target.Entity), player).Contacts.Count != 0 : ReadNativeTargetContacts(session, player, target).Count != 0;
    }

    internal void RecordOrdinaryAlternativeWork(long expanded)
    {
        PhysicalPlanningWork += expanded;
    }

    internal void AdoptPreparedOrdinaryNavigation(Ff7AutoplayDriver prepared)
    {
        CancelObstacleSearch();
        RestoreNavigationMode(prepared.CaptureNavigationMode());
        spatialBoundaryGeneration = prepared.spatialBoundaryGeneration;
        spatialBoundaryTriangles = prepared.spatialBoundaryTriangles.ToHashSet();
        admittedSpatialTriangle = prepared.admittedSpatialTriangle;
        talkRegionCacheKey = prepared.talkRegionCacheKey;
        talkRegionCache = prepared.talkRegionCache;
        retainedTalkRegionGoal = prepared.retainedTalkRegionGoal;
    }
}
