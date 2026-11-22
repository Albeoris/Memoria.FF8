using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativePassiveInputChildTests
{
    private sealed record Fixture(byte[] Code, FieldMachine Machine, FieldVmSnapshot Vm, Ff7FieldProcess Process, Ff7AutoPlayActiveEvent Active, Ff7AutoPlayExecutionOwner Owner);

    internal static void Run()
    {
        Fixture source = Create([0x30, 0x10, 0, 5, 0x80, 0x50, 1, 1, 0x12, 8]);
        string before = JsonSerializer.Serialize(new { source.Vm, source.Process, source.Active, source.Owner });
        Check(Polls(source) && Settles(source), "An exact returned root's live input-idle nonactor child remained activation debt.");
        Fixture guarded = Create([0x14, 0x50, 9, 0, 0, 5, 0x30, 0x10, 0, 5, 0x80, 0x50, 1, 1, 0x12, 14]);
        Check(Polls(guarded) && Settles(guarded), "A current pure predicate before the unmatched native KEY lost passive polling.");
        Check(!source.Machine.CanPollCurrentScriptWithoutInput(1, 2), "A foreign priority borrowed the current input loop.");
        source.Machine.SetInput(0x10, 0x10);
        Check(!Polls(source), "A matched native KEY hid its real material continuation.");
        source.Machine.SetInput(0, 0);
        foreach (byte[] body in new byte[][]
        {
            [0x80, 0x50, 1, 1, 0x30, 0x10, 0, 5, 0x80, 0x50, 1, 1, 0x12, 12],
            [0x24, 1, 0, 0x12, 3],
            [0x12, 0],
            [0],
            [0x01, 0, 1, 0x12, 3],
            [0x30, 0x10, 0, 1, 0]
        })
        {
            Check(!Polls(Create(body)), "A write, timer, call, return or key-free loop acquired the passive-input certificate.");
        }
        foreach (Fixture invalid in new[]
        {
            source with { Active = source.Active with { Accepted = false } },
            source with { Active = source.Active with { Returned = false } },
            source with { Active = source.Active with { ConditionFingerprint = "changed" } },
            source with { Active = source.Active with { ExecutionProvenance = null } },
            source with { Active = source.Active with { ExecutionProvenance = source.Active.ExecutionProvenance! with { RootReturned = false } } },
            source with { Vm = source.Vm with { ProgramIdentity = "foreign" } },
            source with { Owner = source.Owner with { ParentEntity = 1 } },
            source with { Owner = source.Owner with { ParentPriority = 6 } },
            source with { Owner = source.Owner with { Queued = true } },
            source with { Process = source.Process with { PlayerLocked = true } },
            source with { Process = source.Process with { MenuDisabled = true } },
            source with { Process = source.Process with { GlobalCommand = Ff7GlobalCommand.Battle } },
            source with { Process = source.Process with { Dialogues = [new(9, 0, 1, null, null, 0)] } },
            source with { Process = source.Process with { Actors = [new() { Slot = 0, Entity = 1, Model = 0, PlayerControlled = true }] } },
            source with { Process = source.Process with { Motions = [new(7, 1, 20, 30, true, true)] } },
            source with { Vm = source.Vm with { PendingRequests = [new(7, 1, 3, 10, new FieldActorMove(20, 30))] } }
        })
        {
            Check(!Settles(invalid), "Foreign ownership, active native debt or unavailable controls acquired passive-input settlement.");
        }
        FieldEntitySnapshot[] synchronous = source.Vm.Entities.ToArray();
        synchronous[1] = synchronous[1] with { SyncStates = synchronous[1].SyncStates.ToArray(), SyncOwners = synchronous[1].SyncOwners.ToArray() };
        synchronous[1].SyncStates[3] = FieldSyncState.Waiting;
        synchronous[1].SyncOwners[3] = 0;
        Check(!Settles(source with { Vm = source.Vm with { Entities = synchronous } }), "A synchronous child was discharged as passive asynchronous work.");
        Check(!Ff7AutoPlayPolicy.CanSettlePassiveInputChild(source.Active, 2, source.Vm, source.Process, source.Owner, source.Code, true), "Another generation inherited passive-child ownership.");
        Check(!Ff7AutoPlayPolicy.CanSettlePassiveInputChild(source.Active, 1, source.Vm, source.Process, source.Owner, source.Code, false), "An uncertified native continuation was discharged.");
        byte[] foreignCall = source.Code.ToArray();
        foreignCall[0] = 3;
        Check(!Ff7AutoPlayPolicy.CanSettlePassiveInputChild(source.Active, 1, source.Vm, source.Process, source.Owner, foreignCall, true), "A synchronous dispatch packet borrowed asynchronous settlement.");
        Check(before == JsonSerializer.Serialize(new { source.Vm, source.Process, source.Active, source.Owner }) && source.Vm.Entities[1].Priority == 3, "Classification mutated native execution, fabricated RET or removed provenance.");
        Console.WriteLine("Current unmatched KEY polling can settle exact asynchronous activation debt while leaving its native child live.");
    }

    private static Fixture Create(byte[] body)
    {
        byte[] code = new byte[10 + body.Length];
        new byte[] { 1, 1, 0x62 }.CopyTo(code, 0);
        body.CopyTo(code, 10);
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[1][2] = 10;
        FieldProgram program = new(code, entries);
        FieldEntitySnapshot Entity(int pc, int priority) => new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, program.Identity, 30, true, new byte[1280], new byte[256], [Entity(3, 7), Entity(10, 3)], PendingRequests: []);
        FieldMachine machine = FieldMachine.Restore(program, vm);
        Ff7AutoPlayExecutionOwner owner = new(1, 3, 2, 10, 0, 1, 0, 20, false);
        Ff7AutoPlayActiveEvent active = new("visit", "talk", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "before", Accepted: true, Returned: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, program.Identity, 0, 10) with { RootReturned = true, Owners = [owner] } };
        return new(code, machine, vm, new() { Actors = [new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true }] }, active, owner);
    }

    private static bool Polls(Fixture source) => source.Machine.CanPollCurrentScriptWithoutInput(1, 3);
    private static bool Settles(Fixture source) => Ff7AutoPlayPolicy.CanSettlePassiveInputChild(source.Active, 1, source.Vm, source.Process, source.Owner, source.Code, Polls(source));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
