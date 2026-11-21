using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSynchronousTurnOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SYNCHRONOUS_TURN_SAVE") ?? throw new InvalidOperationException("Select an owned native turn fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        bool turnCompleted = false;
        session.Host.NativeOperationExecuted += (request, reply) => turnCompleted |= request is { Id: 4382, Entity: 11, Priority: 6, ProgramCounter: 0x999, Command: FieldActorTurn } && reply.Completed;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.MapId == 293 || turnCompleted && session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed), readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 15 && controller.Steps < 3000 && controller.Running && controller.Error is null && !controller.Completed)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native synchronous turn: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Machine.Tick}, turnCompleted={turnCompleted}, field={session.MapId}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (!turnCompleted || controller.Error is not null || !controller.Completed || !SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The admitted native turn and scene did not advance normally.", controller.Failure);
        }
    }
}
