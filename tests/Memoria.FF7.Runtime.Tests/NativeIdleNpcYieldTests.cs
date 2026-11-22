using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeIdleNpcYieldTests
{
    private sealed record Fixture(Ff7AutoPlayActiveEvent Active, FieldVmSnapshot Vm, Ff7FieldProcess Process, Ff7AutoPlayExecutionOwner Owner, byte[] Script);

    internal static void Run()
    {
        foreach (byte[] instruction in new byte[][] { [0x12, 0], [0x13, 0, 0] })
        {
            Fixture source = Create(instruction);
            string before = JsonSerializer.Serialize(source);
            Check(Settles(source), "A real returned root's exact asynchronous NPC self-yield remained outstanding activation debt.");
            Check(Settles(source with { Vm = source.Vm with { PendingRequests = [new(7, 1, 7, 4, new FieldAnimation(1, 1, true, false))] } }), "An unrelated suspended ambient P7 request prevented settlement of the actual P3 self-yield.");
            Check(before == JsonSerializer.Serialize(source) && source.Vm.Entities[1].Priority == 3 && source.Vm.Entities[1].ProgramCounter == 10 && source.Active.ExecutionProvenance!.Owners.Contains(source.Owner), "Idle classification changed native execution, fabricated child RET or removed live provenance.");
        }
        Fixture valid = Create([0x12, 0]);
        foreach (Fixture invalid in new[]
        {
            valid with { Active = valid.Active with { Accepted = false } },
            valid with { Active = valid.Active with { Returned = false } },
            valid with { Active = valid.Active with { ConditionFingerprint = "changed" } },
            valid with { Active = valid.Active with { ExecutionProvenance = null } },
            valid with { Active = valid.Active with { ExecutionProvenance = valid.Active.ExecutionProvenance! with { RootReturned = false } } },
            valid with { Vm = valid.Vm with { ProgramIdentity = "other" } },
            valid with { Owner = valid.Owner with { Queued = true } },
            valid with { Owner = valid.Owner with { ParentEntity = 1 } },
            valid with { Owner = valid.Owner with { ParentPriority = 6 } },
            valid with { Owner = valid.Owner with { Entry = 11 } },
            valid with { Process = valid.Process with { PlayerLocked = true } },
            valid with { Process = valid.Process with { MenuDisabled = true } },
            valid with { Process = valid.Process with { GlobalCommand = Ff7GlobalCommand.Battle } },
            valid with { Process = valid.Process with { Dialogues = [new(7, 0, 1, null, null, 0)] } },
            valid with { Process = valid.Process with { Actors = [valid.Process.Actors[0] with { PlayerControlled = true }] } },
            valid with { Process = valid.Process with { Actors = [valid.Process.Actors[0] with { EntityBound = false }] } },
            valid with { Process = valid.Process with { Actors = [valid.Process.Actors[0] with { Jump = new(7, 0, 0, 0, 10) }] } },
            valid with { Process = valid.Process with { Actors = [valid.Process.Actors[0] with { Ladder = new(7, 0, 0, 0, 0, 1) }] } },
            valid with { Process = valid.Process with { Actors = [valid.Process.Actors[0] with { TurnMode = 1, TurnSteps = 10, TurnStep = 2 }] } },
            valid with { Process = valid.Process with { Motions = [new(7, 1, 10, 20, true, true)] } },
            valid with { Vm = valid.Vm with { PendingRequests = [new(7, 1, 3, 10, new FieldActorMove(10, 20))] } }
        })
        {
            Check(!Settles(invalid), "Foreign ownership, active native work or unavailable controls acquired idle self-yield settlement.");
        }
        foreach (byte[] instruction in new byte[][] { [0x12, 1], [0x13, 0, 1], [0x30, 0, 0, 1], [0x80, 0x50, 1, 1], [0], [0x24, 1, 0] })
        {
            Check(!Settles(Create(instruction)), "A finite wait, return, material/input opcode or nonzero backjump was classified as an idle self-yield.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            source => source.Script[0] = 3,
            source => source.Script[1] = 0,
            source => source.Script[2] = 0x63,
            source => source.Vm.Entities[1] = source.Vm.Entities[1] with { Priority = 2 },
            source => source.Vm.Entities[1] = source.Vm.Entities[1] with { ModelBound = false },
            source => source.Vm.Entities[1].SyncStates[3] = FieldSyncState.Waiting,
            source => source.Vm.Entities[1].SyncOwners[3] = 0
        })
        {
            Fixture invalid = Create([0x12, 0]);
            invalidate(invalid);
            Check(!Settles(invalid), "A changed dispatch packet, current activation or synchronization state retained idle settlement authority.");
        }
        Check(!Ff7AutoPlayPolicy.CanSettleIdleNpcYield(valid.Active, 2, valid.Vm, valid.Process, valid.Owner, valid.Script), "Another generation borrowed native idle-yield ownership.");
        CheckDestinationReadiness();
        Console.WriteLine("Idle NPC self-yield settlement preserves actual native loops while requiring exact returned-root ownership and free controls.");
    }

    private static Fixture Create(byte[] instruction)
    {
        byte[] script = new byte[24];
        new byte[] { 1, 1, 0x62 }.CopyTo(script, 0);
        instruction.CopyTo(script, 10);
        FieldEntitySnapshot Entity(int priority, int pc) => new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray(), ModelBound: true);
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 30, true, new byte[1280], new byte[256], [Entity(7, 3), Entity(3, 10)], PendingRequests: []);
        Ff7AutoPlayExecutionOwner owner = new(1, 3, 2, 10, 0, 1, 0, 20, false);
        Ff7AutoPlayActiveEvent active = new("visit", "talk", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "before", Accepted: true, Returned: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 0, 10) with { RootReturned = true, Owners = [owner] } };
        return new(active, vm, new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0 }, new() { Slot = 1, Entity = 0, Model = 1, PlayerControlled = true }] }, owner, script);
    }

    private static void CheckDestinationReadiness()
    {
        Fixture source = Create([0x12, 0]);
        source.Vm.Entities[1].SavedProgramCounters[7] = 3;
        FieldScriptAnalysis proof = new(true, null, "closed-self-yield", false, [], [10], []);
        bool Wait(FieldVmSnapshot vm, Ff7FieldProcess process, byte[] script, FieldScriptAnalysis certificate) => new Ff7AutoPlayNativeReadiness().ShouldWaitForDestinationSetup(1, "navigation", vm, process, script, [], (_, _) => certificate);
        string before = JsonSerializer.Serialize(source);
        Check(Ff7AutoPlayPolicy.HasIndependentNpcIdleYield(source.Vm, source.Process, 1, source.Script), "The shared native idle-yield certificate disagreed with owned settlement.");
        Check(!Wait(source.Vm, source.Process, source.Script, proof), "The same native idle NPC blocked destination follow-through after its root completed.");
        Check(Wait(source.Vm, source.Process, source.Script, proof with { Complete = false }), "An incomplete continuation certificate suppressed destination setup work.");
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "navigation", source.Vm, source.Process, source.Script, []), "The destination-specific exemption changed general native readiness.");
        Ff7FieldProcess player = source.Process with { Actors = [source.Process.Actors[0] with { PlayerControlled = true }] };
        Check(!Ff7AutoPlayPolicy.HasIndependentNpcIdleYield(source.Vm, player, 1, source.Script) && Wait(source.Vm, player, source.Script, proof), "A controlled actor's live continuation was ignored as idle NPC presentation.");
        FieldVmSnapshot pending = source.Vm with { PendingRequests = [new(8, 1, 3, 10, new FieldActorMove(10, 20))] };
        Check(!Ff7AutoPlayPolicy.HasIndependentNpcIdleYield(pending, source.Process, 1, source.Script) && Wait(pending, source.Process, source.Script, proof), "Actual current-priority pending work received the idle exemption.");
        byte[] material = source.Script.ToArray();
        material[10] = 0x80;
        Check(!Ff7AutoPlayPolicy.HasIndependentNpcIdleYield(source.Vm, source.Process, 1, material) && Wait(source.Vm, source.Process, material, proof), "A material current instruction borrowed the idle self-yield certificate.");
        FieldEntitySnapshot[] ambient = source.Vm.Entities.ToArray();
        ambient[1] = ambient[1] with { Priority = 7 };
        Check(!Ff7AutoPlayPolicy.HasIndependentNpcIdleYield(source.Vm with { Entities = ambient }, source.Process, 1, source.Script), "Ambient priority acquired the admitted idle certificate.");
        Check(before == JsonSerializer.Serialize(source), "Destination follow-through classification changed native source state.");
    }

    private static bool Settles(Fixture source) => Ff7AutoPlayPolicy.CanSettleIdleNpcYield(source.Active, 1, source.Vm, source.Process, source.Owner, source.Script);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
