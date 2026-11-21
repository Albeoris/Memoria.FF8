using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeSynchronousTurnReadinessTests
{
    internal static void Run()
    {
        byte[] script = [3, 1, 0xC1, 0, 0xB4, 0, 64, 0, 10, 1, 0];
        FieldEntitySnapshot parent = new(0, 1, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        parent.SavedProgramCounters[7] = 3;
        FieldEntitySnapshot child = new(4, 6, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray(), ModelBound: true);
        child.SavedProgramCounters[7] = 10;
        child.SyncStates[6] = FieldSyncState.Waiting;
        child.SyncOwners[6] = 0;
        FieldHostRequest request = new(7, 1, 6, 4, new FieldActorTurn(64, 0, 10, 1));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "turn-fixture", 1, true, new byte[1280], new byte[256], [parent, child], PendingRequests: [request]);
        Ff7Actor actor = new() { Slot = 0, Entity = 1, Model = 0, TurnMode = 1, TurnStep = 2, TurnSteps = 10, TurnStart = 0, TurnEnd = 64, Visible = true };
        Ff7FieldProcess process = new() { Actors = [actor] };
        bool Wait(FieldVmSnapshot? current = null, Ff7FieldProcess? actual = null, byte[]? code = null) => new Ff7AutoPlayNativeReadiness().ShouldWaitForSynchronousHostCompletion(1, "source", current ?? vm, actual ?? process, code ?? script);
        string before = JsonSerializer.Serialize(new { vm, process, script });
        Check(Wait(), "A real admitted synchronous finite turn was stranded before planning.");
        Check(Wait(actual: process with { Actors = [actor with { TurnMode = 3, TurnStep = 10 }] }), "The completed finite turn's actual pending acknowledgement was stranded.");
        Check(!Wait(vm with { PendingRequests = [] }), "Absent native turn debt received readiness.");
        foreach (FieldHostRequest invalid in new[] { request with { Entity = 0 }, request with { Priority = 5 }, request with { ProgramCounter = 5 }, request with { Command = new FieldActorTurn(64, 0, 0, 1) }, request with { Command = new FieldActorTurn(64, 0, 10, 3) }, request with { Command = new FieldDialogueCommand(0, 1) } })
        {
            Check(!Wait(vm with { PendingRequests = [invalid] }), "A mismatched owner, nonfinite turn or unrelated host request acquired readiness.");
        }
        foreach (Ff7Actor invalid in new[] { actor with { EntityBound = false }, actor with { Entity = 0 }, actor with { TurnMode = 2 }, actor with { TurnSteps = 9 }, actor with { TurnStep = -1 }, actor with { TurnStep = 11 } })
        {
            Check(!Wait(actual: process with { Actors = [invalid] }), "An unrelated or invalid physical turn acquired readiness.");
        }
        Check(!Wait(vm with { Entities = [parent with { Priority = 7 }, child] }) && !Wait(vm with { Entities = [parent, child with { Priority = 7 }] }), "An ambient parent or child acquired synchronous turn readiness.");
        Check(!Wait(vm with { Entities = [parent, child with { ModelBound = false }] }), "An unbound VM child acquired physical turn readiness.");
        Check(!Wait(vm with { Entities = [parent, child with { SavedProgramCounters = new int[8] }] }), "An unadmitted child acquired readiness from a pending request alone.");
        Check(!Wait(vm with { Entities = [parent, child with { SyncOwners = Enumerable.Repeat(-1, 8).ToArray() }] }), "An orphan turn borrowed a synchronous parent.");
        byte[] wrongTarget = script.ToArray();
        wrongTarget[1] = 0;
        byte[] wrongPriority = script.ToArray();
        wrongPriority[2] = 0xA1;
        byte[] wrongSteps = script.ToArray();
        wrongSteps[8] = 9;
        Check(!Wait(code: wrongTarget) && !Wait(code: wrongPriority) && !Wait(code: wrongSteps), "The current native request operands did not bind the synchronous turn.");
        byte[] signed = script.ToArray();
        signed[4] = 0xB5;
        signed[9] = 2;
        Check(Wait(vm with { PendingRequests = [request with { Command = new FieldActorTurn(-64, 2, 10, 2, true) }] }, process with { Actors = [actor with { TurnMode = 2 }] }, signed), "A signed B5 finite turn lost its exact current native request.");
        byte[] moveScript = script.ToArray();
        moveScript[4] = 0xA8;
        FieldVmSnapshot moving = vm with { PendingRequests = [request with { Command = new FieldActorMove(20, 30) }] };
        Check(Wait(moving, process with { Motions = [new(7, 1, 20, 30, true, true)] }, moveScript), "A real literal MOVE in the same admitted synchronous chain was stranded.");
        Check(!Wait(moving, process, moveScript), "A literal MOVE without actual motion or endpoint acknowledgement received readiness.");
        byte[] waitScript = script.ToArray();
        waitScript[4] = 0x24;
        waitScript[5] = 20;
        waitScript[6] = 0;
        FieldVmSnapshot waitVm = vm with { Entities = [parent, child with { WaitCounter = 20 }], PendingRequests = [] };
        Check(Wait(waitVm, code: waitScript) && !Wait(waitVm with { Entities = [parent, child] }, code: waitScript), "An authentic finite WAIT or its exhausted counter received incorrect synchronous readiness.");
        Check(before == JsonSerializer.Serialize(new { vm, process, script }), "Turn readiness changed native PCs, sync, variables or physical actor facts.");
        Ff7AutoPlayNativeReadiness frozen = new();
        bool waiting = true;
        for (int tick = 0; tick <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit; tick++)
        {
            waiting = frozen.ShouldWaitForSynchronousHostCompletion(1, "source", vm with { Tick = tick }, process, script);
        }
        Check(!waiting, "Tick-only polling kept a frozen native turn ready indefinitely.");
        Ff7AutoPlayNativeReadiness progressing = new();
        for (int step = 0; step <= 10; step++)
        {
            Check(progressing.ShouldWaitForSynchronousHostCompletion(1, "source", vm with { Tick = step }, process with { Actors = [actor with { TurnStep = step }] }, script), "Actual finite turn progress was mistaken for a frozen callback.");
        }
        Console.WriteLine("Finite synchronous turn readiness authenticates current native ownership and remains bounded without changing native state.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
