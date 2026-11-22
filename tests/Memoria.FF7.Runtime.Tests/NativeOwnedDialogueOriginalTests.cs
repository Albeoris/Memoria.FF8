using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeOwnedDialogueOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_OWNED_DIALOGUE_SAVE") ?? throw new InvalidOperationException("Select an external native button dialogue continuation.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), saved);
        Ff7Dialogue dialogue = saved.Process.Dialogues.Single(item => !item.Completed && item.AwaitingInput);
        FieldHostRequest message = saved.Vm.PendingRequests!.Single(request => request.Id == dialogue.RequestId);
        var workers = saved.AutoPlay!.Active!.ExecutionProvenance!.Owners.Where(owner => !owner.Queued && owner.ParentEntity == message.Entity && owner.ParentPriority == message.Priority).Select(owner => (owner.Entity, owner.Priority)).ToHashSet();
        FieldScriptInputDemand continuation = session.Machine.ReadCurrentInputDemands().Single(demand => workers.Contains((demand.Entity, demand.Priority)) && session.Machine.CanCloseDialogueFromCurrentInput(demand, dialogue.Window, Press(demand.Mask)));
        string rewards = JsonSerializer.Serialize(new { session.Progress.Gil, session.Progress.Inventory, session.Progress.Materia, session.Progress.CompletedBattles, session.Progress.FieldBattleResults });
        bool closed = false;
        bool messageCompleted = false;
        bool workerReturned = false;
        ushort actualPress = 0;
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request.Command is FieldWindowCommand command && command.Window == dialogue.Window && command.Operation is FieldWindowOperation.Close or FieldWindowOperation.CloseAndWait && workers.Contains((request.Entity, request.Priority)))
            {
                if (actualPress == 0)
                {
                    actualPress = session.Machine.Input.RawPressed;
                }
                closed |= reply.Completed;
            }
            messageCompleted |= request.Id == message.Id && reply.Completed;
        };
        session.NativeTickCompleted += observation => workerReturned |= observation.ScriptReturns.Any(returned => workers.Contains((returned.Entity, returned.Priority)));
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native button continuation must not explore optional choices."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && !controller.Paused && controller.Steps < 400 && timer.Elapsed.TotalSeconds < 20)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Original owned button dialogue: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, rawPressed={actualPress:X4}, close={closed}, messageCompleted={messageCompleted}, workerReturned={workerReturned}, sourceCompleted={policy.State.SourceGoal?.Completed}, paused={controller.Paused}, locks={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, status={policy.State.Status}, error={controller.Error}.");
        Check(controller.Error is null && controller.Paused && policy.State.SourceGoal is { Completed: true } && policy.State.Active is null && closed && messageCompleted && workerReturned && (actualPress & continuation.Mask) != 0 && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled, "The real native button close and remaining owned scene did not return and complete its exact source.");
        Check(saved.Progress.Party.SequenceEqual(session.Progress.Party) && rewards == JsonSerializer.Serialize(new { session.Progress.Gil, session.Progress.Inventory, session.Progress.Materia, session.Progress.CompletedBattles, session.Progress.FieldBattleResults }), "The button continuation changed the native selected party or retained guard rewards.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The native button-dialogue standalone was changed.");
    }

    private static FieldInputState Press(ushort mask)
    {
        ushort bit = (ushort)(mask & -mask);
        return new(Held: bit, Pressed: bit, RawHeld: bit, RawPressed: bit);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
