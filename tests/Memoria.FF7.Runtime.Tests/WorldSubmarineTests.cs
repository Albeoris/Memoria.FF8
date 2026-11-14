using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldSubmarineTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed WM2 submarine controls: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        byte[] party = [0, 1, 2];
        byte[] persistent = new byte[1280];
        persistent[0x350] = 8;
        persistent[0x7A] = 4;
        persistent[0x7B] = 0x10;
        using Ff7WorldRuntime world = new(installation, persistent, 0x2A, party, worldId: 2);
        for (int callback = 0; callback < 60; callback++)
        {
            world.Advance();
        }
        Ff7WorldProcess ready = world.Process;
        Ff7WorldEntity initial = Player(world);
        Check(ready is { WorldId: 2, MainState: 1, ControlledModel: 13, MovementEnabled: true, ViewMode: 2, Input.CameraHeadingTarget: 0 } && initial is { Y: -3000, TerrainResolved: true } && (initial.Flags & 128) != 0, $"The original WM2 entry did not initialize the submerged vehicle ({initial.X}/{initial.Y}/{initial.Z}, terrain {initial.Terrain:X4}, bottom {initial.GroundHeight}).");

        world.SetButtons(1, 1);
        world.SetMovement(1, 0, run: true);
        world.Advance();
        Ff7WorldEntity turning = Player(world);
        Check(turning is { TargetDirection: 2032 } && turning.X == initial.X && turning.Z == initial.Z && world.Process is { ViewMode: 2, Input.CameraHeadingTarget: 16, Camera.Heading: 1 }, "Right rotates the submerged heading and ignores the view toggle without supplying thrust.");
        world.SetButtons(8, 8);
        world.SetMovement(1, -1, run: true);
        world.Advance();
        Ff7WorldEntity descending = Player(world);
        Check(descending.Y == initial.Y - 60 && descending.X == initial.X && descending.Z == initial.Z && world.Process.Input!.CameraHeadingTarget == 48, "The shoulder-plus-right input while diving must turn by 32 and descend by 60 without thrust.");

        string savedJson = JsonSerializer.Serialize(world.Process);
        using Ff7WorldRuntime cold = new(installation, world.Machine.CopyPersistent(), 0x2A, party, worldId: 2);
        cold.Restore(JsonSerializer.Deserialize<Ff7WorldProcess>(savedJson)!);
        foreach (Ff7WorldRuntime branch in new[] { world, cold })
        {
            branch.SetButtons(0x120, 0x120);
            branch.SetMovement(0, 1, run: true);
            branch.Advance();
        }
        Ff7WorldEntity advancing = Player(world);
        Ff7WorldMath math = new(Ff7PcExecutableData.LoadWorldPresentation(installation, world.Resources));
        Vector3 step = math.TransformRotation(new(0, 0, -60), 0, -48, 0);
        Check(advancing.X == descending.X + (int)step.X && advancing.Z == descending.Z + (int)step.Z && advancing.Y == -3000 && world.Process.RadarMode == 2, "Confirm must propel the vehicle along the target heading while Down rises and the map button toggles radar.");
        Check(JsonSerializer.Serialize(world.Process) == JsonSerializer.Serialize(cold.Process), "Cold restore changed the next submerged motion, collision, or presentation callback.");
        world.SetButtons(0x1000, 0x1000);
        world.SetMovement(0, 0, run: true);
        for (int callback = 0; callback < 45; callback++)
        {
            world.Advance();
        }
        Ff7WorldEntity bottom = Player(world);
        Check(bottom.X == advancing.X && bottom.Z == advancing.Z && bottom.Y == Math.Max(-5040, Math.Min(-3000, bottom.GroundHeight + 200)), $"Diving must stop at the native depth threshold or seabed clearance (depth {bottom.Y}, bottom {bottom.GroundHeight}).");
        world.Restore(ready);
        world.SetButtons(0x20, 0x20);
        for (int callback = 0; callback < 326; callback++)
        {
            world.Advance();
        }
        Ff7WorldEntity beforeEdge = Player(world);
        world.Advance();
        Ff7WorldEntity edge = Player(world);
        Check(beforeEdge is { X: 166899, Z: 130152, TurnOffset: 0 } && edge is { X: 166885, Z: 130094, TurnOffset: 20, Terrain: 0x2423 }, "The original WM2 terrain edge must select the first 160-unit collision deflection and blend it into model heading.");
        WorldMapTransferTests.Run(installation, world.Machine.CopyPersistent(), ready);
    }

    private static Ff7WorldEntity Player(Ff7WorldRuntime world) => world.Process.Entities.Single(entity => entity.Model == 13);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
