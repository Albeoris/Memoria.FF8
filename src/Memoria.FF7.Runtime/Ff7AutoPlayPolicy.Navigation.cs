using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal static void RecordControlReturn(AutoPlayPlanner<Ff7AutoPlayDeferral> planner, Ff7AutoPlayActiveEvent active, Ff7AutoPlayObservation observation, bool navigationReceipt, string? exitSignature, bool progressChanged, IReadOnlyList<AutoPlayChoiceSelection> selections)
    {
        planner.Completed(active.VisitId, active.EventId, navigationReceipt ? observation.VisitId : null, exitSignature: exitSignature, observedStoryChanged: progressChanged, selections: selections, entrySignature: active.ConditionFingerprint, entrySignatureAuthoritative: true, entryAvailabilityFingerprint: active.EntryAvailabilityFingerprint, availabilityFingerprintAuthoritative: true, targetId: active.EventId);
    }

    internal static Ff7AutoPlayNavigationGuard RestorePhysicalGuard(Ff7AutoPlayNavigationGuard guard, string currentNavigationKey, int currentTriangle)
    {
        // The caller has revalidated the exact native physical fingerprint at this component.
        return guard.NavigationKey is null || guard.NavigationKey == currentNavigationKey ? guard with { NavigationKey = currentNavigationKey, SourceTriangle = currentTriangle } : guard;
    }

    internal static void ObserveNavigationProbes(Ff7AutoPlayState state, Ff7AutoPlayObservation observation)
    {
        AutoPlayNavigationVisit navigation = state.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.AnalysisComplete && target.MayChangeNavigation && !target.ChangesStory))
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(entry => entry.Event.Id == target.Id);
            navigation.RouteRediscovery[target.Id] = coverage.Completed && !state.NavigationProbes.Contains(NavigationProbeKey(observation.NavigationKey, target));
        }
    }

    private bool NeedsNavigationRefresh(AutoPlayVisit visit, Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, IReadOnlyDictionary<string, string> validatedPhysicalGuards, Dictionary<string, IReadOnlyDictionary<string, string>?> componentGuards)
    {
        AutoPlayNavigationVisit? navigation = State.Coverage.NavigationVisits.SingleOrDefault(item => item.Key == visit.Key);
        if (navigation?.VisitKey != observation.VisitId)
        {
            return NeedsNavigationRefresh(State, visit);
        }
        if (!componentGuards.TryGetValue(navigation.Key, out IReadOnlyDictionary<string, string>? projected))
        {
            int? triangle = navigation.Key == observation.NavigationKey ? session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle : State.NavigationGuards.Values.FirstOrDefault(guard => guard.VisitId == navigation.VisitKey && guard.NavigationKey == navigation.Key && guard.SourceTriangle is int source && (uint)source < session.Field.Walkmesh.Count)?.SourceTriangle;
            if (triangle is not null)
            {
                Dictionary<string, string> facts = new(StringComparer.Ordinal);
                foreach (var entry in State.NavigationGuards.Where(entry => entry.Value.VisitId == navigation.VisitKey))
                {
                    Ff7AutoPlayNavigationGuard guard = entry.Value;
                    if (guard.EventId is not string eventId)
                    {
                        facts[entry.Key] = driver.ReadPhysicalAvailabilityFingerprint(guard.Entity, guard.BlockingEntities, triangle);
                        continue;
                    }
                    Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == eventId);
                    facts[entry.Key] = target is null ? $"{observation.ProgramIdentity}:absent:{eventId}" : driver.ReadPhysicalAvailabilityFingerprint(target, guard.BlockingEntities, triangle);
                    facts[entry.Key] = ReadMovementFailureFingerprint(facts[entry.Key], guard.MovementFailure, session.Host.Process.Actors, guard.BlockingEntities);
                }
                projected = facts;
            }
            componentGuards[navigation.Key] = projected;
        }
        return NeedsNavigationRefresh(State, visit, validatedPhysicalGuards, projected);
    }

    internal static bool NeedsNavigationRefresh(Ff7AutoPlayState state, AutoPlayVisit visit, IReadOnlyDictionary<string, string>? validatedPhysicalGuards = null, IReadOnlyDictionary<string, string>? componentPhysicalGuards = null)
    {
        AutoPlayNavigationVisit? navigation = state.Coverage.NavigationVisits.SingleOrDefault(item => item.Key == visit.Key);
        if (navigation is null)
        {
            return false;
        }
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral>? aggregate = state.Coverage.Visits.SingleOrDefault(item => item.Visit.Key == navigation.VisitKey);
        if (aggregate is null)
        {
            return false;
        }
        foreach (AutoPlayEvent observed in navigation.Events)
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? item = aggregate.Events.SingleOrDefault(candidate => candidate.Event.Id == observed.Id);
            if (item is null || !item.Unavailable || item.Event.ConditionSignature != observed.ConditionSignature)
            {
                continue;
            }
            AutoPlayEventAvailability? availability = componentPhysicalGuards is null ? null : AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(item, componentPhysicalGuards);
            AutoPlayEventCoverage<Ff7AutoPlayDeferral> scoped = new()
            {
                Event = observed,
                Completed = item.Completed,
                Conditions = item.Conditions,
                AvailabilityFingerprint = componentPhysicalGuards is null ? item.AvailabilityFingerprint : availability!.Fingerprint,
                RouteNeedsRediscovery = componentPhysicalGuards is null && item.RouteNeedsRediscovery,
                CompletedAvailabilityFingerprints = item.CompletedAvailabilityFingerprints
            };
            // Aggregate route probes and physical eligibility describe the current component.
            // A remote refresh requires a genuine unconsumed entry or availability at its own source.
            if (!AutoPlayPlanner<Ff7AutoPlayDeferral>.NeedsExecution(scoped))
            {
                continue;
            }
            bool attempted = item.UnavailableAttempts.Any(attempt =>
            {
                if (!state.NavigationGuards.TryGetValue(attempt.GuardKey, out Ff7AutoPlayNavigationGuard? guard) || guard.NavigationKey != navigation.Key || guard.VisitId != navigation.VisitKey || guard.EventId != observed.Id)
                {
                    return false;
                }
                if (guard.SourceTriangle is not null && validatedPhysicalGuards is not null && validatedPhysicalGuards.TryGetValue(attempt.GuardKey, out string? fingerprint))
                {
                    return fingerprint == attempt.GuardFingerprint;
                }
                return attempt.SourceConditionSignature == observed.ConditionSignature;
            });
            if (!attempted)
            {
                return true;
            }
        }
        return false;
    }
}
