using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeHolzoffRepeatChoiceTests
{
    private const string Program = "088F313665B33A89B5EF05D2704B3107817DC5C59D9BA77F21BCF7D55D6249C8";

    internal static void Run()
    {
        Fixture fixture = Create();
        string before = JsonSerializer.Serialize(fixture);
        Check(Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(fixture.Source, fixture.State, fixture.Dialogue), "The exact ambient introduction exit prompt was not recognized.");
        Check(Ff7AutoPlayChoiceWorkflow.CanAbandonNativeHolzoffCycle(fixture.Source, fixture.State, fixture.Dialogue, fixture.Choice), "A genuinely repeated native introduction could not leave its repeat option.");
        Check(!Ff7AutoPlayChoiceWorkflow.CanAbandonNativeHolzoffCycle(fixture.Source, fixture.State, fixture.Dialogue, fixture.Choice with { NativeOwner = fixture.Choice.NativeOwner! with { RequestId = fixture.Dialogue.RequestId } }), "The same native request was mistaken for a completed repeat cycle.");
        Check(!Ff7AutoPlayChoiceWorkflow.CanAbandonNativeHolzoffCycle(fixture.Source, fixture.State, fixture.Dialogue, fixture.Choice with { NativeOwner = fixture.Choice.NativeOwner! with { Generation = 2 } }), "A different field generation authorized abandoning a repeat option.");
        Check(!Ff7AutoPlayChoiceWorkflow.CanAbandonNativeHolzoffCycle(fixture.Source, fixture.State, fixture.Dialogue, fixture.Choice with { NativeOwner = null }), "Absent historical owner metadata authorized abandoning a repeat option.");
        Check(!Ff7AutoPlayChoiceWorkflow.CanAbandonNativeHolzoffCycle(fixture.Source, fixture.State, fixture.Dialogue, fixture.Choice with { NativeSourceOwned = true }), "An owned source choice was rewritten as the ambient introduction.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(fixture.Source with { Vm = fixture.Source.Vm with { ProgramIdentity = "other" } }, fixture.State, fixture.Dialogue), "A different native program received introduction advice.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(fixture.Source, fixture.State, fixture.Dialogue with { Window = 1 }), "A different native prompt window received introduction advice.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(fixture.Source, fixture.State, fixture.Dialogue with { Text = 12 }), "A different native prompt text received introduction advice.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(fixture.Source, fixture.State, fixture.Dialogue with { LastChoice = 3 }), "A different native choice domain received introduction advice.");
        fixture.Source.Vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting;
        fixture.Source.Vm.Entities[1].SyncOwners[6] = 0;
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(fixture.Source, fixture.State, fixture.Dialogue), "A synchronous director lifetime was mistaken for the ambient introduction.");
        fixture.Source.Vm.Entities[1].SyncStates[6] = FieldSyncState.None;
        fixture.Source.Vm.Entities[1].SyncOwners[6] = -1;
        fixture.Source.Vm.Entities[1].SavedProgramCounters[6] = 1;
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(fixture.Source, fixture.State, fixture.Dialogue), "A suspended director lifetime received ambient introduction advice.");
        fixture.Source.Vm.Entities[1].SavedProgramCounters[6] = 0;
        Ff7AutoPlayActiveEvent unrelated = fixture.State.Active!;
        fixture.State.Active = unrelated with { Accepted = true };
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(fixture.Source, fixture.State, fixture.Dialogue), "An unrelated admitted event was overridden by ambient introduction advice.");
        fixture.State.Active = unrelated;
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(WithFlags(fixture.Source, 0), fixture.State, fixture.Dialogue), "An introduction without its required native setup flag received advice.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeHolzoffExitPrompt(WithFlags(fixture.Source, 0xC0), fixture.State, fixture.Dialogue), "An already completed introduction received repeat advice.");
        Check(before == JsonSerializer.Serialize(fixture), "Prompt qualification changed native state, stale source ownership or the parent choice.");
        CheckWorkflow(fixture);
        CheckCompletion(Create());
        Console.WriteLine("Native ambient introduction repeat, owner and actual completion guards passed.");
    }

    private static void CheckWorkflow(Fixture fixture)
    {
        var planner = Ff7AutoPlayPolicy.CreatePlanner(fixture.State);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => fixture.Source, planner, fixture.State, (name, _) => name, _ => true);
        string before = JsonSerializer.Serialize(fixture.Source);
        Check(workflow.ChooseSelection(fixture.Dialogue) == 2, "A fresh introduction prompt did not select its real exit option.");
        Ff7AutoPlayChoiceState learned = fixture.State.Choice ?? throw new InvalidOperationException("The native prompt owner was not recorded.");
        var coverage = fixture.State.Coverage.Choices.Single(item => item.Key == learned.Key);
        coverage.ActiveOption = 1;
        coverage.CompletedOptions.Clear();
        coverage.PolicyDeferredOptions.Remove(1);
        planner.ObserveChoice("parent", 3, 2);
        fixture.State.Choice = learned with { NativeOwner = fixture.Choice.NativeOwner, NativeSourceOwned = false, Parent = fixture.Choice.Parent };
        Check(workflow.ChooseSelection(fixture.Dialogue) == 2 && coverage.ActiveOption == 2, "A new native prompt remained stuck in the historical repeat selection.");
        Check(coverage.PolicyDeferredOptions.Contains(1) && !coverage.CompletedOptions.Contains(1), "Repeat abandonment fabricated completed-option credit.");
        Check(ReferenceEquals(fixture.State.Choice?.Parent, fixture.Choice.Parent) && fixture.State.Choice?.NativeSourceOwned == false && fixture.State.PendingRestore is null, "Repeat abandonment changed the finite parent, promoted source ownership or restored historical state.");
        Check(before == JsonSerializer.Serialize(fixture.Source), "Selecting the native exit changed native state.");
    }

    private static void CheckCompletion(Fixture fixture)
    {
        Ff7SessionSave returned = WithFlags(fixture.Source, 0xC0);
        FieldVariables variables = new(returned.Vm.PersistentVariables, returned.Vm.MapVariables);
        variables.WriteByte(FieldBank.MapByte, 0x0D, 2);
        FieldEntitySnapshot[] entities = returned.Vm.Entities.Select(entity => entity with { SavedProgramCounters = entity.SavedProgramCounters.ToArray(), SyncStates = entity.SyncStates.ToArray(), SyncOwners = entity.SyncOwners.ToArray() }).ToArray();
        entities[1] = entities[1] with { ProgramCounter = 0x05C2 };
        FieldHostRequest request = fixture.Source.Vm.PendingRequests!.Single();
        Ff7AutoPlayChoiceState completedChoice = fixture.Choice with { NativeOwner = fixture.Choice.NativeOwner! with { RequestId = request.Id } };
        returned = returned with { Vm = returned.Vm with { PersistentVariables = variables.CopyPersistent(), MapVariables = variables.CopyMap(), Entities = entities, PendingRequests = [] }, Process = new() { Receipts = [new(request, new(true, 2))] } };
        string before = JsonSerializer.Serialize(returned);
        bool Complete(Ff7SessionSave source, int? selection = 2) => Ff7AutoPlayChoiceWorkflow.HasNativeHolzoffControlReturn(source, fixture.State, completedChoice, selection);
        Check(Complete(returned), "The actual native exit, final setup flag and ambient control return did not complete selection 2.");
        Check(!Complete(returned, 1) && !Complete(returned, null), "An unexecuted exit received completion credit.");
        Check(!Complete(fixture.Source), "The still-pending introduction received completion credit.");
        Check(!Complete(WithFlags(returned, 0x40)), "Missing native completion flag received exit credit.");
        Check(!Complete(returned with { Process = returned.Process with { PlayerLocked = true } }), "Locked controls received exit completion credit.");
        Check(!Complete(returned with { Process = returned.Process with { Receipts = [] } }), "Missing actual native answer receipt received exit completion credit.");
        Check(!Complete(returned with { Process = returned.Process with { Receipts = [new(request, new(true, 1))] } }), "The repeat answer receipt received exit completion credit.");
        Check(!Complete(returned with { Vm = returned.Vm with { PendingRequests = fixture.Source.Vm.PendingRequests } }), "The unresolved native prompt received exit completion credit.");
        entities[1] = entities[1] with { ProgramCounter = 0x0517 };
        Check(!Complete(returned), "The ASK instruction was mistaken for the real ambient completion point.");
        entities[1] = entities[1] with { ProgramCounter = 0x05C3 };
        Check(!Complete(returned), "The adjacent script's RET substituted for the actual ambient completion PC.");
        entities[1] = entities[1] with { ProgramCounter = 0x05C2 };
        Check(before == JsonSerializer.Serialize(returned) && fixture.Choice.NativeSourceOwned == false && fixture.State.Active?.Accepted == false, "Completion qualification mutated native state or invented an admitted root.");
    }

    private static Fixture Create()
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 15).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[1] = entities[1] with { ProgramCounter = 0x0517 };
        FieldHostRequest request = new(8, 1, 7, 0x0517, new FieldDialogueCommand(0, 11, 1, 2));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 30, true, new byte[1280], new byte[256], entities, PendingRequests: [request]);
        Ff7Dialogue dialogue = new(8, 0, 11, 1, 2, 1, AwaitingInput: true);
        Ff7SessionSave source = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { PlayerLocked = true, MenuDisabled = true, Dialogues = [dialogue] } };
        source = WithFlags(source, 0x40);
        Ff7AutoPlayActiveEvent unrelated = new("other-visit", "gateway", "condition", 0, 0, 0, Ff7AutoPlayTargetKind.Gateway, null, 0, "before", NavigationKey: "other-nav");
        Ff7AutoPlayChoiceState parent = new("parent", unrelated) { NativeSourceOwned = false, NativeOwner = new(1, Program, 6, 14, 6, 0x09FA, new int[8]) };
        Ff7AutoPlayChoiceState choice = new("choice", unrelated, parent) { NativeSourceOwned = false, NativeOwner = new(1, Program, 7, 1, 7, 0x0517, new int[8]) };
        return new(source, new() { Active = unrelated }, dialogue, choice);
    }

    private static Ff7SessionSave WithFlags(Ff7SessionSave source, byte flags)
    {
        FieldVariables variables = new(source.Vm.PersistentVariables, source.Vm.MapVariables);
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, flags);
        return source with { Vm = source.Vm with { PersistentVariables = variables.CopyPersistent() } };
    }

    private sealed record Fixture(Ff7SessionSave Source, Ff7AutoPlayState State, Ff7Dialogue Dialogue, Ff7AutoPlayChoiceState Choice);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
