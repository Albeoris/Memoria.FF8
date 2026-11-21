using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldDestinationTopologyOriginalTests
{
    internal static void RunLandingContinuation()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_MODE_DESTINATION_SAVE") ?? throw new InvalidOperationException("Select a native landing continuation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("Unexpected choice."));
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        ushort? destination = policy.State.DestinationField;
        var initial = session.World!.CaptureAutoPlayMovementState();
        HashSet<Ff7WorldTerrainReference> visited = [];
        for (int i = 0; i < 100000 && controller.Steps < 24 && timer.Elapsed.TotalSeconds < 35 && controller.Error is null; i++)
        {
            controller.Advance(1);
            var state = session.World!.CaptureAutoPlayMovementState();
            if (state.Input.GroundTriangle is { } ground)
            {
                visited.Add(ground);
            }
        }
        var final = session.World!.CaptureAutoPlayMovementState();
        Console.WriteLine($"Original landing continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, distinctGround={visited.Count}, start={initial.Player.X},{initial.Player.Z}, final={final.Player.X},{final.Player.Z}, ground={final.Input.GroundTriangle}, destination={policy.State.DestinationField}, error={controller.Error}");
        if (controller.Steps != 24 || visited.Count < 5 || controller.Error is not null || policy.State.DestinationField != destination || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The normal landing continuation repeated a local terrain loop or changed its destination/source.");
        }
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_MODE_DESTINATION_SAVE") ?? throw new InvalidOperationException("Select a native destination topology fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The bounded world topology continuation reached a new choice."));
        ushort? destination = policy.State.DestinationField;
        bool pendingObserved = false;
        bool selected = false;
        var controller = new Ff7AutoPlayController(session, driver =>
        {
            var result = policy.ChooseInput(driver);
            pendingObserved |= driver.PlanningPending;
            selected |= policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldControl, WorldModeGoal: not null };
            return result;
        }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 100000 && timer.Elapsed.TotalSeconds < 45 && !selected && controller.Error is null; polls++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Original world destination topology: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, pendingObserved={pendingObserved}, selected={selected}, active={policy.State.Active?.EventId}, rider={policy.State.Active?.WorldModeGoal?.RiderModel}, destination={policy.State.DestinationField}, status={policy.State.Status}, error={controller.Error}");
        if (!pendingObserved || !selected || controller.Error is not null || policy.State.DestinationField != destination || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The normal world destination did not retain and complete its actual topology prerequisite.");
        }
    }
}
