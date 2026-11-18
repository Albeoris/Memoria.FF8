using System.Security.Cryptography;
using System.Text;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private AutoPlayPlanStep? TryProveNativeNavigation(Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, Dictionary<string, string> facts, Dictionary<string, IReadOnlyDictionary<string, string>?> components)
    {
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.AnalysisComplete && State.NavigationGuards.Values.Any(guard => guard.EventId == target.Id && guard.NavigationKey == observation.NavigationKey && guard.MovementFailure is not null)))
        {
            Ff7AutoPlayNativeRoute? proof = driver.ProveNativeRoute(target, observation.Events);
            bool intermediate = proof is not null;
            proof ??= driver.ProveNativeRoute(target, observation.Events, requireIntermediate: false);
            if (proof is null)
            {
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
            if (next?.Event.Id == target.Id && IsDestinationPlan(State, next, observation.NavigationKey) && (intermediate || IsNativeContinuationGoalPending(State, next.Goal, observation.NavigationKey, target.Id)))
            {
                AutoPlayNavigationVisit current = State.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
                AutoPlayEventAvailability availability = ReadComponentAvailability(State, current, current.Events.Single(item => item.Id == target.Id), revised);
                AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
                coverage.AvailabilityFingerprint = availability.Fingerprint;
                coverage.Unavailable = availability.Unavailable;
                coverage.UnavailableVariants = availability.UnavailableVariants;
                State.NativeRoute = proof;
                return next;
            }
            components[observation.NavigationKey] = facts;
        }
        return remoteNavigationProbe is null ? TryProveRemoteNativeNavigation(observation) : null;
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
