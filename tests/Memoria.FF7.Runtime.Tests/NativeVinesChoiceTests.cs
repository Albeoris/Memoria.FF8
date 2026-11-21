using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeVinesChoiceTests
{
    private const string Program = "0416817BBE10A56BFBD83EF25424E548080A669EAA5AFB2C4C1D94BDFE553AE0";
    private sealed record Fixture(Ff7SessionSave Source, Ff7AutoPlayState State, Ff7Dialogue Dialogue);

    internal static void Run()
    {
        foreach (var stage in new[] { (0x089D, 4, 3, 25, 1, 134), (0x09B6, 7, 5, 25, 0, 82), (0x0BC8, 11, 8, 26, 0, 48), (0x0C9D, 11, 8, 25, 1, 48), (0x1043, 19, 13, 26, 0, 21) })
        {
            Fixture fixture = Create(stage.Item1, stage.Item2, stage.Item3, stage.Item4);
            string before = JsonSerializer.Serialize(fixture);
            Check(Preference(fixture) == stage.Item5, "The reviewed current owned vine ASK did not select its native route option.");
            Check(before == JsonSerializer.Serialize(fixture), "Choice advice changed native state or progress.");
            CheckCompletion(fixture, stage.Item6);
        }
        foreach (Action<Fixture> change in new Action<Fixture>[]
        {
            fixture => fixture.State.DestinationField = null,
            fixture => fixture.State.DestinationField = 628,
            fixture => fixture.State.Active = fixture.State.Active! with { Accepted = false },
            fixture => fixture.State.Active = fixture.State.Active! with { ExecutionProvenance = null },
            fixture => fixture.State.Active = fixture.State.Active! with { ExecutionProvenance = fixture.State.Active!.ExecutionProvenance! with { Owners = [] } },
            fixture => fixture.State.Active = fixture.State.Active! with { Entity = 7 },
            fixture => fixture.Source.Vm.Entities[1] = fixture.Source.Vm.Entities[1] with { Priority = 7 },
            fixture => fixture.Source.Vm.Entities[1] = fixture.Source.Vm.Entities[1] with { ProgramCounter = 0x089E }
        })
        {
            Fixture fixture = Create();
            change(fixture);
            Check(Preference(fixture) is null, "Wrong scope, source lifetime or native ASK owner received route advice.");
        }
        Fixture valid = Create();
        Check(Preference(valid with { Source = valid.Source with { Vm = valid.Source.Vm with { ProgramIdentity = "other" } } }) is null, "An unknown resource profile received route advice.");
        Check(Preference(valid with { Dialogue = valid.Dialogue with { LastChoice = 2 } }) is null, "A different ASK domain received route advice.");
        Check(Preference(valid with { Dialogue = valid.Dialogue with { Window = 5 } }) is null, "A different native window received route advice.");
        Check(Preference(valid with { Source = valid.Source with { Vm = valid.Source.Vm with { PendingRequests = [] } } }) is null, "A missing native pending ASK received route advice.");
        Fixture conditional = Create(0x0C9D, 11, 8, 25);
        conditional.Source.Vm.MapVariables[0] = 183;
        Check(Preference(conditional) is null, "A different native branch stage received route advice.");
        CheckCompletedChoicePreference();
        CheckSecondChoiceRecipeConflict();
        Console.WriteLine("Owned vine choice strategy and native completion fences passed.");
    }

    private static void CheckCompletedChoicePreference()
    {
        Fixture fixture = Create();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(fixture.State);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => fixture.Source, planner, fixture.State, (name, _) => name, _ => true);
        Check(workflow.ChooseSelection(fixture.Dialogue) == 1, "The initial native navigation preference was not applied.");
        var coverage = fixture.State.Coverage.Choices.Single();
        coverage.CompletedOptions = [0, 1];
        coverage.ActiveOption = null;
        coverage.Finished = true;
        fixture.State.Choice = null;
        AutoPlayEventRoute historical = new("visit", "condition", false, [new(coverage.Key, 0)], "line");
        fixture.State.Active = fixture.State.Active! with { TravelRoute = historical, TravelSelections = [] };
        string recorded = JsonSerializer.Serialize(historical);
        string completed = JsonSerializer.Serialize(coverage.CompletedOptions);
        Check(workflow.ChooseSelection(fixture.Dialogue) == 1, "Finished coverage or an old opposite recipe overrode the current native navigation preference.");
        Ff7AutoPlayActiveEvent active = fixture.State.Active!;
        Check(active.TravelRoute is null && active.TravelSelections is [{ Selection: 1 }], "The conflicting selected recipe was not detached or the actual selected navigation option was lost.");
        Check(workflow.ReadSourceSelections(active) is [{ Selection: 1 }], "The actual completed-key navigation selection was absent from source accounting.");
        Check(recorded == JsonSerializer.Serialize(historical) && completed == JsonSerializer.Serialize(coverage.CompletedOptions), "Transient recipe retirement rewrote native route history or completed outcomes.");
        fixture.State.DestinationField = 628;
        Check(workflow.ReadSourceSelections(active).Count == 0, "Navigation-only prefix selections leaked into another destination intent.");
        fixture.State.DestinationField = 629;
        Check(workflow.ReadSourceSelections(active with { ExecutionProvenance = active.ExecutionProvenance! with { ProgramIdentity = "other" } }).Count == 0, "Navigation-only prefix selections leaked into another native profile.");
    }

    private static void CheckSecondChoiceRecipeConflict()
    {
        Fixture fixture = Create(0x0C9D, 11, 8, 25);
        var planner = Ff7AutoPlayPolicy.CreatePlanner(fixture.State);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => fixture.Source, planner, fixture.State, (name, _) => name, _ => true);
        Check(workflow.ChooseSelection(fixture.Dialogue) == 1, "The second native ASK fixture did not qualify.");
        var coverage = fixture.State.Coverage.Choices.Single();
        coverage.CompletedOptions = [0, 1];
        coverage.ActiveOption = null;
        coverage.Finished = true;
        fixture.State.Choice = null;
        AutoPlayChoiceSelection first = new("actual-first-choice", 0);
        AutoPlayEventRoute historical = new("visit", "condition", false, [first, new(coverage.Key, 0)], "line");
        fixture.State.Active = fixture.State.Active! with { TravelRoute = historical, TravelSelections = [first] };
        string before = JsonSerializer.Serialize(historical);
        Check(workflow.ChooseSelection(fixture.Dialogue) == 1, "A second-ASK recipe conflict overrode the current native preference.");
        Ff7AutoPlayActiveEvent active = fixture.State.Active!;
        Check(active.TravelRoute is null && active.TravelSelections is [{ Selection: 0 }, { Selection: 1 }] && active.TravelSelections[0] == first && active.TravelSelections[1].Key == coverage.Key, "Detaching a conflicting second recipe discarded the actual first selection or failed to append the current one.");
        Check(workflow.ReadSourceSelections(active).SequenceEqual(new[] { first, new AutoPlayChoiceSelection(coverage.Key, 1) }), "Source accounting reversed the chronological native choice prefix.");
        Check(before == JsonSerializer.Serialize(historical) && coverage.CompletedOptions.SequenceEqual(new[] { 0, 1 }), "Second-ASK advice rewrote route history or native completed outcomes.");
        Check(workflow.ReadSourceSelections(active with { EventId = "different-root" }).Count == 0, "Stale execution provenance leaked a previous root's selected prefix.");
    }

    private static void CheckCompletion(Fixture fixture, int triangle)
    {
        Ff7AutoPlayActiveEvent before = fixture.State.Active!;
        Ff7AutoPlayChoiceState choice = new("choice", before) { NativeSourceOwned = true };
        Ff7AutoPlayActiveEvent returned = before with { Returned = true, ExecutionProvenance = before.ExecutionProvenance! with { RootReturned = true, Owners = [] } };
        fixture.State.Active = returned;
        Ff7SessionSave source = fixture.Source with { Vm = fixture.Source.Vm with { PendingRequests = [] }, Process = fixture.Source.Process with { Actors = [fixture.Source.Process.Actors[0] with { Triangle = triangle }] } };
        bool Completed(Ff7SessionSave actual) => Ff7AutoPlayChoiceWorkflow.HasCompletedVinesChoiceNavigation(actual, fixture.State, choice);
        Check(Completed(source), "A real returned owned choice with its native landing did not complete navigation.");
        Check(!Completed(source with { Process = source.Process with { Actors = [source.Process.Actors[0] with { Triangle = triangle + 1 }] } }), "A wrong player landing completed navigation.");
        fixture.State.Active = returned with { ExecutionProvenance = returned.ExecutionProvenance! with { Owners = before.ExecutionProvenance!.Owners } };
        Check(!Completed(source), "An outstanding native child completed navigation.");
        fixture.State.Active = returned with { ExecutionProvenance = returned.ExecutionProvenance! with { RootDispatchTick = 21 } };
        Check(!Completed(source), "A different source lifetime completed an old choice.");
    }

    private static Fixture Create(int pc = 0x089D, int root = 4, int childScript = 3, int text = 25)
    {
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, root, 2, Ff7AutoPlayTargetKind.LinePush, null, 0, "before", Accepted: true, NavigationKey: "nav");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, 10, 20) with { Owners = [new(1, 6, childScript, pc, root, 1, 10, 20, false)] } };
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 20).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[1] = entities[1] with { ProgramCounter = pc, Priority = 6 };
        entities[1].SavedProgramCounters[7] = 3;
        FieldHostRequest request = new(8, 1, 6, pc, new FieldDialogueCommand(0, text, 0, 1));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 30, true, new byte[1280], new byte[256], entities, PendingRequests: [request]);
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        variables.WriteByte(FieldBank.Persistent1Byte, 9, 2);
        variables.WriteWord(FieldBank.MapWord, 0, 184);
        vm = vm with { PersistentVariables = variables.CopyPersistent(), MapVariables = variables.CopyMap() };
        Ff7SessionSave source = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0, PlayerControlled = true, PartyCharacter = 2 }] } };
        return new(source, new() { Active = active, DestinationField = 629 }, new(8, 0, text, 0, 1, 1));
    }

    private static int? Preference(Fixture fixture) => Ff7AutoPlayChoiceWorkflow.ReadVinesChoicePreference(fixture.Source, fixture.State, fixture.Dialogue);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
