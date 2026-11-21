using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeChoiceSourceAccountingTests
{
    internal static void Run()
    {
        CheckSourceLifetime();
        CheckActualChoiceOwner();
        CheckChoiceFreeAdvice();
        Console.WriteLine("Choice source lifetimes, actual ambient ASK work and choice-free transient route accounting passed.");
    }

    private static Ff7AutoPlayActiveEvent Root()
    {
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, NavigationKey: "nav");
        return active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 10, 20) };
    }

    private static void CheckSourceLifetime()
    {
        Ff7AutoPlayActiveEvent active = Root();
        Ff7AutoPlayChoiceState choice = new("choice", active) { NativeSourceOwned = true };
        string before = JsonSerializer.Serialize(new { active, choice });
        Check(Ff7AutoPlayChoiceWorkflow.MatchesChoiceSource(choice, active), "A choice's actual accepted source lifetime was excluded.");
        Check(!Ff7AutoPlayChoiceWorkflow.MatchesChoiceSource(choice with { NativeSourceOwned = null }, active), "Legacy unknown choice ownership inherited the current source.");
        Check(!Ff7AutoPlayChoiceWorkflow.MatchesChoiceSource(choice with { BeforeActive = active with { Accepted = false } }, active), "An unrelated unaccepted planner owner inherited actual choice selections.");
        Check(!Ff7AutoPlayChoiceWorkflow.MatchesChoiceSource(choice, active with { EventId = "other" }), "Another source inherited actual choice selections.");
        Check(!Ff7AutoPlayChoiceWorkflow.MatchesChoiceSource(choice, active with { ExecutionProvenance = active.ExecutionProvenance! with { RootDispatchTick = 21 } }), "A new same-entry source lifetime inherited the previous choice.");
        Check(!Ff7AutoPlayChoiceWorkflow.MatchesChoiceSource(choice with { BeforeActive = active with { ExecutionProvenance = active.ExecutionProvenance! with { RootDispatchTick = -1 } } }, active), "Missing old lifetime evidence was inferred from matching source IDs.");
        Check(before == JsonSerializer.Serialize(new { active, choice }), "Source selection qualification changed coverage or execution metadata.");
    }

    private static void CheckActualChoiceOwner()
    {
        FieldEntitySnapshot Entity(int pc, int priority) => new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldEntitySnapshot[] entities = [Entity(0, 7), Entity(40, 6), Entity(60, 6)];
        entities[1].SavedProgramCounters[7] = 3;
        entities[2].SyncStates[6] = FieldSyncState.Waiting;
        entities[2].SyncOwners[6] = 1;
        FieldHostRequest request = new(8, 1, 6, 40, new FieldDialogueCommand(0, 4, 1, 2));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 10, true, new byte[1280], new byte[256], entities, PendingRequests: [request]);
        Ff7SessionSave source = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() };
        Ff7Dialogue dialogue = new(8, 0, 4, 1, 2, 1);
        string before = JsonSerializer.Serialize(source);
        Ff7AutoPlayChoiceNativeOwner owner = Ff7AutoPlayChoiceWorkflow.ReadNativeChoiceOwner(source, dialogue) ?? throw new InvalidOperationException("The actual pending ambient-worker ASK was not identified.");
        Check(Ff7AutoPlayNativeReadiness.HasLiveChoiceOwner(1, owner, vm), "A current native ASK worker was treated as finished.");
        Check(new Ff7AutoPlayNativeReadiness().ShouldWaitForChoiceOwner(1, owner, vm, source.Process!, new byte[64]), "An unrelated LINE return could complete a choice whose native worker and synchronous child remain live.");
        Check(!Ff7AutoPlayNativeReadiness.HasLiveChoiceOwner(2, owner, vm), "A stale generation retained choice-owner authority.");
        FieldEntitySnapshot[] returned = entities.ToArray();
        returned[1] = returned[1] with { Priority = 7 };
        Check(!Ff7AutoPlayNativeReadiness.HasLiveChoiceOwner(1, owner, vm with { Entities = returned }), "A returned choice worker remained live indefinitely.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadNativeChoiceOwner(source, dialogue with { Text = 5 }) is null, "A mismatched prompt reused the same request ID.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadNativeChoiceOwner(source, dialogue with { FirstChoice = 0 }) is null, "A changed choice domain reused the original owner.");
        Check(before == JsonSerializer.Serialize(source), "Choice owner inspection changed actual native state.");
    }

    private static void CheckChoiceFreeAdvice()
    {
        AutoPlayEventRoute historical = new("destination", "condition", false, [new("unrelated-choice", 1)], "line");
        Ff7AutoPlayActiveEvent active = Root() with { Returned = true, TravelRoute = historical, TravelSelections = [] };
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [new("line", 0, 1, Ff7AutoPlayTargetKind.LineTouch, "condition", false, default)], true, true) { NavigationKey = "nav" };
        string history = JsonSerializer.Serialize(historical);
        Ff7AutoPlayActiveEvent refreshed = Ff7AutoPlayPolicy.RefreshCompletedChoiceFreeTravel(active, observation, true);
        Check(refreshed.TravelRoute?.Selections is { Count: 0 }, "Actual accepted/returned choice-free execution retained a contaminated transient recipe.");
        AutoPlayTravelChoice.RequireComplete(refreshed.TravelRoute!, refreshed.TravelSelections);
        Check(history == JsonSerializer.Serialize(historical), "Refreshing selected advice rewrote historical route or choice outcomes.");
        Check(ReferenceEquals(Ff7AutoPlayPolicy.RefreshCompletedChoiceFreeTravel(active, observation, false), active), "Unknown source closure discarded required choice selections.");
        Check(ReferenceEquals(Ff7AutoPlayPolicy.RefreshCompletedChoiceFreeTravel(active with { Returned = false }, observation, true).TravelRoute, historical), "Unreturned source execution discarded required choices.");
        Check(ReferenceEquals(Ff7AutoPlayPolicy.RefreshCompletedChoiceFreeTravel(active, observation with { Generation = 2 }, true), active), "Destination-program inspection replaced a source's recipe.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
