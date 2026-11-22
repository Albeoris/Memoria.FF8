using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeBoardingSuffixTests
{
    internal static void Run()
    {
        WorldEntitySnapshot system = new(-1, 1, 0, 0, false, -1, []);
        WorldSnapshot vm = new(1, "program", 0, 1, false, [], [], system, []);
        Ff7WorldEntity rider = new(0, 20, 0, 20, TerrainResolved: true);
        Ff7WorldEntity transport = new(3, 200, 0, 200, TerrainResolved: true);
        Ff7WorldProcess world = new(0, 0, 0, vm, [rider, transport], 0, true, 3, 0, null, [], 0, new(), 0, 0, MainState: 1, Initialized: true, Input: new()) { Vehicle = new([]) };
        WorldScriptAnalysis board = new(true, null, 1, 2, [], [], [new(2, WorldOperation.BoardTransport, null, 3)], [2]);
        Ff7AutoPlayWorldModelScriptObservation contact = new("contact", 3, 3, transport, board, "branch", "geometry", true, null);
        Ff7AutoPlayWorldScriptObservation destination = new("destination", 3, [], board with { Header = 0x8000, Entry = 4 }, "destination-branch", "destination-geometry", [], true, null);
        string before = JsonSerializer.Serialize(new { world, contact, destination });
        string key = Ff7AutoPlayPolicy.ReadBoardingSuffixFingerprint(world, contact, destination);
        Ff7WorldProcess moved = world with { Tick = 99, Vm = vm with { Tick = 99 }, Entities = [rider with { X = 90, Z = 100, Direction = 256, TurnOffset = 128, Terrain = 5 }, transport], Input = new(Held: 0x20, PreviousX: 90, PreviousZ: 100, TerrainScript: 4) { GroundTriangle = new(0, 0, 1) } };
        Check(key == Ff7AutoPlayPolicy.ReadBoardingSuffixFingerprint(moved, contact, destination), "Normal rider approach movement, heading, ground cache or tick invalidated the unchanged boarding suffix.");
        foreach (Ff7WorldProcess changed in new[]
        {
            world with { WorldId = 1 }, world with { WorldMapState = 1 }, world with { ViewMode = 2 },
            world with { Initialized = false }, world with { MovementEnabled = false }, world with { ControlLock = 1 }, world with { MainState = 2 },
            world with { Vm = vm with { ProgramIdentity = "other" } },
            world with { ControlledModel = 1, Entities = [rider with { Model = 1 }, transport] },
            world with { Entities = [rider with { AttachedModel = 3 }, transport] },
            world with { Entities = [rider with { Flags = 128 }, transport] },
            world with { Entities = [rider, transport with { X = 201 }] },
            world with { Entities = [rider, transport with { Direction = 256 }] },
            world with { Entities = [rider, transport with { Flags = 128 }] },
            world with { Entities = [rider, transport with { Terrain = 1 }] },
            world with { Vehicle = world.Vehicle! with { RideStack = [1] } }
        })
        {
            Check(key != Ff7AutoPlayPolicy.ReadBoardingSuffixFingerprint(changed, contact, destination), "Changed allocation, rider role, transport physical state or vehicle contract retained an old boarding suffix.");
        }
        foreach (Ff7AutoPlayWorldModelScriptObservation changed in new[] { contact with { Id = "other" }, contact with { PredicateFingerprint = "changed" }, contact with { GeometryFingerprint = "changed" }, contact with { Analysis = board with { Entry = 3 } } })
        {
            Check(key != Ff7AutoPlayPolicy.ReadBoardingSuffixFingerprint(world, changed, destination), "Changed contact identity, branch, geometry or entry reused a boarding suffix.");
        }
        foreach (Ff7AutoPlayWorldScriptObservation changed in new[] { destination with { Id = "other" }, destination with { PredicateFingerprint = "changed" }, destination with { GeometryFingerprint = "changed" }, destination with { Analysis = destination.Analysis with { Entry = 5 } } })
        {
            Check(key != Ff7AutoPlayPolicy.ReadBoardingSuffixFingerprint(world, contact, changed), "Changed destination identity, branch, geometry or entry reused a boarding suffix.");
        }
        Check(before == JsonSerializer.Serialize(new { world, contact, destination }), "Boarding suffix fingerprinting changed actual native source state.");
        Console.WriteLine("Boarding suffix identity survives rider approach while fencing physical transport and native destination changes.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
