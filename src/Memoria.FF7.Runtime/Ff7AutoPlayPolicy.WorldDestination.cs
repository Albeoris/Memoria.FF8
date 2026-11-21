using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private Ff7WorldRuntime? destinationBoardingOwner;
    private readonly Dictionary<string, bool> destinationBoardingAdvice = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Ff7WorldModeTopologyCursor> destinationBoardingSearches = new(StringComparer.Ordinal);

    private void CancelDestinationBoardingSearches()
    {
        foreach (Ff7WorldModeTopologyCursor search in destinationBoardingSearches.Values)
        {
            search.Dispose();
        }
        destinationBoardingSearches.Clear();
    }

    private bool AddWorldDestinationBoarding(Ff7WorldRuntime world, HashSet<string> targets, Func<AutoPlayEvent, AutoPlayEventAvailability?> readAvailability)
    {
        if (!ReferenceEquals(destinationBoardingOwner, world))
        {
            destinationBoardingAdvice.Clear();
            CancelDestinationBoardingSearches();
            destinationBoardingOwner = world;
        }
        var coverage = State.Coverage.Visits.Single(visit => visit.Visit.Key == worldVisit);
        if (coverage.Events.Any(item => targets.Contains(item.Event.Id) && readAvailability(item.Event)?.Unavailable != true))
        {
            CancelDestinationBoardingSearches();
            return false;
        }
        Ff7AutoPlayWorldScriptObservation[] destinations = worldCatalogs.Values.SelectMany(catalog => catalog.Scripts).Where(target => targets.Contains(target.Id)).ToArray();
        foreach (Ff7AutoPlayWorldModelScriptObservation contact in worldModelCatalog.Values.Where(contact => contact.Complete && contact.Analysis.Transitions.Any(transition => transition.Operation == WorldOperation.BoardTransport && transition.Model is not null)))
        {
            foreach (Ff7AutoPlayWorldScriptObservation destination in destinations)
            {
                string key = WorldModelCondition(contact) + ":destination:" + WorldCondition(destination) + ":context:" + world.ReadAutoPlayNavigationContext() + ":source:" + Ff7AutoplayDriver.WorldMovementKey(world.CaptureAutoPlayMovementState());
                if (!destinationBoardingAdvice.TryGetValue(key, out bool connected))
                {
                    if (!destinationBoardingSearches.TryGetValue(key, out Ff7WorldModeTopologyCursor? search))
                    {
                        CancelDestinationBoardingSearches();
                    }
                    Ff7AutoPlayWorldBoardingAdvice advice = world.ReadAutoPlayBoardingAdvice(contact, destination, ref search, nativePlanningQuantum ?? new());
                    if (advice.Pending)
                    {
                        destinationBoardingSearches[key] = search!;
                        return true;
                    }
                    search?.Dispose();
                    destinationBoardingSearches.Remove(key);
                    connected = advice.Connected;
                    destinationBoardingAdvice[key] = connected;
                }
                if (connected)
                {
                    targets.Add(contact.Id);
                    break;
                }
            }
        }
        return false;
    }

    private (Ff7AutoPlayObservation Observation, HashSet<string> Targets) ObserveRequestedWorldDestination(Ff7WorldRuntime world, Ff7AutoPlayObservation observation)
    {
        ushort destination = State.DestinationField!.Value;
        Ff7AutoPlayWorldEntranceSource[] sources = session.ReadAutoPlayWorldEntranceSources(world.Resources.World).Where(source => source.Destination == destination).ToArray();
        if (sources.Length == 0)
        {
            throw new NotSupportedException($"The current native world program has no proved field-entry source for destination {destination}.");
        }
        bool added = false;
        foreach (Ff7AutoPlayWorldChunk chunk in sources.Select(source => new Ff7AutoPlayWorldChunk(source.ChunkX, source.ChunkZ)).Distinct())
        {
            if (!worldCatalogs.ContainsKey(chunk))
            {
                ObserveWorldChunk(world, chunk);
                added = true;
            }
        }
        if (added)
        {
            worldModeCatalog.Clear();
            worldModeQueue.Clear();
            worldModeCandidatesObserved = false;
        }
        observation = ObserveWorldPlanner(world);
        HashSet<string> sourceIds = sources.Select(source => source.EventId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> targets = [];
        string? incomplete = null;
        foreach (Ff7AutoPlayWorldScriptObservation target in worldCatalogs.Values.SelectMany(catalog => catalog.Scripts).Where(target => sourceIds.Contains(target.Id)))
        {
            WorldScriptAnalysis entry = world.ReadAutoPlayMeshEntryForModel(target, world.ControlledModel);
            if (!target.Complete || !entry.Complete)
            {
                incomplete ??= target.Reason ?? entry.Reason;
                continue;
            }
            if (entry.Transitions.Any(transition => transition.Operation == WorldOperation.FieldTransition && transition.Selector is ushort selector && world.ResolveFieldEntry(selector).Map == destination))
            {
                targets.Add(target.Id);
            }
        }
        foreach (var mode in worldModeCatalog.Where(mode => sourceIds.Contains(mode.Value.Target.Id)))
        {
            targets.Add(mode.Key);
        }
        if (mountedRiderControl is { } mounted)
        {
            targets.Add(mounted.Id);
        }
        RequireWorldDestinationTargets(targets, worldModeTopologyPending, incomplete, destination);
        return (observation, targets);
    }

    internal static void RequireWorldDestinationTargets(IReadOnlyCollection<string> targets, bool topologyPending, string? incomplete, ushort destination)
    {
        if (targets.Count == 0 && !topologyPending)
        {
            throw new NotSupportedException(incomplete ?? $"The native field-entry sources for destination {destination} require a traversal mode whose admitted prerequisite is not established from the current world state.");
        }
    }

    internal static HashSet<string> ResolveWorldDestinationGoal(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, IReadOnlySet<string> candidates, Func<AutoPlayEvent, AutoPlayEventAvailability?> readAvailability)
    {
        ushort destination = state.DestinationField ?? throw new InvalidOperationException("A requested world destination is required.");
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral> visit = state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>[] admitted = visit.Events.Where(item => candidates.Contains(item.Event.Id) && item.Available && item.BlockedReason is null && !item.PolicyDeferred && readAvailability(item.Event)?.Unavailable != true).Where(item => !HasConsumedWorldEntry(item, item.Event.ConditionSignature) || item.Routes.Any(route => route.ConditionSignature == item.Event.ConditionSignature && ReadVisitField(route.DestinationVisitKey) == destination)).ToArray();
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? selected = admitted.FirstOrDefault(item => state.NavigationGoal is { } goal && goal.NavigationKey == observation.NavigationKey && goal.EventId == item.Event.Id && goal.Conditions == item.Event.ConditionSignature) ?? admitted.FirstOrDefault();
        if (selected is null)
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? required = visit.Events.FirstOrDefault(item => candidates.Contains(item.Event.Id));
            state.NavigationGoal = required is null ? null : new(observation.NavigationKey, required.Event.Id, required.Event.ConditionSignature);
            throw new NotSupportedException($"No currently admitted native field-entry or supported mode route to destination {destination} remains in this world component.");
        }
        state.NavigationGoal = new(observation.NavigationKey, selected.Event.Id, selected.Event.ConditionSignature);
        if (HasConsumedWorldEntry(selected, selected.Event.ConditionSignature))
        {
            selected.RouteNeedsRediscovery = true;
            selected.GlobalRouteRediscovery = true;
        }
        return state.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey).Events.Where(item => admitted.All(candidate => candidate.Event.Id != item.Id)).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
    }
}
