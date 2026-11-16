using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal static bool RequiresMovementLineAvoidance(Ff7AutoPlayTarget candidate, Ff7AutoPlayTarget selected, FieldInputState input)
    {
        if (selected.Kind is Ff7AutoPlayTargetKind.ActorTalk or Ff7AutoPlayTargetKind.LineTalk || selected.EntryInputContract is not null || selected.EntryInputDemand is not null || candidate.EntryInputDemand is not { } demand)
        {
            return true;
        }
        // Ordinary approach actions issue only movement and the native run button.
        // Interaction and independently gated goals retain conservative contact avoidance.
        const ushort movementButtons = 0x40;
        ushort possible = demand.Kind switch
        {
            FieldScriptInputKind.Held or FieldScriptInputKind.Pressed => movementButtons,
            FieldScriptInputKind.Released => (ushort)(movementButtons | (demand.Mapped ? input.Held : input.RawHeld)),
            _ => ushort.MaxValue
        };
        return demand.Mask == 0 || (demand.Mask & possible) != 0;
    }

    internal static void RecordControlReturn(AutoPlayPlanner<Ff7AutoPlayDeferral> planner, Ff7AutoPlayActiveEvent active, Ff7AutoPlayObservation observation, bool navigationReceipt, string? exitSignature, bool progressChanged, IReadOnlyList<AutoPlayChoiceSelection> selections)
    {
        planner.Completed(active.VisitId, active.EventId, navigationReceipt ? observation.VisitId : null, exitSignature: exitSignature, observedStoryChanged: progressChanged, selections: selections, entrySignature: active.ConditionFingerprint, entrySignatureAuthoritative: true, entryAvailabilityFingerprint: active.EntryAvailabilityFingerprint, availabilityFingerprintAuthoritative: true, targetId: active.EventId);
    }

    internal static Ff7AutoPlayNavigationGuard RestorePhysicalGuard(Ff7AutoPlayNavigationGuard guard, string currentNavigationKey, int currentTriangle, int[]? spatialBoundaryTriangles = null)
    {
        // The caller has revalidated the exact native physical fingerprint at this component.
        return guard.NavigationKey is null || guard.NavigationKey == currentNavigationKey ? guard with { NavigationKey = currentNavigationKey, SourceTriangle = currentTriangle, SpatialBoundaryTriangles = spatialBoundaryTriangles ?? guard.SpatialBoundaryTriangles } : guard;
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

    private AutoPlayEventAvailability? ReadNavigationAvailability(AutoPlayNavigationVisit navigation, AutoPlayEvent observed, Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, Dictionary<string, IReadOnlyDictionary<string, string>?> componentGuards)
    {
        if (State.WorldVisits.ContainsKey(navigation.VisitKey))
        {
            return ReadRetainedNavigationAvailability(State, navigation, observed);
        }
        if (navigation.VisitKey != observation.VisitId && !State.VisitGuards.ContainsKey(navigation.VisitKey))
        {
            return null;
        }
        if (!componentGuards.TryGetValue(navigation.Key, out IReadOnlyDictionary<string, string>? facts))
        {
            Dictionary<string, string> projected = ReadRetainedNavigationGuardFacts(State, navigation);
            bool current = navigation.Key == observation.NavigationKey;
            string configuration = observation.NavigationKey[..observation.NavigationKey.LastIndexOf(':')];
            if (current || navigation.Key.StartsWith(configuration + ":", StringComparison.Ordinal))
            {
                int? triangle = current ? session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle : State.NavigationGuards.Values.FirstOrDefault(guard => guard.VisitId == navigation.VisitKey && guard.NavigationKey == navigation.Key && guard.SourceTriangle is int source && (uint)source < session.Field.Walkmesh.Count)?.SourceTriangle;
                if (triangle is not null)
                {
                    foreach (var entry in State.NavigationGuards.Where(entry => entry.Value.VisitId == navigation.VisitKey && entry.Value.NavigationKey == navigation.Key).ToArray())
                    {
                        Ff7AutoPlayNavigationGuard guard = entry.Value;
                        if (!current && guard.SpatialBoundaryTriangles is null)
                        {
                            // An older source proof cannot be revalidated using another component's boundary set.
                            continue;
                        }
                        Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == guard.EventId);
                        if (target is null || !navigation.Events.Any(item => item.Id == target.Id && item.ConditionSignature == target.ConditionFingerprint))
                        {
                            continue;
                        }
                        string physical = driver.ReadPhysicalAvailabilityFingerprint(target, guard.BlockingEntities, triangle, current ? null : guard.SpatialBoundaryTriangles!.ToHashSet());
                        projected[entry.Key] = ReadMovementFailureFingerprint(physical, guard.MovementFailure, session.Host.Process.Actors, guard.BlockingEntities);
                        State.NavigationGuards[entry.Key] = guard with { ObservedFingerprint = projected[entry.Key] };
                    }
                }
            }
            facts = projected;
            componentGuards[navigation.Key] = facts;
        }
        return ReadComponentAvailability(State, navigation, observed, facts!);
    }

    internal static AutoPlayEventAvailability? ReadRetainedNavigationAvailability(Ff7AutoPlayState state, AutoPlayNavigationVisit navigation, AutoPlayEvent observed)
    {
        return ReadRetainedNavigationAvailability(state, navigation, observed, null);
    }

    internal static Func<AutoPlayNavigationVisit, AutoPlayEvent, AutoPlayEventAvailability?> CreateRetainedNavigationAvailabilityReader(Ff7AutoPlayState state)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> componentFacts = new(StringComparer.Ordinal);
        Dictionary<(string NavigationKey, AutoPlayEvent Event), AutoPlayEventAvailability?> availability = [];
        return (navigation, observed) =>
        {
            var key = (navigation.Key, observed);
            if (!availability.TryGetValue(key, out AutoPlayEventAvailability? result))
            {
                result = ReadRetainedNavigationAvailability(state, navigation, observed, componentFacts);
                availability.Add(key, result);
            }
            return result;
        };
    }

    private static AutoPlayEventAvailability? ReadRetainedNavigationAvailability(Ff7AutoPlayState state, AutoPlayNavigationVisit navigation, AutoPlayEvent observed, Dictionary<string, IReadOnlyDictionary<string, string>>? componentFacts)
    {
        if (state.WorldVisits.TryGetValue(navigation.VisitKey, out Ff7AutoPlayWorldVisitState? world))
        {
            var aggregate = state.Coverage.Visits.Single(visit => visit.Visit.Key == navigation.VisitKey).Events.Single(item => item.Event.Id == observed.Id);
            AutoPlayEventCoverage<Ff7AutoPlayDeferral> scoped = new() { Event = observed, UnavailableAttempts = aggregate.UnavailableAttempts };
            Dictionary<string, string> facts = aggregate.UnavailableAttempts.GroupBy(attempt => attempt.GuardKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => world.ObservedPhysicalGuards.GetValueOrDefault(group.Key) ?? group.Last().GuardFingerprint, StringComparer.Ordinal);
            return AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(scoped, facts);
        }
        if (!state.VisitGuards.ContainsKey(navigation.VisitKey))
        {
            return null;
        }
        if (componentFacts is null)
        {
            return ReadComponentAvailability(state, navigation, observed, ReadRetainedNavigationGuardFacts(state, navigation));
        }
        if (!componentFacts.TryGetValue(navigation.Key, out IReadOnlyDictionary<string, string>? retainedFacts))
        {
            retainedFacts = ReadRetainedNavigationGuardFacts(state, navigation);
            componentFacts.Add(navigation.Key, retainedFacts);
        }
        return ReadComponentAvailability(state, navigation, observed, retainedFacts);
    }

    private static Dictionary<string, string> ReadRetainedNavigationGuardFacts(Ff7AutoPlayState state, AutoPlayNavigationVisit navigation)
    {
        var aggregate = state.Coverage.Visits.Single(visit => visit.Visit.Key == navigation.VisitKey);
        return aggregate.Events.SelectMany(item => item.UnavailableAttempts).Where(attempt => state.NavigationGuards.TryGetValue(attempt.GuardKey, out var guard) && guard.NavigationKey == navigation.Key).GroupBy(attempt => attempt.GuardKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => state.NavigationGuards[group.Key].ObservedFingerprint ?? group.Last().GuardFingerprint, StringComparer.Ordinal);
    }

    internal static AutoPlayEventAvailability ReadComponentAvailability(Ff7AutoPlayState state, AutoPlayNavigationVisit navigation, AutoPlayEvent observed, IReadOnlyDictionary<string, string> facts)
    {
        var aggregate = state.Coverage.Visits.Single(visit => visit.Visit.Key == navigation.VisitKey).Events.Single(item => item.Event.Id == observed.Id);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> scoped = new() { Event = observed, UnavailableAttempts = aggregate.UnavailableAttempts.Where(attempt => state.NavigationGuards.TryGetValue(attempt.GuardKey, out var guard) && guard.NavigationKey == navigation.Key).ToList() };
        return AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(scoped, facts);
    }

}
