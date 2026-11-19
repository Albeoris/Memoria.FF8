using System.Security.Cryptography;
using System.Text;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private sealed record NativeInputReservation(Ff7AutoPlayNativeRoute Before, Ff7AutoPlayNativeRoute Reserved, string? Status);
    private NativeInputReservation? nativeInputReservation;
    private Ff7NativePlanningCursor? nativePlanning;
    private Ff7NativePlanningQuantum? nativePlanningQuantum;
    private string? nativePlanningSource;
    private string? nativePlanningIntent;
    private string? nativePlanningPhase;
    private string? nativePlanningTaskEvent;
    private long nativePlanningWork;
    internal bool NativePlanningPending => nativePlanning is not null;
    internal long NativePlanningGeneration { get; private set; }
    internal string? NativePlanningPhase => nativePlanningPhase;

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
            nativePlanningTaskEvent = HasArrivedSourceRevalidation(State, observation) ? State.NativeRevalidation!.EventId : null;
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

    private string ReadNativePlanningIntent()
    {
        byte[] bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { State.DestinationField, State.NavigationGoal, State.Active, State.NativeRoute, State.PendingRestore, State.Choice, State.Coverage, State.NavigationGuards, State.VisitGuards, State.NavigationProbes, State.GatewayTopology, State.SourceRevalidationAttempts, State.NativeRevalidation });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private bool CanResumeNativePlanning()
    {
        if (State.PendingRestore is not null || State.NativeRoute is not null || session.Battle is not null || session.World is not null || session.Highway is not null || !Ff7AutoplayDriver.HasNativeRouteClockContract(session))
        {
            return false;
        }
        try
        {
            return nativePlanningSource == Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) && nativePlanningIntent == ReadNativePlanningIntent();
        }
        catch
        {
            CancelNativePlanning();
            throw;
        }
    }

    private void CancelNativePlanning()
    {
        nativePlanning?.Dispose();
        nativePlanning = null;
        nativePlanningSource = null;
        nativePlanningIntent = null;
        nativePlanningPhase = null;
        nativePlanningTaskEvent = null;
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
            IEnumerable<int>[] modes = [driver.EnumerateNativeRoute(target, observation.Events, true, probe), driver.EnumerateNativeRoute(target, observation.Events, false, probe)];
            foreach (int _ in Ff7AutoplayDriver.EnumerateFairNativeCandidates(modes, () => probe.Result is not null))
            {
                nativePlanningPhase = probe.Progress;
                yield return 0;
            }
            Ff7AutoPlayNativeRoute? proof = probe.Result;
            CompleteSourceRevalidation(State, observation.NavigationKey, target.Id, target.ConditionFingerprint);
            Publish();
            nativePlanningIntent = ReadNativePlanningIntent();
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
            Ff7AutoplayInput next = driver.ChooseTargetInput(target, actual.Events, run => target.Kind == Ff7AutoPlayTargetKind.Gateway ? driver.GatewayAt(target.GatewayIndex!.Value, run) : driver.ApproachLine(target.Entity, target.Kind, run));
            return !driver.PlanningPending && driver.PlannedActionAdvances == true && (next.Horizontal != 0 || next.Vertical != 0);
        }
        catch (Ff7AutoPlayNavigationUnavailableException)
        {
            return false;
        }
    }

    internal static Ff7AutoPlayTarget[] ReadNativeProbeCandidates(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, string? completedTask = null)
    {
        Ff7AutoPlaySourceRevalidationTask? required = HasArrivedSourceRevalidation(state, observation) ? state.NativeRevalidation : null;
        return observation.Events.Where(target => target.Id != completedTask && (required is null || target.Id == required.EventId && target.ConditionFingerprint == required.Condition) && target.AnalysisComplete && Ff7AutoplayDriver.SupportsNativeRouteTarget(target) && state.NavigationGuards.Values.Any(guard => guard.EventId == target.Id && guard.NavigationKey == observation.NavigationKey && guard.MovementFailure is not null)).ToArray();
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
