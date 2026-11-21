using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeFieldEntryAdviceTests
{
    private sealed record Fixture(Dictionary<ushort, Ff7AutoPlayFieldTopology> Topology, Ff7AutoPlayState State, Ff7AutoPlayObservation Observation, Ff7Actor Player);

    internal static void Run()
    {
        Fixture source = Create(690);
        JsonSerializerOptions options = new() { IncludeFields = true };
        string before = JsonSerializer.Serialize(source, options);
        var fromUpper = Find(source);
        Check(fromUpper.Target?.Id == "gateway:0" && !fromUpper.Limited, "Entry-qualified advice lost the necessary return to another component of the original field.");
        Check(before == JsonSerializer.Serialize(source, options), "Field-entry advice executed effects or changed native geometry, state or history.");
        Fixture returning = Create(691);
        var fromCave = Find(returning);
        Check(fromCave.Target?.Id == "gateway:1" && !fromCave.Limited, "The cave return chose the old component instead of the entry leading to the native LINE-MAP exit.");
        Check(Find(returning, _ => false).Target is null, "An unavailable current gateway received entry-qualified advice.");
        Check(Ff7AutoPlayPolicy.FindFieldEntryDestinationAdvice(source.State, source.Observation, 692, source.Player, [2], field => source.Topology.GetValueOrDefault(field), target => target.GatewayIndex is not null).Target is null, "A currently blocked reentry component authorized the remote LINE-MAP route.");
        var missing = Ff7AutoPlayPolicy.FindFieldEntryDestinationAdvice(source.State, source.Observation, 692, source.Player, [0], field => field == 691 ? null : source.Topology.GetValueOrDefault(field), target => target.GatewayIndex is not null);
        Check(missing.Target is null && missing.Limited, "An unavailable topology resource was reported as complete route exhaustion.");

        foreach (Func<Ff7AutoPlayFieldTopology, Ff7AutoPlayFieldTopology> invalidate in new Func<Ff7AutoPlayFieldTopology, Ff7AutoPlayFieldTopology>[]
        {
            topology => topology with { Lines = [] },
            topology => topology with { Lines = [topology.Lines.Single() with { Line = topology.Lines.Single().Line with { Enabled = 0 } }] },
            topology => topology with { Lines = [topology.Lines.Single() with { Line = topology.Lines.Single().Line with { X1 = 920, X2 = 930 } }] },
            topology => topology with { Lines = [topology.Lines.Single() with { Transition = topology.Lines.Single().Transition with { X = 500 } }] }
        })
        {
            Fixture invalid = Create(690);
            invalid.Topology[690] = invalidate(invalid.Topology[690]);
            var result = Find(invalid);
            Check(result.Target is null && !result.Limited, "Missing, disabled, disconnected or invalid literal LINE-MAP metadata produced a route.");
        }
        Fixture invalidLanding = Create(690);
        invalidLanding.Topology[690] = invalidLanding.Topology[690] with { Gateways = [invalidLanding.Topology[690].Gateways[0] with { DestinationTriangle = 9 }] };
        Check(Find(invalidLanding).Target is null, "An invalid intermediate triangle authorized a field route.");
        Fixture cycle = Create(690);
        cycle.Topology[690] = cycle.Topology[690] with { Lines = [] };
        Check(Find(cycle) is { Target: null, Limited: false }, "A finite entry cycle did not terminate with honest exhaustion.");
        CheckInvalidEntryDoesNotConsumeNode();
        Console.WriteLine("Entry-qualified field advice preserves native component reentry and finite route guards without executing scripts.");
    }

    private static void CheckInvalidEntryDoesNotConsumeNode()
    {
        Fixture source = Create(691);
        Ff7AutoPlayFieldTopology topology = source.Topology[691];
        source.Topology[691] = topology with
        {
            Gateways = [topology.Gateways[0] with { DestinationTriangle = 1, DestinationX = 999 }, topology.Gateways[1]]
        };
        Check(Find(source).Target?.Id == "gateway:1", "An invalid first incoming point consumed a field/triangle node before the valid entry could be considered.");
    }

    private static (Ff7AutoPlayTarget? Target, bool Limited) Find(Fixture source, Func<Ff7AutoPlayTarget, bool>? canSelect = null)
    {
        return Ff7AutoPlayPolicy.FindFieldEntryDestinationAdvice(source.State, source.Observation, 692, source.Player, [0], field => source.Topology.GetValueOrDefault(field), canSelect ?? (target => target.GatewayIndex is not null));
    }

    private static Fixture Create(int current)
    {
        PcWalkmeshTriangle[] islands = Enumerable.Range(0, 3).Select(index => new PcWalkmeshTriangle(index, new(index * 300, 0, 0), new(index * 300 + 100, 0, 0), new(index * 300, 100, 0), -1, -1, -1)).ToArray();
        PcWalkmeshTriangle[] cave = [new(0, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), -1, -1, -1)];
        PcFieldGateway outbound = new(new(620, 20, 0), new(630, 20, 0), 20, 20, 0, 691, 0, 0);
        PcFieldGateway wrongReturn = new(new(20, 20, 0), new(30, 20, 0), 620, 20, 2, 690, 0, 0);
        PcFieldGateway rightReturn = new(new(60, 20, 0), new(70, 20, 0), 320, 20, 1, 690, 0, 0);
        Ff7AutoPlayLineTransition exit = new(new(0, 10, 320, 20, 0, 330, 20, 0), 4, new(692, 20, 20, 0, 64));
        Dictionary<ushort, Ff7AutoPlayFieldTopology> topology = new()
        {
            [690] = new(islands, [outbound], [exit]),
            [691] = new(cave, [wrongReturn, rightReturn], []),
            [692] = new(cave, [], [])
        };
        Ff7AutoPlayTarget[] targets = current == 690
            ? [Gateway(0, 691), new("line", 10, 4, Ff7AutoPlayTargetKind.LineTouch, "line-condition", false, default, DestinationField: 692, ChangesField: true)]
            : [Gateway(0, 690), Gateway(1, 690)];
        Ff7AutoPlayObservation observation = new($"ff7:{current}:program", "program", 1, targets, true, true) { NavigationKey = "current-component" };
        Ff7Actor player = new() { Slot = 0, Entity = 1, Model = 0, Triangle = current == 690 ? 2 : 0, X4096 = (current == 690 ? 620 : 40) << 12, Y4096 = 20 << 12, CollisionRadius = 10 };
        return new(topology, new() { DestinationField = 692 }, observation, player);
    }

    private static Ff7AutoPlayTarget Gateway(int index, int destination)
    {
        return new($"gateway:{index}", -1, -1, Ff7AutoPlayTargetKind.Gateway, $"gateway-condition:{index}", false, default, GatewayIndex: index, DestinationField: destination);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
