using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDestinationReadinessOriginalTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DESTINATION_READINESS_SAVE") ?? throw new InvalidOperationException("Select a destination readiness fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        var saved = CombinedSessionCodec.Read(input);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), saved.Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The destination readiness regression reached a new choice."));
        bool moved = false;
        var controller = new Ff7AutoPlayController(session, driver =>
        {
            var result = policy.ChooseInput(driver);
            moved |= result.Horizontal != 0 || result.Vertical != 0 || result.Talk;
            return result;
        }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 10000 && timer.Elapsed.TotalSeconds < 30 && !moved && controller.Error is null; polls++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Original destination readiness: callbacks={controller.Steps}, tick={session.Machine.Tick}, moved={moved}, active={policy.State.Active?.EventId}, destination={policy.State.DestinationField}, status={policy.State.Status}, error={controller.Error}");
        if (!moved || policy.State.Active is null || policy.State.DestinationField != saved.Ff7!.AutoPlay!.DestinationField || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("Ambient work still blocked normal destination movement or the original source changed.");
        }
    }
}
