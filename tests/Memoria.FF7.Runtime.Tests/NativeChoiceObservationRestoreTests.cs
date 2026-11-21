using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeChoiceObservationRestoreTests
{
    private sealed record Fixture(Ff7AutoPlayState State, Ff7SessionSave Current, Ff7SessionSave Before);

    internal static void Run()
    {
        CheckCursorNormalization();
        Fixture fixture = Create();
        string native = JsonSerializer.Serialize(new { fixture.Current, fixture.Before });
        Check(Ff7AutoPlayChoiceWorkflow.TryQueueLostChoiceObservationReplay(fixture.State, fixture.Current, fixture.Before), "A registered unfinished choice with a future observation cursor could not replay normally.");
        Check(fixture.State.PendingRestore?.Path == "before-choice" && fixture.State.Active!.ExecutionProvenance!.LastActivityTick == 29, "Recovery did not queue the existing before-choice checkpoint with the corrected observation cursor.");
        var coverage = fixture.State.Coverage.Choices.Single();
        Check(coverage.ActiveOption == 4 && coverage.CompletedOptions.SequenceEqual(new[] { 3 }), "Observation recovery changed the actual selected or completed options.");
        Check(native == JsonSerializer.Serialize(new { fixture.Current, fixture.Before }), "Policy recovery changed native snapshots.");
        foreach (Action<Fixture> change in new Action<Fixture>[]
        {
            item => item.State.Active = item.State.Active! with { EventId = "other" },
            item => item.State.Active = item.State.Active! with { ExecutionProvenance = item.State.Active!.ExecutionProvenance! with { RootDispatchTick = 11 } },
            item => item.State.Choice = item.State.Choice! with { NativeOwner = item.State.Choice!.NativeOwner! with { ProgramIdentity = "other" } },
            item => item.State.Choice = item.State.Choice! with { BeforeActive = item.State.Choice!.BeforeActive! with { ExecutionProvenance = item.State.Choice!.BeforeActive!.ExecutionProvenance! with { LastActivityTick = 30 } } },
            item => item.State.Choice = item.State.Choice! with { BeforeActive = item.State.Choice!.BeforeActive! with { ExecutionProvenance = item.State.Choice!.BeforeActive!.ExecutionProvenance! with { RootDispatchTick = 31 } } },
            item => item.Current.Vm.Entities[0] = item.Current.Vm.Entities[0] with { Priority = 1 },
            item => item.State.Coverage.Choices.Single().CompletedOptions.Add(4),
            item => item.State.Coverage.Choices.Single().BeforeCheckpoint = null
        })
        {
            Fixture invalid = Create();
            change(invalid);
            Check(!Ff7AutoPlayChoiceWorkflow.TryQueueLostChoiceObservationReplay(invalid.State, invalid.Current, invalid.Before) && invalid.State.PendingRestore is null, "Unknown, live, already completed or mismatched work authorized observation replay.");
        }
        Fixture valid = Create();
        Check(!Ff7AutoPlayChoiceWorkflow.TryQueueLostChoiceObservationReplay(valid.State, valid.Current with { FieldGeneration = 2 }, valid.Before), "A different generation authorized historical replay.");
        FieldHostRequest ambient = new(99, 0, 7, 50, new FieldDialogueCommand(0, 5));
        Fixture ambientFixture = Create();
        Check(Ff7AutoPlayChoiceWorkflow.TryQueueLostChoiceObservationReplay(ambientFixture.State, ambientFixture.Current with { Vm = ambientFixture.Current.Vm with { PendingRequests = [ambient] } }, ambientFixture.Before), "An unrelated ambient request on the same entity blocked registered choice replay.");
        Fixture ownedFixture = Create();
        Check(!Ff7AutoPlayChoiceWorkflow.TryQueueLostChoiceObservationReplay(ownedFixture.State, ownedFixture.Current with { Vm = ownedFixture.Current.Vm with { PendingRequests = [ambient with { Priority = 1 }] } }, ownedFixture.Before), "An outstanding request at the tracked owned priority authorized replay.");
        Console.WriteLine("Restored choice observation cursor and registered replay guards passed.");
    }

    private static void CheckCursorNormalization()
    {
        Fixture fixture = Create();
        Ff7AutoPlayActiveEvent original = fixture.State.Choice!.BeforeActive!;
        string before = JsonSerializer.Serialize(original);
        Ff7AutoPlayActiveEvent normalized = Ff7AutoPlayExecutionOwners.NormalizeRestoredObservationCursor(original, fixture.Before.Vm, 1);
        Check(normalized.ExecutionProvenance is { LastActivityTick: 29, ActivityCursor: 0 } && normalized.ExecutionProvenance.Owners.SequenceEqual(original.ExecutionProvenance!.Owners) && normalized.ExecutionProvenance.RootDispatchTick == original.ExecutionProvenance.RootDispatchTick && !normalized.Returned, "Restoration changed execution facts instead of only its observation cursor.");
        Ff7NativeScriptActivity[] returned = [new(Return: new(0, 1, 40, 30))];
        Ff7AutoPlayActiveEvent Fold(Ff7AutoPlayActiveEvent active) => Ff7AutoPlayExecutionOwners.Observe(active, 1, "program", 30, returned, (_, _) => 10);
        Check(Fold(original).ExecutionProvenance!.RootReturned == false && Fold(normalized).ExecutionProvenance!.RootReturned, "The normalized cursor did not observe the real subsequent native RET that the future cursor skipped.");
        Check(before == JsonSerializer.Serialize(original), "Cursor normalization mutated source metadata.");
        Check(ReferenceEquals(original, Ff7AutoPlayExecutionOwners.NormalizeRestoredObservationCursor(original, fixture.Before.Vm with { ProgramIdentity = "other" }, 1)), "A mismatched profile normalized unknown ownership.");
        Check(ReferenceEquals(original, Ff7AutoPlayExecutionOwners.NormalizeRestoredObservationCursor(original, fixture.Before.Vm, 2)), "A different generation normalized unknown ownership.");
    }

    private static Fixture Create()
    {
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, NavigationKey: "nav");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 10, 10) with { LastActivityTick = 100, ActivityCursor = 4 } };
        FieldEntitySnapshot Entity(int priority) => new(20, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest request = new(8, 0, 1, 20, new FieldDialogueCommand(0, 4, 3, 4));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 30, true, new byte[1280], new byte[256], [Entity(1)], PendingRequests: [request]);
        Ff7SessionSave before = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { Dialogues = [new(8, 0, 4, 3, 4, 3)] } };
        Ff7SessionSave current = before with { Vm = vm with { Tick = 100, Entities = [Entity(7)], PendingRequests = [] }, Process = new() };
        Ff7AutoPlayChoiceNativeOwner owner = new(1, "program", 8, 0, 1, 20, new int[8]);
        Ff7AutoPlayState state = new() { Active = active, Choice = new("choice", active) { NativeOwner = owner } };
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.ObserveChoice("choice", 2, 3);
        var coverage = state.Coverage.Choices.Single();
        coverage.BeforeCheckpoint = "before-choice";
        coverage.ActiveOption = 4;
        coverage.CompletedOptions.Add(3);
        return new(state, current, before);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
