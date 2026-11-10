using System.Numerics;
using System.Reflection;
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
