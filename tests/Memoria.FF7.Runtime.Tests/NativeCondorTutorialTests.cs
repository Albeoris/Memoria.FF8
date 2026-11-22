using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeCondorTutorialTests
{
    private const string Program = "F42D2939525C9597ABE30149964C7EE8AEAFEB6B7A43E708A3F9E508122AE642";
    private sealed record Fixture(Ff7SessionSave Source, Ff7AutoPlayState State, Ff7Dialogue Dialogue, Ff7AutoPlayChoiceState Choice);

    internal static void Run()
    {
        Fixture source = Create();
        string before = JsonSerializer.Serialize(source);
        Check(Prompt(source), "The actual owned Condor repeat-tutorial ASK was not recognized.");
        Check(Abandon(source), "A new request in the same actual tutorial root lifetime did not release the old repeat choice.");
        Check(!Abandon(source with { Choice = source.Choice with { NativeOwner = source.Choice.NativeOwner! with { RequestId = 8 } } }), "The same request was treated as a returned tutorial cycle.");
        Check(!Abandon(source with { Choice = source.Choice with { NativeOwner = null } }), "Unknown previous native prompt metadata authorized abandonment.");
        Check(!Abandon(source with { Choice = source.Choice with { BeforeActive = source.State.Active! with { ExecutionProvenance = source.State.Active!.ExecutionProvenance! with { RootDispatchTick = 19 } } } }), "An unrelated root lifetime authorized tutorial abandonment.");
        Check(!Prompt(source with { Source = source.Source with { FieldGeneration = 2 } }), "Another generation borrowed the Condor prompt.");
        Check(!Prompt(source with { Source = source.Source with { Vm = source.Source.Vm with { ProgramIdentity = "other" } } }), "Another native profile acquired tutorial exit advice.");
        foreach (Ff7Dialogue invalid in new[] { source.Dialogue with { Window = 2 }, source.Dialogue with { Text = 14 }, source.Dialogue with { FirstChoice = 0 }, source.Dialogue with { LastChoice = 5 }, source.Dialogue with { RequestId = 99 } })
        {
            Check(!Prompt(source with { Dialogue = invalid }), "An unrelated actual request or ASK header acquired Condor exit advice.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            value => value.State.Active = value.State.Active! with { Accepted = false },
            value => value.State.Active = value.State.Active! with { Returned = true },
            value => value.State.Active = value.State.Active! with { Entity = 21 },
            value => value.State.Active = value.State.Active! with { Script = 2 },
            value => value.State.Active = value.State.Active! with { ExecutionProvenance = null },
            value => value.Source.Vm.Entities[22] = value.Source.Vm.Entities[22] with { ProgramCounter = 0x1D5D },
            value => value.Source.Vm.Entities[10] = value.Source.Vm.Entities[10] with { Priority = 6 },
            value => value.Source.Vm.Entities[10] = value.Source.Vm.Entities[10] with { ProgramCounter = 0x0ACC },
            value => value.Source.Vm.Entities[10].SyncOwners[5] = 21,
            value => value.Source.Vm.Entities[10].SyncStates[5] = FieldSyncState.None
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Prompt(invalid), "A different root, worker, sync owner or admission acquired the tutorial recipe.");
        }
        Ff7AutoPlayChoiceNativeOwner old = source.Choice.NativeOwner!;
        Check(!Abandon(source with { Choice = source.Choice with { NativeOwner = old with { Generation = 2 } } }), "An old choice from another generation authorized repeat abandonment.");
        int[] wrongFrame = old.SavedProgramCounters.ToArray();
        wrongFrame[7]++;
        Check(!Abandon(source with { Choice = source.Choice with { NativeOwner = old with { SavedProgramCounters = wrongFrame } } }), "A different saved native frame authorized repeat abandonment.");
        Check(before == JsonSerializer.Serialize(source), "Tutorial classification changed native state, ownership or coverage.");
        var planner = Ff7AutoPlayPolicy.CreatePlanner(source.State);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => source.Source, planner, source.State, (name, _) => name, _ => true);
        Check(workflow.ChooseSelection(source.Dialogue) == 6, "The current tutorial ASK did not select its real native exit.");
        var coverage = source.State.Coverage.Choices.Single();
        Check(coverage.PolicyDeferredOptions.IsSupersetOf([1, 2, 3, 4, 5]) && coverage.CompletedOptions.Count == 0 && coverage.ActiveOption == 6 && source.State.PendingRestore is null, "Optional tutorial deferral fabricated completion or restored an unrelated state.");
        Console.WriteLine("Owned Condor tutorial exit advice preserves native request/root lifetimes and defers optional repeats without credit.");
    }

    private static Fixture Create()
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 23).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[22] = entities[22] with { Priority = 1, ProgramCounter = 0x1D5C };
        entities[10] = entities[10] with { Priority = 5, ProgramCounter = 0x0ACB };
        entities[10].SyncStates[5] = FieldSyncState.Waiting;
        entities[10].SyncOwners[5] = 22;
        entities[10].SavedProgramCounters[7] = 0x0A90;
        Ff7AutoPlayActiveEvent active = new("visit", "talk", "condition", 1, 22, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 1110, "before", Accepted: true, NavigationKey: "nav");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, 0x1C00, 20) with { Owners = [new(22, 1, 1, 0x1C00, -1, -1, 0, 20, false), new(10, 5, 1, 0x0AB2, 22, 1, 0x1D5C, 21, false)] } };
        FieldVariables variables = new();
        variables.WriteWord(FieldBank.Persistent0Word, 0, 1110);
        FieldHostRequest request = new(8, 10, 5, 0x0ACB, new FieldDialogueCommand(1, 13, 1, 6));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 30, true, variables.CopyPersistent(), variables.CopyMap(), entities, PendingRequests: [request]);
        Ff7Dialogue dialogue = new(8, 1, 13, 1, 6, 1);
        Ff7SessionSave source = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { Dialogues = [dialogue] } };
        Ff7AutoPlayChoiceState choice = new("choice", active) { NativeSourceOwned = true, NativeOwner = new(1, Program, 7, 10, 5, 0x0ACB, entities[10].SavedProgramCounters.ToArray()) };
        return new(source, new() { Active = active }, dialogue, choice);
    }

    private static bool Prompt(Fixture source) => Ff7AutoPlayChoiceWorkflow.IsNativeCondorTutorialExitPrompt(source.Source, source.State, source.Dialogue);
    private static bool Abandon(Fixture source) => Ff7AutoPlayChoiceWorkflow.CanAbandonNativeCondorTutorialCycle(source.Source, source.State, source.Dialogue, source.Choice);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
