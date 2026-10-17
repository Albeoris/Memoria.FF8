using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayChangedRouteTests
{
    [Theory]
    [InlineData("changed-prompt", 0, 3)]
    [InlineData("expected-prompt", 0, 1)]
    public void AChangedActualChoiceRetiresOnlyTheExactRouteWithoutCompletionCredit(string actualKey, int first, int last)
    {
        AutoPlayPlanner planner = CreatePlanner();
        AutoPlayEventCoverage entry = Entry(planner);
        AutoPlayEventRoute stale = entry.Routes[0];
        AutoPlayEventRoute retained = stale with { DestinationVisitKey = "other", Selections = [new("parent", 1), new("other-prompt", 0)] };
        entry.Routes.Add(retained);
        string consumed = JsonSerializer.Serialize(entry.Conditions);

        planner.RetireTravelRoute("source", "entry", stale with { Selections = stale.Selections!.ToArray() }, actualKey, first, last, [new("parent", 1)]);

        Assert.Equal(retained, Assert.Single(entry.Routes));
        AutoPlayRetiredRoute retired = Assert.Single(entry.RetiredRoutes);
        Assert.Equal(JsonSerializer.Serialize(stale), JsonSerializer.Serialize(retired.Route));
        Assert.Equal(actualKey, retired.ActualChoiceKey);
        Assert.Equal(first, retired.FirstChoice);
        Assert.Equal(last, retired.LastChoice);
        Assert.Equal(new[] { new AutoPlayChoiceSelection("parent", 1) }, retired.ExecutedPrefix);
        Assert.True(entry.RouteNeedsRediscovery);
        Assert.True(entry.Completed);
        Assert.Equal(consumed, JsonSerializer.Serialize(entry.Conditions));
        Assert.False(entry.Unavailable);
        Assert.False(entry.PolicyDeferred);
        Assert.Empty(entry.UnavailableAttempts);
        Assert.Empty(entry.ParticipantPolicyDeferrals);
        Assert.Empty(planner.State.Choices);
        Assert.Equal("entry", planner.Plan("source")!.Event.Id);
        Assert.False(planner.Plan("source")!.IsTravel);
    }

    [Fact]
    public void AnUnexpectedChoiceAfterTheEntireRecordedPrefixCanRetireItsRoute()
    {
        AutoPlayPlanner planner = CreatePlanner();
        AutoPlayEventRoute route = Entry(planner).Routes[0];

        planner.RetireTravelRoute("source", "entry", route, "extra-prompt", 1, 2, route.Selections);

        Assert.Empty(Entry(planner).Routes);
        Assert.Equal(route.Selections, Assert.Single(Entry(planner).RetiredRoutes).ExecutedPrefix);
    }

    [Theory]
    [InlineData("matching-choice")]
    [InlineData("missing-prefix")]
    [InlineData("wrong-prefix")]
    [InlineData("long-prefix")]
    [InlineData("unknown-route")]
    [InlineData("ineligible-route")]
    [InlineData("unknown-selections")]
    public void AnUnprovedOrStillMatchingRouteCannotBeRetired(string reason)
    {
        AutoPlayPlanner planner = CreatePlanner();
        AutoPlayEventRoute route = Entry(planner).Routes[0];
        IReadOnlyList<AutoPlayChoiceSelection>? prefix = [new("parent", 1)];
        string actualKey = "changed-prompt";
        switch (reason)
        {
            case "matching-choice":
                actualKey = "expected-prompt";
                break;
            case "missing-prefix":
                prefix = null;
                break;
            case "wrong-prefix":
                prefix = [new("parent", 0)];
                break;
            case "long-prefix":
                prefix = route.Selections!.Append(new("extra", 0)).ToArray();
                break;
            case "unknown-route":
                route = route with { DestinationVisitKey = "unknown" };
                break;
            case "ineligible-route":
                Entry(planner).Available = false;
                break;
            case "unknown-selections":
                route = route with { Selections = null };
                break;
        }
        string before = JsonSerializer.Serialize(planner.State);

        Assert.Throws<InvalidOperationException>(() => planner.RetireTravelRoute("source", "entry", route, actualKey, 0, 3, prefix));

        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Fact]
    public void ColdRestoreDoesNotResurrectTheRetiredRouteAndRealRediscoveryAdmitsTheNewOne()
    {
        AutoPlayPlanner planner = CreatePlanner();
        planner.RetireTravelRoute("source", "entry", Entry(planner).Routes[0], "changed-prompt", 0, 3, [new("parent", 1)]);
        planner = new(JsonSerializer.Deserialize<AutoPlayPlannerState>(JsonSerializer.Serialize(planner.State))!);
        AutoPlayEventCoverage entry = Entry(planner);
        Assert.Empty(entry.Routes);
        Assert.Single(entry.RetiredRoutes);
        Assert.True(entry.RouteNeedsRediscovery);
        Assert.Equal("entry", planner.Plan("source")!.Event.Id);

        AutoPlayChoiceSelection[] actual = [new("parent", 1), new("changed-prompt", 3)];
        planner.Completed("source", "entry", "destination", observedStoryChanged: false, selections: actual, targetId: "target");

        Assert.False(entry.RouteNeedsRediscovery);
        Assert.Equal(actual, Assert.Single(entry.Routes).Selections);
        Assert.Single(entry.RetiredRoutes);
        AutoPlayPlanStep travel = planner.Plan("source", visit => visit.Key == "destination")!;
        Assert.True(travel.IsTravel);
        Assert.Equal(actual, travel.Route!.Selections);
    }

    [Fact]
    public void ARealControlReturnToTheSameVisitAlsoClearsRediscovery()
    {
        AutoPlayPlanner planner = CreatePlanner();
        planner.RetireTravelRoute("source", "entry", Entry(planner).Routes[0], "changed-prompt", 0, 3, [new("parent", 1)]);

        planner.Completed("source", "entry", observedStoryChanged: false, selections: []);

        Assert.False(Entry(planner).RouteNeedsRediscovery);
        Assert.Empty(Entry(planner).Routes);
        Assert.Null(planner.Plan("source"));
        planner = new(JsonSerializer.Deserialize<AutoPlayPlannerState>(JsonSerializer.Serialize(planner.State))!);
        Assert.Empty(Entry(planner).Routes);
    }

    private static AutoPlayEventCoverage Entry(AutoPlayPlanner planner)
    {
        return planner.State.Visits.Single(visit => visit.Visit.Key == "source").Events.Single();
    }

    private static AutoPlayPlanner CreatePlanner()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "garden"), [new("entry", AutoPlayEventKind.Interaction, ConditionSignature: "source-condition", Variants: ["target"])]);
        planner.Observe(new("destination", "garden"), []);
        planner.Observe(new("other", "garden"), []);
        planner.Completed("source", "entry", "destination", observedStoryChanged: false, selections: [new("parent", 1), new("expected-prompt", 2)], targetId: "target");
        return planner;
    }
}
