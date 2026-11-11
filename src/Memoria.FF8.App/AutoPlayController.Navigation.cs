namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private readonly HashSet<int> statefulNavigationFields = [];
    private AutoPlayPlannerState? navigationStateEvidence;
    private int navigationStateRouteCount = -1;
    private int navigationStateRetirementCount = -1;

    internal static IReadOnlySet<int> FindStatefulNavigationFields(AutoPlayPlannerState coverage, IReadOnlyDictionary<string, AutoPlayNavigationReceipt> receipts)
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
        return fields;
    }

    private bool UsesStatefulNavigation(int field)
    {
        if (!ReferenceEquals(navigationStateEvidence, run.Coverage) || navigationStateRouteCount != run.Coverage.NavigationRoutes.Count || navigationStateRetirementCount != run.Coverage.NavigationRetirements.Count)
        {
            if (!ReferenceEquals(navigationStateEvidence, run.Coverage))
            {
                statefulNavigationFields.Clear();
            }
            navigationStateEvidence = run.Coverage;
            navigationStateRouteCount = run.Coverage.NavigationRoutes.Count;
            navigationStateRetirementCount = run.Coverage.NavigationRetirements.Count;
            statefulNavigationFields.UnionWith(FindStatefulNavigationFields(run.Coverage, run.NavigationReceipts));
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
            planner.RecordNavigationRoute(sourceNavigation, destinationNavigation, eventId, route);
        }
    }
}
