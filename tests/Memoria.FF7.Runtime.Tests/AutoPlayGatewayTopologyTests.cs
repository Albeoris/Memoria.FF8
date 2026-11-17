using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;

internal static class AutoPlayGatewayTopologyTests
{
    public static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), 1, -1, -1),
            new(1, new(100, 0, 0), new(100, 100, 0), new(0, 100, 0), 0, -1, -1),
            new(2, new(300, 0, 0), new(400, 0, 0), new(300, 100, 0), -1, -1, -1)
        ];
        PcFieldGateway remote = new(new(320, 20, 0), new(340, 20, 0), 0, 0, 0, 100, 0, 0);
        PcFieldGateway local = remote with { A = new(20, 20, 0), B = new(40, 20, 0) };
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, Triangle = 0, CollisionRadius = 1 };
        Check(!Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [0], player, remote, out int destination) && destination == -1, "A disconnected gateway was admitted without a native contact in the source component.");
        Check(Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [0], player with { Triangle = 2 }, remote, out _), "The gateway's actual native component was rejected.");
        Check(Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [0], player with { Triangle = 1 }, local, out _), "An ordinary connected native source was rejected.");
        Check(!Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [1], player with { Triangle = 1 }, local, out _), "A blocked destination was ignored by the exact native contact contract.");
        Check(Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [2], player with { Triangle = 1 }, local, out _), "A blocked current source was replaced by an assumed unblocked component root.");
        PcFieldGateway spanning = remote with { A = new(20, 20, 0), B = new(640, 20, 0) };
        Check(Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [0], player, spanning, out int spanningTarget) && spanningTarget is 0 or 1, "A long gateway whose midpoint belongs to another component lost its reachable segment contact.");
        Check(!Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [0], player, spanning with { A = spanning.A + new Vector3(0, 0, 100), B = spanning.B + new Vector3(0, 0, 100) }, out _), "A gateway on another native floor acquired an invented contact.");

        Ff7AutoPlayState state = new();
        AutoPlayEvent gateway = new("gateway:0", AutoPlayEventKind.Transition, ConditionSignature: "native-entry");
        string sourceKey = Ff7AutoplayDriver.ReadNavigationKey("field", mesh, [0], 0);
        AutoPlayNavigationVisit source = new(sourceKey, "field", [gateway]);
        AutoPlayNavigationVisit actual = new(Ff7AutoplayDriver.ReadNavigationKey("field", mesh, [0], 2), "field", [gateway]);
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new("field", "program"), [gateway]);
        planner.ObserveNavigation("field", source.Key, [gateway]);
        planner.ObserveNavigation("field", actual.Key, [gateway]);
        Ff7AutoPlayPolicy.RecordGatewayTopology(state, source, gateway, 0, destination, reachable: false);
        var reader = Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(state);
        Check(reader(source, gateway) is { Unavailable: true }, "A typed topology absence was lost without a physical failure receipt.");
        Check(planner.PlanNavigation(source.Key, readAvailability: reader) is null, "A known disconnected gateway remained an executable component frontier.");
        Check(planner.PlanNavigation(actual.Key, readAvailability: reader)?.Event.Id == gateway.Id, "A foreign component's topology absence hid the actual gateway component.");
        AutoPlayEventAvailability physical = new(false, [], "volatile-physical-facts");
        AutoPlayEventAvailability typed = Ff7AutoPlayPolicy.ReadGatewayTopologyAvailability(state, source, gateway, physical) ?? throw new InvalidOperationException("The recorded topology observation has no availability result.");
        Check(typed is { Unavailable: true } && typed.UnavailableVariants.Contains(gateway.Id), "Volatile physical observations reopened a proved raw topology absence.");
        Check(Ff7AutoPlayPolicy.ReadGatewayTopologyAvailability(state, source, gateway, physical with { Fingerprint = "other-facts" })?.Fingerprint == typed.Fingerprint, "The typed absence inherited a volatile physical fingerprint.");
        Check(Ff7AutoPlayPolicy.ReadGatewayTopologyAvailability(state, source, gateway with { ConditionSignature = "changed-entry" }, physical) == physical, "A different native entry predicate inherited an old absence.");
        AutoPlayNavigationVisit changedMask = source with { Key = Ff7AutoplayDriver.ReadNavigationKey("field", mesh, [2], 0) };
        Check(Ff7AutoPlayPolicy.ReadGatewayTopologyAvailability(state, changedMask, gateway, physical) == physical, "A changed native blocking configuration inherited an old absence.");
        Check(Ff7AutoPlayPolicy.ReadGatewayTopologyAvailability(state, source with { Key = "other-field-nav", VisitKey = "other-field" }, gateway, physical) == physical, "An unknown field inherited an unrelated topology absence.");
        Ff7AutoPlayState cold = state.Clone();
        Check(Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(cold)(source, gateway) is { Unavailable: true }, "Cold restoration lost a typed topology observation.");
        string cacheKey = cold.GatewayTopology.Single().Key;
        cold.GatewayTopology[cacheKey] = cold.GatewayTopology[cacheKey] with { Revision = -1 };
        Check(Ff7AutoPlayPolicy.ReadGatewayTopologyAvailability(cold, source, gateway, physical) == physical, "An unsupported observation revision changed eligibility.");
        string coverage = JsonSerializer.Serialize(cold.Coverage);
        Ff7AutoPlayPolicy.RecordGatewayTopology(cold, source, gateway, 0, destination, reachable: true);
        Check(Ff7AutoPlayPolicy.ReadGatewayTopologyAvailability(cold, source, gateway, physical) == physical, "A fresh reachable observation did not remove the retained negative.");
        Check(JsonSerializer.Serialize(cold.Coverage) == coverage, "Topology observation changed executed entries, routes or native event coverage.");
        Check(JsonSerializer.Deserialize<Ff7AutoPlayState>("{}")!.GatewayTopology.Count == 0, "An older state requires newly introduced observation metadata.");
        OriginalSegmentContact();
    }

    private static void OriginalSegmentContact()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_SEGMENT_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original gateway segment contact: configure original resources and MEMORIA_FF7_GATEWAY_SEGMENT_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget target = observation.Events.Single(item => item.Id == "gateway:0");
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        PcFieldGateway gateway = session.Field.Triggers.Gateways[target.GatewayIndex!.Value];
        Check(Ff7AutoplayDriver.HasGatewayTopologyPath(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, gateway, out int contact), "The original full gateway segment lost its current-component contact.");
        Ff7AutoplayDriver driver = new(session);
        driver.ConfigureSpatialBoundaries(observation.Events, target);
        Ff7AutoplayInput action = driver.ChooseTargetInput(target, observation.Events, run => driver.GatewayAt(target.GatewayIndex.Value, run));
        Check(action.Horizontal != 0 || action.Vertical != 0, "The configured native gateway segment approach did not produce movement.");
        Ff7AutoPlayState state = session.AutoPlayState!.Clone();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayNavigationVisit navigation = state.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        AutoPlayEvent observed = navigation.Events.Single(item => item.Id == target.Id);
        Ff7AutoPlayPolicy.RecordGatewayTopology(state, navigation, observed, player.Triangle, contact, reachable: true);
        var availability = Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(state);
        Check(availability(navigation, observed) is not { Unavailable: true }, "Fresh native contact evidence retained the obsolete midpoint topology refusal.");
        Check(planner.PlanNavigation(navigation.Key, readAvailability: availability)?.Event.Id == target.Id, "The shared planner did not select the original reachable gateway.");
        Check(JsonSerializer.Serialize(session.Capture()) == before && bytes.SequenceEqual(File.ReadAllBytes(path)), "Gateway contact planning changed native execution, RNG, progress or the source fixture.");
        Console.WriteLine($"PASS original full gateway segment and native approach {action.Horizontal},{action.Vertical}; shared topology selection and source purity.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
