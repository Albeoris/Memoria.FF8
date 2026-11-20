using System.Diagnostics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRemoteLeafTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_REMOTE_LEAF_SAVE") ?? throw new InvalidOperationException("Select an external native LINE contact fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7!;
        var goal = save.AutoPlay?.NavigationGoal ?? throw new InvalidOperationException("The fixture has no retained native LINE goal.");
        int field = int.Parse(goal.NavigationKey.Split(':')[1]);
        string[] identity = goal.EventId!.Split(':');
        int entity = int.Parse(identity[3]);
        int script = int.Parse(identity[5]);
        int EntryCount(Ff7AutoPlayState state) => state.Coverage.Visits.Single(visit => visit.Visit.Key == goal.NavigationKey.Split(":nav:")[0]).Events.Single(item => item.Event.Id == goal.EventId).Conditions?.ConsumedEntries.Count ?? 0;
        int beforeEntries = EntryCount(save.AutoPlay!);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native contact continuation reached a new choice checkpoint."));
        // Mirror only the existing explicit App start's observational watchdog window.
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        int entryProgramCounter = session.Field.Scripts.Entities[entity].ScriptOffsets[script];
        long admissionTick = -1;
        bool admitted = false;
        bool inputSatisfied = false;
        int? child = null;
        bool childWork = false;
        long sourceGeneration = save.FieldGeneration;
        bool ladderStarted = false;
        bool ladderCompleted = false;
        FieldHostRequest? departure = null;
        session.NativeInteractionScriptRequested += admission =>
        {
            if (session.FieldGeneration == sourceGeneration && session.MapId == field && admission.Entity == entity && admission.Script == script && admission.Accepted && admission.Started)
            {
                FieldScriptInputDemand demand = session.Machine.ReadEntryInputDemand(entity, script) ?? throw new InvalidOperationException("The native contact lost its original input gate.");
                Check(demand.Kind == FieldScriptInputKind.Pressed && demand.Mask == 0x0220 && demand.Mapped, "The fixture's original native pressed-button contract changed.");
                inputSatisfied |= (session.Machine.Input.Pressed & demand.Mask) != 0;
                admitted = true;
                admissionTick = session.Machine.Tick;
            }
        };
        session.Machine.ScriptDispatched += dispatch =>
        {
            if (session.FieldGeneration == sourceGeneration && session.MapId == field && admitted && dispatch.SourceEntity == entity && dispatch.SourcePriority == 1 && dispatch.SourceProgramCounter == entryProgramCounter + 4 && dispatch.Tick == admissionTick && dispatch.TargetScript == 3 && dispatch.TargetPriority == 6 && dispatch.RequestMode == 1 && dispatch.Accepted)
            {
                child = dispatch.TargetEntity;
            }
        };
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (session.FieldGeneration == sourceGeneration && session.MapId == field && child == request.Entity && request.Priority == 6 && request.Command is FieldActorMove or FieldActorPlacement or FieldDialogueCommand or FieldMenuCommand)
            {
                childWork = true;
            }
            if (session.FieldGeneration == sourceGeneration && child == request.Entity && request.Priority == 6 && request.Command is FieldActorLadder)
            {
                ladderStarted = true;
                ladderCompleted |= reply.Completed;
            }
        };
        session.NativeFieldDeparting += _ =>
        {
            if (session.FieldGeneration == sourceGeneration && session.MapId == field)
            {
                FieldHostRequest? owner = session.Host.Capture().Receipts.SingleOrDefault(receipt => receipt.Request.Id == session.Host.Process.GlobalRequestId)?.Request;
                if (owner is { Priority: 6, ProgramCounter: 0x035B, Command: FieldTransitionCommand { Map: 519 } } && owner.Entity == child && owner.Command == session.Host.Process.Transition)
                {
                    ladderCompleted |= session.Host.Process.Actors.Any(actor => actor.Entity == child && actor.Ladder is { Completed: true });
                    departure = owner;
                }
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        for (int slice = 0; slice < 600 && controller.Running && elapsed.Elapsed < TimeSpan.FromSeconds(15); slice++)
        {
            controller.Advance(1);
            if (departure is not null && EntryCount(policy.State) > beforeEntries || session.FieldGeneration != sourceGeneration && departure is null || session.Host.HasUnfinishedBattle)
            {
                break;
            }
        }
        Check(controller.Error is null && admitted && inputSatisfied && child is not null && childWork, $"The native LINE did not admit its real input-gated child work: {controller.Error}; status={policy.Status}.");
        Check(ladderStarted && ladderCompleted && departure is not null && session.MapId == 519 && session.FieldGeneration != sourceGeneration, $"The actual party child did not complete its native ladder and typed source MAPJUMP: ladderStarted={ladderStarted}, completed={ladderCompleted}, field={session.MapId}, steps={controller.Steps}.");
        Check(EntryCount(policy.State) > beforeEntries, "The actual native LINE entry was not recorded after its real admission and work.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Native LINE continuation changed its authoritative source.");
        Console.WriteLine($"PASS original native LINE contact/pressed0220/REQparty0-script3-p6/realwork; callbacks={controller.Steps}; ladder-completed={ladderCompleted}; child-mapjump={departure?.ProgramCounter:X4}; destination={session.MapId}; consumed={EntryCount(policy.State) - beforeEntries}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
