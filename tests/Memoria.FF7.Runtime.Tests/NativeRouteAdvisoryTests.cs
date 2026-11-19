using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeRouteAdvisoryTests
{
    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            Triangle(0, 0, 0, 1),
            Triangle(1, 0, 10, 0, 2),
            Triangle(2, 10, 10, 1, 3),
            Triangle(3, 10, 0, 2),
            Triangle(4, 100, 0)
        ];
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, Triangle = 0, MoveSpeed = 16 };
        Vector3 target = new(10, 0, 0);
        (Vector3 Point, int Triangle)[] contacts = [(target, 3)];
        float Score(Ff7Actor source) => Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [0], source, target, contacts, 256);
        Check(Near(Score(actor), 30), "A U-shaped native route was ranked as the straight Euclidean gap.");
        Ff7Actor detour = actor with { Y4096 = 4096 };
        Ff7Actor straight = actor with { X4096 = 4096 };
        Check(Score(detour) < Score(straight) && Vector3.Distance(new(0, 1, 0), target) > Vector3.Distance(new(1, 0, 0), target), "Existing route topology failed to prioritize the required initial detour.");
        Check(Near(Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [0], actor, target, [(new(1, 0, 0), 0), (target, 3)], 256), 1), "The closest finite current contact was not selected or a same-triangle contact acquired a centroid detour.");
        Check(Near(Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [0], actor, target, [(new(10, 1, 0), 3)], 256), 31), "A moved current contact retained the old target midpoint distance.");
        Check(Near(Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [2], actor, target, contacts, 256), 10), "A blocked native route was used instead of advisory-only Euclidean fallback.");
        Check(Near(Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [0], actor, target, contacts, 256, ownedPortals: new HashSet<(int, int)> { (1, 2) }), 10), "An owned forbidden portal was ignored in advisory topology.");
        Check(Near(Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [0], actor, target, contacts, 256, boundaries: new HashSet<int> { 3 }), 10), "A spatial boundary was ignored by the route advisory.");
        Check(Near(Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [0], actor, target, contacts, 256, boundaries: new HashSet<int> { 3 }, admittedTriangle: 3), 30), "The actual admitted destination exception was lost.");
        Check(Near(Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [1], actor, target, contacts, 256), 30), "An occupied native source triangle could not use the existing source exception.");
        foreach ((Vector3 Point, int Triangle)[] unavailable in new[] { Array.Empty<(Vector3, int)>(), new[] { (new Vector3(100, 0, 0), 4) } })
        {
            Check(Near(Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [0], actor, target, unavailable, 256), 10), "No current/disconnected contact pruned the native graph instead of falling back.");
        }
        Check(Near(Ff7AutoplayDriver.ReadNativeRouteAdvisory(mesh, [0], actor, target, contacts, 4096), 30f / 16), "Advisory cost used scripted actor speed instead of actual controlled speed.");
        CheckWalkingProgressOrdering();
        Console.WriteLine("Native route advisory U-detour, current contact, topology, fallback and speed checks passed.");
    }

    private static void CheckWalkingProgressOrdering()
    {
        PcWalkmeshTriangle[] corridor = [Triangle(0, 0, 0, 1), Triangle(1, 12, 0, 0, 2), Triangle(2, 24, 0, 1)];
        Ff7Actor source = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, Triangle = 0 };
        Ff7Actor walked = source with { X4096 = 16 * 4096, Triangle = 1 };
        Vector3 target = new(24, 0, 0);
        (Vector3 Point, int Triangle)[] contacts = [(target, 2)];
        float Cost(Ff7Actor actor, int callbacks, int nativeSpeed) => callbacks + Ff7AutoplayDriver.ReadNativeRouteAdvisory(corridor, [0], actor, target, contacts, nativeSpeed);
        Check(Cost(walked, 4, 1024) < Cost(source, 1, 1024), "Walking-only corridor progress was outweighed by an earlier idle node when using its native callback units.");
        Check(Cost(walked, 4, 4096) > Cost(source, 1, 4096), "The authored case did not distinguish the previous faster movement scale.");
    }

    private static PcWalkmeshTriangle Triangle(int index, float x, float y, int first = -1, int second = -1)
    {
        return new(index, new(x - 2, y - 2, 0), new(x + 2, y - 2, 0), new(x, y + 4, 0), checked((short)first), checked((short)second), -1);
    }

    private static bool Near(float left, float right) => Math.Abs(left - right) < 0.001f;

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
