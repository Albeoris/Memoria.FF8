using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAircraftBoardingAdviceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP authored aircraft boarding: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        foreach (int model in new[] { 3, 6 })
        {
            using Ff7WorldRuntime world = new(installation, new byte[1280], 0, [0, 1, 2], worldId: 0);
            Set(world, "controlledModel", 0);
            Set(world, "initialized", true);
            Set(world, "mainState", 1);
            Set(world, "movementEnabled", true);
            Set(world, "viewMode", 0);
            Set(world, "vehicle", new Ff7WorldVehicleState([]));
            var entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", Private)!.GetValue(world)!;
            entities.Clear();
            entities[0] = new(0, 2048, 0, 1024, TerrainResolved: true);
            Set(world, "input", new Ff7WorldInputState(PreviousX: 2048, PreviousZ: 1024) { GroundTriangle = new(0, 0, 0) });
            entities[model] = new(model, 4096, 0, 4096, TerrainResolved: true);
            var meshes = (Dictionary<(int, int), PcWorldMesh>)typeof(Ff7WorldRuntime).GetField("collisionMeshes", Private)!.GetValue(world)!;
            meshes[(0, 0)] = new(0, 0, [new(0, 0, 0), new(8192, 0, 0), new(8192, 0, 8192), new(0, 0, 8192)], [], [new(0, 1, 2, 0, default, default, default, 0), new(0, 2, 3, 0, default, default, default, 0)]);
            WorldProgram program = new(new([new(checked((ushort)WorldProgram.MeshDispatchHeader(0, 0, 0)), 1)], [0, 0x100, 0x110, 1, 0x110, 0, 0x318, 0x203]));
            WorldMachine machine = new(program, new byte[1280], world);
            machine.Restore(machine.Capture() with { Models = [new WorldEntitySnapshot(0, 0, 0, 0, false, 0, []), new WorldEntitySnapshot(model, 0, 0, 0, false, 0, [])] });
            Set(world, "<Machine>k__BackingField", machine);
            WorldScriptAnalysis boarding = new(true, null, 0, 1, [], [], [new(1, WorldOperation.BoardTransport, null, model)], [1]);
            Ff7AutoPlayWorldModelScriptObservation contact = new("boarding", model, 3, entities[model], boarding, "predicate", "geometry", true, null);
            WorldScriptAnalysis transfer = machine.AnalyzeMeshDispatch(0, 0, 3, currentEntryOnly: true);
            Ff7AutoPlayWorldScriptObservation destination = new("destination", 3, [new(new(0, 0, 0), new(0, 0, 0), new(8192, 0, 0), new(8192, 0, 8192), 0, 0)], transfer, "predicate", "geometry", [], true, null);
            string before = JsonSerializer.Serialize(world.Process);
            Ff7AutoPlayWorldBoardingAdvice advice = world.ReadAutoPlayBoardingAdvice(contact, destination);
            Check(advice.Supported && advice.Connected && advice.Transport == model && advice.ContactFaces > 0 && advice.TransportFaces > 0 && advice.RiderFaces > 0, $"A parked native transport lost its projected boarding/flight/landing/rider connection: model={model}, {advice.Reason}");
            Check(before == JsonSerializer.Serialize(world.Process), "Boarding projection changed real native VM, vehicle, camera, RNG, input or rider allocation.");
            foreach (Ff7AutoPlayWorldModelScriptObservation invalid in new[]
            {
                contact with { Complete = false },
                contact with { Analysis = boarding with { Transitions = [] } },
                contact with { Analysis = boarding with { Transitions = [new(1, WorldOperation.BoardTransport, null, 0)] } },
                contact with { Analysis = boarding with { Transitions = [new(1, WorldOperation.BoardTransport, null, 99)] } }
            })
            {
                Check(!world.ReadAutoPlayBoardingAdvice(invalid, destination).Connected && before == JsonSerializer.Serialize(world.Process), "An incomplete or unrelated boarding receiver acquired advice or mutated the live source.");
            }
        }
        Console.WriteLine("Parked aircraft and existing surface boarding advice preserve native source state while connecting the actual rider destination.");
    }

    private static void Set(Ff7WorldRuntime world, string field, object value) => typeof(Ff7WorldRuntime).GetField(field, Private)!.SetValue(world, value);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
