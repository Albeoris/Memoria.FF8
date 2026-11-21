using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeTutorialRepeatTests
{
    private const string Program = "A513104CED946295581FA2C752FA5E3804FC542FAF7DA93C594F3A47485F9658";

    internal static void Run()
    {
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 10, 2, Ff7AutoPlayTargetKind.LinePush, null, 0, "before", Accepted: true, NavigationKey: "nav");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, 0x0B60, 20) with { Owners = [new(10, 1, 2, 0x0B60, -1, -1, 0, 20, false), new(25, 6, 3, 0x1500, 10, 1, 0x0B6B, 21, false)] } };
        FieldEntitySnapshot Ambient() => new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 26).Select(_ => Ambient()).ToArray();
        entities[10] = entities[10] with { ProgramCounter = 0x0B6B, Priority = 1 };
        entities[25] = entities[25] with { ProgramCounter = 0x156B, Priority = 6 };
        entities[8] = entities[8] with { ProgramCounter = 0x0AD8, Priority = 6 };
        entities[25].SyncStates[6] = FieldSyncState.Waiting;
        entities[25].SyncOwners[6] = 10;
        entities[8].SyncStates[6] = FieldSyncState.Waiting;
        entities[8].SyncOwners[6] = 25;
        entities[8].SavedProgramCounters[7] = 12;
        FieldHostRequest request = new(8, 8, 6, 0x0AD8, new FieldDialogueCommand(2, 30, 1, 2));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 30, true, new byte[1280], new byte[256], entities, PendingRequests: [request]);
        Ff7SessionSave source = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { PlayerLocked = true, MenuDisabled = true } };
        Ff7AutoPlayState state = new() { Active = active };
        Ff7Dialogue dialogue = new(8, 2, 30, 1, 2, 1);
        Ff7AutoPlayChoiceNativeOwner previous = new(1, Program, 7, 8, 6, 0x0AD8, entities[8].SavedProgramCounters.ToArray());
        Ff7AutoPlayChoiceState choice = new("choice", active) { NativeSourceOwned = false, NativeOwner = previous };
        string before = JsonSerializer.Serialize(new { source, state, choice });
        Check(Ff7AutoPlayChoiceWorkflow.IsNativeTutorialExitPrompt(source, state, dialogue), "The exact current native repeat-tutorial ASK was not recognized.");
        Check(Ff7AutoPlayChoiceWorkflow.CanAbandonNativeTutorialCycle(source, state, dialogue, choice), "A genuinely new repeat prompt under the same native root lifetime could not exit the cycle.");
        Check(!Ff7AutoPlayChoiceWorkflow.CanAbandonNativeTutorialCycle(source, state, dialogue, choice with { NativeOwner = previous with { RequestId = 8 } }), "The same request was treated as a completed tutorial cycle.");
        Check(!Ff7AutoPlayChoiceWorkflow.CanAbandonNativeTutorialCycle(source, state, dialogue, choice with { NativeOwner = null }), "Unknown old prompt metadata authorized cycle abandonment.");
        Check(!Ff7AutoPlayChoiceWorkflow.CanAbandonNativeTutorialCycle(source, state, dialogue, choice with { BeforeActive = active with { ExecutionProvenance = active.ExecutionProvenance! with { RootDispatchTick = 19 } } }), "A different root lifetime authorized cycle abandonment.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeTutorialExitPrompt(source with { Vm = vm with { ProgramIdentity = "other" } }, state, dialogue), "A different profile received tutorial input advice.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeTutorialExitPrompt(source, state, dialogue with { LastChoice = 3 }), "An unrelated choice domain received tutorial advice.");
        entities[8].SyncOwners[6] = 10;
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeTutorialExitPrompt(source, state, dialogue), "A mismatched actual synchronous owner received tutorial advice.");
        entities[8].SyncOwners[6] = 25;
        Check(before == JsonSerializer.Serialize(new { source, state, choice }), "Tutorial qualification changed native state, source ownership or choice history.");
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => source, planner, state, (name, _) => name, _ => true);
        string nativeBefore = JsonSerializer.Serialize(source);
        Check(workflow.ChooseSelection(dialogue) == 2, "A fresh current tutorial exit prompt did not choose the native exit option.");
        Ff7AutoPlayChoiceState learned = state.Choice ?? throw new InvalidOperationException("The native tutorial choice owner was not recorded.");
        var coverage = state.Coverage.Choices.Single(item => item.Key == learned.Key);
        coverage.ActiveOption = 1;
        coverage.CompletedOptions.Clear();
        coverage.PolicyDeferredOptions.Remove(1);
        planner.ObserveChoice("parent", 2, 1);
        Ff7AutoPlayChoiceState parent = new("parent", null);
        state.Choice = learned with { NativeOwner = previous, NativeSourceOwned = false, Parent = parent };
        Check(workflow.ChooseSelection(dialogue) == 2 && coverage.ActiveOption == 2, "A real repeated tutorial prompt remained stuck in the previous active repeat option.");
        Check(coverage.PolicyDeferredOptions.Contains(1) && !coverage.CompletedOptions.Contains(1), "Abandoning the repeat cycle fabricated completed-option credit.");
        Check(ReferenceEquals(state.Choice?.Parent, parent) && state.Choice?.NativeSourceOwned == false && state.PendingRestore is null, "Repeat-cycle exit replaced its parent, invented source ownership or restored historical state.");
        Check(nativeBefore == JsonSerializer.Serialize(source), "Repeat-cycle selection changed native variables or state.");

        FieldEntitySnapshot[] returnedEntities = Enumerable.Range(0, 26).Select(_ => Ambient()).ToArray();
        Ff7SessionSave returned = source with { Vm = vm with { Entities = returnedEntities, PendingRequests = [] }, Process = source.Process with { PlayerLocked = false, MenuDisabled = false } };
        Ff7AutoPlayActiveEvent completed = active with { Returned = true, ExecutionProvenance = active.ExecutionProvenance! with { RootReturned = true, Owners = [] } };
        state.Active = completed;
        bool Complete(Ff7SessionSave actual, int? selection = 2) => Ff7AutoPlayChoiceWorkflow.HasNativeTutorialControlReturn(actual, state, choice, selection);
        Check(Complete(returned), "Actual whole-root return and native control release did not complete the exit selection.");
        Check(!Complete(returned, 1) && !Complete(returned, null), "An unexecuted exit selection received completion credit.");
        Check(!Complete(source), "A live tutorial worker or locked controls received completion credit.");
        Check(!Complete(returned with { Vm = returned.Vm with { PendingRequests = [request] } }), "An outstanding owned ASK completed the tutorial.");
        state.Active = completed with { Returned = false };
        Check(!Complete(returned), "A child return substituted for the actual root return.");
        state.Active = completed with { ExecutionProvenance = completed.ExecutionProvenance! with { RootDispatchTick = 99 } };
        Check(!Complete(returned), "A different root lifetime completed an old choice.");
        Check(choice.NativeSourceOwned == false, "The adapter fabricated native source ownership for an old choice.");
        Console.WriteLine("Native repeat-tutorial prompt, lifetime and whole-root completion guards passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
