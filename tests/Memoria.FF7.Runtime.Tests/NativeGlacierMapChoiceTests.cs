using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeGlacierMapChoiceTests
{
    private const string Program = "22963A38445DD562AE4218135B8F54F47CC4296895D72BAD22BE9DBEC9ADADC1";

    internal static void Run()
    {
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 3, 4, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, NavigationKey: "nav");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, 0x0480, 20) };
        FieldEntitySnapshot Ambient() => new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 5).Select(_ => Ambient()).ToArray();
        entities[3] = entities[3] with { Priority = 1, ProgramCounter = 0x0498 };
        FieldHostRequest request = new(8, 3, 1, 0x0498, new FieldDialogueCommand(0, 12, 0, 1));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 30, true, new byte[1280], new byte[256], entities, PendingRequests: [request]);
        Ff7SessionSave source = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { PlayerLocked = true, MenuDisabled = true } };
        Ff7AutoPlayState state = new() { Active = active };
        Ff7Dialogue dialogue = new(8, 0, 12, 0, 1, 0);
        string before = JsonSerializer.Serialize(new { source, state });
        Check(Ff7AutoPlayChoiceWorkflow.IsNativeGlacierMapPrompt(source, state, dialogue), "The exact accepted native Glacier Map ASK was not recognized.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeGlacierMapPrompt(source with { Vm = vm with { ProgramIdentity = "other" } }, state, dialogue), "Another profile received acquisition advice.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeGlacierMapPrompt(source, state, dialogue with { Text = 13 }), "Another prompt received acquisition advice.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeGlacierMapPrompt(source, state, dialogue with { LastChoice = 2 }), "Another option domain received acquisition advice.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeGlacierMapPrompt(source with { Vm = vm with { PendingRequests = [] } }, state, dialogue), "A missing actual ASK received acquisition advice.");
        state.Active = active with { Accepted = false };
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeGlacierMapPrompt(source, state, dialogue), "An unadmitted root received acquisition advice.");
        state.Active = active;
        Check(before == JsonSerializer.Serialize(new { source, state }), "Prompt qualification mutated native facts.");

        Ff7AutoPlayChoiceState choice = new("choice", active) { NativeSourceOwned = true, NativeOwner = new(1, Program, 8, 3, 1, 0x0498, new int[8]) };
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        variables.WriteByte(FieldBank.Persistent0Byte, 0x45, 0x10);
        variables.WriteByte(FieldBank.Persistent0Byte, 0x82, 0x40);
        variables.WriteByte(FieldBank.MapByte, 0x0A, 1);
        Ff7SessionSave returned = source with { Vm = vm with { PersistentVariables = variables.CopyPersistent(), MapVariables = variables.CopyMap(), Entities = Enumerable.Range(0, 5).Select(_ => Ambient()).ToArray(), PendingRequests = [] }, Process = new() };
        Ff7AutoPlayActiveEvent completed = active with { Returned = true, ExecutionProvenance = active.ExecutionProvenance! with { RootReturned = true, Owners = [] } };
        state.Active = completed;
        bool Complete(Ff7SessionSave current, int? selection = 1) => Ff7AutoPlayChoiceWorkflow.HasNativeGlacierMapControlReturn(current, state, choice, selection, []);
        Check(Complete(returned), "Real acquisition flags and actual whole-root return did not complete selected option1.");
        Check(!Complete(returned, 0) && !Complete(returned, null), "A different or unexecuted option received acquisition completion.");
        foreach (var missing in new[] { (FieldBank.Persistent0Byte, (byte)0x45), (FieldBank.Persistent0Byte, (byte)0x82), (FieldBank.MapByte, (byte)0x0A) })
        {
            FieldVariables incomplete = new(returned.Vm.PersistentVariables, returned.Vm.MapVariables);
            incomplete.WriteByte(missing.Item1, missing.Item2, 0);
            incomplete.WriteByte(FieldBank.MapByte, 0, 1);
            Check(!Complete(returned with { Vm = returned.Vm with { PersistentVariables = incomplete.CopyPersistent(), MapVariables = incomplete.CopyMap() } }), "Missing native acquisition flags received completion credit.");
        }
        state.Active = completed with { Returned = false };
        Check(!Complete(returned), "A native child or unreturned root completed acquisition.");
        state.Active = completed with { ExecutionProvenance = completed.ExecutionProvenance! with { RootDispatchTick = 21 } };
        Check(!Complete(returned), "A different root lifetime completed an old acquisition choice.");
        state.Active = completed with { ExecutionProvenance = completed.ExecutionProvenance! with { Owners = [new(4, 6, 2, 0, 3, 1, 0, 20, false)] } };
        Check(!Complete(returned), "An uncertified live effectful child was ignored at acquisition completion.");
        state.Active = completed;
        state.Choice = choice;
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.ObserveChoice(choice.Key, 2, 0);
        var coverage = state.Coverage.Choices.Single();
        coverage.CompletedOptions.Add(0);
        coverage.ActiveOption = 1;
        coverage.BeforeCheckpoint = "before-choice";
        coverage.ContinuationCheckpoint = "old-primary-option-zero";
        string actual = JsonSerializer.Serialize(returned);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => returned, planner, state, (_, _) => throw new InvalidOperationException("The completed award must retain the existing primary checkpoint."), _ => true, _ => 0);
        workflow.CompleteAtControlReturn();
        Check(state.Choice is null && state.PendingRestore is null && coverage.CompletedOptions.SequenceEqual(new[] { 0, 1 }) && coverage.ContinuationCheckpoint == "old-primary-option-zero", "Award completion restored an old primary or rewrote completed native outcomes.");
        Check(actual == JsonSerializer.Serialize(returned), "Award completion changed the actual acquired native state.");
        Console.WriteLine("Native Glacier Map acquisition choice and actual completion guards passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
