using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldHighwindTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP original Highwind: configure MEMORIA_FF7_GAME_DIR.");
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
        Check(world.CanSave, "Original world entry must establish the bounded aircraft fixture.");
        Ff7WorldProcess ready = world.Process;
        Ff7WorldEntity leader = ready.Entities.Single(entity => entity.Model == ready.ControlledModel);
        Ff7WorldEntity aircraft = new(3, leader.X, leader.Y, leader.Z, Terrain: leader.Terrain, TerrainResolved: true, PreviousX: leader.X, PreviousZ: leader.Z, GroundHeight: leader.GroundHeight) { PreviousY = leader.Y };
        world.Restore(ready with { Entities = [.. ready.Entities.Where(entity => entity.Model != 3), aircraft], Vm = ready.Vm with { Models = [.. ready.Vm.Models.Where(model => model.Model != 3), new WorldEntitySnapshot(3, 0, 0, 0, false, 0, [])] } });
        world.Execute(new(ready.Vm.NextRequestId, 3, 3, 0, new(WorldOperation.BoardTransport, [])));
        Check(world.ControlledModel == 3 && world.Process.Vehicle is { FlightState: 1, FlightProgress: 0, FlightMaxHeight: 4000 }, "Native Highwind boarding starts its4000-height flight transition and preserves the rider.");
        Check(world.Process.Effects?.Particles.Any(particle => particle.Sprite == 0x13 && particle.Y == aircraft.Y && particle.VelocityY == 50 && particle.GroundAligned) == true, "Highwind boarding emits its original neutral takeoff expansion at the current aircraft position.");
        using Ff7WorldRuntime cold = new(installation, world.Machine.CopyPersistent(), 1, new byte[] { 0, 1, 2 });
        cold.Restore(Cold(world.Process));
        int takeoff = 0;
        while (world.Process.Vehicle!.FlightState != 0 && takeoff < 40)
        {
            Advance(world, cold);
            takeoff++;
            if (takeoff == 1)
            {
                Check(world.Process.ViewMode == 3 && world.Process.Vehicle is { FlightProgress: 2, FlightVelocity: 2 }, "The native transition selects flight camera3 and integrates its first two-unit velocity step.");
            }
        }
        Check(takeoff == 22 && world.Process.Vehicle is { FlightState: 0, FlightProgress: 242, FlightVelocity: 0 }, "Native multiplier2 uses the actual triangular transition242 endpoint without inventing a256 snap.");
        Check(world.Process.Camera!.Focal == 211 && world.Process.Entities.Single(entity => entity.Model == 3).Y > aircraft.Y, "Takeoff applies native projection and aircraft height without requiring forward movement.");
        int x = world.Process.Entities.Single(entity => entity.Model == 3).X;
        int z = world.Process.Entities.Single(entity => entity.Model == 3).Z;
        SetButtons(world, cold, 0x20);
        Advance(world, cold);
        Check(world.Process.Vehicle!.FlightSpeed == 60 && world.Process.Entities.Single(entity => entity.Model == 3).X == x && world.Process.Entities.Single(entity => entity.Model == 3).Z == z - 60, "Flight Confirm accelerates from0 to60 of its240-unit native maximum.");
        SetButtons(world, cold, 0x2020);
        Advance(world, cold);
        Check(world.Process.Vehicle is { FlightSpeed: 105, FlightRoll: < 0 } && world.Process.Entities.Single(entity => entity.Model == 3).TiltZ > 0, "Flight steering accelerates, banks and publishes the native body tilt.");
        Ff7WorldProcess beforeStrafe = world.Process;
        SetButtons(world, cold, 0x2080);
        Advance(world, cold);
        Check(world.Process.Input!.CameraHeadingTarget == beforeStrafe.Input!.CameraHeadingTarget && world.Process.Vehicle!.FlightSpeed == 138, "Strafe uses directional thrust without rotating the flight heading.");
        SetButtons(world, cold, 0);
        Advance(world, cold);
        Check(world.Process.Vehicle!.FlightSpeed == 103, "Released flight thrust decelerates through the native integer filter.");
        cold.Restore(Cold(world.Process));
        Advance(world, cold);
        int height = world.Process.Entities.Single(entity => entity.Model == 3).Y;
        SetButtons(world, cold, 0x1000);
        Advance(world, cold);
        Check(world.Process.Entities.Single(entity => entity.Model == 3).Y == height - 100, "Flight Up applies the native negative100-height step above terrain clearance.");
        SetButtons(world, cold, 0x4000);
        Advance(world, cold);
        Check(world.Process.Entities.Single(entity => entity.Model == 3).Y == height, "Flight Down reverses the native height step below its maximum altitude.");

        (int landingX, int landingY, int landingZ) = FindGrassLanding(world, aircraft.X, aircraft.Z);
        Ff7WorldProcess airborne = world.Process;
        Ff7WorldEntity landingAircraft = airborne.Entities.Single(entity => entity.Model == 3) with { X = landingX, Y = landingY + 100, Z = landingZ, PreviousX = landingX, PreviousY = landingY + 100, PreviousZ = landingZ, GroundHeight = landingY, Terrain = 0, Direction = 0, TargetDirection = 0 };
        world.Restore(airborne with { Entities = airborne.Entities.Select(entity => entity.Model == 3 ? landingAircraft : entity).ToArray(), Input = airborne.Input! with { Horizontal = 0, Vertical = 0, Held = 0, Pressed = 0, PreviousControlHeld = 0, CameraHeadingTarget = 0, TerrainCache = [] }, Vehicle = airborne.Vehicle! with { FlightSpeed = 0 } });
        cold.Restore(Cold(world.Process));
        SetButtons(world, cold, 0);
        Advance(world, cold);
        Check(world.Process.Entities.Single(entity => entity.Model == 3) is { Terrain: 0 } floor && floor.Y == floor.GroundHeight + 200, "Aircraft terrain clearance comes from the actual native triangle and enforces its200-unit floor.");
        SetButtons(world, cold, 64);
        Advance(world, cold);
        SetButtons(world, cold, 0);
        Advance(world, cold);
        Check(world.Process.Vehicle is { DismountPhase: 1, FlightState: -1 }, "Short Cancel on original grass starts the landing transition before the side-step dismount.");
        for (int callback = 0; callback < 40 && world.ControlledModel == 3; callback++)
        {
            Advance(world, cold);
        }
        Check(world.ControlledModel == leader.Model && world.Process.Vehicle is { FlightState: 0, FlightProgress: 0, DismountPhase: 0, RideStack.Length: 0 }, "Landing reaches its native ground endpoint and restores the saved rider without replaying on_load.");
        Check((world.Process.Entities.Single(entity => entity.Model == 3).Flags & 128) == 0 && world.Process.ViewMode == ready.ViewMode, "Parked Highwind clears the flight flag and restores its previous view.");
        Console.WriteLine("PASS original Highwind boarding, takeoff, flight controls, terrain clearance, landing, rider restoration and cold continuation");
    }

    private static (int X, int Y, int Z) FindGrassLanding(Ff7WorldRuntime world, int x, int z)
    {
        (int X, int Y, int Z) found = default;
        float greatestArea = 0;
        for (int chunkZ = (z >> 13) - 1; chunkZ <= (z >> 13) + 1; chunkZ++)
        {
            for (int chunkX = (x >> 13) - 1; chunkX <= (x >> 13) + 1; chunkX++)
            {
                int block = Ff7WorldVisibility.ResolveBlockIndex((chunkZ >> 2) * 9 + (chunkX >> 2), world.WorldMapState);
                var mesh = world.Resources.Map.ReadMesh(block, (chunkZ & 3) * 4 + (chunkX & 3));
                foreach (var face in mesh.Triangles.Where(face => face.TerrainAndScript == 0))
                {
                    var a = mesh.Vertices[face.A];
                    var b = mesh.Vertices[face.B];
                    var c = mesh.Vertices[face.C];
                    float area = Math.Abs((b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X));
                    if (area > greatestArea)
                    {
                        greatestArea = area;
                        found = ((chunkX << 13) + (int)((a.X + b.X + c.X) / 3), (int)((a.Y + b.Y + c.Y) / 3), (chunkZ << 13) + (int)((a.Z + b.Z + c.Z) / 3));
                    }
                }
            }
        }
        Check(greatestArea > 0, "The original nearby mesh must provide a native grass triangle for the landing fixture.");
        return found;
    }

    private static void SetButtons(Ff7WorldRuntime world, Ff7WorldRuntime cold, ushort held)
    {
        world.SetButtons(held, held);
        cold.SetButtons(held, held);
    }

    private static Ff7WorldProcess Cold(Ff7WorldProcess state)
    {
        return JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(state))!;
    }

    private static void Advance(Ff7WorldRuntime world, Ff7WorldRuntime cold)
    {
        world.Advance();
        cold.Advance();
        Check(JsonSerializer.Serialize(world.Process) == JsonSerializer.Serialize(cold.Process), "Aircraft scripts, terrain, flight filters and camera must cold-continue identically each callback.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
