using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeAheadPushPrerequisiteTests
{
    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), -1, 1, -1),
            new(1, new(100, 0, 0), new(100, 100, 0), new(0, 100, 0), 2, -1, 0),
            new(2, new(100, 0, 0), new(200, 0, 0), new(100, 100, 0), -1, 3, 1),
            new(3, new(200, 0, 0), new(200, 100, 0), new(100, 100, 0), 4, -1, 2),
            new(4, new(200, 0, 0), new(300, 0, 0), new(200, 100, 0), -1, 5, 3),
            new(5, new(300, 0, 0), new(300, 100, 0), new(200, 100, 0), -1, -1, 4)
        ];
        byte[] blocked = [0];
        Ff7Actor player = new() { Slot = 0, Entity = 1, Model = 0, Triangle = 0, X4096 = 20 << 12, Y4096 = 20 << 12, CollisionRadius = 10 };
        Vector3 goal = new(240, 20, 0);
        Ff7Line line = new(0, 4, 150, 0, 0, 150, 100, 0);
        FieldActorJumpLanding jump = new(1, 10, new(220, 20, 4, 17), true);
        Ff7AutoPlayTarget target = new("forward", 4, 2, Ff7AutoPlayTargetKind.LinePush, "condition", false, Vector3.Zero, MayChangeNavigation: true) { PossibleJumpLandings = [jump] };
        int[] Read(Ff7AutoPlayTarget? candidate = null, Ff7Line? actualLine = null, byte[]? mask = null) => Ff7AutoplayDriver.ReadAheadNavigationLinePrerequisites(mesh, mask ?? blocked, player, goal, 4, [candidate ?? target], [actualLine ?? line]);
        string before = JsonSerializer.Serialize(new { mesh, blocked, player, goal, line, target });
        int distance = Ff7Session.DistanceToSegment(new(line.X1, line.Y1, line.Z1), new(line.X2, line.Y2, line.Z2), new(20, 20, 0), out _);
        Check(distance >= player.CollisionRadius * player.CollisionRadius, "The authored horizon fixture accidentally began inside immediate LINE contact.");
        Check(Read().SequenceEqual(new[] { 4 }), "A real forward jump LINE across the selected directed route was invisible beyond immediate contact radius.");
        foreach (Ff7AutoPlayTarget invalid in new[]
        {
            target with { AnalysisComplete = false },
            target with { Kind = Ff7AutoPlayTargetKind.LineTouch },
            target with { MayChangeNavigation = false },
            target with { ChangesField = true },
            target with { ChangesStory = true },
            target with { PossibleJumpLandings = [] },
            target with { PossibleJumpLandings = [jump with { Entity = 2 }] },
            target with { PossibleJumpLandings = [jump with { Literal = false }] },
            target with { PossibleJumpLandings = [jump with { Command = jump.Command with { Duration = 0 } }] },
            target with { PossibleJumpLandings = [jump with { Command = jump.Command with { X = 20, Y = 20, Triangle = 0 } }] },
            target with { PossibleJumpLandings = [jump with { Command = jump.Command with { X = 500 } }] }
        })
        {
            Check(Read(invalid).Length == 0, "An incomplete, unrelated, nonliteral, reverse or invalid landing acquired forward prerequisite advice.");
        }
        Check(Read(actualLine: line with { Enabled = 0 }).Length == 0, "A disabled LINE acquired horizon advice.");
        Check(Read(actualLine: line with { Entity = 5 }).Length == 0, "A different LINE owner acquired the target's forward jump.");
        Check(Read(actualLine: line with { Y2 = 10 }).Length == 0, "A reachable but unrelated short LINE segment was mistaken for an intersection with the selected route.");
        Check(Read(actualLine: line with { Z1 = 300, Z2 = 300 }).Length == 0, "A native height-incompatible LINE acquired horizon advice.");
        Check(Read(actualLine: line with { Z1 = 200, Z2 = 200 }).Length == 0, "A different floor inside the broad native height band bypassed strict collision-radius contact.");
        Check(Read(mask: [16]).Length == 0 && Read(mask: [4]).Length == 0, "A blocked landing or directed route acquired horizon advice.");
        Check(Ff7AutoplayDriver.ReadAheadNavigationLinePrerequisites(mesh, blocked, player, goal, 4, [target], []).Length == 0, "A missing actual LINE object was invented by route advice.");
        Ff7AutoPlayTarget later = target with { Id = "later", Entity = 9 };
        Ff7AutoPlayTarget earlier = target with { Id = "earlier", Entity = 17 };
        Ff7Line laterLine = line with { Entity = 9, X1 = 175, X2 = 175 };
        Ff7Line earlierLine = line with { Entity = 17, X1 = 110, X2 = 110 };
        string orderingBefore = JsonSerializer.Serialize(new { later, earlier, laterLine, earlierLine });
        int[] ordered = Ff7AutoplayDriver.ReadAheadNavigationLinePrerequisites(mesh, blocked, player, goal, 4, [later, earlier], [laterLine, earlierLine]);
        Check(ordered.SequenceEqual(new[] { 17, 9 }), "A later low-entity Push preempted the earlier protected crossing along the selected route.");
        Check(Ff7AutoplayDriver.ReadAheadNavigationLinePrerequisites(mesh, blocked, player, goal, 4, [earlier, later], [earlierLine, laterLine]).SequenceEqual(ordered), "Crossing order depended on target or LINE enumeration instead of actual directed-route position.");
        Check(orderingBefore == JsonSerializer.Serialize(new { later, earlier, laterLine, earlierLine }), "Crossing-order advice mutated native LINE geometry or target metadata.");
        Check(before == JsonSerializer.Serialize(new { mesh, blocked, player, goal, line, target }), "Horizon advice changed native geometry, actor pose, variables or target metadata.");
        Console.WriteLine("Ahead Push advice finds only reachable forward native jump sources that intersect the selected route.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
