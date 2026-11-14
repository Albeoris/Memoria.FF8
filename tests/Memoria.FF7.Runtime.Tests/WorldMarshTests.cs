using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldMarshTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP original marsh pursuit: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        byte[] persistent = new byte[1280];
        persistent[0] = 152;
        persistent[29] = 255;
        using Ff7WorldRuntime world = new(installation, persistent, 1, new byte[] { 0, 1, 2 });
        for (int callback = 0; callback < 700 && !world.CanSave; callback++)
        {
            if (world.Process.Dialogue is { AwaitingInput: true, Completed: false } dialogue)
            {
                world.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            world.Advance();
        }
        Check(world.CanSave, "Original field entry must establish the marsh fixture.");
        Ff7WorldProcess ready = world.Process;
        int leader = ready.ControlledModel;
        world.Restore(ready with { Marsh = new() });
        world.Execute(new(ready.Vm.NextRequestId, leader, leader, 0, new(WorldOperation.ResetMarsh, [])));
        Check(world.Process.Marsh is { Initialized: true, X: 0x2008, Z: 0x6338, Direction: 1024, Head: 0, Points.Length: 48 } reset && reset.Points.All(point => point == new Ff7WorldMarshPoint(0x2008, 0x6338)), "Native reset320 initializes the whole48-point ring on the original side of the marsh.");
        Ff7WorldMarsh before = world.Process.Marsh;
        world.Advance();
        Check(before.Head == world.Process.Marsh.Head && before.TurnTimer == world.Process.Marsh.TurnTimer && before.TurnDelta == world.Process.Marsh.TurnDelta && JsonSerializer.Serialize(before.Points) == JsonSerializer.Serialize(world.Process.Marsh.Points), "A distant original player must not advance marsh motion or consume its wander state.");

        Ff7WorldMarsh legacy = new(true, 0x2008, 0x6338);
        world.Restore(ready with { Marsh = legacy });
        Check(world.Process.Marsh.Points is { Length: 48 }, "A legacy recorded marsh head restores its nonprogress ring without replaying any script.");
        (int x, int y, int z, int terrain) = FindTerrain(world, 7);
        Ff7WorldEntity player = ready.Entities.Single(entity => entity.Model == leader) with { X = x, Y = y, Z = z, PreviousX = x, PreviousY = y, PreviousZ = z, GroundHeight = y, Terrain = terrain, Flags = 2 };
        Ff7WorldMarshPoint point = new(x - 0x34000, z - 0x20000, 0, unchecked((ushort)y));
        Ff7WorldMarsh fixtureMarsh = new(true, point.X, point.Z) { Points = Enumerable.Repeat(point, 48).ToArray(), PreviousModel = leader, PreviousTerrain = 0 };
        Ff7WorldProcess fixture = ready with { Entities = ready.Entities.Select(entity => entity.Model == leader ? player : entity).ToArray(), Marsh = fixtureMarsh, EncounterRate = 0, Input = ready.Input! with { Horizontal = 0, Vertical = 0, Held = 0, Pressed = 0, PreviousControlHeld = 0, TerrainCache = [], TerrainScript = terrain >> 5 & 7 } };
        world.Restore(fixture);
        using Ff7WorldRuntime cold = new(installation, world.Machine.CopyPersistent(), 1, new byte[] { 0, 1, 2 });
        cold.Restore(Cold(world.Process));
        Advance(world, cold);
        Check(!world.Process.Marsh.Initialized && world.Process.Vm.System.Priority == 3 && world.Process.Vm.System.ProgramCounter > 0, "Native Manhattan contact starts original system event7 and disables the serpent until its scripted reset.");
        Check(world.ReadSpecial(11, leader) == (x > 0x36000 ? 1 : 0), "World selector11 retains the exact native grass-to-marsh side predicate.");
        for (int callback = 0; callback < 90 && world.Process.BattleState?.Pending is null; callback++)
        {
            if (world.Process.Dialogue is { AwaitingInput: true, Completed: false } dialogue)
            {
                world.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
                cold.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            Advance(world, cold);
        }
        Check(world.Process.BattleState?.Pending is { Scripted: true }, "The original system event7 must reach its native scripted encounter request without injected battle commands.");

        Ff7WorldMarshPoint distant = point with { X = point.X + 1000 };
        world.Restore(fixture with { Marsh = fixtureMarsh with { X = distant.X, Points = Enumerable.Repeat(distant, 48).ToArray() } });
        cold.Restore(Cold(world.Process));
        Advance(world, cold);
        Check(world.Process.Marsh is { Initialized: true, Head: 1 } advanced && advanced.Points![1] != distant && advanced.TurnTimer == 0, "A swamp player outside contact range advances the pursuit ring without drawing random wandering turns.");
        cold.Restore(Cold(world.Process));
        for (int callback = 0; callback < 6 && world.Process.Marsh.Initialized; callback++)
        {
            Advance(world, cold);
        }
        (int grassX, int grassY, int grassZ, int grassTerrain) = FindTerrain(world, 0);
        Ff7WorldEntity grassPlayer = player with { X = grassX, Y = grassY, Z = grassZ, PreviousX = grassX, PreviousY = grassY, PreviousZ = grassZ, Terrain = grassTerrain };
        world.Restore(fixture with { Entities = fixture.Entities.Select(entity => entity.Model == leader ? grassPlayer : entity).ToArray(), Marsh = fixtureMarsh with { PreviousTerrain = 0 }, Input = fixture.Input! with { TerrainScript = grassTerrain >> 5 & 7 } });
        world.ReadSpecial(16, leader);
        int randomIndex = world.Process.BattleState!.RandomIndex;
        cold.Restore(Cold(world.Process));
        Advance(world, cold);
        Check(world.Process.Marsh is { Initialized: true, Head: 1, TurnTimer: >= 16 and <= 79, TurnDelta: >= -32 and <= 31 } && world.Process.BattleState!.RandomIndex == (randomIndex + 2) % 0x209, "Grass-side wandering consumes exactly two native shared random draws for its timer and turning delta.");
        Check(ready.Entities.Single(entity => entity.Model == leader).X != x, "The marsh fixture must remain separate from actual saved gameplay.");
        Console.WriteLine("PASS original marsh reset, legacy ring restoration, terrain, pursuit, native system7 battle handoff, selector11 and cold continuation");
    }

    private static (int X, int Y, int Z, int Terrain) FindTerrain(Ff7WorldRuntime world, int terrain)
    {
        float greatestArea = 0;
        (int X, int Y, int Z, int Terrain) found = default;
        for (int chunkZ = 16; chunkZ < 20; chunkZ++)
        {
            for (int chunkX = 27; chunkX < 29; chunkX++)
            {
                int block = Ff7WorldVisibility.ResolveBlockIndex((chunkZ >> 2) * 9 + (chunkX >> 2), world.WorldMapState);
                var mesh = world.Resources.Map.ReadMesh(block, (chunkZ & 3) * 4 + (chunkX & 3));
                foreach (var face in mesh.Triangles.Where(face => face.Terrain == terrain))
                {
                    var a = mesh.Vertices[face.A];
                    var b = mesh.Vertices[face.B];
                    var c = mesh.Vertices[face.C];
                    float area = Math.Abs((b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X));
                    if (area > greatestArea)
                    {
                        greatestArea = area;
                        found = ((chunkX << 13) + (int)((a.X + b.X + c.X) / 3), (int)((a.Y + b.Y + c.Y) / 3), (chunkZ << 13) + (int)((a.Z + b.Z + c.Z) / 3), face.TerrainAndScript);
                    }
                }
            }
        }
        Check(greatestArea > 0, "Original meshes must contain the native marsh-region terrain fixture.");
        return found;
    }

    private static Ff7WorldProcess Cold(Ff7WorldProcess state) => JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(state))!;

    private static void Advance(Ff7WorldRuntime world, Ff7WorldRuntime cold)
    {
        world.Advance();
        cold.Advance();
        Check(JsonSerializer.Serialize(world.Process) == JsonSerializer.Serialize(cold.Process), "Marsh callbacks must cold-continue the exact native ring, RNG, terrain and script owners.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
