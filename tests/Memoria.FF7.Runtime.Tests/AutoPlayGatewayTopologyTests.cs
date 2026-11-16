using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

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
        Check(Ff7AutoplayDriver.ReadGatewayInteriorTriangle(mesh, remote) == 2, "Gateway topology did not reuse the driver's exact interior selector.");
        Check(!Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [0], 0, remote, out int destination) && destination == 2, "A disconnected gateway was admitted from a foreign native component.");
        Check(Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [0], 2, remote, out _), "The gateway's actual native component was rejected.");
        Check(Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [0], 1, local, out _), "An ordinary connected native source was rejected.");
        Check(!Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [1], 1, local, out _), "A blocked destination was ignored by the exact raw route contract.");
        Check(Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, [2], 1, local, out _), "A blocked current source was replaced by an assumed unblocked component root.");

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
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
