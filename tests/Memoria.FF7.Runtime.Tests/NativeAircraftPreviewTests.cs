using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAircraftPreviewTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP authored aircraft preview: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        foreach (int model in new[] { 3, 5 })
        {
            using Fixture fixture = new(installation, model);
            Compare(fixture.World, new(Held: 0x20));
            Compare(fixture.World, new(Held: 0x2020));
            Set(fixture.World, "scriptCamera", new Ff7WorldScriptCameraState(1, 16, 1, 2, 1200, 2000));
            Compare(fixture.World, new(Held: 0x4020));
            Ff7AutoPlayWorldMovementState source = fixture.World.CaptureAutoPlayMovementState();
            Check(source.Vehicle is not null && source.ScriptCamera is not null, "Aircraft capture omitted its actual vehicle or script-camera continuation.");
            string before = Snapshot(fixture.World, source);
            Ff7AutoPlayWorldMovementPreview rejected = fixture.World.PreviewAutoPlayInput(new(Held: 0x10), source);
            Check(!rejected.Supported && before == Snapshot(fixture.World, source), "The native Select/module boundary mutated the VM or was silently included in physical preview.");
            CheckKeys(source);
            CheckAdvice(fixture.World, source);
            int nativeRider = fixture.World.Process.Vehicle!.RideStack[0];
            source.Vehicle!.RideStack[0] = 99;
            Check(fixture.World.Process.Vehicle.RideStack[0] == nativeRider, "A captured vehicle ride stack shared mutable native storage.");
        }
        using Fixture takingOff = new(installation, 3);
        Set(takingOff.World, "vehicle", new Ff7WorldVehicleState([0], FlightState: 1, FlightProgress: 64, FlightVelocity: 8, FlightMaxHeight: 4000) { FlightPreviousView = 0 });
        Ff7AutoPlayWorldMovementPreview lift = Compare(takingOff.World, default);
        Check(lift.State!.Vehicle is { FlightProgress: > 64 } && lift.State.Player.Y != 1000, "The actual takeoff camera tail did not advance its native vehicle/height state.");
        using Fixture landing = new(installation, 3);
        CheckLanding(landing.World);
        using Fixture terrainCallback = new(installation, 3);
        Set(terrainCallback.World, "viewMode", 0);
        var entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", Private)!.GetValue(terrainCallback.World)!;
        entities[3] = entities[3] with { Flags = 2 };
        var meshes = (Dictionary<(int, int), PcWorldMesh>)typeof(Ff7WorldRuntime).GetField("collisionMeshes", Private)!.GetValue(terrainCallback.World)!;
        PcWorldMesh mesh = meshes[(0, 0)];
        meshes[(0, 0)] = mesh with { Triangles = mesh.Triangles.Select(triangle => triangle with { TerrainAndScript = 3 << 5 }).ToArray() };
        Ff7AutoPlayWorldMovementState callbackSource = terrainCallback.World.CaptureAutoPlayMovementState();
        string callbackBefore = Snapshot(terrainCallback.World, callbackSource);
        Ff7AutoPlayWorldMovementPreview callback = terrainCallback.World.PreviewAutoPlayInput(default, callbackSource);
        Check(!callback.Supported && callback.Reason!.Contains("terrain callback", StringComparison.Ordinal) && callbackBefore == Snapshot(terrainCallback.World, callbackSource), "A grounded aircraft terrain callback dispatched into the real VM during preview.");
        Console.WriteLine("Aircraft previews match native physical and flight-camera phases while preserving source state and module boundaries.");
    }

    private static Ff7AutoPlayWorldMovementPreview Compare(Ff7WorldRuntime world, Ff7AutoplayInput action)
    {
        Ff7AutoPlayWorldMovementState source = world.CaptureAutoPlayMovementState();
        string before = Snapshot(world, source);
        Ff7AutoPlayWorldMovementPreview preview = world.PreviewAutoPlayInput(action, source);
        Check(preview is { Supported: true, RequiresVmPhase: true, State: not null }, "Native aircraft preview was rejected: " + preview.Reason);
        Check(before == Snapshot(world, source), "Aircraft preview changed live process/VM/RNG/camera/vehicle state or its retained source.");
        Ff7AutoPlayWorldMovementPreview again = world.PreviewAutoPlayInput(action, source);
        Check(JsonSerializer.Serialize(preview) == JsonSerializer.Serialize(again), "Identical aircraft sources produced different native projections.");
        world.SetButtons(action.Held, (ushort)(action.Held & ~source.Input.Held));
        world.SetMovement(action.Horizontal, action.Vertical, action.Run);
        Invoke(world, "AdvancePlayerInputCore", false);
        Invoke(world, "AdvanceWorldCameraHeading");
        Invoke(world, "AdvanceScriptCamera");
        Invoke(world, "AdvanceWorldScriptFacing");
        Invoke(world, "ResolvePlayerTerrain", false, null, false);
        Invoke(world, "AdvanceHighwindFlightCamera");
        string expected = Physical(preview.State!);
        string actual = Physical(world.CaptureAutoPlayMovementState());
        Check(expected == actual, "Aircraft projection differs from actual native physical/flight-camera phases. Projected=" + expected + "; actual=" + actual);
        return preview;
    }

    private static string Physical(Ff7AutoPlayWorldMovementState state)
    {
        // Terrain's real callback also advances these presentation-only actor counters.
        return JsonSerializer.Serialize(state with { Player = state.Player with { AnimationFrame = 0, PoseAnimation = 0, PoseFrame = 0, FrameRemainder = 0 } });
    }

    private static string Snapshot(Ff7WorldRuntime world, Ff7AutoPlayWorldMovementState source) => JsonSerializer.Serialize(new { Process = world.Process, Vm = world.Machine.Capture(), Source = source });

    private static void CheckKeys(Ff7AutoPlayWorldMovementState source)
    {
        string exact = Ff7AutoplayDriver.WorldMovementKey(source);
        Check(exact != Ff7AutoplayDriver.WorldMovementKey(source with { Vehicle = source.Vehicle! with { FlightSpeed = source.Vehicle!.FlightSpeed + 1 } }), "A changed native flight speed reused an exact movement source.");
        Check(exact != Ff7AutoplayDriver.WorldMovementKey(source with { Camera = source.Camera with { Heading = source.Camera.Heading + 1 } }), "Changed aircraft camera heading reused an exact movement source.");
        Check(exact == Ff7AutoplayDriver.WorldMovementKey(source with { RandomIndex = source.RandomIndex + 1 }), "A discarded aircraft RNG value became physical progression.");
        Check(exact == Ff7AutoplayDriver.WorldMovementKey(source with { Vehicle = source.Vehicle! with { HighwindPropellerFrame = 17, HighwindPropellerPoseFrame = 18 } }), "Propeller presentation changed aircraft physical identity.");
        Check(exact != Ff7AutoplayDriver.WorldMovementKey(source with { ScriptCamera = source.ScriptCamera! with { RotationSpeed = source.ScriptCamera!.RotationSpeed + 1 } }), "Changed native script-camera rotation reused an exact movement source.");
    }

    private static void CheckAdvice(Ff7WorldRuntime world, Ff7AutoPlayWorldMovementState source)
    {
        string before = Snapshot(world, source);
        Ff7WorldTerrainReference? native = world.ReadAutoPlayAdviceGround(source);
        Check(native is not null && source.Input.GroundTriangle is null, "Aircraft advice did not sample native terrain without manufacturing foot state.");
        Check(world.ReadAutoPlayAdviceGround(source with { Input = source.Input with { GroundTriangle = new(35, 27, 999) } }) == native, "A stale preboarding foot identity replaced current aircraft terrain advice.");
        Check(before == Snapshot(world, source), "Aircraft terrain advice changed the live cache or source state.");
    }

    private static void CheckLanding(Ff7WorldRuntime world)
    {
        Ff7AutoPlayWorldMovementState source = world.CaptureAutoPlayMovementState();
        string before = Snapshot(world, source);
        Ff7AutoPlayWorldLandingPreview advised = world.PreviewAutoPlaySurfaceLanding(source);
        Check(advised is { Supported: true, Rider.Model: 0, Ground: not null }, "Native aircraft landing advice did not execute the finite flight/control phases: " + advised.Reason);
        Check(before == Snapshot(world, source), "Aircraft landing advice changed the real vehicle, ownership, VM or source.");
        Ff7AutoPlayWorldControlObservation control = world.ReadAutoPlayControlFrontiers().Single();
        for (int callback = 0; callback < 100 && !world.HasAutoPlayControlCompleted(control); callback++)
        {
            Ff7AutoplayInput action = world.ChooseAutoPlayControlInput(control);
            world.SetButtons(action.Held, (ushort)(action.Held & ~world.Process.Input!.Held));
            world.SetMovement(action.Horizontal, action.Vertical, action.Run);
            Invoke(world, "AdvancePlayerInputCore", false);
            Invoke(world, "AdvanceWorldCameraHeading");
            Invoke(world, "AdvanceScriptCamera");
            Invoke(world, "AdvanceWorldScriptFacing");
            Invoke(world, "ResolvePlayerTerrain", false, null, false);
            Invoke(world, "AdvanceSurfaceVehicleTransport", false);
            Invoke(world, "AdvanceHighwindFlightCamera");
        }
        Ff7WorldEntity rider = world.Process.Entities.Single(entity => entity.Model == 0);
        Check(world.HasAutoPlayControlCompleted(control) && world.Process.Input!.GroundTriangle == advised.Ground && (rider.X, rider.Y, rider.Z) == (advised.Rider!.X, advised.Rider.Y, advised.Rider.Z), "Actual native landing/control return differs from its read-only advice.");
    }

    private static void Set(Ff7WorldRuntime world, string name, object value) => typeof(Ff7WorldRuntime).GetField(name, Private)!.SetValue(world, value);
    private static void Invoke(Ff7WorldRuntime world, string name, params object?[] arguments) => typeof(Ff7WorldRuntime).GetMethod(name, Private)!.Invoke(world, arguments);

    private sealed class Fixture : IDisposable
    {
        internal Fixture(Ff7Installation installation, int model)
        {
            World = new(installation, new byte[1280], 0, [0, 1, 2], worldId: 0);
            Set(World, "controlledModel", model);
            Set(World, "initialized", true);
            Set(World, "mainState", 1);
            Set(World, "movementEnabled", true);
            Set(World, "viewMode", 3);
            Set(World, "vehicle", new Ff7WorldVehicleState([0], FlightSpeed: 120, FlightProgress: 256, FlightMaxHeight: model == 3 ? 4000 : 2000) { TinyBroncoFlightEnabled = model == 5 });
            var entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", Private)!.GetValue(World)!;
            entities[model] = new(model, 4096, 1000, 4096, Flags: 128, TerrainResolved: true);
            World.Machine.Restore(World.Machine.Capture() with { Models = [new WorldEntitySnapshot(model, 0, 0, 0, false, 0, [])] });
            var meshes = (Dictionary<(int, int), PcWorldMesh>)typeof(Ff7WorldRuntime).GetField("collisionMeshes", Private)!.GetValue(World)!;
            meshes[(0, 0)] = new(0, 0, [new(0, 0, 0), new(8192, 0, 0), new(8192, 0, 8192), new(0, 0, 8192)], [], [new(0, 1, 2, 0, default, default, default, 0), new(0, 2, 3, 0, default, default, default, 0)]);
        }

        internal Ff7WorldRuntime World { get; }

        public void Dispose()
        {
            World.Dispose();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
