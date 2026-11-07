using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayGatewayReconciliationTests
{
    [Fact]
    public void ExactNativeGatewayContextCanBeCheckedWithoutReplayingItsTravel()
    {
        AutoPlayRunState state = CreateContext();
        GameState actual = new() { Scene = SceneKind.Field, FieldId = 20, PreviousFieldId = 10 };

        Assert.True(AutoPlayController.HasNativeGatewayDestinationContext(state, actual, out int gateway));
        Assert.Equal(2, gateway);
    }

    [Theory]
    [InlineData("previous-field")]
    [InlineData("world")]
    [InlineData("choice")]
    [InlineData("missing-choice")]
    [InlineData("condition")]
    [InlineData("receipt")]
    [InlineData("target")]
    [InlineData("event")]
    public void IncompleteOrContradictoryNativeGatewayContextCannotReconcileAStaleRoute(string defect)
    {
        AutoPlayRunState state = CreateContext();
        GameState actual = new() { Scene = SceneKind.Field, FieldId = 20, PreviousFieldId = 10 };
        switch (defect)
        {
            case "previous-field":
                actual = actual with { PreviousFieldId = 11 };
                break;
            case "world":
                actual = actual with { Scene = SceneKind.WorldMap };
                break;
            case "choice":
                state.ActiveSelections = [new("unexpected", 0)];
                break;
            case "missing-choice":
                state.ActiveTravelRoute = state.ActiveTravelRoute! with { Selections = [new("expected", 0)] };
                break;
            case "condition":
                state.ActiveConditionSignature = "changed";
                break;
            case "receipt":
                state.NavigationReceipts.Clear();
                break;
            case "target":
                state.ActiveTargetId = "10:gateway:3:0:0";
                break;
            case "event":
                state.ActiveEvent = "10:line:2";
                break;
        }

        Assert.False(AutoPlayController.HasNativeGatewayDestinationContext(state, actual, out _));
    }

    [Fact]
    public void CorrectingAnObservedGatewayRetiresOnlyItsExactOldRouteAndKeepsOtherEvidence()
    {
        AutoPlayPlanner planner = new();
        AutoPlayEvent door = new("10:gateway:2", AutoPlayEventKind.Transition, ConditionSignature: "entry", Variants: ["10:gateway:2:0:0", "10:gateway:2:0:1"]);
        planner.Observe(new("source", "region"), [door]);
        planner.Observe(new("old", "region"), []);
        planner.Observe(new("actual", "region"), []);
        planner.ObserveNavigation("source", "source-nav", [door]);
        planner.ObserveNavigation("old", "old-nav", []);
        planner.ObserveNavigation("actual", "actual-nav", []);
        planner.Completed("source", door.Id, "old", observedStoryChanged: false, selections: [], targetId: "10:gateway:2:0:0");
        planner.Completed("source", door.Id, "old", observedStoryChanged: false, selections: [], targetId: "10:gateway:2:0:1");
        AutoPlayEventCoverage coverage = planner.State.Visits.Single(item => item.Visit.Key == "source").Events.Single();
        AutoPlayEventRoute stale = coverage.Routes.Single(route => route.TargetId == "10:gateway:2:0:0");
        AutoPlayEventRoute other = coverage.Routes.Single(route => route.TargetId == "10:gateway:2:0:1");
        string consumed = JsonSerializer.Serialize(coverage.Conditions);
        planner.RecordNavigationRoute("source-nav", "old-nav", door.Id, stale);
        planner.RecordNavigationRoute("source-nav", "old-nav", door.Id, other);

        planner.RetireObservedGatewayDestination("source-nav", door.Id, stale, "actual-nav");

        Assert.Equal(other, Assert.Single(coverage.Routes));
        Assert.True(coverage.Completed);
        Assert.Equal(consumed, JsonSerializer.Serialize(coverage.Conditions));
        Assert.Equal(other, Assert.Single(planner.State.NavigationRoutes).Route);
        Assert.Equal("actual-nav", Assert.Single(planner.State.NavigationRetirements).ActualDestinationKey);
        planner.Completed("source", door.Id, "actual", observedStoryChanged: false, selections: [], targetId: stale.TargetId);
        AutoPlayEventRoute observed = coverage.Routes.Single(route => route.DestinationVisitKey == "actual");
        planner.RecordNavigationRoute("source-nav", "actual-nav", door.Id, observed);
        Assert.Equal(2, coverage.Routes.Count);
    }

    private static AutoPlayRunState CreateContext()
    {
        AutoPlayNavigationReceipt receipt = new(1, 10, "program", "mesh", [], [1]);
        string navigation = receipt.Key("10:5");
        AutoPlayRunState state = new()
        {
            ActiveField = 10,
            ActiveVisit = "10:5",
            ActiveNavigationKey = navigation,
            ActiveEvent = "10:gateway:2",
            ActiveTargetId = "10:gateway:2:0:0",
            ActiveConditionSignature = "entry",
            ActiveSelections = [],
            ActiveTravelRoute = new("old", "entry", false, [], "10:gateway:2:0:0")
        };
        state.NavigationReceipts[navigation] = receipt;
        return state;
    }
}
