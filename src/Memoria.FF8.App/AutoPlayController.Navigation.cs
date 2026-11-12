namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private readonly HashSet<int> statefulNavigationFields = [];
    private AutoPlayPlannerState? navigationStateEvidence;
    private int navigationStateRouteCount = -1;
    private int navigationStateRetirementCount = -1;
    private bool navigationGuardsChanged;

    internal static IReadOnlySet<int> FindStatefulNavigationFields(AutoPlayPlannerState coverage, IReadOnlyDictionary<string, AutoPlayNavigationReceipt> receipts, IReadOnlyDictionary<string, AutoPlayVisitGuard>? guards = null)
    {
        HashSet<int> fields = receipts.Values.Where(receipt => receipt.StateFingerprint is not null).Select(receipt => receipt.FieldId).ToHashSet();
        Dictionary<(string Source, string Event), string?> predicates = [];
        foreach (AutoPlayNavigationRoute edge in coverage.NavigationRoutes)
        {
            if (!receipts.TryGetValue(edge.SourceKey, out AutoPlayNavigationReceipt? receipt))
            {
                continue;
            }
            int state = edge.SourceKey.IndexOf(":state:", StringComparison.Ordinal);
            string physicalSource = state < 0 ? edge.SourceKey : edge.SourceKey[..state];
            var key = (physicalSource, edge.EventId);
            if (predicates.TryGetValue(key, out string? previous) && previous != edge.Route.ConditionSignature)
            {
                fields.Add(receipt.FieldId);
            }
            predicates.TryAdd(key, edge.Route.ConditionSignature);
        }
        if (guards is null || fields.Count == 0)
        {
            return fields;
        }
        // Transit fields can preserve a branch predicate even when none of their
        // own exits test it. Only link observed routes with matching portable reads.
        Dictionary<int, HashSet<int>> sharedState = [];
        foreach (AutoPlayNavigationRoute edge in coverage.NavigationRoutes)
        {
            if (edge.Route.ObservedStoryChanged == true || !TryReadGuard(edge.SourceKey, out AutoPlayVisitGuard source) || !TryReadGuard(edge.DestinationKey, out AutoPlayVisitGuard destination) || source.FieldId == destination.FieldId || source.Fingerprint != destination.Fingerprint || !source.Reads.SequenceEqual(destination.Reads) || !source.Reads.Any(read => read.Address != 256))
            {
                continue;
            }
            AddLink(source.FieldId, destination.FieldId);
            AddLink(destination.FieldId, source.FieldId);
        }
        Queue<int> pending = new(fields);
        while (pending.TryDequeue(out int field))
        {
            if (!sharedState.TryGetValue(field, out HashSet<int>? neighbors))
            {
                continue;
            }
            foreach (int neighbor in neighbors)
            {
                if (fields.Add(neighbor))
                {
                    pending.Enqueue(neighbor);
                }
            }
        }
        return fields;

        bool TryReadGuard(string key, out AutoPlayVisitGuard guard)
        {
            if (guards.TryGetValue(key, out AutoPlayVisitGuard? candidate) && receipts.TryGetValue(key, out AutoPlayNavigationReceipt? receipt) && candidate.Version == AutoPlayVisitGuard.CurrentVersion && candidate.FieldId == receipt.FieldId && candidate.ProgramHash == receipt.ProgramHash)
            {
                guard = candidate;
                return true;
            }
            guard = null!;
            return false;
        }

        void AddLink(int source, int destination)
        {
            if (!sharedState.TryGetValue(source, out HashSet<int>? neighbors))
            {
                neighbors = [];
                sharedState.Add(source, neighbors);
            }
            neighbors.Add(destination);
        }
    }

    private bool UsesStatefulNavigation(int field)
    {
        if (navigationGuardsChanged || !ReferenceEquals(navigationStateEvidence, run.Coverage) || navigationStateRouteCount != run.Coverage.NavigationRoutes.Count || navigationStateRetirementCount != run.Coverage.NavigationRetirements.Count)
        {
            if (!ReferenceEquals(navigationStateEvidence, run.Coverage))
            {
                statefulNavigationFields.Clear();
            }
            navigationStateEvidence = run.Coverage;
            navigationStateRouteCount = run.Coverage.NavigationRoutes.Count;
            navigationStateRetirementCount = run.Coverage.NavigationRetirements.Count;
            navigationGuardsChanged = false;
            statefulNavigationFields.UnionWith(FindStatefulNavigationFields(run.Coverage, run.NavigationReceipts, run.VisitGuards));
        }
        return statefulNavigationFields.Contains(field);
    }

    private string CurrentNavigationKey(GameWorld world, string visit)
    {
        if (visit.StartsWith("world:", StringComparison.Ordinal))
        {
            return visit + ":nav:world";
        }
        AutoPlayNavigationReceipt receipt = world.CaptureAutoPlayNavigationReceipt(includeState: UsesStatefulNavigation(world.Events.FieldId));
        string key = receipt.Key(visit);
        run.NavigationReceipts[key] = receipt;
        return key;
    }

    private string ObserveNavigation(GameWorld world, string visit)
    {
        string key = CurrentNavigationKey(world, visit);
        AutoPlayVisitCoverage coverage = run.Coverage.Visits.Single(item => item.Visit.Key == visit);
        planner.ObserveNavigation(visit, key, coverage.Events.Where(item => item.Available).Select(item => item.Event).ToArray());
        return key;
    }

    private bool IsNavigationResourceCurrent(GameWorld world, AutoPlayNavigationVisit vertex)
    {
        if (vertex.VisitKey.StartsWith("world:", StringComparison.Ordinal))
        {
            return vertex.Key == vertex.VisitKey + ":nav:world";
        }
        if (!run.NavigationReceipts.TryGetValue(vertex.Key, out AutoPlayNavigationReceipt? receipt) || receipt.Key(vertex.VisitKey) != vertex.Key)
        {
            throw new InvalidDataException("A navigation vertex lacks its exact original resource and component receipt.");
        }
        return world.IsAutoPlayNavigationResourceCurrent(receipt);
    }

    private void RecordNavigationControlReturn(string source, string destination, string eventId)
    {
        if (run.ActiveNavigationKey is not string sourceNavigation)
        {
            // Historical aggregates carry no component proof. Only newly observed native attempts establish navigation edges.
            return;
        }
        string destinationNavigation = CurrentNavigationKey(getWorld(), destination);
        AutoPlayEventCoverage item = run.Coverage.Visits.Single(visit => visit.Visit.Key == source).Events.Single(item => item.Event.Id == eventId);
        AutoPlayEventRoute? route = item.Routes.LastOrDefault(route => route.DestinationVisitKey == destination && route.ConditionSignature == run.ActiveConditionSignature && route.TargetId == run.ActiveTargetId && (route.Selections is null ? run.ActiveSelections is null : run.ActiveSelections is not null && route.Selections.SequenceEqual(run.ActiveSelections)));
        if (route is not null)
        {
            planner.RecordNavigationRoute(sourceNavigation, destinationNavigation, eventId, route, run.ActiveNavigationSource);
        }
    }
}
