using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAircraftOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AIRCRAFT_SAVE") ?? throw new InvalidOperationException("Select a native aircraft fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("The fixture requires its actual world allocation.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during flight transition."));
        policy.SetDestinationField(712);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        while (world.HasAutoPlayFlightTransition && controller.Error is null && controller.Steps < 100)
        {
            controller.Advance(1);
        }
        controller.Pause();
        if (controller.Error is not null || world.HasAutoPlayFlightTransition)
        {
            throw new InvalidOperationException("The real native takeoff did not settle through ordinary autoplay callbacks.", controller.Failure);
        }
        Ff7AutoPlayWorldMovementState source = world.CaptureAutoPlayMovementState();
        string before = JsonSerializer.Serialize(new { Process = world.Process, Vm = world.Machine.Capture(), Source = source });
        Ff7WorldTerrainReference? advice = world.ReadAutoPlayAdviceGround(source);
        Ff7AutoplayInput action = new(1, 0, Held: 0x80);
        Ff7AutoPlayWorldMovementPreview preview = world.PreviewAutoPlayInput(action, source);
        if (advice is null || !preview.Supported || preview.State is null || !preview.Moved || before != JsonSerializer.Serialize(new { Process = world.Process, Vm = world.Machine.Capture(), Source = source }))
        {
            throw new InvalidOperationException("Native aircraft advice or physical preview failed or changed its source: " + preview.Reason);
        }
        world.SetButtons(action.Held, (ushort)(action.Held & ~source.Input.Held));
        world.SetMovement(action.Horizontal, action.Vertical, action.Run);
        session.AdvanceTick(advanceClock: false);
        bool parity = Ff7AutoplayDriver.WorldMovementKey(preview.State) == Ff7AutoplayDriver.WorldMovementKey(world.CaptureAutoPlayMovementState());
        Console.WriteLine($"Native aircraft: takeoffCallbacks={controller.Steps}, tick={world.Process.Tick}, model={world.ControlledModel}, advice={advice}, moved={preview.Moved}, actualParity={parity}, ground={world.Process.Input?.GroundTriangle}");
        if (!parity || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("Actual native flight differs from its isolated physical projection.");
        }
        Ff7WorldEntity aircraft = world.CaptureAutoPlayMovementState().Player;
        controller.Resume();
        Stopwatch timer = Stopwatch.StartNew();
        bool nativeModePulse = false;
        while (timer.Elapsed.TotalSeconds < 30 && controller.Error is null && !nativeModePulse)
        {
            controller.Advance(1);
            Ff7WorldEntity current = world.CaptureAutoPlayMovementState().Player;
            nativeModePulse = policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldControl, WorldModeGoal: not null } && (current.X != aircraft.X || current.Z != aircraft.Z);
        }
        Console.WriteLine($"Native aircraft mode approach: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={world.Process.Tick}, nativePulse={nativeModePulse}, active={policy.State.Active?.EventId}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !nativeModePulse || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The bounded aircraft mode-advice probe did not reach its first actual native waypoint pulse.", controller.Failure);
        }
    }
}
