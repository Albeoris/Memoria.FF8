using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayScriptedInputTests
{
    public static void Run()
    {
        CheckInteractionInputOwnership();
        FieldScriptInputDemand demand = new("program:0:1:0001", "program", 0, 1, 1, FieldScriptInputKind.Held, 0x220, true, true);
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new()).Held == 0x20, "Held demand did not choose one admitted native button.");
        demand = demand with { Kind = FieldScriptInputKind.Pressed };
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new(Held: 0x20, RawHeld: 0x20)).Held == 0, "KEYON must release an already held button before its fresh edge.");
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new()).Held == 0x20, "KEYON did not emit its press after release.");
        Check(!Ff7AutoPlayScriptedInput.IsInputSatisfied(demand, new(Released: 0x20)), "A KEYON release setup tick must not consume the native event.");
        Check(!Ff7AutoPlayScriptedInput.IsInputSatisfied(demand, new(Held: 0x20)), "A held button without a fresh edge must not consume KEYON admission.");
        Check(Ff7AutoPlayScriptedInput.IsInputSatisfied(demand, new(Held: 0x20, Pressed: 0x20)), "A fresh mapped press failed its native entry gate.");
        Check(!Ff7AutoPlayScriptedInput.IsInputSatisfied(demand, new(RawPressed: 0x20)), "Mapped entry admission used the raw pressed bank.");
        Ff7Line line = new(0, 1, 0, 0, 0, 100, 0, 0);
        Check(!Ff7AutoPlayPolicy.IsLineEntryReady(Ff7AutoPlayTargetKind.LineTouch, line, false), "An unobserved contact must not consume an entry pulse before native LINE dispatch.");
        Check(Ff7AutoPlayPolicy.IsLineEntryReady(Ff7AutoPlayTargetKind.LineTouch, line with { Touch = true }, false), "A saved native TOUCH request must receive its entry pulse before this tick's movement updates contacts.");
        Check(!Ff7AutoPlayPolicy.IsLineEntryReady(Ff7AutoPlayTargetKind.LineEnter, line with { Touch = true }, false), "Persistent TOUCH must not substitute for an ENTER edge.");
        Check(Ff7AutoPlayPolicy.IsLineEntryReady(Ff7AutoPlayTargetKind.LineEnter, line with { Enter = true }, false), "Native ENTER edge lost its entry pulse.");
        demand = demand with { Kind = FieldScriptInputKind.Released };
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new()).Held == 0x20, "KEYOFF must establish a held button before its release edge.");
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new(Held: 0x20, RawHeld: 0x20)).Held == 0, "KEYOFF failed to emit a distinct release tick.");
        demand = demand with { Mapped = false };
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new(Held: 0x20, RawHeld: 0)).Held == 0x20, "Raw KEYOFF used the mapped held bank.");
        Check(Ff7AutoPlayScriptedInput.SelectInput(demand, new(Held: 0, RawHeld: 0x20)).Held == 0, "Raw KEYOFF ignored its native held bank.");
        FieldScriptInputDemand direction = demand with { Identity = "direction", Kind = FieldScriptInputKind.Held, Mapped = false, Mask = 0x1000 };
        FieldScriptInputDemand confirm = direction with { Identity = "confirm", Mapped = true, Mask = 0x220 };
        FieldScriptInputDemand cancel = confirm with { Identity = "cancel", Mask = 0x240 };
        Check(Ff7AutoPlayScriptedInput.SelectDemand([direction, confirm, cancel]) == cancel, "A native menu loop selected navigation or Confirm before its mapped Cancel gate.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([direction, confirm]) == confirm, "A native menu loop selected navigation before mapped Confirm.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([cancel with { AwaitingInput = false }, direction, confirm]) == confirm, "An unproved Cancel branch displaced a proved input wait.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([cancel with { Mapped = false }, confirm]) == confirm, "A raw Cancel-shaped bitmask displaced mapped Confirm.");
        FieldScriptInputDemand earlierOwner = direction with { Entity = 0, Priority = 0 };
        Check(Ff7AutoPlayScriptedInput.SelectDemand([cancel, earlierOwner]) == earlierOwner, "Button preference displaced native script priority.");
        earlierOwner = direction with { Entity = 0 };
        Check(Ff7AutoPlayScriptedInput.SelectDemand([cancel with { Entity = 1 }, earlierOwner]) == earlierOwner, "Button preference displaced native entity ordering.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([direction, direction with { Identity = "second-direction", Mask = 0x2000 }]) == direction, "Equivalent navigation gates lost stable source ordering.");
        Check(Ff7AutoPlayScriptedInput.SelectDemand([]) is null && Ff7AutoPlayScriptedInput.SelectDemand([cancel with { AwaitingInput = false }]) is null, "No proved wait fabricated an autoplay button demand.");
    }

    private static void CheckInteractionInputOwnership()
    {
        FieldProgram program = new([0, 0], [new ushort[32], new ushort[32], new ushort[32], new ushort[32]]);
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldVmSnapshot vm = machine.Capture();
        vm.Entities[0] = vm.Entities[0] with { Priority = 1 };
        FieldScriptInputDemand demand = new("interaction-input", program.Identity, 0, 1, 0, FieldScriptInputKind.Held, 0x20, true, true);
        Ff7AutoPlayActiveEvent active = new("field-visit", "actor:0:script:1", "native-condition", 4, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "before-native-request", Accepted: true);
        Check(!Owned(null), "An unlocked background input poller acquired interaction ownership without an active event.");
        Check(!Owned(active with { Generation = 3 }), "An active event from an older native generation authorized background input.");
        Check(!Owned(active with { Accepted = false }), "An unaccepted interaction authorized a native input wait.");
        Check(!Owned(active with { Returned = true }), "A returned interaction continued authorizing native input waits.");
        Check(!Owned(active with { Entity = -1 }) && !Owned(active with { Entity = vm.Entities.Length }), "An invalid active native owner authorized an input wait.");
        Check(Owned(active), "An accepted native priority-one interaction could not supply its own input wait.");
        Check(!Ff7AutoPlayScriptedInput.IsInteractionInputOwner(demand with { Priority = 7 }, vm, active, 4), "The interaction owner's autonomous priority-seven poller stole unlocked navigation input.");
        Check(!Ff7AutoPlayScriptedInput.IsInteractionInputOwner(demand with { Entity = 3 }, vm, active, 4), "An unrelated native entity stole an accepted interaction's input ownership.");

        FieldEntitySnapshot[] entities = vm.Entities.ToArray();
        entities[1] = Waiting(entities[1], 2, 0);
        entities[2] = Waiting(entities[2], 3, 1);
        FieldVmSnapshot chain = vm with { Entities = entities };
        FieldScriptInputDemand childDemand = demand with { Entity = 2, Priority = 3 };
        string before = System.Text.Json.JsonSerializer.Serialize(chain);
        Check(Ff7AutoPlayScriptedInput.IsInteractionInputOwner(childDemand, chain, active, 4), "A transitive synchronous native request lost its accepted interaction input owner.");
        Check(!Ff7AutoPlayScriptedInput.IsInteractionInputOwner(demand with { Entity = 3 }, chain, active, 4), "A synchronous request chain admitted an unrelated input poller.");
        Check(before == System.Text.Json.JsonSerializer.Serialize(chain), "Reading synchronous interaction ownership mutated native scheduler state.");

        FieldEntitySnapshot[] completed = entities.ToArray();
        FieldSyncState[] doneStates = completed[1].SyncStates.ToArray();
        doneStates[2] = FieldSyncState.Done;
        completed[1] = completed[1] with { SyncStates = doneStates };
        Check(!Ff7AutoPlayScriptedInput.IsInteractionInputOwner(childDemand, chain with { Entities = completed }, active, 4), "A completed synchronous owner retained authority over a descendant input wait.");
        FieldEntitySnapshot[] inactive = entities.ToArray();
        inactive[1] = inactive[1] with { Priority = 3 };
        Check(!Ff7AutoPlayScriptedInput.IsInteractionInputOwner(childDemand, chain with { Entities = inactive }, active, 4), "A stale waiting request below the current native scheduling priority authorized input.");
        FieldEntitySnapshot[] preempted = entities.ToArray();
        preempted[1] = preempted[1] with { Priority = 1 };
        Check(!Ff7AutoPlayScriptedInput.IsInteractionInputOwner(demand with { Entity = 1, Priority = 1 }, chain with { Entities = preempted }, active, 4), "An unrelated higher-priority request inherited the suspended synchronous child's input ownership.");
        Check(!Ff7AutoPlayScriptedInput.IsInteractionInputOwner(childDemand, chain with { Entities = preempted }, active, 4), "A preempted synchronous child continued authorizing a transitive input wait.");

        bool Owned(Ff7AutoPlayActiveEvent? owner)
        {
            return Ff7AutoPlayScriptedInput.IsInteractionInputOwner(demand, vm, owner, 4);
        }

        static FieldEntitySnapshot Waiting(FieldEntitySnapshot entity, int priority, int owner)
        {
            FieldSyncState[] states = entity.SyncStates.ToArray();
            int[] owners = entity.SyncOwners.ToArray();
            states[priority] = FieldSyncState.Waiting;
            owners[priority] = owner;
            return entity with { Priority = priority, SyncStates = states, SyncOwners = owners };
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
