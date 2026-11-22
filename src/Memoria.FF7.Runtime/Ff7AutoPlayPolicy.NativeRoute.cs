using System.Security.Cryptography;
using System.Text;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7NativeSiblingCandidate(Ff7AutoPlayTarget Target, AutoPlayPlanStep Plan, string[] AvoidFirstEvents, Ff7NativeSourceRevalidation? Revalidation = null, bool Guarded = false);
internal sealed record Ff7NativePlanningHeader(ushort? Destination, Ff7AutoPlayActiveEvent? Active, AutoPlayTravelGoal? Goal, object? Route, object? Choice, object? Restore, Ff7AutoPlaySourceRevalidationTask? Revalidation, object Coverage, object Guards, object Visits, object Probes, object Topology, object Attempts, Ff7AutoPlayTravelCommitment? TravelCommitment);
internal sealed record Ff7NativePhysicalFailureFence(Ff7AutoPlayActiveEvent Owner, AutoPlayTravelGoal? Goal, string Source, int RunSpeed, int WalkSpeed)
{
    internal bool Matches(Ff7AutoPlayState state, string source, int runSpeed, int walkSpeed) => state.Active == Owner && !Owner.Accepted && state.NavigationGoal == Goal && Source == source && RunSpeed == runSpeed && WalkSpeed == walkSpeed;
}

public sealed partial class Ff7AutoPlayPolicy
{
    private sealed record NativeInputReservation(Ff7AutoPlayNativeRoute Before, Ff7AutoPlayNativeRoute Reserved, string? Status);
    private NativeInputReservation? nativeInputReservation;
    private Ff7NativePlanningCursor? nativePlanning;
    private Ff7NativePlanningQuantum? nativePlanningQuantum;
    private string? nativePlanningSource;
    private string? nativePlanningIntent;
    private Ff7NativePlanningHeader? nativePlanningHeader;
    private Dictionary<string, AutoPlayEventAvailability>? nativePlanningAvailability;
    private Ff7NativePhysicalFailureFence? nativePhysicalFailure;
    private string? nativePlanningPhase;
    private long nativePlanningWork;
    private bool nativePlanningForActive;
    private bool activeNativeTurn;
    private string? completedActiveNativeSource;
    private string? completedActiveNativeIntent;
    internal bool NativePlanningPending => nativePlanning is not null;
    internal long NativePlanningGeneration { get; private set; }
    internal string? NativePlanningCancellation { get; private set; }
    internal string? NativePlanningPublication { get; private set; }
    internal string? NativePlanningPhase => nativePlanningPhase;

    private void BeginActiveNativePlanning(Ff7AutoplayDriver driver)
    {
        if (nativePlanning is not null || State.Active is not { Accepted: false } active || State.PendingRestore is not null || State.NativeRoute is not null || session.Battle is not null || session.World is not null || !Ff7AutoplayDriver.HasNativeRouteClockContract(session))
        {
            return;
        }
        Ff7AutoPlayObservation observation = discovery.Observe();
        Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => MatchesActiveNativeTarget(active, observation, target));
        if (target is null || !Ff7AutoplayDriver.SupportsNativeRouteTarget(target) || !IsActiveNativeGoalPending(State, active, observation))
        {
            return;
        }
        string source = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        string intent = ReadNativePlanningIntent();
        if (source == completedActiveNativeSource && intent == completedActiveNativeIntent)
        {
            return;
        }
        NativePlanningGeneration++;
        nativePlanningSource = source;
        nativePlanningIntent = intent;
        nativePlanningHeader = ReadNativePlanningHeader(State);
        nativePlanningAvailability = ReadNativePlanningAvailability(State, observation.VisitId);
        nativePlanningForActive = true;
        activeNativeTurn = true;
        nativePlanning = new(EnumerateActiveNativeNavigation(driver, observation, active, target, source).GetEnumerator());
    }

    internal static bool MatchesActiveNativeTarget(Ff7AutoPlayActiveEvent active, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        return active is { Accepted: false } && active.VisitId == observation.VisitId && active.NavigationKey == observation.NavigationKey && active.Generation == observation.Generation && active.EventId == target.Id && active.ConditionFingerprint == target.ConditionFingerprint && active.Entity == target.Entity && active.Script == target.Script && active.Kind == target.Kind && active.DestinationField == target.DestinationField && target.AnalysisComplete;
    }

    internal static bool IsActiveNativeGoalPending(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, Ff7AutoPlayObservation observation)
    {
        return IsNativeContinuationGoalPending(state, state.NavigationGoal, observation.NavigationKey, active.EventId) || state.NavigationGoal is { EventId: null } goal && ReadPendingSourceRevalidations(state, observation).Any(debt => debt.NavigationKey == goal.NavigationKey);
    }

    internal static bool TakeNativePlanningTurn(bool activeScope, ref bool nativeTurn)
    {
        if (!activeScope)
        {
            return true;
        }
        bool resume = nativeTurn;
        nativeTurn = !nativeTurn;
        return resume;
    }

    internal static bool TryInstallActiveNativeRoute(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, AutoPlayTravelGoal? goal, Ff7AutoPlayObservation observation, Ff7AutoPlayNativeRoute proof, string source)
    {
        if (state.Active != active || state.NavigationGoal != goal || state.NativeRoute is not null || state.PendingRestore is not null || !observation.Events.Any(target => MatchesActiveNativeTarget(active, observation, target)) || !IsActiveNativeGoalPending(state, active, observation) || proof.EventId != active.EventId || proof.ConditionFingerprint != active.ConditionFingerprint || proof.Generation != active.Generation || proof.Cursor != 0 || proof.Steps.Length == 0 || proof.Steps[0].Before != source)
        {
            return false;
        }
        state.NativeRoute = proof;
        return true;
    }

    private IEnumerable<AutoPlayPlanStep?> EnumerateActiveNativeNavigation(Ff7AutoplayDriver driver, Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active, Ff7AutoPlayTarget target, string source)
    {
        AutoPlayTravelGoal? goal = State.NavigationGoal;
        Ff7NativeSiblingCandidate[] siblings = ReadNativeSiblingCandidates(State, planner, observation, NeedsVisitRefresh, IsPendingOwnedSourceGoal(goal, observation));
        bool installed = false;
        IEnumerable<int> Search(Ff7AutoPlayTarget candidate, Ff7NativeSiblingCandidate? sibling)
        {
            Ff7NativeRouteProbe probe = new() { ExpectedNavigationKey = ReadExpectedNativeTransportNavigation(State, planner, observation, active, sibling), SourceRelease = (branch, actual) => CanReleaseNativeSource(State, observation, candidate, branch, actual), NavigationPrerequisites = sibling is null ? ReadKnownNavigationPrerequisites(observation, candidate) : [] };
            foreach (int _ in Ff7AutoplayDriver.EnumerateFairNativeCandidates(driver.EnumerateNativeRouteModes(candidate, observation.Events, probe), () => probe.Result is not null))
            {
                nativePlanningPhase = probe.Progress;
                yield return 0;
            }
            bool authentic = CanResumeNativePlanning(validateIntent: true);
            if (probe.Result is { } observedProof)
            {
                NativePlanningPublication = $"{candidate.Id}:steps:{observedProof.Steps.Length}:sourceReleased:{observedProof.SourceReleased}:authenticated:{authentic}";
            }
            if (!authentic || State.Active != active || State.NavigationGoal != goal || probe.Result is not { } proof)
            {
                yield break;
            }
            if (sibling is null)
            {
                installed = proof.NavigationPrerequisite is null ? TryInstallActiveNativeRoute(State, active, goal, observation, proof, source) : TryInstallNavigationPrerequisite(active, goal, observation, proof, source);
            }
            else
            {
                var read = sibling.Guarded ? CreateNativeCandidateAvailabilityReader(State, observation, candidate) : CreateRetainedNavigationAvailabilityReader(State);
                bool sourceGoalPending = IsPendingOwnedSourceGoal(State.NavigationGoal, observation);
                AutoPlayPlanStep? fresh = ReselectNativeSiblingPlan(State, planner, observation, sibling, NeedsVisitRefresh, sourceGoalPending);
                AutoPlayNavigationVisit current = State.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
                AutoPlayEventAvailability availability = read(current, current.Events.Single(item => item.Id == candidate.Id)) ?? new(false, [], null);
                installed = TryAdoptSiblingNativeRoute(State, active, goal, observation, sibling, fresh, proof, source, availability, ReadRetainedNavigationGuardFacts(State, current), Story(), session.CaptureEventProgressWitness(), sourceGoalPending);
                if (installed)
                {
                    State.Status = $"{session.Field.Name}: {candidate.Id}";
                }
            }
            if (installed)
            {
                Publish();
            }
            NativePlanningPublication += $":installed:{installed}";
        }
        foreach (int _ in EnumerateNativeGoalFamilies(Search(target, null), siblings.Select(sibling => Search(sibling.Target, sibling)), () => installed))
        {
            yield return null;
        }
        if (!installed && CanResumeNativePlanning(validateIntent: true) && State.Active == active)
        {
            completedActiveNativeSource = nativePlanningSource;
            completedActiveNativeIntent = nativePlanningIntent;
        }
    }

    internal static IEnumerable<int> EnumerateNativeGoalFamilies(IEnumerable<int> current, IEnumerable<IEnumerable<int>> siblings, Func<bool> completed)
    {
        IEnumerable<int>[] alternatives = siblings.ToArray();
        IEnumerable<int>[] families = alternatives.Length == 0 ? [current] : [current, Ff7AutoplayDriver.EnumerateFairNativeCandidates(alternatives, completed)];
        return Ff7AutoplayDriver.EnumerateFairNativeCandidates(families, completed);
    }

    internal static Ff7NativeSiblingCandidate[] ReadNativeSiblingCandidates(Ff7AutoPlayState state, AutoPlayPlanner<Ff7AutoPlayDeferral> sharedPlanner, Ff7AutoPlayObservation observation, Func<AutoPlayVisit, bool>? needsRefresh = null, bool sourceGoalPending = false)
    {
        // An alternative first event cannot leave and revisit the current navigation node.
        if (OwnsLocalSourceGoal(state, observation, state.Active))
        {
            return [];
        }
        bool committedGoal = CanContinueOwnedTravel(state, observation, sourceGoalPending);
        var retained = CreateRetainedNavigationAvailabilityReader(state);
        List<Ff7NativeSiblingCandidate> candidates = [];
        Ff7NativeSourceRevalidation[] debts = state.DestinationField is null ? ReadPendingSourceRevalidations(state, observation) : [];
        void AddPlans(Func<AutoPlayNavigationVisit, AutoPlayEvent, AutoPlayEventAvailability?> read, Ff7NativeSourceRevalidation? debt, Ff7AutoPlayTarget? guarded)
        {
            HashSet<string> avoid = new(StringComparer.Ordinal);
            while (true)
            {
                string[] previous = avoid.ToArray();
                AutoPlayPlanStep? plan = debt is null
                    ? sharedPlanner.PlanNavigation(observation.NavigationKey, needsRefresh: needsRefresh, allowRemoteConditionRoutes: true, avoidFirstEvents: avoid, preferredGoal: state.NavigationGoal, readAvailability: read, strongPreferredGoal: committedGoal)
                    : SelectSourceRefreshPlan(state, sharedPlanner, observation, debt, needsRefresh, read, avoid);
                if (plan is null || !avoid.Add(plan.Event.Id))
                {
                    break;
                }
                Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(item => item.Id == plan.Event.Id && item.ConditionFingerprint == plan.Event.ConditionSignature && item.AnalysisComplete);
                bool sourceMatches = debt is null || plan.RefreshVisitKey == debt.NavigationKey && plan.Goal is { EventId: null } refreshGoal && refreshGoal.NavigationKey == debt.NavigationKey;
                bool meaningful = guarded is null || IsNativeContinuationGoalPending(state, plan.Goal, observation.NavigationKey, guarded.Id) || debt is not null && plan.Goal is { EventId: null } sourceGoal && sourceGoal.NavigationKey == debt.NavigationKey;
                if (target is not null && target.Id != state.Active?.EventId && MatchesCommittedNativeSibling(state, observation, plan, sourceGoalPending) && (guarded is null || target.Id == guarded.Id) && !candidates.Any(item => item.Target.Id == target.Id && item.Revalidation == debt && item.Guarded == (guarded is not null) && MatchesNativePlan(item.Plan, plan)) && Ff7AutoplayDriver.SupportsNativeRouteTarget(target) && IsAllowedAlternativePlan(state, plan, observation.NavigationKey) && meaningful && sourceMatches)
                {
                    candidates.Add(new(target, plan, previous, debt, guarded is not null));
                }
            }
        }
        AddPlans(retained, null, null);
        foreach (Ff7NativeSourceRevalidation debt in debts.DistinctBy(item => item.NavigationKey))
        {
            AddPlans(retained, debt, null);
        }
        foreach (Ff7AutoPlayTarget guarded in ReadNativeProbeCandidates(state, observation).Where(target => target.Id != state.Active?.EventId && !candidates.Any(item => item.Target.Id == target.Id)))
        {
            var read = CreateNativeCandidateAvailabilityReader(state, observation, guarded);
            AddPlans(read, null, guarded);
            foreach (Ff7NativeSourceRevalidation debt in debts.DistinctBy(item => item.NavigationKey))
            {
                AddPlans(read, debt, guarded);
            }
        }
        return candidates.ToArray();
    }

    internal static Func<AutoPlayNavigationVisit, AutoPlayEvent, AutoPlayEventAvailability?> CreateNativeCandidateAvailabilityReader(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        var retained = CreateRetainedNavigationAvailabilityReader(state);
        AutoPlayNavigationVisit current = state.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        Dictionary<string, string> revised = ReadRetainedNavigationGuardFacts(state, current);
        foreach (var entry in state.NavigationGuards.Where(entry => entry.Value.NavigationKey == current.Key && entry.Value.EventId == target.Id && entry.Value.MovementFailure is not null))
        {
            revised[entry.Key] = ReadNativeContinuationAvailability(revised[entry.Key]);
        }
        return (navigation, candidate) => navigation.Key == current.Key ? ReadComponentAvailability(state, navigation, candidate, revised) : retained(navigation, candidate);
    }

    private static bool IsMeaningfulGuardedSibling(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, AutoPlayPlanStep? plan, Ff7NativeSourceRevalidation? debt)
    {
        return plan is not null && (IsNativeContinuationGoalPending(state, plan.Goal, observation.NavigationKey, target.Id) || debt is not null && plan.Goal is { EventId: null } sourceGoal && sourceGoal.NavigationKey == debt.NavigationKey && ReadPendingSourceRevalidations(state, observation).Contains(debt));
    }

    internal static AutoPlayPlanStep? ReselectNativeSiblingPlan(Ff7AutoPlayState state, AutoPlayPlanner<Ff7AutoPlayDeferral> sharedPlanner, Ff7AutoPlayObservation observation, Ff7NativeSiblingCandidate sibling, Func<AutoPlayVisit, bool>? needsRefresh = null, bool sourceGoalPending = false)
    {
        var read = sibling.Guarded ? CreateNativeCandidateAvailabilityReader(state, observation, sibling.Target) : CreateRetainedNavigationAvailabilityReader(state);
        HashSet<string> avoid = sibling.AvoidFirstEvents.ToHashSet(StringComparer.Ordinal);
        AutoPlayPlanStep? plan = sibling.Revalidation is { } debt
            ? SelectSourceRefreshPlan(state, sharedPlanner, observation, debt, needsRefresh, read, avoid)
            : sharedPlanner.PlanNavigation(observation.NavigationKey, needsRefresh: needsRefresh, allowRemoteConditionRoutes: true, avoidFirstEvents: avoid, preferredGoal: state.NavigationGoal, readAvailability: read, strongPreferredGoal: CanContinueOwnedTravel(state, observation, sourceGoalPending));
        return MatchesCommittedNativeSibling(state, observation, plan, sourceGoalPending) ? plan : null;
    }

    internal static bool MatchesCommittedNativeSibling(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, AutoPlayPlanStep? plan, bool sourceGoalPending = false)
    {
        return !CanContinueOwnedTravel(state, observation, sourceGoalPending) || state.NavigationGoal is { } goal && MatchesOwnedTravelPlan(goal, plan, observation.NavigationKey);
    }

    internal static string? ReadExpectedNativeTransportNavigation(Ff7AutoPlayState state, AutoPlayPlanner<Ff7AutoPlayDeferral> sharedPlanner, Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active, Ff7NativeSiblingCandidate? sibling)
    {
        Ff7AutoPlayTargetKind kind = sibling?.Target.Kind ?? active.Kind;
        if (kind is < Ff7AutoPlayTargetKind.LineTalk or > Ff7AutoPlayTargetKind.LineLeave)
        {
            return null;
        }
        if (sibling is not null)
        {
            return sibling.Plan.IsTravel ? sibling.Plan.DestinationNavigationKey : null;
        }
        if (active.TravelRoute is not { } route)
        {
            return null;
        }
        var availability = CreateRetainedNavigationAvailabilityReader(state);
        AutoPlayPlanStep? fresh = sharedPlanner.PlanNavigation(observation.NavigationKey, allowRemoteConditionRoutes: true, preferredGoal: state.NavigationGoal, readAvailability: availability);
        if (fresh is { IsTravel: true, DestinationNavigationKey: not null } && fresh.Event.Id == active.EventId && fresh.Event.ConditionSignature == active.ConditionFingerprint && SameNativeTravelRoute(route, fresh.Route))
        {
            return fresh.DestinationNavigationKey;
        }
        string[] destinations = state.Coverage.NavigationRoutes.Where(item => item.SourceKey == observation.NavigationKey && item.EventId == active.EventId && SameNativeTravelRoute(route, item.Route)).Select(item => item.DestinationKey).Distinct(StringComparer.Ordinal).ToArray();
        return destinations.Length == 1 ? destinations[0] : null;
    }

    private static bool SameNativeTravelRoute(AutoPlayEventRoute expected, AutoPlayEventRoute? actual)
    {
        return actual is not null && expected.DestinationVisitKey == actual.DestinationVisitKey && expected.ConditionSignature == actual.ConditionSignature && expected.ObservedStoryChanged == actual.ObservedStoryChanged && expected.TargetId == actual.TargetId && (expected.Selections ?? []).SequenceEqual(actual.Selections ?? []);
    }

    internal static bool MatchesNativePlan(AutoPlayPlanStep expected, AutoPlayPlanStep? actual)
    {
        return actual is not null && expected.VisitKey == actual.VisitKey && expected.Event.Id == actual.Event.Id && expected.Event.ConditionSignature == actual.Event.ConditionSignature && expected.Event.DestinationVisitKey == actual.Event.DestinationVisitKey && expected.IsTravel == actual.IsTravel && expected.RefreshVisitKey == actual.RefreshVisitKey && expected.Goal == actual.Goal && expected.DestinationNavigationKey == actual.DestinationNavigationKey && (expected.Route is null ? actual.Route is null : SameNativeTravelRoute(expected.Route, actual.Route));
    }

    internal static bool TryAdoptSiblingNativeRoute(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent expected, AutoPlayTravelGoal? expectedGoal, Ff7AutoPlayObservation observation, Ff7NativeSiblingCandidate sibling, AutoPlayPlanStep? fresh, Ff7AutoPlayNativeRoute proof, string source, AutoPlayEventAvailability availability, IReadOnlyDictionary<string, string> facts, int story, string witness, bool sourceGoalPending = false)
    {
        Ff7AutoPlayTarget target = sibling.Target;
        if (!MatchesCommittedNativeSibling(state, observation, fresh, sourceGoalPending))
        {
            return false;
        }
        if (state.Active != expected || expected.Accepted || expected.VisitId != observation.VisitId || expected.NavigationKey != observation.NavigationKey || expected.Generation != observation.Generation || state.NavigationGoal != expectedGoal || state.NativeRoute is not null || state.PendingRestore is not null || target.Id == expected.EventId || !Ff7AutoplayDriver.SupportsNativeRouteTarget(target) || sibling.Revalidation is { } requiredDebt && !ReadPendingSourceRevalidations(state, observation).Contains(requiredDebt) || sibling.Guarded && !IsMeaningfulGuardedSibling(state, observation, sibling.Target, fresh, sibling.Revalidation) || !MatchesNativePlan(sibling.Plan, fresh) || !IsAllowedAlternativePlan(state, fresh, observation.NavigationKey) || availability.Unavailable || !observation.Events.Any(item => item.Id == target.Id && item.ConditionFingerprint == target.ConditionFingerprint && item.Entity == target.Entity && item.Script == target.Script && item.Kind == target.Kind && item.DestinationField == target.DestinationField) || proof.EventId != target.Id || proof.ConditionFingerprint != target.ConditionFingerprint || proof.Generation != observation.Generation || proof.Cursor != 0 || proof.Steps.Length == 0 || proof.Steps[0].Before != source)
        {
            return false;
        }
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> old = state.Coverage.Visits.Single(visit => visit.Visit.Key == expected.VisitId).Events.Single(item => item.Event.Id == expected.EventId);
        if (old.Conditions?.ActiveEntry == expected.ConditionFingerprint)
        {
            old.Conditions.ActiveEntry = null;
        }
        ApplyNativePlanningAvailability(state, observation.VisitId, target.Id, availability);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> selected = state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
        selected.Conditions ??= new();
        selected.Conditions.ActiveEntry = target.ConditionFingerprint;
        if (state.NativeRevalidation is { } pending && (fresh!.Goal?.EventId is not null || pending.NavigationKey != fresh.Goal?.NavigationKey))
        {
            state.NativeRevalidation = null;
        }
        RetainSourceRevalidationTask(state, fresh!, sibling.Revalidation);
        RetainDestinationGoal(state, fresh!, observation.NavigationKey, target.ConditionFingerprint);
        state.Active = CreateNativeActiveEvent(state, observation, target, fresh!, story, witness, facts, availability.Fingerprint);
        state.NativeRoute = proof;
        return true;
    }

    internal static Ff7AutoPlayActiveEvent CreateNativeActiveEvent(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, AutoPlayPlanStep plan, int story, string witness, IReadOnlyDictionary<string, string> facts, string? availability)
    {
        return new(observation.VisitId, target.Id, target.ConditionFingerprint, observation.Generation, target.Entity, target.Script, target.Kind, target.DestinationField, story, witness, NavigationKey: observation.NavigationKey, NavigationProbeKey: target.MayChangeNavigation ? NavigationProbeKey(observation.NavigationKey, target) : null, EntryAvailabilityFingerprint: availability, SpatialDestinations: target.SpatialDestinations, SpatialOwner: target.SpatialOwner, DirectEffectOwner: target.DirectEffectOwner, TravelRoute: plan.Route, TravelSelections: plan.Route is null ? null : [], EntryGuardFingerprints: facts.Where(entry => state.NavigationGuards.TryGetValue(entry.Key, out var guard) && guard.NavigationKey == observation.NavigationKey && guard.EventId == target.Id).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
    }

    private AutoPlayPlanStep? TryProveNativeNavigation(Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, Dictionary<string, string> facts, Dictionary<string, IReadOnlyDictionary<string, string>?> components, string? completedTask = null)
    {
        string source = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        string intent = ReadNativePlanningIntent();
        if (nativePlanning is not null && (source != nativePlanningSource || intent != nativePlanningIntent || !Ff7AutoplayDriver.HasNativeRouteClockContract(session)))
        {
            CancelNativePlanning();
        }
        if (nativePlanning is null)
        {
            NativePlanningGeneration++;
            nativePlanningSource = source;
            nativePlanningIntent = intent;
            nativePlanningHeader = ReadNativePlanningHeader(State);
            nativePlanningAvailability = ReadNativePlanningAvailability(State, observation.VisitId);
            nativePlanning = new(EnumerateNativeNavigation(observation, driver, facts, components, completedTask).GetEnumerator());
        }
        return ContinueNativePlanning(driver);
    }

    private AutoPlayPlanStep? ContinueNativePlanning(Ff7AutoplayDriver driver)
    {
        if (nativePlanning is null)
        {
            throw new InvalidOperationException("There is no pending native planning job.");
        }
        long before = nativePlanning.Operations;
        try
        {
            nativePlanning.Resume(nativePlanningQuantum ?? new());
        }
        catch
        {
            CancelNativePlanning();
            throw;
        }
        nativePlanningWork += nativePlanning.Operations - before;
        if (!nativePlanning.Completed)
        {
            driver.DeferPlanning();
            return null;
        }
        AutoPlayPlanStep? result = nativePlanning.Result;
        CancelNativePlanning();
        return result;
    }

    private string ReadNativePlanningIntent() => ReadNativePlanningIntent(State);

    internal static Ff7NativePlanningHeader ReadNativePlanningHeader(Ff7AutoPlayState state)
    {
        return new(state.DestinationField, state.Active, state.NavigationGoal, state.NativeRoute, state.Choice, state.PendingRestore, state.NativeRevalidation, state.Coverage, state.NavigationGuards, state.VisitGuards, state.NavigationProbes, state.GatewayTopology, state.SourceRevalidationAttempts, state.TravelCommitment);
    }

    private static Dictionary<string, AutoPlayEventAvailability> ReadNativePlanningAvailability(Ff7AutoPlayState state, string visitId)
    {
        return state.Coverage.Visits.Single(visit => visit.Visit.Key == visitId).Events.Where(item => item.Available).ToDictionary(item => item.Event.Id, item => new AutoPlayEventAvailability(item.Unavailable, new(item.UnavailableVariants, StringComparer.Ordinal), item.AvailabilityFingerprint), StringComparer.Ordinal);
    }

    private void ObserveNativePlanningAvailability(string eventId, AutoPlayEventAvailability actual)
    {
        if (nativePlanning is not null && nativePlanningAvailability is not null && (!nativePlanningAvailability.TryGetValue(eventId, out var expected) || expected.Unavailable != actual.Unavailable || expected.Fingerprint != actual.Fingerprint || !expected.UnavailableVariants.SetEquals(actual.UnavailableVariants)))
        {
            CancelNativePlanning();
        }
    }

    private bool CanWaitForNativeAlternatives()
    {
        return nativePlanningForActive && nativePlanning is not null && nativePhysicalFailure is { } failure && Ff7AutoplayDriver.HasNativeRouteClockContract(session) && failure.Matches(State, Ff7AutoplayDriver.ReadNativeRouteFingerprint(session), session.GetPlayerMovementSpeed(true), session.GetPlayerMovementSpeed(false));
    }

    internal static bool TryRetainFailedOrdinaryTarget(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent owner, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, IReadOnlyDictionary<string, string> facts)
    {
        if (state.Active != owner || !MatchesActiveNativeTarget(owner, observation, target) || !Ff7AutoplayDriver.SupportsNativeRouteTarget(target) || !IsActiveNativeGoalPending(state, owner, observation))
        {
            return false;
        }
        PreservePendingPhysicalOwner(state, owner, observation, target, facts);
        return true;
    }

    internal static void PreservePendingPhysicalOwner(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent owner, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, IReadOnlyDictionary<string, string> facts)
    {
        AutoPlayNavigationVisit source = state.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        AutoPlayEventAvailability availability = ReadComponentAvailability(state, source, source.Events.Single(item => item.Id == target.Id), facts);
        ApplyNativePlanningAvailability(state, observation.VisitId, target.Id, availability);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
        coverage.Conditions ??= new();
        coverage.Conditions.ActiveEntry = owner.ConditionFingerprint;
        state.Active = owner;
    }

    internal static string ReadNativePlanningIntent(Ff7AutoPlayState state)
    {
        byte[] bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { state.DestinationField, state.NavigationGoal, state.Active, state.NativeRoute, state.PendingRestore, state.Choice, state.Coverage, state.NavigationGuards, state.VisitGuards, state.NavigationProbes, state.GatewayTopology, state.SourceRevalidationAttempts, state.NativeRevalidation, state.TravelCommitment });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private bool CanResumeNativePlanning(bool validateIntent = false)
    {
        if (State.PendingRestore is not null || State.NativeRoute is not null || session.Battle is not null || session.World is not null || session.Highway is not null || !Ff7AutoplayDriver.HasNativeRouteClockContract(session) || !HasCurrentPersistentNavigationGoal())
        {
            return false;
        }
        try
        {
            return nativePlanningSource == Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) && nativePlanningHeader == ReadNativePlanningHeader(State) && (!validateIntent || nativePlanningIntent == ReadNativePlanningIntent());
        }
        catch
        {
            CancelNativePlanning();
            throw;
        }
    }

    private void CancelNativePlanning([System.Runtime.CompilerServices.CallerMemberName] string member = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        if (nativePlanning is not null)
        {
            NativePlanningCancellation = $"{member}:{line}";
        }
        nativePlanning?.Dispose();
        nativePlanning = null;
        nativePlanningSource = null;
        nativePlanningIntent = null;
        nativePlanningHeader = null;
        nativePlanningAvailability = null;
        nativePhysicalFailure = null;
        nativePlanningPhase = null;
        nativePlanningForActive = false;
        activeNativeTurn = false;
    }

    private IEnumerable<AutoPlayPlanStep?> EnumerateNativeNavigation(Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, Dictionary<string, string> facts, Dictionary<string, IReadOnlyDictionary<string, string>?> components, string? completedTask)
    {
        if (!Ff7AutoplayDriver.HasNativeRouteClockContract(session))
        {
            yield break;
        }
        Ff7AutoPlayTarget[] candidates = ReadNativeProbeCandidates(State, observation, completedTask);
        AutoPlayPlanStep? planned = null;
        IEnumerable<int> EnumerateTarget(Ff7AutoPlayTarget target)
        {
            Ff7NativeRouteProbe probe = new() { SourceRelease = (branch, actual) => CanReleaseNativeSource(State, observation, target, branch, actual) };
            IEnumerable<int>[] modes = driver.EnumerateNativeRouteModes(target, observation.Events, probe);
            foreach (int _ in Ff7AutoplayDriver.EnumerateFairNativeCandidates(modes, () => probe.Result is not null))
            {
                nativePlanningPhase = probe.Progress;
                yield return 0;
            }
            Ff7AutoPlayNativeRoute? proof = probe.Result;
            if (!CanResumeNativePlanning(validateIntent: true))
            {
                yield break;
            }
            CompleteSourceRevalidation(State, observation.NavigationKey, target.Id, target.ConditionFingerprint);
            Publish();
            nativePlanningIntent = ReadNativePlanningIntent();
            nativePlanningHeader = ReadNativePlanningHeader(State);
            nativePlanningAvailability = ReadNativePlanningAvailability(State, observation.VisitId);
            if (proof is null)
            {
                yield break;
            }
            // The original negative receipts remain intact. This availability
            // is a distinct, finite native continuation from the exact source.
            Dictionary<string, string> revised = new(facts, StringComparer.Ordinal);
            foreach (var entry in State.NavigationGuards.Where(entry => entry.Value.EventId == target.Id && entry.Value.NavigationKey == observation.NavigationKey && entry.Value.MovementFailure is not null))
            {
                revised[entry.Key] = ReadNativeContinuationAvailability(facts[entry.Key]);
            }
            components[observation.NavigationKey] = revised;
            AutoPlayPlanStep? next = planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, allowRemoteConditionRoutes: true, preferredGoal: State.NavigationGoal, readAvailability: (navigation, candidate) => ReadNavigationAvailability(navigation, candidate, observation, driver, components));
            bool IsPendingPlan(AutoPlayPlanStep? candidate) => candidate?.Event.Id == target.Id && IsDestinationPlan(State, candidate, observation.NavigationKey) && IsNativeContinuationGoalPending(State, candidate.Goal, observation.NavigationKey, target.Id);
            Ff7NativeSourceRevalidation[] revalidations = State.DestinationField is null ? ReadPendingSourceRevalidations(State, observation) : [];
            if (!IsPendingPlan(next) && revalidations.FirstOrDefault() is { } refresh)
            {
                next = planner.PlanNavigation(observation.NavigationKey, needsRefresh: visit => revalidations.Any(item => item.NavigationKey == visit.Key) || NeedsVisitRefresh(visit), refreshNavigationKey: refresh.NavigationKey, allowRemoteConditionRoutes: true, preferredGoal: new(refresh.NavigationKey), readAvailability: (navigation, candidate) => ReadNavigationAvailability(navigation, candidate, observation, driver, components));
            }
            bool pendingSource = next?.Goal is { EventId: null } sourceGoal && revalidations.Any(item => item.NavigationKey == sourceGoal.NavigationKey);
            if (IsPendingPlan(next) || next?.Event.Id == target.Id && pendingSource && IsDestinationPlan(State, next, observation.NavigationKey))
            {
                AutoPlayNavigationVisit current = State.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
                AutoPlayEventAvailability availability = ReadComponentAvailability(State, current, current.Events.Single(item => item.Id == target.Id), revised);
                AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
                coverage.AvailabilityFingerprint = availability.Fingerprint;
                coverage.Unavailable = availability.Unavailable;
                coverage.UnavailableVariants = availability.UnavailableVariants;
                State.NativeRoute = proof;
                planned = next;
                yield break;
            }
            components[observation.NavigationKey] = facts;
        }
        IEnumerable<int>[] targets = PrioritizeNativeProbeCandidates(State, planner, observation, candidates, facts, NeedsVisitRefresh).Select(EnumerateTarget).ToArray();
        foreach (int _ in Ff7AutoplayDriver.EnumerateFairNativeCandidates(targets, () => planned is not null))
        {
            yield return null;
        }
        if (planned is not null)
        {
            yield return planned;
        }
    }

    internal static bool CanReleaseNativeSource(Ff7AutoPlayState state, Ff7AutoPlayObservation source, Ff7AutoPlayTarget target, Ff7Session candidate, Ff7AutoPlayObservation actual, bool authenticatedEntryReadiness = false)
    {
        if (!(actual.CanAcceptInput || authenticatedEntryReadiness && Ff7AutoPlayDiscovery.NativeControlReady(candidate)) || actual.VisitId != source.VisitId || actual.NavigationKey != source.NavigationKey || actual.Generation != source.Generation || !actual.Events.Any(item => item.Id == target.Id && item.ConditionFingerprint == target.ConditionFingerprint && item.Entity == target.Entity && item.Script == target.Script && item.Kind == target.Kind && item.DestinationField == target.DestinationField))
        {
            return false;
        }
        Ff7AutoplayDriver driver = new(candidate);
        driver.ConfigureSpatialBoundaries(actual.Events);
        Ff7Actor player = candidate.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Dictionary<string, string> facts = [];
        foreach (var entry in state.NavigationGuards.Where(entry => entry.Value.NavigationKey == source.NavigationKey && entry.Value.EventId == target.Id))
        {
            facts[entry.Key] = ReadMovementFailureFingerprint(driver.ReadPhysicalAvailabilityFingerprint(target, entry.Value.BlockingEntities), entry.Value.MovementFailure, candidate.Host.Process.Actors, entry.Value.BlockingEntities);
        }
        AutoPlayNavigationVisit navigation = state.Coverage.NavigationVisits.Single(visit => visit.Key == source.NavigationKey);
        if (ReadComponentAvailability(state, navigation, navigation.Events.Single(item => item.Id == target.Id && item.ConditionSignature == target.ConditionFingerprint), facts, (_, guard) => guard.MovementFailure is null ? null : ReadMovementSourceInside(guard.MovementFailure, player)).Unavailable)
        {
            return false;
        }
        driver.ConfigureSpatialBoundaries(actual.Events, target);
        try
        {
            Ff7AutoplayInput next = driver.ChooseTargetInput(target, actual.Events, run => target.Kind switch
            {
                Ff7AutoPlayTargetKind.ActorTalk => driver.TalkTo(target.Entity, run),
                Ff7AutoPlayTargetKind.Gateway => driver.GatewayAt(target.GatewayIndex!.Value, run),
                >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave => driver.ApproachLine(target.Entity, target.Kind, run),
                _ => throw new NotSupportedException("This target has no native source-release movement contract.")
            });
            return !driver.PlanningPending && driver.PlannedActionAdvances == true && (next.Horizontal != 0 || next.Vertical != 0);
        }
        catch (Ff7AutoPlayNavigationUnavailableException)
        {
            return false;
        }
    }

    internal static Ff7AutoPlayTarget[] ReadNativeProbeCandidates(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, string? completedTask = null)
    {
        return [];

    }

    internal static Ff7AutoPlayTarget[] PrioritizeNativeProbeCandidates(Ff7AutoPlayState state, AutoPlayPlanner<Ff7AutoPlayDeferral> sharedPlanner, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget[] candidates, IReadOnlyDictionary<string, string> actualFacts, Func<AutoPlayVisit, bool> needsRefresh)
    {
        Ff7NativeSourceRevalidation[] debts = state.DestinationField is null ? ReadPendingSourceRevalidations(state, observation) : [];
        var retained = CreateRetainedNavigationAvailabilityReader(state);
        bool IsSelectedTravel(Ff7AutoPlayTarget target)
        {
            Dictionary<string, string> hypothetical = new(actualFacts, StringComparer.Ordinal);
            foreach (var entry in state.NavigationGuards.Where(entry => entry.Value.NavigationKey == observation.NavigationKey && entry.Value.EventId == target.Id && entry.Value.MovementFailure is not null))
            {
                hypothetical[entry.Key] = ReadNativeContinuationAvailability(actualFacts[entry.Key]);
            }
            AutoPlayEventAvailability? Read(AutoPlayNavigationVisit navigation, AutoPlayEvent native) => navigation.Key == observation.NavigationKey ? ReadComponentAvailability(state, navigation, native, hypothetical) : retained(navigation, native);
            bool IsMeaningful(AutoPlayPlanStep? step) => step is { IsTravel: true } && step.Event.Id == target.Id && IsDestinationPlan(state, step, observation.NavigationKey) && IsNativeContinuationGoalPending(state, step.Goal, observation.NavigationKey, target.Id);
            AutoPlayPlanStep? next = sharedPlanner.PlanNavigation(observation.NavigationKey, needsRefresh: needsRefresh, allowRemoteConditionRoutes: true, preferredGoal: state.NavigationGoal, readAvailability: Read);
            if (IsMeaningful(next))
            {
                return true;
            }
            if (debts.FirstOrDefault() is not { } debt)
            {
                return false;
            }
            next = sharedPlanner.PlanNavigation(observation.NavigationKey, needsRefresh: visit => debts.Any(item => item.NavigationKey == visit.Key) || needsRefresh(visit), refreshNavigationKey: debt.NavigationKey, allowRemoteConditionRoutes: true, preferredGoal: new(debt.NavigationKey), readAvailability: Read);
            return IsMeaningful(next) || next is { IsTravel: true, Goal.EventId: null } && next.Event.Id == target.Id && debts.Any(item => item.NavigationKey == next.Goal.NavigationKey) && IsDestinationPlan(state, next, observation.NavigationKey);
        }
        return candidates.OrderBy(target => IsSelectedTravel(target) ? 0 : 1).ToArray();
    }

    internal static void ApplyNativePlanningAvailability(Ff7AutoPlayState state, string visitId, string eventId, AutoPlayEventAvailability availability)
    {
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == visitId).Events.Single(item => item.Event.Id == eventId);
        coverage.AvailabilityFingerprint = availability.Fingerprint;
        coverage.Unavailable = availability.Unavailable;
        coverage.UnavailableVariants = new(availability.UnavailableVariants, StringComparer.Ordinal);
    }

    internal static string ReadNativeContinuationAvailability(string physical)
    {
        // Native-state hashes authenticate a particular execution. Consumption
        // uses this stable capability identity, independent of ticks/requests.
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{physical}|native-continuation:2")));
    }

    internal static bool IsNativeContinuationGoalPending(Ff7AutoPlayState state, AutoPlayTravelGoal? goal, string currentNavigationKey, string repairedEvent)
    {
        if (goal is null)
        {
            AutoPlayEvent? local = state.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == currentNavigationKey)?.Events.SingleOrDefault(item => item.Id == repairedEvent);
            if (local is not null)
            {
                goal = new(currentNavigationKey, local.Id, local.ConditionSignature);
            }
        }
        AutoPlayNavigationVisit? source = state.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == goal?.NavigationKey);
        AutoPlayEvent? observed = source?.Events.SingleOrDefault(item => item.Id == goal?.EventId && item.ConditionSignature == goal.Conditions);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = source is null || observed is null ? null : state.Coverage.Visits.Single(visit => visit.Visit.Key == source.VisitKey).Events.Single(item => item.Event.Id == observed.Id);
        if (coverage is null || observed is null || source is null)
        {
            return false;
        }
        if (observed.ConditionSignature is string condition ? coverage.Conditions?.HasExecutedEntry(condition) != true : !coverage.Completed)
        {
            return true;
        }
        if (source.RouteRediscovery.GetValueOrDefault(observed.Id) || coverage.RouteNeedsRediscovery || coverage.GlobalRouteRediscovery)
        {
            return true;
        }
        if (source.Key == currentNavigationKey && observed.Id == repairedEvent)
        {
            return false;
        }
        AutoPlayEventAvailability? availability = ReadRetainedNavigationAvailability(state, source, observed);
        return availability is { Unavailable: false, Fingerprint: string fingerprint } && !coverage.CompletedAvailabilityFingerprints.Contains(fingerprint);
    }

    private Ff7AutoplayInput ReadNativeRouteInput()
    {
        Ff7AutoPlayNativeRoute proof = State.NativeRoute ?? throw new InvalidOperationException("There is no owned native continuation.");
        if (proof.Cursor == proof.Steps.Length)
        {
            ObserveNativeRouteBoundary();
            return default;
        }
        if (!Ff7AutoplayDriver.HasNativeRouteClockContract(session) || proof.Generation != session.FieldGeneration || State.Active is not { } active || active.EventId != proof.EventId || active.ConditionFingerprint != proof.ConditionFingerprint || proof.Cursor >= proof.Steps.Length || proof.Steps[proof.Cursor].Before != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session))
        {
            State.NativeRoute = null;
            Publish();
            throw new NotSupportedException("The bounded native navigation continuation diverged from its authenticated source.");
        }
        Ff7AutoplayInput input = proof.Steps[proof.Cursor].Input;
        Ff7AutoPlayNativeRoute reserved = proof with { Cursor = proof.Cursor + 1 };
        nativeInputReservation = new(proof, reserved, State.Status);
        State.NativeRoute = reserved;
        State.Status = $"Following native continuation toward {proof.EventId}, callback {proof.Cursor + 1}/{proof.Steps.Length}";
        Publish();
        return input;
    }

    private void RestoreUnappliedNativeInput()
    {
        if (nativeInputReservation is not { } reservation || !ReferenceEquals(State.NativeRoute, reservation.Reserved) || reservation.Before.Steps[reservation.Before.Cursor].Before != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session))
        {
            return;
        }
        State.NativeRoute = reservation.Before;
        State.Status = reservation.Status;
        Publish();
    }

    private void ObserveNativeFieldDeparture(Ff7NativeGatewayDeparture? departure)
    {
        if (State.NativeRoute?.GatewayDeparture is null)
        {
            return;
        }
        bool accepted = departure is not null && session.HasNativeGatewayDeparture(departure);
        AuthenticateNativeGatewayDeparture(State, departure, accepted, accepted ? Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) : null, Publish);
    }

    internal static void AuthenticateNativeGatewayDeparture(Ff7AutoPlayState state, Ff7NativeGatewayDeparture? departure, bool nativeAccepted, string? sourceFingerprint, Action publish)
    {
        if (state.NativeRoute is not { GatewayDeparture: { } expected } proof)
        {
            return;
        }
        bool matches = nativeAccepted && expected == departure && proof.Generation == departure?.Generation && proof.Cursor == proof.Steps.Length && !proof.BattleBoundary && proof.After == sourceFingerprint;
        state.NativeRoute = null;
        publish();
        if (!matches)
        {
            throw new NotSupportedException("The bounded native gateway continuation reached a different or unowned source departure.");
        }
    }

    private void ObserveNativeRouteBoundary()
    {
        if (State.NativeRoute is not { } proof)
        {
            return;
        }
        Ff7AutoPlayObservation? releaseObservation = proof.SourceReleased ? discovery.Observe() : null;
        if (proof.Cursor != proof.Steps.Length)
        {
            return;
        }
        string actualFingerprint = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        bool released = true;
        if (proof.SourceReleased)
        {
            Ff7AutoPlayObservation actual = releaseObservation!;
            Ff7AutoPlayTarget? target = actual.Events.SingleOrDefault(item => item.Id == proof.EventId && item.ConditionFingerprint == proof.ConditionFingerprint);
            released = State.Active is { } active && target is not null && active.VisitId == actual.VisitId && active.NavigationKey == actual.NavigationKey && active.Generation == proof.Generation && active.Generation == actual.Generation && active.EventId == proof.EventId && active.ConditionFingerprint == proof.ConditionFingerprint && active.Entity == target.Entity && active.Script == target.Script && active.Kind == target.Kind && active.DestinationField == target.DestinationField && CanReleaseNativeSource(State, actual, target, session, actual, proof.After == actualFingerprint);
        }
        if (!released || proof.After != actualFingerprint || proof.BattleBoundary != (session.Battle is not null) || proof.GatewayDeparture is { } gateway && !session.HasNativeGatewayDeparture(gateway))
        {
            State.NativeRoute = null;
            Publish();
            throw new NotSupportedException("The bounded native navigation continuation reached a different native boundary.");
        }
        State.NativeRoute = null;
        Publish();
    }
}
