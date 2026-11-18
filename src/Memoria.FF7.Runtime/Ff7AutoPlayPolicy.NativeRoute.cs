using System.Security.Cryptography;
using System.Text;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private Ff7NativePlanningCursor? nativePlanning;
    private Ff7NativePlanningQuantum? nativePlanningQuantum;
    private string? nativePlanningSource;
    private string? nativePlanningIntent;
    private string? nativePlanningPhase;
    private long nativePlanningWork;
    internal bool NativePlanningPending => nativePlanning is not null;
    internal long NativePlanningGeneration { get; private set; }
    internal string? NativePlanningPhase => nativePlanningPhase;

    private AutoPlayPlanStep? TryProveNativeNavigation(Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, Dictionary<string, string> facts, Dictionary<string, IReadOnlyDictionary<string, string>?> components)
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
            nativePlanning = new(EnumerateNativeNavigation(observation, driver, facts, components).GetEnumerator());
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
    }

    private IEnumerable<AutoPlayPlanStep?> EnumerateNativeNavigation(Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, Dictionary<string, string> facts, Dictionary<string, IReadOnlyDictionary<string, string>?> components)
    {
        if (!Ff7AutoplayDriver.HasNativeRouteClockContract(session))
        {
            yield break;
        }
        Ff7AutoPlaySourceRevalidationTask? required = HasArrivedSourceRevalidation(State, observation) ? State.NativeRevalidation : null;
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => (required is null || target.Id == required.EventId && target.ConditionFingerprint == required.Condition) && target.AnalysisComplete && target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && State.NavigationGuards.Values.Any(guard => guard.EventId == target.Id && guard.NavigationKey == observation.NavigationKey && guard.MovementFailure is not null)))
        {
            Ff7NativeRouteProbe probe = new();
            foreach (int _ in driver.EnumerateNativeRoute(target, observation.Events, true, probe))
            {
                nativePlanningPhase = probe.Progress;
                yield return null;
            }
            bool intermediate = probe.Result is not null;
            if (!intermediate)
            {
                foreach (int _ in driver.EnumerateNativeRoute(target, observation.Events, false, probe))
                {
                    nativePlanningPhase = probe.Progress;
                    yield return null;
                }
            }
            Ff7AutoPlayNativeRoute? proof = probe.Result;
            CompleteSourceRevalidation(State, observation.NavigationKey, target.Id, target.ConditionFingerprint);
            Publish();
            nativePlanningIntent = ReadNativePlanningIntent();
            if (proof is null)
            {
                if (required is not null)
                {
                    yield break;
                }
                continue;
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
            bool IsPendingPlan(AutoPlayPlanStep? candidate) => candidate?.Event.Id == target.Id && IsDestinationPlan(State, candidate, observation.NavigationKey) && (intermediate || IsNativeContinuationGoalPending(State, candidate.Goal, observation.NavigationKey, target.Id));
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
                yield return next;
                yield break;
            }
            components[observation.NavigationKey] = facts;
            if (required is not null)
            {
                yield break;
            }
        }
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
        State.NativeRoute = proof with { Cursor = proof.Cursor + 1 };
        State.Status = $"Following native continuation toward {proof.EventId}, callback {proof.Cursor + 1}/{proof.Steps.Length}";
        Publish();
        return input;
    }

    private void ObserveNativeRouteBoundary()
    {
        if (State.NativeRoute is not { } proof || proof.Cursor != proof.Steps.Length)
        {
            return;
        }
        if (proof.After != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) || proof.BattleBoundary != (session.Battle is not null))
        {
            State.NativeRoute = null;
            Publish();
            throw new NotSupportedException("The bounded native navigation continuation reached a different native boundary.");
        }
        State.NativeRoute = null;
        Publish();
    }
}
