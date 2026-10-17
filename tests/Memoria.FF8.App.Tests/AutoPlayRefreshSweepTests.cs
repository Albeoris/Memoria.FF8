using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayRefreshSweepTests
{
    [Fact]
    public void EachStaleVisitIsInspectedBeforeTheKnownStoryFrontierWithoutCompletingIt()
    {
        AutoPlayPlanner planner = CreateCycle();
        HashSet<string> inspected = ["room"];
        string before = JsonSerializer.Serialize(planner.State);
        bool NeedsRefresh(AutoPlayVisit visit) => !inspected.Contains(visit.Key);

        AutoPlayPlanStep hall = planner.Plan("room", NeedsRefresh)!;
        Assert.True(hall.IsTravel);
        Assert.Equal("hall", hall.Route!.DestinationVisitKey);
        inspected.Add("hall");
        AutoPlayPlanStep remote = planner.Plan("hall", NeedsRefresh)!;
        Assert.True(remote.IsTravel);
        Assert.Equal("remote", remote.Route!.DestinationVisitKey);
        inspected.Add("remote");

        // A refresh observation changes scheduling only, even if entry changes the guard again.
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
        AutoPlayPlanStep returnToFrontier = planner.Plan("remote", NeedsRefresh)!;
        Assert.True(returnToFrontier.IsTravel);
        Assert.Equal("room", returnToFrontier.Route!.DestinationVisitKey);
        AutoPlayPlanStep frontier = planner.Plan("room", NeedsRefresh)!;
        Assert.False(frontier.IsTravel);
        Assert.Equal("story", frontier.Event.Id);
        Assert.Equal(1, planner.ReachableFrontierCount("room", needsRefresh: NeedsRefresh));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
        AutoPlayEventCoverage story = planner.State.Visits.Single(visit => visit.Visit.Key == "room").Events.Single(item => item.Event.Id == "story");
        Assert.False(story.Completed);
        Assert.Empty(story.Conditions!.ConsumedEntries);
        Assert.Empty(story.Conditions.ConsumedExits);
    }

    [Fact]
    public void RepeatedGuardChangesDuringTheSameSweepDoNotRestartItsInspections()
    {
        AutoPlayPlanner planner = CreateCycle();
        HashSet<string> inspected = ["room", "hall", "remote"];
        int guardGeneration = 1;
        bool NeedsRefresh(AutoPlayVisit visit) => guardGeneration > 0 && !inspected.Contains(visit.Key);
        string before = JsonSerializer.Serialize(planner.State);

        for (int changedEntry = 0; changedEntry < 3; changedEntry++)
        {
            guardGeneration++;
            Assert.Equal("story", planner.Plan("room", NeedsRefresh)!.Event.Id);
            Assert.Equal(1, planner.ReachableFrontierCount("room", needsRefresh: NeedsRefresh));
            Assert.False(inspected.Add("hall"));
        }

        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Fact]
    public void GenuineFrontierCompletionCanStartAnotherSweepWithoutReopeningConsumedEvents()
    {
        AutoPlayPlanner planner = CreateCycle();
        HashSet<string> inspected = ["room", "hall", "remote"];
        bool NeedsRefresh(AutoPlayVisit visit) => !inspected.Contains(visit.Key);
        Assert.Equal("story", planner.Plan("room", NeedsRefresh)!.Event.Id);

        // Model an actual event's control return, rather than crediting a refresh as progress.
        planner.BeginEvent("room", "story");
        planner.Completed("room", "story", exitSignature: "after-story");
        inspected.Clear();
        inspected.Add("room");
        string afterCompletion = JsonSerializer.Serialize(planner.State);

        Assert.Equal("hall", planner.Plan("room", NeedsRefresh)!.Route!.DestinationVisitKey);
        Assert.Equal(2, planner.ReachableFrontierCount("room", needsRefresh: NeedsRefresh));
        inspected.Add("hall");
        Assert.Equal("remote", planner.Plan("hall", NeedsRefresh)!.Route!.DestinationVisitKey);
        inspected.Add("remote");
        Assert.Null(planner.Plan("room", NeedsRefresh));
        Assert.Equal(0, planner.ReachableFrontierCount("room", needsRefresh: NeedsRefresh));
        Assert.Equal(afterCompletion, JsonSerializer.Serialize(planner.State));
        AutoPlayEventCoverage story = planner.State.Visits.Single(visit => visit.Visit.Key == "room").Events.Single(item => item.Event.Id == "story");
        Assert.Equal(new[] { "before-story" }, story.Conditions!.ConsumedEntries);
        Assert.Equal(new[] { "after-story" }, story.Conditions.ConsumedExits);
    }

    [Fact]
    public void ARefreshGoalSurvivesPhaseRegionAndDistanceChangesBetweenTravelHops()
    {
        bool preferCity = false;
        AutoPlayPlanner planner = new(phaseOrder: (visit, _) => preferCity && visit.Region == "garden" ? 10 : 0);
        planner.Observe(new("room", "garden"), [new("to-hall", AutoPlayEventKind.Transition)]);
        planner.Observe(new("hall", "city"), [new("to-middle", AutoPlayEventKind.Transition), new("to-other", AutoPlayEventKind.Transition)]);
        planner.Observe(new("middle", "city"), [new("to-goal", AutoPlayEventKind.Transition)]);
        planner.Observe(new("goal", "garden"), []);
        planner.Observe(new("other", "city"), []);
        planner.Completed("room", "to-hall", "hall", observedStoryChanged: false, selections: []);
        planner.Completed("hall", "to-middle", "middle", observedStoryChanged: false, selections: []);
        planner.Completed("hall", "to-other", "other", observedStoryChanged: false, selections: []);
        planner.Completed("middle", "to-goal", "goal", observedStoryChanged: false, selections: [new("native-choice", 2)]);
        bool NeedsRefresh(AutoPlayVisit visit) => visit.Key is "goal" or "other";
        string before = JsonSerializer.Serialize(planner.State);

        AutoPlayPlanStep first = planner.Plan("room", NeedsRefresh)!;
        Assert.Equal("goal", first.RefreshVisitKey);
        Assert.Equal("hall", first.Route!.DestinationVisitKey);
        preferCity = true;
        Assert.Equal("other", planner.Plan("hall", NeedsRefresh)!.RefreshVisitKey);
        AutoPlayPlanStep second = planner.Plan("hall", NeedsRefresh, first.RefreshVisitKey)!;
        Assert.Equal("goal", second.RefreshVisitKey);
        Assert.Equal("middle", second.Route!.DestinationVisitKey);
        AutoPlayPlanStep last = planner.Plan("middle", NeedsRefresh, second.RefreshVisitKey)!;
        Assert.Equal("goal", last.RefreshVisitKey);
        Assert.Equal("goal", last.Route!.DestinationVisitKey);
        Assert.Equal(new[] { new AutoPlayChoiceSelection("native-choice", 2) }, last.Route.Selections);
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("unreachable")]
    [InlineData("room")]
    public void AnUnknownUnreachableOrCurrentGoalFallsBackToTheSweep(string goal)
    {
        AutoPlayPlanner planner = CreateCycle();
        planner.Observe(new("unreachable", "garden"), []);

        AutoPlayPlanStep step = planner.Plan("room", visit => visit.Key == "hall", goal)!;

        Assert.Equal("hall", step.RefreshVisitKey);
        Assert.Equal("hall", step.Route!.DestinationVisitKey);
    }

    [Fact]
    public void ATemporarilyMatchingGuardAtAnIntermediateVisitDoesNotCancelTheGoal()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("first", "garden"), [new("to-second", AutoPlayEventKind.Transition)]);
        planner.Observe(new("second", "garden"), [new("back", AutoPlayEventKind.Transition), new("to-goal", AutoPlayEventKind.Transition)]);
        planner.Observe(new("goal", "garden"), []);
        planner.Completed("first", "to-second", "second", observedStoryChanged: false, selections: []);
        planner.Completed("second", "back", "first", observedStoryChanged: false, selections: []);
        planner.Completed("second", "to-goal", "goal", observedStoryChanged: false, selections: []);
        bool intermediateEntryChangedGuard = false;
        bool NeedsRefresh(AutoPlayVisit visit) => intermediateEntryChangedGuard ? visit.Key == "first" : visit.Key == "goal";

        AutoPlayPlanStep first = planner.Plan("first", NeedsRefresh)!;
        Assert.Equal("goal", first.RefreshVisitKey);
        Assert.Equal("second", first.Route!.DestinationVisitKey);
        intermediateEntryChangedGuard = true;
        Assert.Equal("first", planner.Plan("second", NeedsRefresh)!.RefreshVisitKey);

        AutoPlayPlanStep continued = planner.Plan("second", NeedsRefresh, first.RefreshVisitKey)!;

        Assert.Equal("goal", continued.RefreshVisitKey);
        Assert.Equal("goal", continued.Route!.DestinationVisitKey);
        Assert.Equal("to-goal", continued.Event.Id);
        Assert.True(continued.IsTravel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnOrdinaryOrExplicitEarlyFrontierPreemptsTheSavedRefreshGoal(bool explicitEarly)
    {
        AutoPlayPlanner planner = CreateCycle();
        AutoPlayVisitCoverage room = planner.State.Visits.Single(visit => visit.Visit.Key == "room");
        planner.Observe(room.Visit, room.Events.Select(item => item.Event).Append(new("new-frontier", AutoPlayEventKind.Interaction, ChangesStory: explicitEarly)).ToArray());
        planner = new(planner.State, progressionOrder: (_, item) => explicitEarly && item.Id == "new-frontier" ? -1 : 0);

        AutoPlayPlanStep step = planner.Plan("room", _ => true, "remote")!;

        Assert.Equal("new-frontier", step.Event.Id);
        Assert.Null(step.RefreshVisitKey);
        Assert.False(step.IsTravel);
    }

    private static AutoPlayPlanner CreateCycle()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("room", "garden"), [new("to-hall", AutoPlayEventKind.Transition, ConditionSignature: "room-door"), new("story", AutoPlayEventKind.Interaction, ChangesStory: true, ConditionSignature: "before-story")]);
        planner.Observe(new("hall", "garden"), [new("to-remote", AutoPlayEventKind.Transition, ConditionSignature: "hall-door")]);
        planner.Observe(new("remote", "garden"), [new("to-room", AutoPlayEventKind.Transition, ConditionSignature: "remote-door")]);
        planner.Completed("room", "to-hall", "hall", observedStoryChanged: false, selections: []);
        planner.Completed("hall", "to-remote", "remote", observedStoryChanged: false, selections: []);
        planner.Completed("remote", "to-room", "room", observedStoryChanged: false, selections: []);
        return planner;
    }
}
