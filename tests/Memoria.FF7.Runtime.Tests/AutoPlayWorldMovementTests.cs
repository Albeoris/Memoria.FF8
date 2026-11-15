using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldMovementTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP native autoplay world movement preview: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        using Ff7WorldRuntime world = new(Ff7Installation.Discover(root), new byte[1280], 0, new byte[] { 0, 1, 2 });
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(Ff7WorldRuntime).GetField(name, flags)!.SetValue(world, value);
        var entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", flags)!.GetValue(world)!;
        var meshes = (Dictionary<(int, int), PcWorldMesh>)typeof(Ff7WorldRuntime).GetField("collisionMeshes", flags)!.GetValue(world)!;
        var deformations = (Dictionary<(int, int), PcWorldMesh>)typeof(Ff7WorldRuntime).GetField("deformedCollisionMeshes", flags)!.GetValue(world)!;
        Set("controlledModel", 0);
        Set("initialized", true);
        Set("mainState", 1);
        entities.Add(0, new(0, 3890, 0, 4096, TerrainResolved: true));
        PcWorldMesh coast = Mesh((0, 0, 4096, 8192, 0), (4096, 0, 8192, 8192, 3));
        meshes[(0, 0)] = coast;
        string before = JsonSerializer.Serialize(world.Process);
        Ff7AutoPlayWorldMovementState source = world.CaptureAutoPlayMovementState();
        Ff7AutoPlayWorldMovementPreview result = world.PreviewAutoPlayInput(new(Horizontal: 1), source);
        Check(result is { Supported: true, RequiresVmPhase: true, TerrainBoundary: null, State.Player.X: 3895, State.Player.Z: 4037 }, "The complete native five-point collision did not choose its proved coast slide; a valid center point alone cannot authorize the direct step.");
        Check(result.State!.Input.TerrainCache is { Length: > 0 and <= 6 } && result.State.Random is not null, "The projected collision cache or native control RNG continuation was omitted.");
        Check(before == JsonSerializer.Serialize(world.Process), "A native preview changed VM, models, input/cache, bridge, camera or RNG state.");
        Check(meshes.Count == 1 && ReferenceEquals(meshes[(0, 0)], coast) && deformations.Count == 0, "A native preview leaked resource/deformation cache changes.");
        Check(world.PreviewAutoPlayInput(default, result.State).Supported && before == JsonSerializer.Serialize(world.Process), "A chained projected input mutated the real world continuation.");
        Check(!world.PreviewAutoPlayInput(new(Horizontal: 1), source with { ViewMode = 3 }).Supported && before == JsonSerializer.Serialize(world.Process), "An unsupported native control mechanism failed to restore the original world state.");

        PcWorldMesh scripted = Mesh((0, 0, 8192, 8192, 0x60));
        meshes[(0, 0)] = scripted;
        Ff7AutoPlayWorldMovementState scriptedSource = source with { Player = source.Player with { Terrain = 0x60 }, Input = source.Input with { TerrainCache = null, TerrainScript = 0 } };
        string beforeScript = JsonSerializer.Serialize(world.Process);
        Ff7AutoPlayWorldMovementPreview boundary = world.PreviewAutoPlayInput(new(Horizontal: 1), scriptedSource);
        Check(boundary is { Supported: true, TerrainBoundary: { ChunkX: 0, ChunkZ: 0, Script: 0, RestoresPreviousPosition: false } }, "A native mesh-script entry was not reported as an explicit execution boundary.");
        Check(beforeScript == JsonSerializer.Serialize(world.Process) && ReferenceEquals(meshes[(0, 0)], scripted), "Reporting a mesh boundary executed or altered the authoritative world VM/cache.");
    }

    private static PcWorldMesh Mesh(params (int X1, int Z1, int X2, int Z2, byte Terrain)[] regions)
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

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
