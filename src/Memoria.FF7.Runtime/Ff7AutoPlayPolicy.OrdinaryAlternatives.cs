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
        string intent = ReadNativePlanningIntent();
        AutoPlayTravelGoal? expectedGoal = State.NavigationGoal;
        if (ordinaryAlternativeSource != source || ordinaryAlternativeIntent != intent || ordinaryAlternativeOwner != expected || ordinaryAlternativeGoal != expectedGoal)
        {
            ordinaryAlternativeSource = source;
            ordinaryAlternativeIntent = intent;
            ordinaryAlternativeOwner = expected;
            ordinaryAlternativeGoal = expectedGoal;
            ordinaryAlternativeDrivers.Clear();
            ordinaryAlternativeFailures.Clear();
        }
        var availability = CreateOrdinarySchedulingAvailability(observation, liveDriver, CreateRetainedNavigationAvailabilityReader(State));
        HashSet<string> avoid = new(StringComparer.Ordinal) { expected.EventId };
        while (true)
        {
            string[] preceding = avoid.ToArray();
            AutoPlayPlanStep? plan = planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, allowRemoteConditionRoutes: true, avoidFirstEvents: avoid, preferredGoal: expectedGoal, readAvailability: availability);
            if (plan is null || !avoid.Add(plan.Event.Id))
            {
                return false;
            }
            Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(item => item.Id == plan.Event.Id && item.ConditionFingerprint == plan.Event.ConditionSignature && item.AnalysisComplete);
            if (target is null || !IsDestinationPlan(State, plan, observation.NavigationKey) || ordinaryAlternativeFailures.Contains(target.Id))
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
            Ff7AutoplayInput prepared;
            long work = candidate.PhysicalPlanningWork;
            try
            {
                prepared = candidate.ChooseTargetInput(target, observation.Events, run => PrepareEntryInput(target, ChooseFreshOrdinaryAlternativeInput(candidate, target, run)));
            }
            catch (Ff7AutoPlayNavigationUnavailableException)
            {
                ordinaryAlternativeFailures.Add(target.Id);
                RecordOrdinaryGoalDeferral(observation, target, candidate);
                continue;
            }
            finally
            {
                liveDriver.RecordOrdinaryAlternativeWork(candidate.PhysicalPlanningWork - work);
            }
            if (candidate.PlanningPending || prepared.Horizontal == 0 && prepared.Vertical == 0 && prepared.Held == 0 && !prepared.Talk)
            {
                continue;
            }
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
            liveDriver.BeginPlanningStep();
            ordinaryAlternativeDrivers.Clear();
            ordinaryAlternativeFailures.Clear();
            ordinaryAlternativeSource = null;
            State.Status = $"{session.Field.Name}: {target.Id}";
            Publish();
            input = prepared;
            return true;
        }
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
