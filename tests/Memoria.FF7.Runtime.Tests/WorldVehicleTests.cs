using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldVehicleTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP original surface vehicles: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        byte[] persistent = new byte[1280];
        persistent[0] = 152;
        persistent[29] = 255;
        using Ff7WorldRuntime world = new(installation, persistent, 1, new byte[] { 0, 1, 2 });
        for (int callback = 0; callback < 700; callback++)
        {
            if (world.Process.Dialogue is { AwaitingInput: true, Completed: false } dialogue)
            {
                world.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            world.Advance();
            if (world.CanSave)
            {
                break;
            }
        }
        Ff7WorldProcess ready = world.Process;
        Check(world.CanSave, "Original field entry must establish a saveable world before the vehicle fixture.");
        Ff7WorldEntity leader = ready.Entities.Single(entity => entity.Model == ready.ControlledModel);
        Ff7WorldEntity buggy = new(6, leader.X, leader.Y, leader.Z, Terrain: leader.Terrain, TerrainResolved: true, PreviousX: leader.X, PreviousZ: leader.Z, GroundHeight: leader.GroundHeight) { PreviousY = leader.Y };
        Ff7WorldProcess fixture = ready with
        {
            Entities = [.. ready.Entities.Where(entity => entity.Model != 6), buggy],
            Vm = ready.Vm with { Models = [.. ready.Vm.Models.Where(model => model.Model != 6), new WorldEntitySnapshot(6, 0, 0, 0, false, 0, [])] }
        };
        world.Restore(fixture);
        world.Execute(new(fixture.Vm.NextRequestId, 6, 6, 0, new(WorldOperation.BoardTransport, [])));
        Check(world.ControlledModel == 6 && world.Process.Vehicle?.RideStack.SequenceEqual(new[] { leader.Model }) == true, "Boarding a buggy must retain the native rider stack and select the vehicle.");
        Check(!world.Process.Vm.Models.Any(model => model.Model == leader.Model) && !world.Process.Entities.Any(entity => entity.Model == leader.Model), "A boarded leader must release its native model-pool slot and stop executing its world scripts.");
        using Ff7WorldRuntime cold = new(installation, world.Machine.CopyPersistent(), 1, new byte[] { 0, 1, 2 });
        cold.Restore(Cold(world.Process));
        world.SetMovement(1, 0, true);
        cold.SetMovement(1, 0, true);
        Advance(world, cold);
        Ff7WorldEntity moved = world.Process.Entities.Single(entity => entity.Model == 6);
        Check(moved.X == buggy.X + 90 && moved.Z == buggy.Z && world.CanSave, "The original buggy must use90-unit cardinal displacement and preserve native menu permission.");
        Ff7WorldTerrainReference ground = world.Process.Input!.GroundTriangle ?? throw new InvalidOperationException("The buggy has no native center-triangle normal owner.");
        Memoria.FF7.Formats.PcWorldMesh mesh = world.Resources.Map.ReadMesh(Ff7WorldVisibility.ResolveBlockIndex((ground.ChunkZ >> 2) * 9 + (ground.ChunkX >> 2), world.WorldMapState), (ground.ChunkZ & 3) * 4 + (ground.ChunkX & 3));
        Memoria.FF7.Formats.PcWorldTriangle triangle = mesh.Triangles[ground.Triangle];
        System.Numerics.Vector3 normal = mesh.Normals[triangle.A] + mesh.Normals[triangle.B] + mesh.Normals[triangle.C];
        Ff7WorldMath math = new(Ff7PcExecutableData.LoadWorldPresentation(installation, world.Resources));
        Check(moved.TiltX == -math.Direction((int)normal.Z, -(int)normal.Y) >> 2 && moved.TiltZ == -math.Direction((int)normal.X, -(int)normal.Y) >> 2, "The buggy must filter the accepted center triangle's summed original vertex normals into its native body tilt.");
        world.SetMovement(0, 0, true);
        cold.SetMovement(0, 0, true);
        world.SetButtons(64, 64);
        cold.SetButtons(64, 64);
        Advance(world, cold);
        world.SetButtons(0, 0);
        cold.SetButtons(0, 0);
        Advance(world, cold);
        Check(world.Process.Vehicle?.DismountPhase == 1 && !world.MovementEnabled, "Short Cancel release must start the native vehicle exit phase.");
        Advance(world, cold);
        Check(world.Process.Vehicle?.DismountPhase == 2, "Vehicle exit must defer its native terrain placement to phase2.");
        cold.Restore(Cold(world.Process));
        Advance(world, cold);
        Check(world.ControlledModel == leader.Model && world.Process.Vehicle?.RideStack.Length == 0 && world.Process.Vm.Models.Single(model => model.Model == leader.Model) is { Detached: false, ProgramCounter: 0 }, "A successful native side-step must restore its rider without replaying on_load.");
        Check(world.Process.Entities.Single(entity => entity.Model == 6).AttachedModel is null, "A parked buggy must release its rider attachment.");
        CheckColoredChocobos(world, cold, ready);
        Console.WriteLine("PASS original buggy controls, native rider ownership, dismount and cold continuation");
    }

    private static void CheckColoredChocobos(Ff7WorldRuntime world, Ff7WorldRuntime cold, Ff7WorldProcess ready)
    {
        Ff7WorldEntity leader = ready.Entities.Single(entity => entity.Model == ready.ControlledModel);
        foreach (int tint in Enumerable.Range(0, 5))
        {
            Ff7WorldEntity bird = new(19, leader.X, leader.Y, leader.Z, Terrain: leader.Terrain, TerrainResolved: true, PreviousX: leader.X, PreviousZ: leader.Z, GroundHeight: leader.GroundHeight) { PreviousY = leader.Y };
            Ff7WorldProcess fixture = ready with
            {
                Entities = [.. ready.Entities.Where(entity => entity.Model != 19), bird],
                Vm = ready.Vm with { Models = [.. ready.Vm.Models.Where(model => model.Model != 19), new WorldEntitySnapshot(19, 0, 0, 0, false, 0, [])] }
            };
            world.Restore(fixture);
            world.Execute(new(fixture.Vm.NextRequestId, 19, 19, 0, new(WorldOperation.ChocoboColor, [tint])));
            world.Execute(new(fixture.Vm.NextRequestId + 1, 19, 19, 0, new(WorldOperation.BoardTransport, [])));
            cold.Restore(Cold(world.Process));
            world.SetMovement(1, 0, true);
            cold.SetMovement(1, 0, true);
            Advance(world, cold);
            Ff7WorldEntity moved = world.Process.Entities.Single(entity => entity.Model == 19);
            Check(world.ControlledModel == 19 && world.Process.Chocobo is { Model: 19, RiderModel: 0 } && moved.X == leader.X + 120 && moved.Z == leader.Z && world.Process.ChocoboTint == tint, "Every original Chocobo color must preserve its rider, doubled movement and native tint through cold continuation.");
            world.SetMovement(0, 0, true);
            cold.SetMovement(0, 0, true);
            world.SetButtons(64, 64);
            cold.SetButtons(64, 64);
            Advance(world, cold);
            world.SetButtons(0, 0);
            cold.SetButtons(0, 0);
            for (int callback = 0; callback < 3; callback++)
            {
                Advance(world, cold);
            }
            Check(world.ControlledModel == leader.Model && world.Process.Chocobo is { Model: 19, RiderModel: null, Landing: not null }, "The original colored Chocobo must dismount through native terrain placement without entering the wild yellow release owner.");
        }
        Console.WriteLine("PASS original five Chocobo colors, physical boarding, dismount, tint and cold continuation");
    }

    private static Ff7WorldProcess Cold(Ff7WorldProcess state)
    {
        return JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(state))!;
    }

    private static void Advance(Ff7WorldRuntime original, Ff7WorldRuntime cold)
    {
        original.Advance();
        cold.Advance();
        Check(JsonSerializer.Serialize(original.Process) == JsonSerializer.Serialize(cold.Process), "Every vehicle callback must retain exact cold script, terrain, input and rider state.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
