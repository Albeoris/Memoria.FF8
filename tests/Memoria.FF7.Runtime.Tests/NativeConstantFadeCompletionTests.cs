using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeConstantFadeCompletionTests
{
    private sealed record Fixture(Ff7AutoPlayActiveEvent Active, FieldVmSnapshot Vm, Ff7FieldProcess Process, Ff7AutoPlayExecutionOwner Owner, FieldScriptAnalysis Analysis);

    internal static void Run()
    {
        Fixture fixture = Create();
        string before = JsonSerializer.Serialize(fixture);
        Check(Settled(fixture), "A returned source's actual live constant-fade child was not presentation-settled.");
        Check(fixture.Active.Returned && fixture.Vm.Entities[1].Priority == 6 && fixture.Active.ExecutionProvenance!.Owners.Contains(fixture.Owner), "Presentation settlement fabricated a child RET or removed its live provenance.");
        Check(before == JsonSerializer.Serialize(fixture), "Presentation settlement changed actual native state or recorded execution.");
        foreach (Fixture invalid in new[]
        {
            fixture with { Active = fixture.Active with { Returned = false } },
            fixture with { Active = fixture.Active with { ConditionFingerprint = "other" } },
            fixture with { Active = fixture.Active with { ExecutionProvenance = null } },
            fixture with { Active = fixture.Active with { ExecutionProvenance = fixture.Active.ExecutionProvenance! with { Owners = [] } } },
            fixture with { Vm = fixture.Vm with { ProgramIdentity = "other" } },
            fixture with { Analysis = fixture.Analysis with { Complete = false } },
            fixture with { Analysis = fixture.Analysis with { RepeatingConstantFade = null } },
            fixture with { Process = fixture.Process with { PlayerLocked = true } },
            fixture with { Process = fixture.Process with { MenuDisabled = true } },
            fixture with { Process = fixture.Process with { Transition = new(1, 0, 0, 0, 0) } },
            fixture with { Process = fixture.Process with { Dialogues = [new(1, 0, 0, null, null, 0)] } },
            fixture with { Process = fixture.Process with { FadeState = fixture.Process.FadeState! with { Position = 1 } } },
            fixture with { Process = fixture.Process with { FadeState = fixture.Process.FadeState! with { Red = 11 } } },
            fixture with { Process = fixture.Process with { FadeState = fixture.Process.FadeState! with { EntryCurve = true } } },
            fixture with { Process = fixture.Process with { FadeState = fixture.Process.FadeState! with { FieldHidden = true } } }
        })
        {
            Check(!Settled(invalid), "An unauthenticated, unsettled or interactive child was treated as completed presentation.");
        }
        Check(!Ff7AutoPlayPolicy.CanSettlePresentationChild(fixture.Active, 2, fixture.Vm, fixture.Process, fixture.Owner, fixture.Analysis), "Another field generation inherited presentation settlement.");
        Fixture queued = Create();
        var owner = queued.Owner with { Queued = true };
        queued = queued with { Owner = owner, Active = queued.Active with { ExecutionProvenance = queued.Active.ExecutionProvenance! with { Owners = [owner] } } };
        Check(!Settled(queued), "An unstarted queued child acquired presentation settlement.");
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            source => source.Vm.Entities[1] = source.Vm.Entities[1] with { Priority = 1 },
            source => source.Vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting,
            source => source.Vm.Entities[1].SyncOwners[6] = 0
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Settled(invalid), "A suspended or synchronous child lost its actual obligation.");
        }
        CheckCertificate();
        CheckDestinationReadiness();
        Console.WriteLine("Constant fade child settlement, native lifetime/UI guards and literal closed-loop certificate checks passed.");
    }

    private static void CheckDestinationReadiness()
    {
        Fixture fixture = Create();
        FieldEntitySnapshot[] entities = fixture.Vm.Entities.Select(entity => entity with { SavedProgramCounters = entity.SavedProgramCounters.ToArray(), SyncStates = entity.SyncStates.ToArray(), SyncOwners = entity.SyncOwners.ToArray() }).ToArray();
        entities[1].SavedProgramCounters[7] = 1;
        FieldVmSnapshot vm = fixture.Vm with { Entities = entities };
        byte[] script = [0, 0, 0x6B, 0, 0, 10, 20, 30, 255, 8, 0, 0x12, 9];
        bool Wait(Ff7FieldProcess process, FieldScriptAnalysis? proof) => new Ff7AutoPlayNativeReadiness().ShouldWaitForDestinationSetup(1, "nav", vm, process, script, [], (_, _) => proof ?? (fixture.Analysis with { RepeatingConstantFade = null }));
        string before = JsonSerializer.Serialize(new { vm, fixture.Process, script });
        Check(!Wait(fixture.Process, fixture.Analysis), "A completed repeating constant fade blocked destination follow-through.");
        Check(Wait(fixture.Process, null), "A missing constant-fade certificate suppressed admitted work.");
        Check(Wait(fixture.Process, fixture.Analysis with { Complete = false }), "An incomplete certificate suppressed admitted work.");
        Check(Wait(fixture.Process with { FadeState = null }, fixture.Analysis), "A missing actual fade was treated as completed presentation.");
        Check(Wait(fixture.Process with { FadeState = fixture.Process.FadeState! with { Red = 11 } }, fixture.Analysis), "An unrelated actual color suppressed native readiness.");
        Check(Wait(fixture.Process with { FadeState = fixture.Process.FadeState! with { Position = 1 } }, fixture.Analysis), "An unfinished fade suppressed native readiness.");
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "nav", vm, fixture.Process, script, []), "The destination-only rule changed general native readiness.");
        Check(before == JsonSerializer.Serialize(new { vm, fixture.Process, script }), "Destination readiness changed native state or the fade loop.");
        entities[1].SyncStates[6] = FieldSyncState.Waiting;
        entities[1].SyncOwners[6] = 0;
        Check(Wait(fixture.Process, fixture.Analysis), "A real waiting synchronous caller was ignored because its child only changes presentation.");
    }

    private static Fixture Create()
    {
        FieldMachine machine = Machine([0x6B, 0, 0, 10, 20, 30, 255, 8, 0, 0x12, 9]);
        FieldScriptAnalysis analysis = machine.AnalyzeScriptAtProgramCounter(1, 2);
        Check(analysis.Complete && analysis.RepeatingConstantFade is not null, "The authored constant-fade loop has no native certificate.");
        FieldVmSnapshot vm = machine.Capture();
        vm.Entities[1] = vm.Entities[1] with { ProgramCounter = 2, Priority = 6 };
        Ff7AutoPlayExecutionOwner owner = new(1, 6, 2, 2, 0, 1, 0, 1, false);
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, Returned: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, vm.ProgramIdentity, 0, 1) with { Owners = [owner], RootReturned = true } };
        Ff7FieldProcess process = new() { FadeState = new(8, 255, 255, 10, 20, 30, true) };
        return new(active, vm, process, owner, analysis);
    }

    private static void CheckCertificate()
    {
        foreach (byte[] body in new byte[][]
        {
            [0x6B, 0, 0, 10, 20, 30, 255, 8, 0, 0],
            [0x6B, 0, 0, 10, 20, 30, 255, 7, 0, 0x12, 9],
            [0x6B, 0, 0, 10, 20, 30, 1, 8, 0, 0x12, 9],
            [0x6B, 0, 0, 10, 20, 30, 255, 8, 0, 0x80, 0x50, 0, 1, 0x12, 13],
            [0x6B, 0, 0, 10, 20, 30, 255, 8, 0, 0x30, 1, 0, 1, 0x12, 13]
        })
        {
            Check(Machine(body).AnalyzeScriptAtProgramCounter(1, 2).RepeatingConstantFade is null, "A returning, changing, writing or conditional effect acquired a constant-fade certificate.");
        }
    }

    private static FieldMachine Machine(byte[] body)
    {
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[1][2] = 2;
        FieldMachine machine = new(new FieldProgram([0, 0, .. body], entries));
        machine.Initialize();
        return machine;
    }

    private static bool Settled(Fixture fixture)
    {
        return Ff7AutoPlayPolicy.CanSettlePresentationChild(fixture.Active, 1, fixture.Vm, fixture.Process, fixture.Owner, fixture.Analysis);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
