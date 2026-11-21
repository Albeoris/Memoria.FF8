using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGatewayCrossingOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_CROSSING_SAVE") ?? throw new InvalidOperationException("Select a gateway crossing fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during a gateway approach."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (session.MapId == 459 && controller.Error is null && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native gateway crossing: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, triangle={player.Triangle}, active={policy.State.Active?.EventId}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || session.MapId != 460 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native long-segment gateway did not reach its actual destination.", controller.Failure);
        }
    }
}
