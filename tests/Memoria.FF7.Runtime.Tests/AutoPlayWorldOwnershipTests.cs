using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldOwnershipTests
{
    internal static void Run()
    {
        Ff7AutoPlayWorldDispatchReceipt receipt = new("world", "model", 10, 1, 0, [], Model: 6, OwnerPriority: 2, Owners: [new(6, 10, 1, 2), new(0, 20, 1, 3)]);
        WorldEntitySnapshot system = new(-1, 0, 0, 0, false, -1, []);
        WorldEntitySnapshot parent = new(6, 1, 0, 0, false, 0, []);
        WorldEntitySnapshot child = new(0, 20, 0, 3, false, -1, [new(1, 0, 0)]);
        WorldSnapshot state = new(1, "ownership", 0, 1, false, new byte[64], [], system, [parent, child]);
        Check(!Ff7AutoPlayPolicy.HasWorldOwnershipReturned(receipt, state), "A returned parent discarded its queued native child before that child returned control.");
        Ff7AutoPlayWorldDispatchReceipt restored = JsonSerializer.Deserialize<Ff7AutoPlayWorldDispatchReceipt>(JsonSerializer.Serialize(receipt))!;
        Check(!Ff7AutoPlayPolicy.HasWorldOwnershipReturned(restored, state), "Cold restoration lost the accepted native descendant owner.");
        WorldSnapshot returned = state with { Models = [parent, child with { Calls = [], Priority = 0, ProgramCounter = 1 }, new(25, 90, 0, 3, false, -1, [new(2, 0, 0)])] };
        Check(Ff7AutoPlayPolicy.HasWorldOwnershipReturned(receipt, returned), "An unrelated ambient owner prevented completion of the returned native closure.");
        Check(Ff7AutoPlayPolicy.HasWorldOwnershipReturned(receipt, state with { Models = [parent] }), "Native removal retained an execution owner which no longer exists.");
        WorldScriptDispatchObservation nested = new(6, 11, 0, 16, 20, 1, 3, SourcePriority: 2, SourceCallDepth: 1);
        Check(Ff7AutoPlayPolicy.OwnsWorldDispatchSource(receipt, nested), "A native request lost its exact admitted source frame.");
        Check(!Ff7AutoPlayPolicy.OwnsWorldDispatchSource(receipt, nested with { SourcePriority = 0 }), "The parent's later ambient callback inherited its previous request ownership.");
        Check(!Ff7AutoPlayPolicy.OwnsWorldDispatchSource(receipt, nested with { SourceCallDepth = 0 }), "A returned caller frame retained a descendant admission.");
        Check(!Ff7AutoPlayPolicy.OwnsWorldDispatchSource(receipt, nested with { SourceModel = 25 }), "A different model inherited the admitted caller's frame.");
        Ff7AutoPlayWorldDispatchReceipt legacy = new("world", "mesh", 40, 0, 0, []);
        Check(!Ff7AutoPlayPolicy.HasWorldOwnershipReturned(legacy, state with { System = system with { ProgramCounter = 40, Priority = 3 } }), "An older mesh receipt lost its required system owner.");
        Check(Ff7AutoPlayPolicy.HasWorldOwnershipReturned(legacy, state), "An older returned mesh receipt acquired a required descendant marker.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
