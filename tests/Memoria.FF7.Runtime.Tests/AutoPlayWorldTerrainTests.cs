using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldTerrainTests
{
    private static readonly JsonSerializerOptions geometryOptions = new() { IncludeFields = true };

    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_TRANSPORT_SAVE");
        if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original autoplay world terrain observations: configure MEMORIA_FF7_GAME_DIR and MEMORIA_FF7_WORLD_TRANSPORT_SAVE.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The world observation fixture has no FFVII session.");
        Ff7Installation installation = Ff7Installation.Discover(game);
        using Ff7Session observed = Ff7Session.Restore(installation, saved);
        using Ff7Session untouched = Ff7Session.Restore(installation, saved);
        Ff7WorldRuntime world = observed.World ?? throw new InvalidDataException("The observation fixture must retain an original world continuation.");
        Ff7WorldEntity player = world.Process.Entities.Single(entity => entity.Model == world.ControlledModel);
        string before = JsonSerializer.Serialize(observed.Capture());
        Ff7AutoPlayWorldTerrainPoint point = world.ReadAutoPlayTerrain(player.X, player.Z) ?? throw new InvalidDataException("The original controlled world position has no terrain sample for this query contract.");
        Check(JsonSerializer.Serialize(observed.Capture()) == before, "Reading the original terrain point changed captured native execution state.");
        var mesh = world.ReadAutoPlayCollisionMesh(point.Triangle.ChunkX, point.Triangle.ChunkZ);
        string geometry = JsonSerializer.Serialize(mesh, geometryOptions);
        string sample = JsonSerializer.Serialize(point);
        for (int repetition = 0; repetition < 3; repetition++)
        {
            foreach (var triangle in mesh.Triangles.Take(4))
            {
                var center = (mesh.Vertices[triangle.A] + mesh.Vertices[triangle.B] + mesh.Vertices[triangle.C]) / 3;
                world.ReadAutoPlayTerrain((point.Triangle.ChunkX << 13) + (int)center.X, (point.Triangle.ChunkZ << 13) + (int)center.Z);
                Check(JsonSerializer.Serialize(observed.Capture()) == before, "A neighboring original triangle query changed complete native process, VM, progress or input-cache state.");
            }
            Check(JsonSerializer.Serialize(world.ReadAutoPlayTerrain(player.X, player.Z)) == sample, "Repeated native terrain observations changed their original triangle selection.");
            Check(JsonSerializer.Serialize(world.ReadAutoPlayCollisionMesh(point.Triangle.ChunkX, point.Triangle.ChunkZ), geometryOptions) == geometry, "Repeated native collision geometry reads changed their resource result.");
            Check(JsonSerializer.Serialize(observed.Capture()) == before, "Repeated terrain and geometry reads changed the complete standalone continuation.");
        }
        foreach (Ff7Session session in new[] { observed, untouched })
        {
            session.SetInput(0, 0);
            session.MovePlayer(0, 0, true);
            session.AdvanceTick(advanceClock: false);
        }
        Check(JsonSerializer.Serialize(observed.Capture()) == JsonSerializer.Serialize(untouched.Capture()), "Observational resource-cache warming changed the next original neutral callback or RNG continuation.");
        Check(original.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Terrain observation modified its standalone source.");
        Console.WriteLine("PASS original world terrain observations preserve full native state, input-cache ownership, deterministic geometry and the next neutral callback");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
