using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeAltarInputTests
{
    private const string Program = "C69128E2485D4AC0863A6C58078468395BD9D9A70B6EC2EB479FC97A2B2FCEE2";
    private sealed record Fixture(FieldVmSnapshot Vm, Ff7FieldProcess Process, FieldScriptInputDemand Demand);

    internal static void Run()
    {
        foreach (int story in new[] { 669, 671 })
        {
            Fixture source = Create(story);
            string before = JsonSerializer.Serialize(source);
            Check(Eligible(source), "The reviewed native altar polling phase was excluded.");
            Check(before == JsonSerializer.Serialize(source), "Altar input qualification changed native story, ownership or pose.");
        }
        foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
        {
            source => source with { Vm = source.Vm with { ProgramIdentity = "unreviewed" } },
            source => source with { Demand = source.Demand with { ProgramIdentity = "unreviewed" } },
            source => source with { Demand = source.Demand with { ProgramCounter = 0x02F0 } },
            source => source with { Demand = source.Demand with { PresentationPolling = false } },
            source => source with { Demand = source.Demand with { Entity = 1 } },
            source => source with { Demand = source.Demand with { Priority = 6 } },
            source => source with { Demand = source.Demand with { Mask = 0x40 } },
            source => source with { Demand = source.Demand with { Mapped = true } },
            source => source with { Demand = source.Demand with { Kind = FieldScriptInputKind.Held } },
            source => source with { Demand = source.Demand with { AwaitingInput = false } },
            source => source with { Process = source.Process with { PlayerLocked = true } },
            source => source with { Process = source.Process with { MenuDisabled = true } },
            source => source with { Process = source.Process with { Transition = new(1, 0, 0, 0, 0) } },
            source => source with { Process = source.Process with { Dialogues = [new(1, 0, 0, null, null, 0)] } },
            source => source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { Entity = 4 }] } },
            source => source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { EntityBound = false }] } },
            source => source with { Vm = source.Vm with { PendingRequests = [new(1, 3, 6, 0, new FieldFadeCommand(0))] } }
        })
        {
            Check(!Eligible(invalidate(Create())), "Another program, prompt, input, actor or active native context inherited altar input admission.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            source => source.Vm.Entities[0] = source.Vm.Entities[0] with { ProgramCounter = 0x02D0 },
            source => source.Vm.Entities[0] = source.Vm.Entities[0] with { Priority = 6 },
            source => source.Vm.Entities[3] = source.Vm.Entities[3] with { Priority = 6 },
            source => source.Vm.Entities[4] = source.Vm.Entities[4] with { Priority = 6 },
            source => source.Vm.Entities[3].SyncStates[6] = FieldSyncState.Waiting,
            source => source.Vm.Entities[4].SyncOwners[6] = 0
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Eligible(invalid), "A busy, stale or synchronous native execution slot admitted altar input.");
        }
        Check(!Eligible(Create(670)) && !Eligible(Create(672)), "An unsupported story phase admitted altar input.");
        CheckLockedForwardGates();
        Console.WriteLine("Reviewed altar input program/story/site/actor/native-work fences and source purity checks passed.");
    }

    private static void CheckLockedForwardGates()
    {
        foreach (int pc in new[] { 0x031E, 0x0348, 0x034F })
        {
            Fixture fixture = Create(671);
            fixture.Vm.Entities[0] = fixture.Vm.Entities[0] with { ProgramCounter = pc };
            Ff7FieldProcess process = fixture.Process with { PlayerLocked = true, MenuDisabled = true };
            FieldScriptInputDemand forward = fixture.Demand with { ProgramCounter = pc };
            FieldScriptInputDemand optionalHeld = forward with { Kind = FieldScriptInputKind.Held, Mask = 0x2000 };
            string before = JsonSerializer.Serialize(new { fixture.Vm, process, forward });
            Check(Ff7AutoPlayScriptedInput.SelectAltarForwardInput(fixture.Vm, process, [optionalHeld, forward]) == forward, "A current locked native forward gate lost priority to an optional held input.");
            Check(Ff7AutoPlayScriptedInput.SelectAltarForwardInput(fixture.Vm, process, [optionalHeld]) is null, "A held input was invented as the native forward edge.");
            Check(Ff7AutoPlayScriptedInput.SelectAltarForwardInput(fixture.Vm, process, [forward with { Entity = 1 }]) is null, "An unrelated owner inherited locked forward input.");
            Check(Ff7AutoPlayScriptedInput.SelectAltarForwardInput(fixture.Vm, process, [forward with { AwaitingInput = false }]) is null, "An inactive gate inherited forward admission.");
            Check(before == JsonSerializer.Serialize(new { fixture.Vm, process, forward }), "Locked forward selection changed native story or controls.");
            fixture.Vm.Entities[0] = fixture.Vm.Entities[0] with { ProgramCounter = 0x02F0 };
            Check(Ff7AutoPlayScriptedInput.SelectAltarForwardInput(fixture.Vm, process, [forward]) is null, "A stale native source PC inherited a later forward gate.");
            fixture.Vm.Entities[0] = fixture.Vm.Entities[0] with { ProgramCounter = pc };
            fixture.Vm.Entities[4] = fixture.Vm.Entities[4] with { Priority = 6 };
            Check(Ff7AutoPlayScriptedInput.SelectAltarForwardInput(fixture.Vm, process, [forward]) is null, "A busy controlled native child admitted another forward edge.");
        }
        Fixture oldStage = Create(669);
        oldStage.Vm.Entities[0] = oldStage.Vm.Entities[0] with { ProgramCounter = 0x031E };
        Check(Ff7AutoPlayScriptedInput.SelectAltarForwardInput(oldStage.Vm, oldStage.Process with { PlayerLocked = true, MenuDisabled = true }, [oldStage.Demand with { ProgramCounter = 0x031E }]) is null, "Another story stage inherited locked forward input.");
    }

    private static Fixture Create(int story = 669)
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 5).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[0] = entities[0] with { ProgramCounter = story == 671 ? 0x02F0 : 0x02C0 };
        FieldVariables variables = new();
        variables.WriteWord(FieldBank.Persistent0Word, 0, (short)story);
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 1, true, variables.CopyPersistent(), variables.CopyMap(), entities);
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = story == 671 ? 4 : 3, Model = 0, PlayerControlled = true }] };
        FieldScriptInputDemand demand = new("native-altar", Program, 0, 7, story == 671 ? 0x02F0 : 0x02D7, FieldScriptInputKind.Pressed, 0x20, false, true) { PresentationPolling = story != 671 };
        return new(vm, process, demand);
    }

    private static bool Eligible(Fixture fixture)
    {
        return Ff7AutoPlayScriptedInput.IsAltarInput(fixture.Vm, fixture.Process, fixture.Demand);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
