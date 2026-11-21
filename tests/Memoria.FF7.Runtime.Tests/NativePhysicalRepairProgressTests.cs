using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class NativePhysicalRepairProgressTests
{
    private static Ff7Actor Source => new() { Slot = 0, Entity = 0, Model = 0, Triangle = 0, MoveSpeed = 1 };

    internal static void Run()
    {
        Ff7Actor source = Source;
        string before = JsonSerializer.Serialize(source);
        int Remaining(int triangle) => triangle == 0 ? 2 : triangle == 1 ? 1 : 0;
        Ff7NativeMoveResult Repair(Ff7Actor actor, Ff7AutoplayInput input)
        {
            return actor.Triangle == 0 && input == new Ff7AutoplayInput(1, 0) ? new(actor with { Triangle = 1 }, false, false, []) : new(actor, false, false, []);
        }
        Check(!Ff7AutoplayDriver.HasPhysicalRouteProgress(source, source with { Triangle = 1 }, Vector3.UnitX, Remaining), "A triangle-only native repair counted as movement progress.");
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = new(source, Vector3.UnitX, Remaining, Repair, (_, _) => true);
        Check(cursor.Resume(new(8, TimeSpan.MaxValue)) is { Status: Ff7AutoPlayPhysicalSearchStatus.Exhausted, Inputs.Length: 0 }, "A native rejected-step repair terminated ordinary route search.");

        Ff7NativeMoveResult Advance(Ff7Actor actor, Ff7AutoplayInput input)
        {
            return actor.Triangle == 0 && input == new Ff7AutoplayInput(1, 0) ? new(actor with { Triangle = 1, X4096 = 4096 }, false, true, []) : new(actor, false, false, []);
        }
        cursor = new(source, Vector3.UnitX, Remaining, Advance, (_, _) => true);
        Check(cursor.Resume(new(8, TimeSpan.MaxValue)) is { Status: Ff7AutoPlayPhysicalSearchStatus.Progress, Inputs.Length: 1 }, "A genuine native moved step lost its directed route-progress receipt.");
        Ff7NativeMoveResult Unadmitted(Ff7Actor actor, Ff7AutoplayInput input)
        {
            return actor.Triangle == 0 && input == new Ff7AutoplayInput(1, 0) ? new(actor with { Triangle = 1, X4096 = 4096 }, false, false, []) : new(actor, false, false, []);
        }
        cursor = new(source, Vector3.UnitX, Remaining, Unadmitted, (_, _) => true);
        Check(cursor.Resume(new(8, TimeSpan.MaxValue)).Status == Ff7AutoPlayPhysicalSearchStatus.Exhausted, "Changed coordinates without native Moved authorization terminated default search.");

        Ff7NativeMoveResult RepairThenAdvance(Ff7Actor actor, Ff7AutoplayInput input)
        {
            if (actor.Triangle == 0)
            {
                return Repair(actor, input);
            }
            return actor.Triangle == 1 && input == new Ff7AutoplayInput(0, 1) ? new(actor with { Triangle = 2, Y4096 = 4096 }, false, true, []) : new(actor, false, false, []);
        }
        cursor = new(source, Vector3.UnitY, Remaining, RepairThenAdvance, (_, _) => true);
        Ff7AutoPlayPhysicalRoute route = cursor.Resume(new(8, TimeSpan.MaxValue));
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && route.Inputs.SequenceEqual(new[] { new Ff7AutoplayInput(1, 0), new Ff7AutoplayInput(0, 1) }), "The native repair intermediate was discarded before a genuinely moving successor.");

        Ff7NativeMoveResult Heading(Ff7Actor actor, Ff7AutoplayInput input)
        {
            return actor.Direction == 0 && input == new Ff7AutoplayInput(1, 0) ? new(actor with { Direction = 64 }, false, false, []) : new(actor, false, false, []);
        }
        cursor = new(source, Vector3.Zero, _ => 0, Heading, (_, _) => true, actor => actor.Direction == 64);
        Check(cursor.Resume(new(8, TimeSpan.MaxValue)) is { Status: Ff7AutoPlayPhysicalSearchStatus.Progress, Inputs.Length: 1 }, "An exact semantic heading goal was confused with default route movement progress.");
        Check(before == JsonSerializer.Serialize(source), "Pure native search mutated its source actor.");
        Console.WriteLine("Native repair intermediates and actual movement-progress receipts passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
