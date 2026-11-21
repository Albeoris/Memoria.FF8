using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativePassiveNpcPollingTests
{
    private sealed record Fixture(Ff7AutoPlayActiveEvent Active, FieldVmSnapshot Vm, Ff7FieldProcess Process, Ff7AutoPlayExecutionOwner Owner, byte[] Script);

    internal static void Run()
    {
        Fixture source = Create();
        string before = JsonSerializer.Serialize(source);
        Check(Settles(source), "A returned root's actually owned passive NPC polling activation was not classified.");
        Check(Settles(source with { Vm = source.Vm with { Entities = [source.Vm.Entities[0], source.Vm.Entities[1] with { ProgramCounter = 16 }, source.Vm.Entities[2]] } }), "The exact native backjump position lost its passive polling classification.");
        Ff7AutoPlayExecutionOwner nested = source.Owner with { ParentEntity = 2, ParentPriority = 6, SourceProgramCounter = 4 };
        Check(Settles(source with { Owner = nested, Active = source.Active with { ExecutionProvenance = source.Active.ExecutionProvenance! with { Owners = [nested] } } }), "An actual recorded asynchronous descendant required its already-returned intermediate parent to remain live.");
        foreach (Fixture invalid in new[]
        {
            source with { Active = source.Active with { Returned = false } },
            source with { Active = source.Active with { CompletionRecorded = true } },
            source with { Active = source.Active with { ConditionFingerprint = "another" } },
            source with { Active = source.Active with { ExecutionProvenance = null } },
            source with { Active = source.Active with { ExecutionProvenance = source.Active.ExecutionProvenance! with { RootReturned = false } } },
            source with { Active = source.Active with { ExecutionProvenance = source.Active.ExecutionProvenance! with { Owners = [] } } },
            source with { Owner = source.Owner with { Queued = true } },
            source with { Vm = source.Vm with { ProgramIdentity = "other" } },
            source with { Process = source.Process with { PlayerLocked = true } },
            source with { Process = source.Process with { MenuDisabled = true } },
            source with { Process = source.Process with { Dialogues = [new(9, 0, 1, null, null, 0)] } },
            source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { PlayerControlled = true }] } },
            source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { EntityBound = false }] } },
            source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { Jump = new(1, 0, 0, 0, 10) }] } },
            source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { Ladder = new(1, 0, 0, 0, 0, 1) }] } },
            source with { Process = source.Process with { Motions = [new(7, 1, 20, 30, true, true)] } },
            source with { Vm = source.Vm with { PendingRequests = [new(7, 1, 6, 10, new FieldActorMove(20, 30))] } }
        })
        {
            Check(!Settles(invalid), "Unowned, queued, controlled, modal or outstanding native work was classified as passive polling.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            value => value.Script[0] = 3,
            value => value.Script[1] = 0,
            value => value.Script[2] = 0xC3,
            value => value.Script[10] = 0x30,
            value => value.Script[11] = 0x60,
            value => value.Script[14] = 1,
            value => value.Script[15] = 4,
            value => value.Script[16] = 0x80,
            value => value.Script[17] = 5,
            value => value.Vm.Entities[1] = value.Vm.Entities[1] with { Priority = 7 },
            value => value.Vm.Entities[1] = value.Vm.Entities[1] with { ProgramCounter = 9 },
            value => value.Vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting,
            value => value.Vm.Entities[1].SyncOwners[6] = 0
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Settles(invalid), "Changed source dispatch, effectful/input guard, branch or synchronous ownership was ignored.");
        }
        FieldVariables changed = new(source.Vm.PersistentVariables, source.Vm.MapVariables);
        changed.WriteByte(FieldBank.MapByte, 9, 1);
        Check(!Settles(source with { Vm = source.Vm with { MapVariables = changed.CopyMap() } }), "A guard that will exit the polling loop was classified as passive current work.");
        foreach (long tick in new long[] { 9, 31 })
        {
            Ff7AutoPlayExecutionOwner wrongLifetime = source.Owner with { DispatchTick = tick };
            Check(!Settles(source with { Owner = wrongLifetime, Active = source.Active with { ExecutionProvenance = source.Active.ExecutionProvenance! with { Owners = [wrongLifetime] } } }), "A dispatch outside the authenticated root/native time interval acquired passive polling authority.");
        }
        Check(!Ff7AutoPlayPolicy.CanSettlePassiveNpcPolling(source.Active, 2, source.Vm, source.Process, source.Owner, source.Script), "Another generation borrowed passive polling ownership.");
        Check(before == JsonSerializer.Serialize(source) && source.Vm.Entities[1].Priority == 6 && source.Active.ExecutionProvenance!.Owners.Contains(source.Owner), "Classification changed the live NPC, provenance or native source state.");
        Console.WriteLine("Passive NPC polling activation classification preserves actual live native loops and exact asynchronous ownership.");
    }

    private static Fixture Create()
    {
        byte[] script = new byte[20];
        new byte[] { 1, 1, 0xC2 }.CopyTo(script, 0);
        new byte[] { 2, 1, 0xC2 }.CopyTo(script, 4);
        new byte[] { 0x14, 0x50, 9, 0, 0, 3, 0x12, 6 }.CopyTo(script, 10);
        FieldEntitySnapshot Entity(int priority, int pc) => new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray(), ModelBound: true);
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 30, true, new byte[1280], new byte[256], [Entity(7, 3), Entity(6, 10), Entity(7, 7)], PendingRequests: []);
        Ff7AutoPlayExecutionOwner owner = new(1, 6, 2, 10, 0, 1, 0, 20, false);
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 0, 2, Ff7AutoPlayTargetKind.LinePush, null, 0, "before", Accepted: true, Returned: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 0, 10) with { RootReturned = true, Owners = [owner] } };
        return new(active, vm, new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0 }] }, owner, script);
    }

    private static bool Settles(Fixture source) => Ff7AutoPlayPolicy.CanSettlePassiveNpcPolling(source.Active, 1, source.Vm, source.Process, source.Owner, source.Script);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
