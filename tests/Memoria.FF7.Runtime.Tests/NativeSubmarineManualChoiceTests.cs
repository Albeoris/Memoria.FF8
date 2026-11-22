using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeSubmarineManualChoiceTests
{
    private const string Program = "56FA50A22C8DFDA10D2E5000DE0C55A8246523A36D1D437B61809FB7D502D019";
    private sealed record Fixture(Ff7SessionSave Source, Ff7AutoPlayState State, Ff7Dialogue Dialogue, Ff7AutoPlayChoiceState Choice);

    internal static void Run()
    {
        Fixture source = Create();
        string before = JsonSerializer.Serialize(source);
        Check(Prompt(source) && Abandon(source), "The actual completed manual's new native offer did not release its repeat option.");
        Check(!Abandon(source with { Choice = source.Choice with { NativeOwner = source.Choice.NativeOwner! with { RequestId = source.Dialogue.RequestId } } }), "The same native ASK request was mistaken for a completed manual cycle.");
        foreach (Ff7AutoPlayChoiceState invalid in new[]
        {
            source.Choice with { NativeOwner = null },
            source.Choice with { NativeSourceOwned = false },
            source.Choice with { BeforeActive = source.State.Active! with { ConditionFingerprint = "other" } },
            source.Choice with { BeforeActive = source.State.Active! with { ExecutionProvenance = source.State.Active!.ExecutionProvenance! with { RootDispatchTick = 21 } } },
            source.Choice with { NativeOwner = source.Choice.NativeOwner! with { Generation = 2 } },
            source.Choice with { NativeOwner = source.Choice.NativeOwner! with { ProgramIdentity = "other" } },
            source.Choice with { NativeOwner = source.Choice.NativeOwner! with { Entity = 18 } },
            source.Choice with { NativeOwner = source.Choice.NativeOwner! with { ProgramCounter = 0x147E } },
            source.Choice with { NativeOwner = source.Choice.NativeOwner! with { SavedProgramCounters = Enumerable.Repeat(1, 8).ToArray() } }
        })
        {
            Check(!Abandon(source with { Choice = invalid }), "An unrelated choice, owner stack or root lifetime abandoned a native option.");
        }
        foreach (Ff7Dialogue invalid in new[] { source.Dialogue with { RequestId = 9 }, source.Dialogue with { Window = 1 }, source.Dialogue with { Text = 38 }, source.Dialogue with { FirstChoice = 1 }, source.Dialogue with { LastChoice = 4 }, source.Dialogue with { Completed = true } })
        {
            Check(!Prompt(source with { Dialogue = invalid }), "A foreign prompt inherited submarine manual advice.");
        }
        foreach (Action<Fixture> change in new Action<Fixture>[]
        {
            fixture => fixture.State.Active = fixture.State.Active! with { Accepted = false },
            fixture => fixture.State.Active = fixture.State.Active! with { Returned = true },
            fixture => fixture.State.Active = fixture.State.Active! with { Entity = 18 },
            fixture => fixture.State.Active = fixture.State.Active! with { Script = 3 },
            fixture => fixture.State.Active = fixture.State.Active! with { ExecutionProvenance = null },
            fixture => fixture.Source.Vm.MapVariables[0x17] = 1,
            fixture => fixture.Source.Vm.Entities[17] = fixture.Source.Vm.Entities[17] with { ProgramCounter = 0x10EF },
            fixture => fixture.Source.Vm.Entities[19] = fixture.Source.Vm.Entities[19] with { Priority = 5 },
            fixture => fixture.Source.Vm.Entities[19].SyncOwners[6] = 18,
            fixture => fixture.Source.Vm.Entities[19].SyncStates[6] = FieldSyncState.None,
            fixture => fixture.Source.Vm.Entities[9] = fixture.Source.Vm.Entities[9] with { Priority = 6 },
            fixture => fixture.Source.Vm.Entities[9].SyncStates[6] = FieldSyncState.Waiting,
            fixture => fixture.State.Active = fixture.State.Active! with { ExecutionProvenance = fixture.State.Active!.ExecutionProvenance! with { Owners = [.. fixture.State.Active!.ExecutionProvenance!.Owners, new(9, 6, 2, 0x0800, 19, 6, 0x1400, 29, false)] } }
        })
        {
            Fixture invalid = Create();
            change(invalid);
            Check(!Prompt(invalid), "An unfinished manual, foreign owner or missing native completion fact received repeat-cycle advice.");
        }
        Check(!Prompt(source with { Source = source.Source with { Vm = source.Source.Vm with { ProgramIdentity = "other" } } }), "A different native profile acquired manual advice.");
        Check(!Prompt(source with { Source = source.Source with { Process = source.Source.Process with { Motions = [new(9, 9, 1, 2, true, false)] } } }), "A still-moving manual child was considered finished.");
        Check(!Prompt(source with { Source = source.Source with { Vm = source.Source.Vm with { PendingRequests = [.. source.Source.Vm.PendingRequests!, new(9, 9, 6, 0x0800, new FieldDialogueCommand(1, 1))] } } }), "A manual child with a pending host operation was considered finished.");
        Check(!Prompt(source with { Source = source.Source with { Process = source.Source.Process with { Dialogues = [source.Dialogue, new(9, 1, 1, null, null, 0)] } } }), "An unfinished manual window was ignored.");
        Check(before == JsonSerializer.Serialize(source), "Manual-cycle qualification changed native script, choice coverage, money or source ownership.");
        Console.WriteLine("Submarine manual repeats require a new exact ASK and actual finished manual child without option credit.");
    }

    private static Fixture Create()
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 20).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[17] = entities[17] with { Priority = 1, ProgramCounter = 0x10EE };
        entities[17].SavedProgramCounters[7] = 0x10E0;
        entities[19] = entities[19] with { Priority = 6, ProgramCounter = 0x147D };
        entities[19].SavedProgramCounters[7] = 0x13BE;
        entities[19].SyncStates[6] = FieldSyncState.Waiting;
        entities[19].SyncOwners[6] = 17;
        FieldHostRequest request = new(8, 19, 6, 0x147D, new FieldDialogueCommand(0, 37, 2, 3));
        byte[] persistent = new byte[1280];
        persistent[0] = 3;
        persistent[1] = 5;
        byte[] map = new byte[256];
        map[0x17] = 2;
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 30, true, persistent, map, entities, PendingRequests: [request]);
        Ff7Dialogue dialogue = new(8, 0, 37, 2, 3, 2, AwaitingInput: true);
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 17, 4, Ff7AutoPlayTargetKind.LineTouch, null, 1283, "before", Accepted: true, NavigationKey: "nav");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, 0x10E6, 20) };
        active = active with { ExecutionProvenance = active.ExecutionProvenance! with { Owners = [.. active.ExecutionProvenance!.Owners, new(19, 6, 14, 0x13BF, 17, 1, 0x10EE, 21, false)] } };
        Ff7SessionSave save = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { PlayerLocked = true, MenuDisabled = true, Dialogues = [dialogue] } };
        Ff7AutoPlayChoiceState choice = new("choice", active) { NativeSourceOwned = true, NativeOwner = new(1, Program, 7, 19, 6, 0x147D, entities[19].SavedProgramCounters.ToArray()) };
        return new(save, new() { Active = active, Choice = choice }, dialogue, choice);
    }

    private static bool Prompt(Fixture source) => Ff7AutoPlayChoiceWorkflow.IsNativeSubmarineManualPrompt(source.Source, source.State, source.Dialogue);
    private static bool Abandon(Fixture source) => Ff7AutoPlayChoiceWorkflow.CanAbandonNativeSubmarineManualCycle(source.Source, source.State, source.Dialogue, source.Choice);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
