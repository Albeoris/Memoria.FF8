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
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) is null, "Airborne native overrides were excluded using a surface mask.");
        entities[5] = entities[5] with { Flags = 0 };
        input.SetValue(world, new Ff7WorldInputState(Bridge: new(Phase: 1)));
        Check(world.ReadAutoPlayCurrentModeTerrainEligibility(target) is null, "A live bridge override became a static negative proof.");
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

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
