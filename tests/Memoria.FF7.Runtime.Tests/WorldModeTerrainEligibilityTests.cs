using System.Reflection;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldModeTerrainEligibilityTests
{
    internal static void Run()
    {
        Check(Ff7WorldRuntime.ReadAutoPlayTerrainEligibility([0, 16], 0x70) == false, "A necessary current-mode terrain exclusion was lost.");
        Check(Ff7WorldRuntime.ReadAutoPlayTerrainEligibility([0, 4], 0x70) == true && Ff7WorldRuntime.ReadAutoPlayTerrainEligibility([4], 0x70) == true, "One native-allowed face did not preserve a mixed or allowed target.");
        Check(Ff7WorldRuntime.ReadAutoPlayTerrainEligibility([0], null) is null && Ff7WorldRuntime.ReadAutoPlayTerrainEligibility([], 0x70) is null && Ff7WorldRuntime.ReadAutoPlayTerrainEligibility([32], 0x70) is null, "Unknown terrain facts became a negative proof.");
        Check(Ff7WorldRuntime.HasInactiveWorldBridgeTerrain(new(Phase: 702), 0), "A retained historical bridge phase became current geometry activity.");
        Check(!Ff7WorldRuntime.HasInactiveWorldBridgeTerrain(new(Counter0: 1), 0) && !Ff7WorldRuntime.HasInactiveWorldBridgeTerrain(new(Counter1: 1), 0) && !Ff7WorldRuntime.HasInactiveWorldBridgeTerrain(new(Counter2: 1), 0), "Actual bridge amplitude became static terrain.");
        Check(!Ff7WorldRuntime.HasInactiveWorldBridgeTerrain(new(DriftX: 1), 0) && !Ff7WorldRuntime.HasInactiveWorldBridgeTerrain(new(DriftZ: 1), 0) && !Ff7WorldRuntime.HasInactiveWorldBridgeTerrain(new(Deformation: new(0, 0)), 0) && !Ff7WorldRuntime.HasInactiveWorldBridgeTerrain(new(), 1), "Active bridge geometry/cache became a negative terrain proof.");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP original native mask reuse: select resources.");
            return;
        }
        using Ff7WorldRuntime world = new(Ff7Installation.Discover(game), new byte[1280], 1, new byte[] { 0, 1, 2 });
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo controlled = typeof(Ff7WorldRuntime).GetField("controlledModel", flags)!;
        FieldInfo input = typeof(Ff7WorldRuntime).GetField("input", flags)!;
        Dictionary<int, Ff7WorldEntity> entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", flags)!.GetValue(world)!;
        entities[5] = new(5);
        entities[0] = new(0);
        Ff7AutoPlayWorldFrontierFace Face(int terrain) => new(new(0, 0, terrain), default, default, default, terrain, 0);
        Ff7AutoPlayWorldScriptObservation target = new("target", 3, [Face(0), Face(16)], new(true, null, 0, 0, [], [], [], []), "condition", "geometry", [], true, null);
        controlled.SetValue(world, 5);
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) == false && world.ReadAutoPlayCurrentModeTerrainEligibility(target with { Faces = [Face(0), Face(4)] }) == true, "Current surface vehicle eligibility disagreed with the shared native mask.");
        controlled.SetValue(world, 0);
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) == true, "A real controller change did not reopen native rider terrain.");
        entities[0] = entities[0] with { Terrain = 13 };
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) == true, "A temporary preceding-terrain mask hid faces reachable after another legal native terrain step.");
        controlled.SetValue(world, 5);
        entities[5] = entities[5] with { Flags = 128 };
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) == false, "Airborne native motion was offered a terrain callback that it never dispatches.");
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target with { Complete = false }) is null, "Incomplete airborne source analysis became a negative proof.");
        entities[3] = new(3, Flags: 128);
        controlled.SetValue(world, 3);
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) == false, "Native Highwind flight admitted a ground-only mesh source.");
        typeof(Ff7WorldRuntime).GetField("vehicle", flags)!.SetValue(world, new Ff7WorldVehicleState([], DismountPhase: 2));
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) is null, "Native phase-two placement was mistaken for airborne terrain dispatch.");
        typeof(Ff7WorldRuntime).GetField("vehicle", flags)!.SetValue(world, null);
        controlled.SetValue(world, 0);
        entities[0] = entities[0] with { Flags = 128 };
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) is null, "An unrelated actor flag acquired aircraft semantics.");
        entities[0] = entities[0] with { Flags = 0 };
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) == true, "Actual foot ownership did not reopen its native mesh sources.");
        controlled.SetValue(world, 5);
        entities[5] = entities[5] with { Flags = 0 };
        input.SetValue(world, new Ff7WorldInputState(Bridge: new(Phase: 1)));
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) == false, "An inactive nonzero historical bridge phase suppressed native terrain eligibility.");
        input.SetValue(world, new Ff7WorldInputState(Bridge: new(Counter0: 1)));
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) is null, "Actual bridge amplitude became a static negative proof.");
        input.SetValue(world, new Ff7WorldInputState());
        FieldInfo vehicle = typeof(Ff7WorldRuntime).GetField("vehicle", flags)!;
        vehicle.SetValue(world, new Ff7WorldVehicleState([], DismountPhase: 2));
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) is null, "A native dismount transition became a surface-mode negative proof.");
        vehicle.SetValue(world, null);
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target with { Complete = false }) is null, "Incomplete target facts became a negative proof.");
        entities[30] = new(30);
        controlled.SetValue(world, 30);
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) is null, "An unknown native profile became a restrictive mask.");
        Console.WriteLine("PASS native terrain-mask reuse, mixed target faces, current-mode change and unknown/airborne/bridge overrides");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_TERRAIN_SAVE") ?? throw new InvalidOperationException("Select a native mode-terrain fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream source = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(source).Ff7!);
        var world = session.World ?? throw new InvalidDataException("No native world.");
        string id = session.AutoPlayState!.Active!.EventId;
        string[] parts = id.Split(':');
        var target = world.ReadAutoPlayFrontierCatalog(int.Parse(parts[1]), int.Parse(parts[2])).Scripts.Single(item => item.Id == id);
        string before = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) == false, "The exact native target bypassed inactive-bridge terrain eligibility.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        Ff7AutoplayDriver driver = new(session);
        var input = policy.ChooseInput(driver);
        Check(policy.State.Active?.EventId != id && driver.PhysicalPlanningWork == 0 && before == Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Excluded native terrain allocated physical work or changed native/source state.");
        Console.WriteLine($"PASS original inactive-bridge terrain exclusion: faces={target.Faces.Length}, terrain={string.Join(',', target.Faces.Select(face => face.Terrain).Distinct())}, callbacks=0, tick={world.Process.Tick}, controlled={world.ControlledModel}, physical={driver.PhysicalPlanningWork}, active={policy.State.Active?.EventId}, pending={driver.PlanningPending}; native/source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
