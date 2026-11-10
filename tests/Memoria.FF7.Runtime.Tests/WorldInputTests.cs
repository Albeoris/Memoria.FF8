using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldInputTests
{
    public static void Run()
    {
        Ff7WorldMath math = new(new([], [], 360f / 4096, Math.PI / 180));
        Vector3 rotated = math.TransformRotation(new(0, 0, -60), 0, 160, 0);
        Check((int)rotated.X == -14 && (int)rotated.Z == -58, "Native negative rotated displacement must truncate toward zero after its float store.");
        Vector3 opposite = math.TransformRotation(new(0, 0, -60), 0, -160, 0);
        Check((int)opposite.X == 14 && (int)opposite.Z == -58, "Opposite collision candidate must preserve the original signed rotation.");

        PcWorldTriangle triangle = new(0, 1, 2, 0, default, default, default, 0);
        PcWorldMesh mesh = new(0, 0, new Vector3[] { new(0, 10, 0), new(1024, 20, 0), new(0, 30, 1024) }, [], new[] { triangle });
        Check(Height(mesh, 256, 256) == 18, "Native world height must round a positive halfway result to the nearest even integer.");
        Check(Height(mesh, 0, 0) == 10 && Height(mesh, 1024, 0) == 20, "Native triangle edges must remain inclusive.");
        Check(Height(mesh, 1024, 1024) is null && Height(mesh, -1, 0) is null, "Native world triangle containment must reject an outside point.");
        PcWorldMesh negative = mesh with { Vertices = mesh.Vertices.Select(vertex => vertex with { Y = -vertex.Y }).ToArray() };
        Check(Height(negative, 256, 256) == -18, "Native negative terrain height must round to even, independently of displacement truncation.");
        PcWorldMesh degenerate = mesh with { Vertices = new Vector3[] { new(0, 1, 0), new(0, 2, 0), new(0, 3, 0) } };
        Check(Height(degenerate, 0, 0) == 2, "A zero projected normal must use the native mean-vertex height fallback.");
        CollisionBatch();
        BridgeAndHeightOwners();
    }

    private static void BridgeAndHeightOwners()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        using Ff7WorldRuntime runtime = new(Ff7Installation.Discover(root), new byte[1280], 1, new byte[] { 0, 1, 2 });
        runtime.Advance();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo inputField = typeof(Ff7WorldRuntime).GetField("input", flags)!;
        MethodInfo height = typeof(Ff7WorldRuntime).GetMethod("ResolveFootHeight", flags)!;
        Type resultType = typeof(Ff7WorldRuntime).GetNestedType("FootCollisionResult", BindingFlags.NonPublic)!;
        object result = Activator.CreateInstance(resultType, [0, 0, 0, 1, 0, 0])!;
        Ff7WorldEntity player = new(0, 0, 100, 0);
        inputField.SetValue(runtime, new Ff7WorldInputState(PreviousY: 100));
        Check((int)height.Invoke(runtime, [player, result, true, 1])! == 100, "A native large forest-height step must start smoothing without teleporting vertically.");
        Ff7WorldInputState first = (Ff7WorldInputState)inputField.GetValue(runtime)!;
        Check(first.HeightSmoothVelocity == -1 && first.HeightSmoothRemaining == 29, "Native smoothing must retain its signed initial velocity and movement countdown.");
        Check((int)height.Invoke(runtime, [player, result, true, 1])! == 99, "The next moving callback must apply the retained one-unit velocity.");
        Ff7WorldInputState second = (Ff7WorldInputState)inputField.GetValue(runtime)!;
        Check(second.HeightSmoothVelocity == -2, "Native smoothing velocity must double after applying an incomplete step.");
        Check((int)height.Invoke(runtime, [player, result, false, 1])! == 100, "An idle callback must preserve previous height while smoothing is active.");
        height.Invoke(runtime, [player, result, true, 2]);
        Check(((Ff7WorldInputState)inputField.GetValue(runtime)!).HeightSmoothVelocity == 0, "Native terrain2 must clear the smoothing owner.");

        var definitions = (IReadOnlyList<Ff7WorldBridgeDefinition>)typeof(Ff7WorldRuntime).GetField("bridgeDefinitions", flags)!.GetValue(runtime)!;
        Ff7WorldBridgeDefinition bridge = definitions[0];
        var entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", flags)!.GetValue(runtime)!;
        entities[0] = entities[0] with { X = bridge.ChunkX * 8192 + bridge.CenterX, Z = bridge.ChunkZ * 8192 + bridge.CenterZ, Y = 20000, Terrain = 14 };
        inputField.SetValue(runtime, new Ff7WorldInputState(Bridge: new(16, 100), HeightSmoothVelocity: -2, HeightSmoothRemaining: 29));
        typeof(Ff7WorldRuntime).GetMethod("PrepareBridgeTerrain", flags)!.Invoke(runtime, [bridge.ChunkX, bridge.ChunkZ]);
        Ff7WorldInputState prepared = (Ff7WorldInputState)inputField.GetValue(runtime)!;
        Check(prepared.Bridge is { Phase: 17, Deformation.Index: 0, Deformation.Tilt: 3 }, "Native bridge preparation must sample the old phase once before incrementing it.");
        Ff7WorldProcess saved = JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(runtime.Process))!;
        using Ff7WorldRuntime cold = new(Ff7Installation.Discover(root), runtime.Machine.CopyPersistent(), 1, new byte[] { 0, 1, 2 });
        cold.Restore(saved);
        Check(JsonSerializer.Serialize(cold.Process) == JsonSerializer.Serialize(runtime.Process), "Cold continuation must preserve bridge phase, sampled deformation, drift and smoothing owners.");
        cold.Restore(saved with { Input = saved.Input! with { Bridge = null } });
        Check(cold.Process.Input!.Bridge is { Counter0: 0, Phase: 0 }, "An older snapshot without presentation bridge state must recover the native saved counters instead of rejecting progress.");
        List<Memoria.FF7.Events.FieldAudioCommand> sounds = [];
        runtime.AudioRequested = sounds.Add;
        inputField.SetValue(runtime, prepared with { Horizontal = 1 });
        MethodInfo audio = typeof(Ff7WorldRuntime).GetMethod("AdvanceBridgeAudio", flags)!;
        audio.Invoke(runtime, null);
        audio.Invoke(runtime, null);
        Check(sounds.Count == 1 && sounds[0].Operation == 0x20, "Repeated bridge movement must keep one native prioritized sound owner.");
        Check(((Ff7WorldInputState)inputField.GetValue(runtime)!).Bridge!.Counter0 == 108, "Bridge excitation must add ten below100, then decrement each callback.");
        int resets = 0;
        runtime.ResetSoundPitchesRequested = () => resets++;
        inputField.SetValue(runtime, new Ff7WorldInputState(Bridge: new(17, 2)));
        audio.Invoke(runtime, null);
        Check(sounds.Count == 2 && sounds[1].Operation == 0xF1 && resets == 1, "Counter decay to one must stop the native bridge sound and reset its shared pitch owners once.");
    }

    private static void CollisionBatch()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original world numeric profile for authored collision fixtures: resource selection is unset.");
            return;
        }
        using Ff7WorldRuntime runtime = new(Ff7Installation.Discover(root), new byte[1280], 1, new byte[] { 0, 1, 2 });
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var meshes = (Dictionary<(int, int), PcWorldMesh>)typeof(Ff7WorldRuntime).GetField("collisionMeshes", flags)!.GetValue(runtime)!;
        FieldInfo input = typeof(Ff7WorldRuntime).GetField("input", flags)!;
        MethodInfo collide = typeof(Ff7WorldRuntime).GetMethod("ResolveFootCollision", flags)!;
        object? Resolve(PcWorldMesh mesh, int previousX, int previousZ, int age)
        {
            meshes[(0, 0)] = mesh;
            input.SetValue(runtime, new Ff7WorldInputState(PreviousX: previousX, PreviousZ: previousZ, CollisionAge: age));
            return collide.Invoke(runtime, [new Ff7WorldEntity(0, previousX + 60, 0, previousZ)]);
        }
        static int Value(object result, string property) => (int)result.GetType().GetProperty(property)!.GetValue(result)!;
        PcWorldMesh blocked = CreateMesh((0, 0, 8192, 8192, 3));
        Check(Resolve(blocked, 4096, 4096, 12) is null, "The native five-point candidates must reject an entirely blocked terrain instead of moving through it.");
        PcWorldMesh coast = CreateMesh((0, 0, 4096, 8192, 0), (4096, 0, 8192, 8192, 3));
        object slide = Resolve(coast, 3890, 4096, 0)!;
        Check(slide is not null && Value(slide, "Angle") == 960 && Value(slide, "Side") == 0 && Value(slide, "X") == 3895 && Value(slide, "Z") == 4037, "The first valid preferred-side coast candidate must preserve its native rotation, truncation and five-point clearance.");
        PcWorldMesh corner = CreateMesh((0, 0, 4096, 4200, 3), (0, 4200, 4096, 8192, 0), (4096, 0, 8192, 8192, 3));
        Check(Resolve(corner, 3890, 4401, 0) is null, "The opposite candidate side must remain ineligible before the native eight-callback age gate.");
        object reverse = Resolve(corner, 3890, 4401, 8)!;
        Check(reverse is not null && Value(reverse, "Angle") == -960 && Value(reverse, "Side") == 1, "The native aged collision must select the first valid opposite-side candidate.");
        Ff7WorldInputState cache = (Ff7WorldInputState)input.GetValue(runtime)!;
        Check(cache.TerrainCache is { Length: > 0 and <= 6 }, "Native collision queries must retain their bounded six-triangle cache, including rejected candidates.");
    }

    private static PcWorldMesh CreateMesh(params (int X1, int Z1, int X2, int Z2, byte Terrain)[] regions)
    {
        List<Vector3> vertices = [];
        List<PcWorldTriangle> triangles = [];
        foreach (var region in regions)
        {
            byte first = checked((byte)vertices.Count);
            vertices.AddRange([new(region.X1, 0, region.Z1), new(region.X2, 0, region.Z1), new(region.X2, 0, region.Z2), new(region.X1, 0, region.Z2)]);
            triangles.Add(new(first, (byte)(first + 1), (byte)(first + 2), region.Terrain, default, default, default, 0));
            triangles.Add(new(first, (byte)(first + 2), (byte)(first + 3), region.Terrain, default, default, default, 0));
        }
        return new(0, 0, vertices, [], triangles);
    }

    private static int? Height(PcWorldMesh mesh, int x, int z)
    {
        MethodInfo query = typeof(Ff7WorldRuntime).GetMethod("TryTriangleHeight", BindingFlags.NonPublic | BindingFlags.Static)!;
        object[] arguments = [mesh, 0, x, z, 0];
        return (bool)query.Invoke(null, arguments)! ? (int)arguments[4] : null;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
