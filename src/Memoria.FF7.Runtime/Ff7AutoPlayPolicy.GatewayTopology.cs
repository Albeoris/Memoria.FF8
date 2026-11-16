using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal const int GatewayTopologyRevision = 1;

    private static string GatewayTopologyKey(AutoPlayNavigationVisit navigation, AutoPlayEvent observed)
    {
        return $"{navigation.Key}|{observed.Id}";
    }

    private void ObserveGatewayTopology(AutoPlayNavigationVisit navigation, AutoPlayEvent observed, Ff7AutoPlayObservation observation)
    {
        if (navigation.VisitKey != observation.VisitId || !navigation.Key.StartsWith(observation.NavigationKey[..(observation.NavigationKey.LastIndexOf(':') + 1)], StringComparison.Ordinal))
        {
            return;
        }
        Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == observed.Id && target.Kind == Ff7AutoPlayTargetKind.Gateway && target.AnalysisComplete && target.ConditionFingerprint == observed.ConditionSignature);
        if (target?.GatewayIndex is not int index || (uint)index >= session.Field.Triggers.Gateways.Count)
        {
            return;
        }
        int? source = navigation.Key == observation.NavigationKey ? session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle : State.GatewayTopology.GetValueOrDefault(GatewayTopologyKey(navigation, observed))?.SourceTriangle;
        source ??= State.NavigationGuards.Values.FirstOrDefault(guard => guard.NavigationKey == navigation.Key && guard.EventId == observed.Id && guard.SourceTriangle is int triangle && (uint)triangle < session.Field.Walkmesh.Count)?.SourceTriangle;
        if (source is not int sourceTriangle || (uint)sourceTriangle >= session.Field.Walkmesh.Count || Ff7AutoplayDriver.ReadNavigationKey(observation.VisitId, session.Field.Walkmesh, session.Host.Process.BlockedTriangles, sourceTriangle) != navigation.Key)
        {
            return;
        }
        bool reachable = Ff7AutoplayDriver.HasGatewayTopologyPath(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, sourceTriangle, session.Field.Triggers.Gateways[index], out int destination);
        RecordGatewayTopology(State, navigation, observed, sourceTriangle, destination, reachable);
    }

    internal static void RecordGatewayTopology(Ff7AutoPlayState state, AutoPlayNavigationVisit navigation, AutoPlayEvent observed, int sourceTriangle, int targetTriangle, bool reachable)
    {
        string key = GatewayTopologyKey(navigation, observed);
        if (reachable)
        {
            state.GatewayTopology.Remove(key);
        }
        else
        {
            state.GatewayTopology[key] = new(navigation.Key, observed.Id, observed.ConditionSignature, GatewayTopologyRevision, sourceTriangle, targetTriangle);
        }
    }

    internal static AutoPlayEventAvailability? ReadGatewayTopologyAvailability(Ff7AutoPlayState state, AutoPlayNavigationVisit navigation, AutoPlayEvent observed, AutoPlayEventAvailability? physical)
    {
        if (!state.GatewayTopology.TryGetValue(GatewayTopologyKey(navigation, observed), out Ff7AutoPlayGatewayTopology? topology) || topology is null || topology.Revision != GatewayTopologyRevision || topology.NavigationKey != navigation.Key || topology.EventId != observed.Id || topology.ConditionSignature != observed.ConditionSignature)
        {
            return physical;
        }
        HashSet<string> variants = physical is null ? [] : new(physical.UnavailableVariants, StringComparer.Ordinal);
        variants.Add(observed.Id);
        return new(true, variants, $"{observed.ConditionSignature}|gateway-topology:{GatewayTopologyRevision}:{navigation.Key}:{observed.Id}:{topology.TargetTriangle}");
    }
}
