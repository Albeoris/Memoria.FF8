using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayRemoteRefreshTests
{
    [Fact]
    public void StaleRemoteVisitReturnsItsActualFirstTravelEdgeWithoutChangingCoverage()
    {
        AutoPlayPlanner planner = CreateChain();
        string before = JsonSerializer.Serialize(planner.State);
        AutoPlayPlanStep step = planner.Plan("room", visit => visit.Key == "remote")!;
        Assert.True(step.IsTravel);
        Assert.Equal("room", step.VisitKey);
        Assert.Equal("door", step.Event.Id);
        Assert.Equal("hall", step.Route!.DestinationVisitKey);
        Assert.Equal(new[] { new AutoPlayChoiceSelection("actual-choice", 2) }, step.Route.Selections);
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryLocalOrRemoteFrontierPrecedesARefresh(bool remote)
    {
        AutoPlayPlanner planner = CreateChain();
        string destination = remote ? "hall" : "room";
        AutoPlayVisitCoverage visit = planner.State.Visits.Single(item => item.Visit.Key == destination);
        planner.Observe(visit.Visit, visit.Events.Select(item => item.Event).Append(new("ordinary", AutoPlayEventKind.Interaction)).ToArray());
        AutoPlayPlanStep step = planner.Plan("room", item => item.Key == "remote")!;
        Assert.Equal(remote, step.IsTravel);
        Assert.Equal(remote ? "door" : "ordinary", step.Event.Id);
        planner.Completed(destination, "ordinary");
        Assert.Equal("hall", planner.Plan("room", item => item.Key == "remote")!.Route!.DestinationVisitKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefreshPrecedesAConservativeStoryOrExplicitLateEntry(bool explicitPriority)
    {
        AutoPlayPlanner planner = CreateChain();
        AutoPlayVisitCoverage room = planner.State.Visits.Single(item => item.Visit.Key == "room");
        planner.Observe(room.Visit, room.Events.Select(item => item.Event).Append(new("progression", AutoPlayEventKind.Transition, ChangesStory: !explicitPriority)).ToArray());
        planner = new(planner.State, progressionOrder: (_, item) => explicitPriority && item.Id == "progression" ? 1 : 0);
        Assert.True(planner.Plan("room", visit => visit.Key == "remote")!.IsTravel);
        Assert.Equal("progression", planner.Plan("room", _ => false)!.Event.Id);
        Assert.False(planner.Plan("room", _ => false)!.IsTravel);
    }

    [Fact]
    public void UnknownReceiptCanRequestOneActualRevisitThenBecomeFresh()
    {
        AutoPlayPlanner planner = CreateChain();
        HashSet<string> known = ["room", "hall"];
        Assert.True(planner.Plan("room", visit => !known.Contains(visit.Key))!.IsTravel);
        known.Add("remote");
        Assert.Null(planner.Plan("room", visit => !known.Contains(visit.Key)));
        Assert.Null(planner.Plan("room"));
    }

    [Fact]
    public void AChangedRemoteGuardRaisesCanonicalFrontierScoreWithoutCountingTheConsumedWardrobe()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("wardrobe", "garden"), [new("get-changed", AutoPlayEventKind.Interaction, ConditionSignature: "consumed"), new("door", AutoPlayEventKind.Transition)]);
        planner.Observe(new("corridor", "garden"), [new("exit", AutoPlayEventKind.Interaction)]);
        planner.Completed("wardrobe", "get-changed");
        planner.Completed("wardrobe", "door", "corridor", observedStoryChanged: false, selections: []);
        planner.Completed("corridor", "exit");
        string before = JsonSerializer.Serialize(planner.State);
        int unchangedBranch = planner.ReachableFrontierCount("wardrobe", "wardrobe", "get-changed", _ => false);
        int changedGuardBranch = planner.ReachableFrontierCount("wardrobe", "wardrobe", "get-changed", visit => visit.Key == "corridor");
        Assert.Equal(0, unchangedBranch);
        Assert.Equal(1, changedGuardBranch);
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
        Assert.Equal(0, planner.ReachableFrontierCount("wardrobe", "wardrobe", "get-changed"));
    }

    [Fact]
    public void RefreshScoringIgnoresCurrentExcludedAndUnreachableVisitsAndCountsEachReachableVisitOnce()
    {
        AutoPlayPlanner planner = CreateChain();
        planner.Observe(new("unreachable", "garden"), []);
        Assert.Equal(1, planner.ReachableFrontierCount("room", "hall", "onward", _ => true));
        Assert.Equal(0, planner.ReachableFrontierCount("room", "hall", "onward", visit => visit.Key is "room" or "hall" or "unreachable"));
        AutoPlayVisitCoverage remote = planner.State.Visits.Single(item => item.Visit.Key == "remote");
        planner.Observe(remote.Visit, [new("return", AutoPlayEventKind.Transition)]);
        planner.Completed("remote", "return", "hall", observedStoryChanged: false, selections: []);
        Assert.Equal(2, planner.ReachableFrontierCount("room", needsRefresh: _ => true));
        planner.Block("room", "door", "Native route is unavailable.");
        Assert.Equal(0, planner.ReachableFrontierCount("room", needsRefresh: _ => true));
    }

    [Fact]
    public void CallbackNeverRefreshesTheCurrentVisitOrAnUnknownOrUnreachableVisit()
    {
        AutoPlayPlanner planner = CreateChain();
        planner.Observe(new("unreachable", "garden"), []);
        HashSet<string> queried = [];
        Assert.Null(planner.Plan("room", visit =>
        {
            queried.Add(visit.Key);
            return visit.Key is "room" or "unreachable" or "unknown";
        }));
        Assert.Equal(new[] { "hall", "remote" }, queried.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("blocked")]
    [InlineData("unavailable")]
    [InlineData("changed-story")]
    [InlineData("unknown-story")]
    [InlineData("changed-condition")]
    public void RefreshCannotCrossAnIneligibleRecordedRoute(string reason)
    {
        AutoPlayPlanner planner = CreateChain();
        AutoPlayEventCoverage door = planner.State.Visits.Single(item => item.Visit.Key == "room").Events.Single();
        switch (reason)
        {
            case "blocked":
                planner.Block("room", "door", "Native prerequisite is unavailable.");
                break;
            case "unavailable":
                door.Available = false;
                break;
            case "changed-story":
                door.Routes[0] = door.Routes[0] with { ObservedStoryChanged = true };
                break;
            case "unknown-story":
                door.Event = door.Event with { ChangesStory = true };
                door.Routes[0] = door.Routes[0] with { ObservedStoryChanged = null };
                break;
            case "changed-condition":
                door.Routes[0] = door.Routes[0] with { ConditionSignature = "old" };
                break;
        }
        Assert.Null(planner.Plan("room", visit => visit.Key == "remote"));
        Assert.Equal(0, planner.ReachableFrontierCount("room", needsRefresh: _ => true));
    }

    [Fact]
    public void RefreshUsesPhaseThenDistanceThenStableVisitOrdering()
    {
        AutoPlayPlanner planner = CreateChain();
        AutoPlayVisitCoverage room = planner.State.Visits.Single(item => item.Visit.Key == "room");
        planner.Observe(room.Visit, room.Events.Select(item => item.Event).Append(new("city-door", AutoPlayEventKind.Transition)).Append(new("annex-door", AutoPlayEventKind.Transition)).Append(new("a-zeta-door", AutoPlayEventKind.Transition)).ToArray());
        planner.Observe(new("city", "city"), []);
        planner.Observe(new("annex", "garden"), []);
        planner.Observe(new("zeta", "garden"), []);
        planner.Completed("room", "city-door", "city", observedStoryChanged: false, selections: []);
        planner.Completed("room", "annex-door", "annex", observedStoryChanged: false, selections: []);
        planner.Completed("room", "a-zeta-door", "zeta", observedStoryChanged: false, selections: []);
        planner = new(planner.State, phaseOrder: (visit, _) => visit.Region == "garden" ? 0 : 10);
        Assert.Equal("annex-door", planner.Plan("room", visit => visit.Key is "remote" or "annex" or "city" or "zeta")!.Event.Id);
        Assert.Equal("door", planner.Plan("room", visit => visit.Key is "remote" or "city")!.Event.Id);
        Assert.Equal("city-door", planner.Plan("room", visit => visit.Key == "city")!.Event.Id);
    }

    private static AutoPlayPlanner CreateChain()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("room", "garden"), [new("door", AutoPlayEventKind.Transition)]);
        planner.Observe(new("hall", "garden"), [new("onward", AutoPlayEventKind.Transition)]);
        planner.Observe(new("remote", "garden"), []);
        planner.Completed("room", "door", "hall", observedStoryChanged: false, selections: [new("actual-choice", 2)]);
        planner.Completed("hall", "onward", "remote", observedStoryChanged: false, selections: []);
        return planner;
    }
}
