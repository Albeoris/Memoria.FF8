using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldDestinationTopologyOriginalTests
{
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
