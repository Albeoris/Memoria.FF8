using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowWorldPreviewOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SNOW_WORLD_SAVE") ?? throw new InvalidOperationException("Select a native snow-world fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The fixture has no native session.");
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        Ff7AutoplayInput[] actions = [default, new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-1, -1), new(Held: 0x100)];
        foreach (Ff7AutoplayInput action in actions)
        {
            using Ff7Session session = Ff7Session.Restore(installation, source);
            Ff7WorldRuntime world = session.World ?? throw new InvalidDataException("The fixture has no native world allocation.");
            if (world.Resources.World != 3)
            {
                throw new InvalidDataException("The selected fixture is not the native snow world.");
            }
            string before = JsonSerializer.Serialize(session.Capture());
            Ff7AutoPlayWorldMovementPreview preview = world.PreviewAutoPlayInput(action);
            if (!preview.Supported || preview.State is null || before != JsonSerializer.Serialize(session.Capture()))
            {
                throw new InvalidOperationException($"The original snow input was not previewable without mutation: {preview.Reason}");
            }
            new Ff7AutoplayDriver(session).Apply(action);
            Invoke(world, "AdvancePlayerInputCore", false);
            Invoke(world, "AdvanceWorldCameraHeading");
            Invoke(world, "AdvanceScriptCamera");
            Invoke(world, "AdvanceWorldScriptFacing");
            Invoke(world, "ResolvePlayerTerrain", false, null, false);
            if (PhysicalSnapshot(preview.State) != PhysicalSnapshot(world.CaptureAutoPlayMovementState()))
            {
                throw new InvalidOperationException("The original snow preview differs from its native physical input/camera/facing/collision phases.");
            }
        }
        using Ff7Session live = Ff7Session.Restore(installation, source);
        Ff7WorldRuntime snow = live.World!;
        using Ff7AutoPlayPolicy policy = new(live, (_, _) => throw new InvalidOperationException("The snow movement fixture unexpectedly requested a choice checkpoint."));
        policy.SetDestinationField(686);
        Ff7AutoPlayController controller = new(live, policy.ChooseInput, advanceTick: () => live.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        int moved = 0;
        for (int poll = 0; poll < 512 && timer.Elapsed.TotalSeconds < 20 && controller.Error is null && moved < 16 && live.World is not null; poll++)
        {
            Ff7AutoPlayWorldMovementState before = snow.CaptureAutoPlayMovementState();
            controller.Advance(1);
            if (live.World is not null && snow.CaptureAutoPlayMovementState().Player is { } player && (player.X != before.Player.X || player.Y != before.Player.Y || player.Z != before.Player.Z))
            {
                moved++;
            }
        }
        Console.WriteLine($"Original snow preview: actions={actions.Length}, normalCallbacks={controller.Steps}, moved={moved}, seconds={timer.Elapsed.TotalSeconds:F3}, field={live.MapId}, world={live.World?.Resources.World}, tick={live.World?.Process.Tick}, status={policy.Status}, error={controller.Error}");
        if (controller.Error is not null || moved == 0 || live.World is null && live.MapId != 686 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The ordinary snow-world controller did not continue its current native allocation unchanged.", controller.Failure);
        }
    }

    private static string PhysicalSnapshot(Ff7AutoPlayWorldMovementState state)
    {
        return JsonSerializer.Serialize(state with { Player = state.Player with { AnimationFrame = 0, PoseAnimation = 0, PoseFrame = 0, FrameRemainder = 0 } });
    }

    private static void Invoke(Ff7WorldRuntime world, string method, params object?[] arguments)
    {
        try
        {
            typeof(Ff7WorldRuntime).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            throw error.InnerException;
        }
    }
}
