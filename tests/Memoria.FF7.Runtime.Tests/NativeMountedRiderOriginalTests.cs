using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMountedRiderOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MOUNTED_RIDER_SAVE") ?? throw new InvalidOperationException("Select a mounted-rider fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        Ff7WorldRuntime world = session.World!;
        Ff7AutoPlayWorldControlObservation control = world.ReadAutoPlayChocoboControlCandidate() ?? throw new InvalidOperationException("No current native mounted-rider control was admitted.");
        string before = JsonSerializer.Serialize(new { Process = world.Process, Vm = world.Machine.Capture(), session.Progress });
        if (!world.ProveAutoPlayChocoboDismount(control) || before != JsonSerializer.Serialize(new { Process = world.Process, Vm = world.Machine.Capture(), session.Progress }))
        {
            throw new InvalidOperationException("The native landing fork failed or changed the live source.");
        }
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected field choice during native rider control."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        bool owned = false;
        bool settled = false;
        bool pulse = false;
        (int X, int Z)? returnedPosition = null;
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 30 && controller.Error is null && !pulse)
        {
            controller.Advance(1);
            owned |= policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldControl, WorldControl: not null };
            if (owned && world.HasAutoPlayControlCompleted(control))
            {
                settled = true;
                Ff7WorldEntity rider = world.CaptureAutoPlayMovementState().Player;
                returnedPosition ??= (rider.X, rider.Z);
                pulse = policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh } && (rider.X, rider.Z) != returnedPosition.Value;
            }
        }
        Console.WriteLine($"Native mounted rider: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={world.Process.Tick}, controlled={world.ControlledModel}, lock={world.Process.ControlLock}, owned={owned}, settled={settled}, onwardPulse={pulse}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !owned || !settled || !pulse || !SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native rider prerequisite did not settle through its actual control/UI continuation and onward input.", controller.Failure);
        }
    }
}
