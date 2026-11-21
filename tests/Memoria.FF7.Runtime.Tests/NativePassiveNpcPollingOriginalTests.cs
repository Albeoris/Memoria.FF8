using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePassiveNpcPollingOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PASSIVE_POLL_SAVE") ?? throw new InvalidOperationException("Select a native asynchronous activation fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the escape transition."));
        bool nextLadder = false;
        bool released = false;
        session.Machine.ScriptDispatched += request => nextLadder |= request is { SourceEntity: 9, TargetEntity: 2, TargetScript: 4, Accepted: true };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.MapId == 415 || nextLadder && session.MapId == 414 && session.Machine.Variables.ReadByte(FieldBank.MapByte, 9) == 1, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 60 && controller.Steps < 3000 && controller.Running && controller.Error is null && !controller.Completed)
        {
            controller.Advance(1);
            released |= session.MapId == 414 && session.Machine.Variables.ReadByte(FieldBank.MapByte, 9) == 1;
        }
        Console.WriteLine($"Native passive polling: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, nextLadder={nextLadder}, nativeGuardReleased={released}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !nextLadder || !released || !controller.Completed || !SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The returned activation did not permit its next genuine native ladder and guard release.", controller.Failure);
        }
    }
}
