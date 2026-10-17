using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayProgressionOrderTests
{
    [Theory]
    [InlineData("137:142", "137:line:15", 1)]
    [InlineData("137:141", "137:line:15", 0)]
    [InlineData("137:143", "137:line:15", 0)]
    [InlineData("137:142", "137:line:14", 0)]
    [InlineData("240:142", "240:line:12", 0)]
    public void ExplicitEntryPriorityAppliesOnlyToItsObservedStoryAndEvent(string visit, string eventId, int expected)
    {
        Assert.Equal(expected, AutoPlayTraversalPolicy.ProgressionEntryOrder(new(visit, "bg"), new(eventId, AutoPlayEventKind.Transition)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EntryWaitsForReachableConservativeStoryFrontiersBeforeLocalOrRemoteSelection(bool remoteEntry)
    {
        AutoPlayPlanner planner = CreateObservedGarden();
        string current = remoteEntry ? "172:142" : "137:142";
        AutoPlayPlanStep first = planner.Plan(current)!;
        Assert.True(first.IsTravel);
        Assert.Equal(remoteEntry ? "172:line:13" : "137:line:14", first.Event.Id);
        Assert.NotEqual("137:line:15", first.Event.Id);
        planner.Completed("179:142", "179:talk:10");
        AutoPlayPlanStep second = planner.Plan(current)!;
        Assert.True(second.IsTravel);
        Assert.Equal(remoteEntry ? "172:line:13" : "137:line:14", second.Event.Id);
        planner.Completed("240:142", "240:line:12");
        AutoPlayPlanStep entry = planner.Plan(current)!;
        Assert.Equal(remoteEntry ? "172:return" : "137:line:15", entry.Event.Id);
        Assert.Equal(remoteEntry, entry.IsTravel);
        Assert.Equal("137:142", remoteEntry ? entry.Route!.DestinationVisitKey : entry.VisitKey);
        AutoPlayEventCoverage coverage = planner.State.Visits.Single(visit => visit.Visit.Key == "137:142").Events.Single(item => item.Event.Id == "137:line:15");
        Assert.False(coverage.Event.ChangesStory);
        Assert.False(coverage.Completed);
        Assert.True(coverage.Available);
        Assert.Null(coverage.BlockedReason);
        Assert.Empty(coverage.Routes);
    }

    [Fact]
    public void AbsentPriorityCallbackPreservesTheOriginalStoryOrdering()
    {
        AutoPlayPlanner configured = CreateObservedGarden();
        AutoPlayPlanner neutral = new(configured.State);
        AutoPlayPlanStep next = neutral.Plan("137:142")!;
        Assert.False(next.IsTravel);
        Assert.Equal("137:line:15", next.Event.Id);
    }

    [Fact]
    public void AnUnreachableFrontierDoesNotPreventTheAvailableEntry()
    {
        AutoPlayPlanner planner = CreateObservedGarden();
        planner.Completed("179:142", "179:talk:10");
        planner.Completed("240:142", "240:line:12");
        Assert.Equal("137:line:15", planner.Plan("137:142")!.Event.Id);
        Assert.False(planner.State.Visits.Single(visit => visit.Visit.Key == "135:142").Events.Single().Completed);
    }

    [Fact]
    public void PriorityDoesNotAdmitAnUnavailableEntry()
    {
        AutoPlayPlanner planner = CreateObservedGarden();
        planner.Completed("179:142", "179:talk:10");
        planner.Completed("240:142", "240:line:12");
        planner.Observe(new("137:142", "bg"), [new("137:line:14", AutoPlayEventKind.Transition, "172:142")]);
        Assert.Null(planner.Plan("137:142"));
        AutoPlayEventCoverage entry = planner.State.Visits.Single(visit => visit.Visit.Key == "137:142").Events.Single(item => item.Event.Id == "137:line:15");
        Assert.False(entry.Available);
        Assert.False(entry.Completed);
    }

    private static AutoPlayPlanner CreateObservedGarden()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("137:142", "bg"), [new("137:line:15", AutoPlayEventKind.Transition, ConditionSignature: "entry137"), new("137:line:14", AutoPlayEventKind.Transition)]);
        planner.Observe(new("172:142", "bg"), [new("172:line:13", AutoPlayEventKind.Transition), new("172:return", AutoPlayEventKind.Transition)]);
        planner.Observe(new("165:142", "bg"), [new("165:gateway:3", AutoPlayEventKind.Transition)]);
        planner.Observe(new("168:142", "bg"), [new("168:gateway:0", AutoPlayEventKind.Transition), new("168:line:24", AutoPlayEventKind.Transition)]);
        planner.Observe(new("174:142", "bg"), [new("174:line:24", AutoPlayEventKind.Transition)]);
        planner.Observe(new("228:142", "bg"), [new("228:line:21", AutoPlayEventKind.Transition)]);
        planner.Observe(new("237:142", "bg"), [new("237:gateway:1", AutoPlayEventKind.Transition)]);
        planner.Observe(new("227:142", "bg"), [new("227:gateway:0", AutoPlayEventKind.Transition)]);
        planner.Observe(new("240:142", "bg"), [new("240:line:12", AutoPlayEventKind.Interaction, ChangesStory: true)]);
        planner.Observe(new("179:142", "bg"), [new("179:talk:10", AutoPlayEventKind.Interaction, ChangesStory: true)]);
        planner.Observe(new("135:142", "bg"), [new("135:line:9", AutoPlayEventKind.Interaction)]);
        Record("137:142", "137:line:14", "172:142");
        Record("172:142", "172:line:13", "165:142");
        Record("172:142", "172:return", "137:142");
        Record("165:142", "165:gateway:3", "168:142");
        Record("168:142", "168:gateway:0", "174:142");
        Record("174:142", "174:line:24", "228:142");
        Record("228:142", "228:line:21", "237:142");
        Record("237:142", "237:gateway:1", "240:142");
        Record("168:142", "168:line:24", "227:142");
        Record("227:142", "227:gateway:0", "179:142");
        return new(JsonSerializer.Deserialize<AutoPlayPlannerState>(JsonSerializer.Serialize(planner.State))!, progressionOrder: AutoPlayTraversalPolicy.ProgressionEntryOrder);

        void Record(string source, string eventId, string destination)
        {
            planner.Completed(source, eventId, destination, observedStoryChanged: false, selections: []);
        }
    }
}
