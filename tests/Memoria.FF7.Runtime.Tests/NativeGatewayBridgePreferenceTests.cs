using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeGatewayBridgePreferenceTests
{
    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh = Enumerable.Range(0, 5).Select(index => new PcWalkmeshTriangle(index, new(index * 300, 0, 0), new(index * 300 + 100, 0, 0), new(index * 300, 100, 0), -1, -1, -1)).ToArray();
        mesh[0] = mesh[0] with { AdjacentAB = 1 };
        mesh[1] = mesh[1] with { AdjacentAB = 0 };
        Ff7Actor player = new() { Slot = 0, Entity = 1, Model = 0, Triangle = 0, X4096 = 20 << 12, Y4096 = 20 << 12, CollisionRadius = 10 };
        PcFieldGateway gateway = new(new(1220, 20, 0), new(1230, 20, 0), 0, 0, 0, 200, 0, 0);
        Ff7AutoPlayTarget Jump(string id, int entity, int triangle, int x) => new(id, entity, 2, Ff7AutoPlayTargetKind.LinePush, id + "-condition", false, Vector3.Zero, MayChangeNavigation: true) { PossibleJumpLandings = [new(1, 10, new((short)x, 20, (short)triangle, 17), true)] };
        Ff7AutoPlayTarget reverse = Jump("reverse", 4, 2, 620);
        Ff7AutoPlayTarget side = Jump("side", 5, 3, 920);
        Ff7AutoPlayTarget back = Jump("back", 6, 1, 320);
        Ff7AutoPlayTarget sideBack = Jump("side-back", 7, 1, 320);
        Ff7AutoPlayTarget direct = Jump("direct", 9, 4, 1220);
        Ff7AutoPlayTarget tie = direct with { Id = "tie", Entity = 10, ConditionFingerprint = "tie-condition" };
        Ff7AutoPlayTarget[] targets = [reverse, side, back, sideBack, direct, tie];
        Ff7Line[] lines = [new(0, 4, 20, 20, 0, 30, 20, 0), new(1, 5, 20, 60, 0, 30, 60, 0), new(2, 6, 620, 20, 0, 630, 20, 0), new(3, 7, 920, 20, 0, 930, 20, 0), new(4, 9, 20, 40, 0, 30, 40, 0), new(5, 10, 20, 40, 0, 30, 40, 0)];
        byte[] blocked = [0];
        int? Length(Ff7AutoPlayTarget first, IReadOnlyList<Ff7AutoPlayTarget>? candidates = null, byte[]? mask = null, IReadOnlyList<Ff7Line>? actualLines = null) => Ff7AutoPlayPolicy.ReadFieldGatewayComponentRouteLength(mesh, mask ?? blocked, player, gateway, first, candidates ?? targets, actualLines ?? lines);
        string before = JsonSerializer.Serialize(new { mesh, player, gateway, targets, lines, blocked });
        Ff7AutoPlayTarget placement = direct with { Kind = Ff7AutoPlayTargetKind.LineCross, PossibleJumpLandings = [], PlacementLandings = [new(1, 10, new(1220, 20, 0, 4))] };
        Check(Length(placement, [placement]) == 1, "An exact player placement could not connect the disconnected gateway component.");
        Ff7AutoPlayTarget nonplayer = placement with { PlacementLandings = [new(2, 10, new(1220, 20, 0, 4))] };
        Check(Length(placement, [placement], mask: [16]) is null && Length(nonplayer, [nonplayer]) is null, "A blocked or nonplayer placement authorized a route.");
        Check(Length(direct) == 1, "A direct native bridge was charged for triangle edges instead of one admission.");
        Check(Length(reverse) == 3 && Length(side) == 3, "Returning to another triangle in the main component lost the valid three-bridge route.");
        Check(Length(reverse, [reverse, back with { PossibleJumpLandings = reverse.PossibleJumpLandings }, direct]) is null, "A closed reverse cycle invented a path back to the main component.");
        Check(Length(reverse, mask: [4]) is null && Length(reverse, actualLines: []) is null, "Blocked landing or absent native contact authorized a bridge chain.");
        Check(Length(reverse with { AnalysisComplete = false }) is null && Length(reverse with { PossibleJumpLandings = [reverse.PossibleJumpLandings[0] with { Entity = 2 }] }) is null, "Incomplete or nonplayer effects received a bridge cost.");
        Check(Ff7AutoPlayPolicy.CanEstablishFieldGatewayComponentRoute(mesh, blocked, player, gateway, reverse, targets, lines), "The existing boolean route contract changed when adding shortest-bridge advice.");
        Check(before == JsonSerializer.Serialize(new { mesh, player, gateway, targets, lines, blocked }), "Bridge advice changed native geometry, inputs or source metadata.");

        Ff7AutoPlayTarget gateTarget = new("gateway", -1, -1, Ff7AutoPlayTargetKind.Gateway, "gateway-condition", false, Vector3.Zero, GatewayIndex: 0, DestinationField: 200);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [.. targets, gateTarget], true, true) { NavigationKey = "main" };
        Ff7AutoPlayState state = new() { DestinationField = 300 };
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent[] events = observation.Events.Select(target => new AutoPlayEvent(target.Id, AutoPlayEventKind.Interaction, ConditionSignature: target.ConditionFingerprint)).ToArray();
        planner.Observe(new(observation.VisitId, observation.ProgramIdentity), events);
        planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, events);
        string history = JsonSerializer.Serialize(state.Coverage);
        int? Cost(Ff7AutoPlayTarget target, Ff7AutoPlayTarget selected) => selected.Id == gateTarget.Id ? Length(target) : null;
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, field => field == 200 ? [300] : [], target => target.Id != gateTarget.Id, (target, selected) => Cost(target, selected) is not null, readGatewayPrerequisiteLength: Cost);
        Check(state.NavigationGoal?.EventId == direct.Id, "An earlier reverse or side bridge displaced the shortest native gateway prerequisite, or a tied later source displaced its existing order.");
        Check(history == JsonSerializer.Serialize(state.Coverage) && state.DestinationField == 300, "Shortest bridge preference credited unexecuted events or changed explicit intent.");
        Console.WriteLine("Native gateway advice ranks complete current-contact chains by real bridge admissions and preserves native execution.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
