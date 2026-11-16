using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldModelContactTests
{
    internal static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP native world model contact: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7WorldRuntime world = new(installation, new byte[1280], 0, [0, 1, 2]);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object? value)
        {
            typeof(Ff7WorldRuntime).GetField(name, flags)!.SetValue(world, value);
        }
        void Contact()
        {
            typeof(Ff7WorldRuntime).GetMethod("AdvancePlayerInputCore", flags)!.Invoke(world, [false]);
            world.ModelScriptCompleted(0);
        }
        var entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", flags)!.GetValue(world)!;
        var meshes = (Dictionary<(int, int), PcWorldMesh>)typeof(Ff7WorldRuntime).GetField("collisionMeshes", flags)!.GetValue(world)!;
        Set("controlledModel", 0);
        Set("initialized", true);
        Set("mainState", 1);
        Set("movementEnabled", true);
        Set("collisionMasks", Enumerable.Repeat((byte)255, 26 * 8).ToArray());
        entities[0] = new(0, 4096, 0, 4096, TerrainResolved: true);
        entities[6] = new(6, 4096, 0, 4096, TerrainResolved: true);
        entities[5] = new(5, 6000, 0, 4096, TerrainResolved: true);
        entities[4] = new(4, 4096, 0, 4096, Flags: 8, TerrainResolved: true);
        entities[3] = new(3, 4096, 0, 4096, TerrainResolved: true);
        entities[19] = new(19, 4096, 0, 4096, TerrainResolved: true);
        entities[7] = new(7, 4096, 0, 4096, TerrainResolved: true);
        WorldSnapshot fixture = world.Machine.Capture() with
        {
            Models = [new(0, 0, 0, 0, false, 0, []), new(6, 0, 0, 0, false, 0, []), new(5, 0, 0, 0, false, 0, []), new(4, 0, 0, 0, false, 0, []), new(3, 0, 0, 0, true, 0, []), new WorldEntitySnapshot(19, 0, 0, 0, false, 0, []) { Detached = true }, new(7, 0, 0, 0, false, 0, [])]
        };
        world.Machine.Restore(fixture);
        meshes[(0, 0)] = FlatMesh();
        Check(world.ReadAutoPlayModelContactSources().Select(entity => entity.Model).Order().SequenceEqual([4, 5, 6, 7]), "Contact discovery must retain distant and generic native sources while excluding disabled and detached models; native visibility bit8 does not disable collision.");
        string before = JsonSerializer.Serialize(world.Process);
        List<Ff7WorldModelDispatchObservation> observed = [];
        world.NativeModelDispatched += observed.Add;
        Ff7AutoPlayWorldMovementPreview preview = world.PreviewAutoPlayInput(default);
        Check(preview is { Supported: true, RequiresVmPhase: true, ModelBoundaries.Length: 1 } && preview.ModelBoundaries[0] == new Ff7AutoPlayWorldModelBoundary(6, 3), "The collision preview must identify event 3 only for the contacting enabled model.");
        Check(world.PreviewAutoPlayInput(new(Held: 32)).ModelBoundaries?.Single() == new Ff7AutoPlayWorldModelBoundary(6, 4), "Held Confirm must select native event 4.");
        Check(before == JsonSerializer.Serialize(world.Process) && observed.Count == 0, "Contact previews must preserve authoritative state and never dispatch the VM.");
        Contact();
        WorldProgram program = new(world.Resources.ReadEvents());
        int entry = program.Find(0x4603);
        Ff7WorldModelDispatchObservation dispatch = observed.Single(item => item.Model == 6);
        Check(dispatch.Event == 3 && dispatch.Accepted == (entry != 0) && dispatch.Entry == entry && dispatch.CallDepth == 0, "Native event 3 admission must match the original model header, including an absent contact callback.");
        entities[0] = entities[0] with { ContactModel = 3 };
        world.ModelScriptCompleted(0);
        Check(observed.Single(item => item.Model == 3).Accepted == false, "A retained disabled native contact must preserve its no-op dispatch behavior.");
        entities[0] = entities[0] with { ContactModel = 6 };
        observed.Clear();
        Contact();
        Check(!observed.Single(item => item.Model == 6).Accepted, "A repeated equal-priority event 3 must not be credited as accepted.");
        int confirmEntry = program.Find(0x4604);
        Check(confirmEntry != 0, "The original buggy must supply its native Confirm callback.");
        WorldSnapshot beforeConfirm = world.Machine.Capture();
        world.Machine.Restore(beforeConfirm with { Models = beforeConfirm.Models.Select(model => model.Model == 6 ? model with { ProgramCounter = confirmEntry, Priority = 1, Calls = [] } : model).ToArray() });
        observed.Clear();
        world.SetButtons(32, 32);
        Contact();
        Check(observed.Single(item => item.Model == 6) is { Event: 4, Accepted: true, CallDepth: 1 } confirm && confirm.Entry == confirmEntry, "The higher-priority Confirm dispatch must report acceptance even when its entry matches the suspended model PC.");
        observed.Clear();
        Contact();
        Check(observed.Single(item => item.Model == 6) is { Event: 4, Accepted: false, CallDepth: 1 }, "An equal-priority Confirm callback must not be credited as another accepted dispatch.");
        Set("movementEnabled", false);
        Check(world.ReadAutoPlayModelContactSources().Length == 0, "Disabled movement must suppress native contact sources.");
        Set("movementEnabled", true);
        Set("controlledModel", 6);
        Check(world.ReadAutoPlayModelContactSources().Any(entity => entity.Model == 0), "Native contact sources must also serve a controlled vehicle without a foot-only gate.");
        Set("controlledModel", 0);
        entities[6] = entities[6] with { X = 4608 };
        entities[0] = entities[0] with { X = 4096, ContactModel = null, Flags = 0 };
        world.SetMovement(1, 0, true);
        typeof(Ff7WorldRuntime).GetMethod("AdvancePlayerInputCore", flags)!.Invoke(world, [false]);
        Check(entities[0] is { X: 4096, ContactModel: 6 } && (entities[0].Flags & 1) == 0, "Approaching native contact must roll back position while retaining the first collision owner.");
        world.SetMovement(-1, 0, true);
        typeof(Ff7WorldRuntime).GetMethod("AdvancePlayerInputCore", flags)!.Invoke(world, [false]);
        Check(entities[0] is { X: 4036, ContactModel: 6 } && (entities[0].Flags & 1) != 0, "Moving away from contact must retain the native contact owner without rolling movement back.");
        Set("movementEnabled", false);
        typeof(Ff7WorldRuntime).GetMethod("AdvancePlayerInputCore", flags)!.Invoke(world, [false]);
        Check(entities[0].ContactModel is null, "The next native frame must clear a retained contact when either owner moved.");
        CheckOriginalFixture(installation);
    }

    private static void CheckOriginalFixture(Ff7Installation installation)
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_MODEL_SAVE");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original world model contact continuation: configure MEMORIA_FF7_WORLD_MODEL_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("The selected fixture has no world allocation.");
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayWorldMovementPreview preview = world.PreviewAutoPlayInput(default);
        Check(preview.Supported && before == JsonSerializer.Serialize(session.Capture()), "An original contact preview must be supported and read-only.");
        List<Ff7WorldModelDispatchObservation> observed = [];
        world.NativeModelDispatched += observed.Add;
        world.SetButtons(0, 0);
        world.SetMovement(0, 0, true);
        world.Advance();
        foreach (Ff7WorldModelDispatchObservation dispatch in observed.Where(item => item.Accepted))
        {
            WorldEntitySnapshot model = world.Machine.Capture().Models.Single(item => item.Model == dispatch.Model);
            Check(model.ProgramCounter == dispatch.Entry && model.Calls.Length == dispatch.CallDepth, "An ordinary tick must report the exact accepted model VM continuation.");
        }
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Ordinary continuation must preserve the source fixture bytes.");
        Console.WriteLine($"PASS original world contact preview and ordinary tick; observed {observed.Count} model dispatches.");
    }

    private static PcWorldMesh FlatMesh()
    {
        Vector3[] vertices = [new(0, 0, 0), new(8192, 0, 0), new(8192, 0, 8192), new(0, 0, 8192)];
        PcWorldTriangle[] triangles = [new(0, 1, 2, 0, default, default, default, 0), new(0, 2, 3, 0, default, default, default, 0)];
        return new(0, 0, vertices, [], triangles);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
