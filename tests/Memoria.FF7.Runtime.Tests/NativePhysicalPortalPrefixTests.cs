using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class NativePhysicalPortalPrefixTests
{
    internal static void Run()
    {
        CheckDirectedPortalPrefix();
        CheckPrefixRejections();
        CheckFinalContactAndDefaultSearch();
        Console.WriteLine("Native physical portal prefix checks passed.");
    }

    private static Ff7Actor Source => new() { Slot = 0, Entity = 0, Model = 0, Triangle = 0, MoveSpeed = 1 };

    private static Ff7NativeMoveResult PreviewDetour(Ff7Actor actor, Ff7AutoplayInput input)
    {
        if (input != new Ff7AutoplayInput(-1, 0) || actor.Triangle >= 3)
        {
            return new(actor, false, false, []);
        }
        int next = actor.Triangle + 1;
        int x = next == 3 ? 8192 : -4096 * next;
        return new(actor with { X4096 = x, Triangle = next }, false, true, []);
    }

    private static Ff7Actor Replay(Ff7Actor source, Ff7AutoPlayPhysicalRoute route)
    {
        foreach (Ff7AutoplayInput input in route.Inputs)
        {
            Ff7NativeMoveResult step = PreviewDetour(source, input);
            Check(step.Moved, "A reconstructed prefix contained a nonmoving fixture edge.");
            source = step.Actor;
        }
        return source;
    }

    private static void CheckDirectedPortalPrefix()
    {
        Ff7Actor source = Source;
        string before = JsonSerializer.Serialize(source);
        bool Goal(Ff7Actor actor) => actor.Triangle == 3 && actor.X4096 == 8192;
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = new(source, -Vector3.UnitX, triangle => 3 - triangle, PreviewDetour, (_, _) => true, Goal, allowRouteProgress: true);
        Ff7AutoPlayPhysicalRoute empty = cursor.Resume(new(0, TimeSpan.MaxValue));
        Check(empty.Status == Ff7AutoPlayPhysicalSearchStatus.Pending && empty.Expanded == 0 && empty.Inputs.Length == 0, "A zero quantum published an unvalidated prefix.");
        Ff7AutoPlayPhysicalRoute route = cursor.Resume(new(1, TimeSpan.MaxValue));
        Ff7Actor end = Replay(source, route);
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && route.Inputs.Length == 1 && end.Triangle == 1 && !Goal(end), "Directed portal progress required the final semantic contact instead of returning its real prefix.");
        Check(Math.Abs(end.X4096 - 8192) > Math.Abs(source.X4096 - 8192), "The detour fixture did not move away from the final contact.");
        Check(JsonSerializer.Serialize(source) == before, "Pure prefix search or replay mutated its source actor.");
        Check(ReferenceEquals(route, cursor.Resume(new(1, TimeSpan.MaxValue))), "A completed prefix resumed or changed its published route.");
    }

    private static void CheckPrefixRejections()
    {
        void Reject(Ff7Actor destination, bool moved, bool allowed, Func<int, int> remaining, string reason)
        {
            Ff7NativeMoveResult Preview(Ff7Actor actor, Ff7AutoplayInput input)
            {
                return actor.Triangle == 0 && input == new Ff7AutoplayInput(1, 0) ? new(destination, false, moved, []) : new(actor, false, false, []);
            }
            Ff7AutoplayDriver.PhysicalSearchCursor cursor = new(Source, Vector3.UnitX, remaining, Preview, (_, _) => allowed, _ => false, allowRouteProgress: true);
            Ff7AutoPlayPhysicalRoute route = cursor.Resume(new(8, TimeSpan.MaxValue));
            Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Exhausted && route.Inputs.Length == 0, reason);
        }

        Reject(Source with { Triangle = 1 }, true, true, triangle => 1 - triangle, "A triangle relabel without XYZ movement became a route prefix.");
        Reject(Source with { Triangle = 1, Direction = 128 }, false, true, triangle => 1 - triangle, "A rejected native step or heading change became a route prefix.");
        Reject(Source with { Triangle = 1, X4096 = 4096 }, false, true, triangle => 1 - triangle, "A step without the native Moved receipt became a prefix.");
        Reject(Source with { Triangle = 1, X4096 = 4096 }, true, false, triangle => 1 - triangle, "A prefix bypassed the protected movement validator.");
        Reject(Source with { Triangle = 1, X4096 = 4096 }, true, true, _ => 0, "Euclidean progress within the final route triangle bypassed the semantic contact predicate.");
        Reject(Source with { Triangle = 1, X4096 = 4096 }, true, true, _ => 2, "A closer point without directed remaining-route progress became a prefix.");
    }

    private static void CheckFinalContactAndDefaultSearch()
    {
        bool Goal(Ff7Actor actor) => actor.Triangle == 3;
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = new(Source, -Vector3.UnitX, triangle => 3 - triangle, PreviewDetour, (_, _) => true, Goal);
        Check(cursor.Resume(new(1, TimeSpan.MaxValue)).Status == Ff7AutoPlayPhysicalSearchStatus.Pending, "The default semantic search returned a local portal prefix.");
        Ff7AutoPlayPhysicalRoute route = cursor.Resume(new(8, TimeSpan.MaxValue));
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && route.Inputs.Length == 3 && Goal(Replay(Source, route)), "The default search lost the full actual semantic goal or its retained quantum.");
        cursor = new(Source, -Vector3.UnitX, _ => 0, PreviewDetour, (_, _) => true, Goal, allowRouteProgress: true);
        route = cursor.Resume(new(8, TimeSpan.MaxValue));
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && route.Inputs.Length == 3 && Goal(Replay(Source, route)), "A source already in the final route region could not reach its exact semantic contact.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
