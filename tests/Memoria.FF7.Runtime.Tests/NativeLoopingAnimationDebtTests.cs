using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeLoopingAnimationDebtTests
{
    private sealed record Fixture(Ff7AutoPlayActiveEvent Active, FieldVmSnapshot Vm, Ff7FieldProcess Process, Ff7AutoPlayExecutionOwner Owner, byte[] Script, FieldScriptAnalysis Analysis);

    internal static void Run()
    {
        Fixture fixture = Create();
        string before = JsonSerializer.Serialize(fixture);
        Check(Settled(fixture), "A returned root's recorded nested NPC animation/wait cycle was not cosmetic debt.");
        FieldHostRequest request = new(9, 1, 6, 10, new FieldAnimation(4, 1, true, false));
        Fixture pending = fixture with { Vm = fixture.Vm with { PendingRequests = [request] }, Process = fixture.Process with { AnimationRequests = [new(9, 1)] } };
        Check(Settled(pending), "The exact current native blocking animation request was excluded from its complete presentation cycle.");
        Check(!Settled(pending with { Vm = pending.Vm with { PendingRequests = [request with { ProgramCounter = 13 }] } }), "A different pending PC was assigned to the current animation cycle.");
        Check(!Settled(pending with { Vm = pending.Vm with { PendingRequests = [request with { Command = new FieldAnimation(5, 1, true, false) }] } }), "A different typed pending command was ignored.");
        Check(!Settled(pending with { Process = pending.Process with { AnimationRequests = [] } }), "An unrecorded pending animation acquired cosmetic settlement.");
        foreach (Fixture invalid in new[]
        {
            fixture with { Active = fixture.Active with { Returned = false } },
            fixture with { Active = fixture.Active with { ExecutionProvenance = null } },
            fixture with { Active = fixture.Active with { ConditionFingerprint = "other" } },
            fixture with { Vm = fixture.Vm with { ProgramIdentity = "other" } },
            fixture with { Analysis = fixture.Analysis with { Complete = false } },
            fixture with { Analysis = fixture.Analysis with { ChangesStory = true } },
            fixture with { Analysis = fixture.Analysis with { MayChangeNavigation = true } },
            fixture with { Analysis = fixture.Analysis with { ExitProgramCounters = [18] } },
            fixture with { Process = fixture.Process with { PlayerLocked = true } },
            fixture with { Process = fixture.Process with { MenuDisabled = true } },
            fixture with { Process = fixture.Process with { Actors = [fixture.Process.Actors[0] with { PlayerControlled = true }] } }
        })
        {
            Check(!Settled(invalid), "Unknown, interactive, controlled or effectful native work became cosmetic debt.");
        }
        foreach (Action<Fixture> change in new Action<Fixture>[]
        {
            item => item.Script[10] = 0x74,
            item => item.Script[10] = 0x80,
            item => item.Script[10] = 0x30,
            item => item.Script[10] = 0,
            item => item.Script[12] = 0,
            item => item.Script[14] = 0,
            item => item.Script[17] = 5,
            item => item.Script[2] = 0x03,
            item => item.Vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting,
            item => item.Vm.Entities[1].SyncOwners[6] = 2
        })
        {
            Fixture invalid = Create();
            change(invalid);
            Check(!Settled(invalid), "A query, write, key, return, invalid cycle, synchronous dispatch or live sync debt was ignored.");
        }
        Check(before == JsonSerializer.Serialize(fixture) && fixture.Active.ExecutionProvenance!.Owners.Contains(fixture.Owner) && fixture.Vm.Entities[1].Priority == 6, "Cosmetic qualification changed native execution or removed live ownership.");
        Check(Ff7AutoPlayPolicy.IsClosedNpcAnimationCycle([0xA3, 4, 1, 0x24, 1, 0, 0x13, 6, 0], 0), "A native wide backward jump lost the same closed presentation cycle.");
        CheckDestinationReadiness();
        Console.WriteLine("Closed nested NPC animation debt checks passed without native return.");
    }

    private static void CheckDestinationReadiness()
    {
        Fixture fixture = Create();
        fixture.Vm.Entities[1].SavedProgramCounters[7] = 1;
        FieldHostRequest request = new(9, 1, 6, 10, new FieldAnimation(4, 1, true, false));
        FieldVmSnapshot vm = fixture.Vm with { PendingRequests = [request] };
        Ff7FieldProcess process = fixture.Process with { AnimationRequests = [new(9, 1)] };
        bool Wait(FieldVmSnapshot actual, Ff7FieldProcess host, FieldScriptAnalysis proof) => new Ff7AutoPlayNativeReadiness().ShouldWaitForDestinationSetup(1, "nav", actual, host, fixture.Script, [], (_, _) => proof);
        string before = JsonSerializer.Serialize(new { vm, process, fixture.Script });
        Check(!Wait(vm, process, fixture.Analysis), "A complete independent NPC cycle with its exact pending A3 blocked destination follow-through.");
        Check(Wait(vm, process, fixture.Analysis with { Complete = false }), "An incomplete cycle proof suppressed actual admitted setup.");
        Check(Wait(vm with { PendingRequests = [request with { ProgramCounter = 11 }] }, process, fixture.Analysis), "A mismatched pending animation PC was suppressed.");
        Check(Wait(vm, process with { AnimationRequests = [] }, fixture.Analysis), "An unrecorded actual animation request was suppressed.");
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "nav", vm, process, fixture.Script, []), "The destination-only cycle exemption changed general native readiness.");
        Check(before == JsonSerializer.Serialize(new { vm, process, fixture.Script }), "Destination readiness changed native NPC animation state.");
        vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting;
        vm.Entities[1].SyncOwners[6] = 0;
        Check(Wait(vm, process, fixture.Analysis), "A real synchronous child was discarded because its body only changes presentation.");
    }

    private static Fixture Create()
    {
        byte[] script = new byte[20];
        new byte[] { 0x02, 1, 0xC2 }.CopyTo(script, 2);
        new byte[] { 0xA3, 4, 1, 0x24, 1, 0, 0x12, 6 }.CopyTo(script, 10);
        FieldProgram program = new(script, [new ushort[32], new ushort[32], new ushort[32]]);
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldVmSnapshot vm = machine.Capture() with { Tick = 30 };
        vm.Entities[1] = vm.Entities[1] with { ProgramCounter = 10, Priority = 6, ModelBound = true };
        FieldScriptAnalysis analysis = FieldMachine.Restore(program, vm).AnalyzeScriptAtProgramCounter(1, 10);
        Check(analysis.Complete, "The authored native animation cycle did not analyze completely.");
        Ff7AutoPlayExecutionOwner owner = new(1, 6, 2, 10, 2, 6, 2, 20, false);
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, Returned: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, vm.ProgramIdentity, 0, 10) with { RootReturned = true, Owners = [owner] } };
        return new(active, vm, new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0, Animation = 4 }] }, owner, script, analysis);
    }

    private static bool Settled(Fixture fixture) => Ff7AutoPlayPolicy.CanSettleLoopingNpcAnimations(fixture.Active, 1, fixture.Vm, fixture.Process, fixture.Owner, fixture.Script, fixture.Analysis);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
