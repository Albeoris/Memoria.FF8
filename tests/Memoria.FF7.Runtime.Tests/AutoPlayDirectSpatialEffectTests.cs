using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;

internal static class AutoPlayDirectSpatialEffectTests
{
    internal static void Run()
    {
        FieldDialogueCommand command = new(0, 0, 0, 1, 0);
        FieldSpatialEffectOwner owner = new(0, 7, 104, command, [130, 160]);
        Ff7AutoPlayActiveEvent source = new("visit", "spatial:0:0068:effect:triangle:0", "condition", 1, 0, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, null, 0, "before", DirectEffectOwner: owner);
        FieldHostRequest request = new(1, owner.Entity, owner.Priority, owner.ProgramCounter, command);
        Ff7NativeScriptActivity Admission(FieldHostRequest packet)
        {
            return new(HostOperation: new(packet, new(false)));
        }
        Ff7NativeScriptActivity Boundary(int entity = 0, int priority = 7, int pc = 130)
        {
            return new(Boundary: new(entity, priority, pc, 0x00));
        }
        foreach (FieldHostRequest unrelated in new[] { request with { Entity = 1 }, request with { Priority = 1 }, request with { ProgramCounter = 105 }, request with { Command = command with { Text = 1 } } })
        {
            Check(Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(source, [Admission(unrelated), Boundary()]) == source, "An ambient native operation with a different entity, priority, source PC or typed operand must not admit the selected direct effect.");
        }
        Check(Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(source, [Boundary()]) == source, "A source boundary observed before actual host admission must not complete an effect.");
        Ff7AutoPlayActiveEvent admitted = Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(source, [Admission(request)]);
        Check(admitted.Accepted && !admitted.Returned, "The exact actual pending native host operation must admit its owner without inventing completion.");
        foreach (Ff7NativeScriptActivity unrelated in new[] { Boundary(entity: 1), Boundary(priority: 1), Boundary(pc: 131), new Ff7NativeScriptActivity(Return: new(0, 1, 130, 0)) })
        {
            Check(Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(admitted, [unrelated]) == admitted, "Only an executed source-proved exit instruction may finish an accepted direct effect.");
        }
        Ff7AutoPlayActiveEvent finished = Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(source, [Boundary(), Admission(request), Boundary()]);
        Check(finished is { Accepted: true, Returned: true }, "Same-callback ordered admission and subsequent proved body exit must remain observable.");
        Ff7AutoPlayState savedState = new() { Active = admitted };
        Ff7AutoPlayActiveEvent cold = savedState.Clone().Active!;
        Check(cold.DirectEffectOwner is { } restored && restored.Command == command && restored.ExitProgramCounters.SequenceEqual(owner.ExitProgramCounters), "Cold observation state must preserve the typed direct-effect owner and completion contract.");
        var legacyNode = JsonSerializer.SerializeToNode(source)!.AsObject();
        legacyNode.Remove(nameof(Ff7AutoPlayActiveEvent.DirectEffectOwner));
        Ff7AutoPlayActiveEvent legacy = legacyNode.Deserialize<Ff7AutoPlayActiveEvent>()!;
        Check(legacy.DirectEffectOwner is null, "An older active event must retain the absent direct-effect default without replay or rejection.");
        var field = NativeDialogueTests.Field();
        Ff7FieldHost host = new(field, (_, _) => 1, new Ff7FieldProcess { Tick = 42, Windows = [new Ff7Window(0, 10, 20, 200, 70)] });
        List<Ff7NativeHostOperation> operations = [];
        host.NativeOperationExecuted += (packet, reply) => operations.Add(new(packet, reply));
        FieldHostRequest immediate = new(10, 0, 7, 90, new FieldControlCommand(FieldControlKind.MenuDisabled, 1));
        host.Execute(immediate);
        host.Execute(immediate);
        Check(operations.Count == 1 && operations[0].Request == immediate && operations[0].Reply.Completed, "Cached completed host receipts must not become new native operation observations.");
        host.Execute(request);
        Check(operations.Count == 2 && operations[^1].Request == request && !operations[^1].Reply.Completed, "Actual pending dialogue dispatch must retain its exact typed owner packet.");
        FieldMachine machine = new(FieldProgram.FromPcScripts(field.Scripts));
        machine.Initialize();
        FieldVmSnapshot vm = machine.Capture() with { NextRequestId = 20 };
        Check(Ff7AutoPlayScriptedInput.ReadInteractionOwners(vm, admitted, 1).TryGetValue(0, out int priority) && priority == 7 && Ff7AutoPlayScriptedInput.ReadInteractionOwners(vm, source, 1).Count == 0, "Only an admitted direct-effect source may own priority-seven input continuations.");
        Ff7SessionSave snapshot = new()
        {
            ResourceProfile = "authored-dialogue",
            Language = "en",
            FieldName = field.Name,
            FieldGeneration = 1,
            Progress = new Ff7Progress { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [], Materia = [] },
            Vm = vm,
            Process = host.Capture()
        };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(savedState.Coverage);
        int checkpoints = 0;
        Ff7AutoPlayChoiceWorkflow choices = new(() => snapshot, planner, savedState, (_, before) =>
        {
            checkpoints++;
            Check(before.Process.Receipts.Any(receipt => receipt.Request == request), "A direct choice checkpoint must retain its exact native priority-seven dialogue receipt.");
            return "authored-before.ffsave";
        });
        Check(choices.ChooseSelection(snapshot.Process.Dialogues.Single()) == 0 && checkpoints == 1 && savedState.Choice?.BeforeActive?.DirectEffectOwner?.Command == command, "The existing choice workflow must preserve the admitted direct-effect owner and exact native choice domain.");
        OriginalDirectEffect();
    }

    private static void OriginalDirectEffect()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SPATIAL_EFFECT_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original direct spatial effect: configure original resources and MEMORIA_FF7_SPATIAL_EFFECT_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The original direct-effect source has no FFVII state.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        string native = JsonSerializer.Serialize(session.Capture() with { AutoPlay = null });
        using (Ff7Session cold = Ff7Session.Restore(installation, saved))
        {
            Check(native == JsonSerializer.Serialize(cold.Capture() with { AutoPlay = null }), "Original direct-effect independent cold restores differ in native state.");
        }
        _ = new Ff7AutoPlayDiscovery(session).Observe();
        Check(native == JsonSerializer.Serialize(session.Capture() with { AutoPlay = null }), "Read-only direct-effect discovery changed the native continuation.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded direct-effect admission regression reached a choice checkpoint before its expected native source."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        FieldHostRequest? accepted = null;
        string? eventId = null;
        session.NativeTickCompleted += observation =>
        {
            if (policy.State.Active is not { Kind: Ff7AutoPlayTargetKind.AutonomousSpatial, Accepted: true, DirectEffectOwner: { } effect } active || effect.Command is not FieldDialogueCommand { FirstChoice: not null })
            {
                return;
            }
            foreach (Ff7NativeScriptActivity activity in observation.ScriptActivity ?? [])
            {
                if (activity.HostOperation is { } operation && Ff7AutoPlayPolicy.MatchesDirectSpatialEffect(effect, operation.Request))
                {
                    accepted = operation.Request;
                    eventId = active.EventId;
                    Check(effect.Priority == 7 && !operation.Reply.Completed, "The original direct choice must admit its actual pending priority-seven native host operation.");
                }
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        controller.Start();
        for (int callback = 0; callback < 1024 && accepted is null; callback++)
        {
            controller.Advance(1);
            Check(controller.Error is null, controller.Error ?? "The original direct-effect route failed.");
        }
        Check(accepted is not null, $"No exact direct spatial choice owner was admitted within 1024 ordinary callbacks; field={session.Field.Name}, tick={session.Host.Process.Tick}, active={policy.State.Active?.EventId}, status={policy.Status}.");
        FieldHostRequest packet = accepted!;
        Check((session.Machine.Capture().PendingRequests ?? []).Any(request => request == packet) && session.Host.Process.Dialogues.Any(dialogue => dialogue.RequestId == packet.Id && dialogue.FirstChoice is not null && !dialogue.Completed), "The original admitted direct effect did not retain its exact priority-seven pending dialogue owner.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Original direct-effect admission changed the supplied standalone bytes.");
        Console.WriteLine($"PASS original direct spatial effect {eventId}, owner={packet.Entity}/{packet.Priority}@{packet.ProgramCounter:X4}, request={packet.Id}, {controller.Steps} ordinary callbacks, field={session.Field.Name}, tick={session.Host.Process.Tick}; stopped before choice selection, source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
