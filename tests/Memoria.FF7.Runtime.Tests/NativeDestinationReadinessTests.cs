using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeDestinationReadinessTests
{
    internal static void Run()
    {
        CheckAmbientWork();
        CheckAdmittedWork();
        CheckSynchronousWork();
        Console.WriteLine("Destination readiness excludes ambient motion and wait loops while preserving admitted native work.");
    }

    private static FieldEntitySnapshot Entity(int pc, int priority)
    {
        return new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
    }

    private static FieldVmSnapshot Vm(params FieldEntitySnapshot[] entities)
    {
        return new(1, FieldMachine.SemanticProfile, "destination-readiness", 1, true, new byte[1280], new byte[256], entities);
    }

    private static bool Wait(FieldVmSnapshot vm, Ff7FieldProcess process, byte[] script)
    {
        return new Ff7AutoPlayNativeReadiness().ShouldWaitForDestinationSetup(1, "component", vm, process, script, []);
    }

    private static void CheckAmbientWork()
    {
        byte[] script = [0x24, 30, 0, 0xA3, 0];
        FieldHostRequest request = new(8, 1, 7, 3, new FieldActorMove(10, 20));
        FieldVmSnapshot vm = Vm(Entity(0, 7) with { WaitCounter = 20 }, Entity(3, 7)) with { PendingRequests = [request] };
        Ff7Actor actor = new() { Slot = 0, Entity = 1, Model = 0, X4096 = 4096, Y4096 = 8192 };
        Ff7FieldProcess process = new() { Actors = [actor], Motions = [new(request.Id, 1, 10, 20, true, true)] };
        string before = JsonSerializer.Serialize(new { vm, process, script });
        Check(!Wait(vm, process, script), "Unowned ambient WAIT and movement held destination resolution hostage.");
        Ff7AutoPlayNativeReadiness readiness = new();
        for (int tick = 0; tick < 4; tick++)
        {
            FieldVmSnapshot changed = vm with { Tick = tick, Entities = [vm.Entities[0] with { WaitCounter = (ushort)(20 - tick) }, vm.Entities[1]] };
            Ff7FieldProcess moved = process with { Actors = [actor with { X4096 = actor.X4096 + tick * 4096, AnimationFrame16 = tick * 16 }] };
            Check(!readiness.ShouldWaitForDestinationSetup(1, "component", changed, moved, script, []), "Ambient native progress renewed destination setup readiness.");
        }
        Check(before == JsonSerializer.Serialize(new { vm, process, script }), "Ambient qualification changed actual native state.");
    }

    private static void CheckAdmittedWork()
    {
        byte[] script = [0x24, 30, 0, 0xA3, 0];
        FieldEntitySnapshot admitted = Entity(0, 6) with { WaitCounter = 20 };
        admitted.SavedProgramCounters[7] = 4;
        Check(Wait(Vm(admitted), new(), script), "An actually admitted finite native WAIT could not finish before destination planning.");
        FieldHostRequest request = new(8, 0, 6, 3, new FieldActorMove(10, 20));
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, X4096 = 4096 };
        Ff7FieldProcess process = new() { Actors = [actor], Motions = [new(request.Id, 0, 10, 20, true, true)] };
        Check(Wait(Vm(admitted with { ProgramCounter = 3, WaitCounter = 0 }) with { PendingRequests = [request] }, process, script), "An actual admitted movement owner was mistaken for ambient work.");
        FieldVmSnapshot wrong = Vm(Entity(3, 7)) with { PendingRequests = [request] };
        Check(!Wait(wrong, process, script), "A stale request at another priority inherited readiness.");
    }

    private static void CheckSynchronousWork()
    {
        byte[] script = [0x03, 1, 0xC0, 0];
        FieldEntitySnapshot caller = Entity(0, 7);
        FieldEntitySnapshot child = Entity(3, 6);
        child.SyncStates[6] = FieldSyncState.Waiting;
        child.SyncOwners[6] = 0;
        Check(Wait(Vm(caller, child), new(), script), "A genuine native synchronous child was hidden with ambient work.");
        FieldEntitySnapshot done = child with { Priority = 7, SyncStates = child.SyncStates.ToArray() };
        done.SyncStates[6] = FieldSyncState.Done;
        Check(Wait(Vm(caller, done), new(), script), "A real completed-child acknowledgment could not resume its caller.");
        done.SyncOwners[6] = 1;
        Check(!Wait(Vm(caller, done), new(), script), "An unrelated saved sync owner invented destination setup work.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
