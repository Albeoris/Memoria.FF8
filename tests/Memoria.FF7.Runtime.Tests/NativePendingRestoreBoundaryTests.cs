using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativePendingRestoreBoundaryTests
{
    private sealed record Fixture(Ff7AutoPlayState State, Ff7SessionSave Current, Ff7SessionSave Before);

    internal static void Run()
    {
        Fixture fixture = Create();
        string native = JsonSerializer.Serialize(new { fixture.Current, fixture.Before });
        Ff7AutoPlayActiveEvent original = fixture.State.Choice!.BeforeActive!;
        Ff7AutoPlayActiveEvent? primary = fixture.State.Choice.ContinuationActive;
        Check(Ff7AutoPlayChoiceWorkflow.TryQueueContaminatedChoiceObservationReplay(fixture.State, fixture.Current, fixture.Before), "An interrupted registered restore with impossible future return metadata was not replayed.");
        Check(ReferenceEquals(fixture.State.Active, original) && !fixture.State.Active.Returned && fixture.State.PendingRestore?.Path == "before-choice", "Recovery borrowed a primary option's return or changed the registered replay source.");
        Check(ReferenceEquals(fixture.State.Choice!.ContinuationActive, primary) && fixture.State.Coverage.Choices.Single().CompletedOptions.SequenceEqual(new[] { 0 }) && fixture.State.Coverage.Choices.Single().ActiveOption == 1, "Recovery rewrote the primary continuation or actual choice outcomes.");
        Check(native == JsonSerializer.Serialize(new { fixture.Current, fixture.Before }), "Recovery changed native VM/process snapshots.");
        foreach (Action<Fixture> change in new Action<Fixture>[]
        {
            item => item.State.Active = item.State.Active! with { ConditionFingerprint = "other" },
            item => item.State.Active = item.State.Active! with { CompletionRecorded = true },
            item => item.State.Active = item.State.Active! with { TransitionOwned = true },
            item => item.State.Active = item.State.Active! with { ExecutionProvenance = item.State.Active!.ExecutionProvenance! with { LastActivityTick = 30 } },
            item => item.State.Active = item.State.Active! with { ExecutionProvenance = item.State.Active!.ExecutionProvenance! with { RootDispatchTick = 11 } },
            item => item.State.Choice = item.State.Choice! with { NativeOwner = item.State.Choice!.NativeOwner! with { ProgramCounter = 21 } },
            item => item.State.Choice = item.State.Choice! with { BeforeActive = item.State.Choice!.BeforeActive! with { Returned = true } },
            item => item.State.Coverage.Choices.Single().CompletedOptions.Add(1),
            item => item.State.Coverage.Choices.Single().BeforeCheckpoint = null,
            item => item.Current.Vm.Entities[0] = item.Current.Vm.Entities[0] with { ProgramCounter = 21 },
            item => item.Current.Vm.Entities[0] = item.Current.Vm.Entities[0] with { Priority = 7 }
        })
        {
            Fixture invalid = Create();
            change(invalid);
            Check(!Ff7AutoPlayChoiceWorkflow.TryQueueContaminatedChoiceObservationReplay(invalid.State, invalid.Current, invalid.Before) && invalid.State.PendingRestore is null, "Unproved or already completed work authorized historical replay.");
        }
        Fixture valid = Create();
        Check(!Ff7AutoPlayChoiceWorkflow.TryQueueContaminatedChoiceObservationReplay(valid.State, valid.Current with { FieldGeneration = 2 }, valid.Before), "Another field generation authorized replay.");
        Check(!Ff7AutoPlayChoiceWorkflow.TryQueueContaminatedChoiceObservationReplay(valid.State, valid.Current with { Vm = valid.Current.Vm with { ProgramIdentity = "other" } }, valid.Before), "Another native profile authorized replay.");
        Console.WriteLine("Interrupted choice restore observation and registered replay guards passed.");
    }

    private static Fixture Create()
    {
        Ff7AutoPlayActiveEvent original = new("visit", "line", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, NavigationKey: "nav");
        original = original with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(original, "program", 10, 10) with { LastActivityTick = 29 } };
        Ff7AutoPlayActiveEvent contaminated = original with { Returned = true, ExecutionProvenance = original.ExecutionProvenance! with { RootReturned = true, LastActivityTick = 100, Owners = [] } };
        FieldEntitySnapshot Entity() => new(20, 1, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest request = new(8, 0, 1, 20, new FieldDialogueCommand(0, 4, 0, 1));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 30, true, new byte[1280], new byte[256], [Entity()], PendingRequests: [request]);
        Ff7SessionSave before = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { Dialogues = [new(8, 0, 4, 0, 1, 0)] } };
        Ff7SessionSave current = before with { Vm = vm with { Entities = [Entity()], PendingRequests = [] }, Process = new() };
        Ff7AutoPlayChoiceState choice = new("choice", original, ContinuationActive: contaminated with { CompletionRecorded = true }) { NativeOwner = new(1, "program", 8, 0, 1, 20, new int[8]) };
        Ff7AutoPlayState state = new() { Active = contaminated, Choice = choice };
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.ObserveChoice("choice", 2, 0);
        var coverage = state.Coverage.Choices.Single();
        coverage.BeforeCheckpoint = "before-choice";
        coverage.CompletedOptions.Add(0);
        coverage.ActiveOption = 1;
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
