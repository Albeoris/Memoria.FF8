using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayTransitionOwnershipTests
{
    public static void Run()
    {
        DirectChildAndTransitiveTransitions();
        WrongCompletedAndPreemptedOwners();
        ProofSurvivesSourceTeardownAndJson();
        Console.WriteLine("PASS native child-owned transition attribution");
    }

    private static void DirectChildAndTransitiveTransitions()
    {
        (FieldVmSnapshot vm, Ff7AutoPlayActiveEvent active) = Source();
        Check(Owned(vm, active, Request(0, 1)), "A direct accepted native transition lost its root ownership.");
        vm.Entities[1] = Waiting(vm.Entities[1], 2, 0);
        Check(Owned(vm, active, Request(1, 2)), "A synchronous requested child transition was not attributed to its accepted root.");
        vm.Entities[2] = Waiting(vm.Entities[2], 3, 1);
        string before = JsonSerializer.Serialize(vm);
        Check(Owned(vm, active, Request(2, 3)), "A transitive synchronous requested transition lost its accepted root.");
        Check(before == JsonSerializer.Serialize(vm), "Ownership projection changed saved PCs, variables or scheduler progress.");
        FieldScriptInputDemand input = new("child", vm.ProgramIdentity, 2, 3, 0, FieldScriptInputKind.Held, 0x20, true, true);
        Check(Ff7AutoPlayScriptedInput.IsInteractionInputOwner(input, vm, active, 4), "Input and transition ownership disagreed on the same synchronous request chain.");
        Check(!Ff7AutoPlayScriptedInput.IsInteractionInputOwner(input with { Priority = 1 }, vm, active, 4), "A child's unrelated priority inherited the accepted request chain.");
    }

    private static void WrongCompletedAndPreemptedOwners()
    {
        (FieldVmSnapshot vm, Ff7AutoPlayActiveEvent active) = Source();
        vm.Entities[1] = Waiting(vm.Entities[1], 2, 0);
        vm.Entities[2] = Waiting(vm.Entities[2], 3, 1);
        Check(!Owned(vm, active with { Accepted = false }, Request(2, 3)), "An unaccepted root acquired a transition.");
        Check(!Owned(vm, active with { Returned = true }, Request(2, 3)), "A completed root acquired a child transition.");
        Check(!Owned(vm, active with { Generation = 3 }, Request(2, 3)), "A stale source generation acquired a transition.");
        Check(!Owned(vm, active with { Entity = 3 }, Request(2, 3)), "A different root acquired a matching destination transition.");
        Check(!Owned(vm, active, Request(3, 3)), "An unrelated entity acquired accepted transition credit.");
        Check(!Owned(vm, active, Request(2, 2)), "A request at the wrong child priority acquired transition credit.");
        Check(Ff7Session.ReadTransitionOwnership(vm, Request(2, 3), active, 4, [new(0, 1, 0, 10)]) is null, "A root that returned during the current callback retained transition authority.");
        FieldVmSnapshot preemptedRoot = vm with { Entities = vm.Entities.ToArray() };
        preemptedRoot.Entities[0] = preemptedRoot.Entities[0] with { Priority = 0 };
        Check(!Owned(preemptedRoot, active, Request(2, 3)), "A preempted root authorized its former request descendants.");
        FieldVmSnapshot completedRoot = vm with { Entities = vm.Entities.ToArray() };
        completedRoot.Entities[0] = completedRoot.Entities[0] with { Priority = 7 };
        Check(!Owned(completedRoot, active, Request(2, 3)), "An autonomous completed root authorized a transition.");
        FieldVmSnapshot preemptedChild = vm with { Entities = vm.Entities.ToArray() };
        preemptedChild.Entities[1] = preemptedChild.Entities[1] with { Priority = 1 };
        Check(!Owned(preemptedChild, active, Request(2, 3)), "A preempted intermediary left transitive ownership active.");
        FieldVmSnapshot doneChild = vm with { Entities = vm.Entities.ToArray() };
        doneChild.Entities[1] = Waiting(doneChild.Entities[1], 2, 0, FieldSyncState.Done);
        Check(!Owned(doneChild, active, Request(2, 3)), "A completed synchronous intermediary authorized a transition.");
    }

    private static void ProofSurvivesSourceTeardownAndJson()
    {
        (FieldVmSnapshot vm, Ff7AutoPlayActiveEvent active) = Source();
        vm.Entities[1] = Waiting(vm.Entities[1], 2, 0);
        FieldHostRequest owner = Request(1, 2);
        Ff7NativeTransitionOwnership proof = Ff7Session.ReadTransitionOwnership(vm, owner, active, 4, [])!;
        Ff7NativeTransitionOwnership cold = JsonSerializer.Deserialize<Ff7NativeTransitionOwnership>(JsonSerializer.Serialize(proof))!;
        Ff7NativeTickObservation observation = new("source", 4, 10, [], (FieldTransitionCommand)owner.Command, "progress", owner, cold);
        vm.Entities[0] = vm.Entities[0] with { Priority = 7 };
        vm.Entities[1] = vm.Entities[1] with { Priority = 7 };
        Check(Ff7AutoPlayPolicy.IsOwnedTransition(observation, active), "A source-proved transition was re-evaluated against a torn-down or destination VM.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation with { TransitionOwnership = null }, active), "Destination or owner identity alone substituted for source request ownership.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation, active with { Script = 5 }), "A different accepted script reused another event's source proof.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation with { Generation = 5 }, active), "A source proof crossed native generations.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation with { TransitionOwner = owner with { Entity = 2 } }, active), "A changed request owner reused another child proof.");
    }

    private static bool Owned(FieldVmSnapshot vm, Ff7AutoPlayActiveEvent active, FieldHostRequest owner)
    {
        Ff7NativeTransitionOwnership? proof = Ff7Session.ReadTransitionOwnership(vm, owner, active, 4, []);
        Ff7NativeTickObservation observation = new("source", 4, 10, [], (FieldTransitionCommand)owner.Command, "progress", owner, proof);
        return Ff7AutoPlayPolicy.IsOwnedTransition(observation, active);
    }

    private static (FieldVmSnapshot, Ff7AutoPlayActiveEvent) Source()
    {
        FieldProgram program = new([0, 0], [new ushort[32], new ushort[32], new ushort[32], new ushort[32]]);
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldVmSnapshot vm = machine.Capture();
        vm.Entities[0] = vm.Entities[0] with { Priority = 1 };
        Ff7AutoPlayActiveEvent active = new("source-visit", "line:0:4", "condition", 4, 0, 4, Ff7AutoPlayTargetKind.LineTalk, null, 0, "witness", Accepted: true);
        return (vm, active);
    }

    private static FieldHostRequest Request(int entity, int priority)
    {
        return new(1, entity, priority, 0, new FieldTransitionCommand(472, 53, 140, 12, 128));
    }

    private static FieldEntitySnapshot Waiting(FieldEntitySnapshot entity, int priority, int owner, FieldSyncState state = FieldSyncState.Waiting)
    {
        FieldSyncState[] states = entity.SyncStates.ToArray();
        int[] owners = entity.SyncOwners.ToArray();
        states[priority] = state;
        owners[priority] = owner;
        return entity with { Priority = priority, SyncStates = states, SyncOwners = owners };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
