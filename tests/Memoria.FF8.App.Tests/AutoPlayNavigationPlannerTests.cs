using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNavigationPlannerTests
{
    [Fact]
    public void ObservingTheWestComponentDoesNotEraseTheMainComponentsAuthenticatedExit()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayEvent mainExit = Event(planner, "165:142", "165:gateway:5");
        AutoPlayEvent westExit = Event(planner, "165:142", "165:gateway:3");
        planner.Observe(new("165:142", "garden"), [westExit]);
        planner.ObserveNavigation("165:142", "west", [westExit]);
        string before = JsonSerializer.Serialize(planner.State);

        AutoPlayPlanStep main = planner.PlanNavigation("main")!;

        Assert.False(planner.State.Visits.Single(visit => visit.Visit.Key == "165:142").Events.Single(item => item.Event.Id == mainExit.Id).Available);
        Assert.Equal("165:142", main.VisitKey);
        Assert.Equal(mainExit.Id, main.Event.Id);
        Assert.True(main.IsTravel);
        Assert.Equal("172:142", main.Route!.DestinationVisitKey);
        Assert.Null(planner.PlanNavigation("west"));
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("west"));
        Assert.Equal(1, planner.ReachableNavigationFrontierCount("main"));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Fact]
    public void AnotherActualIngressCanReachTheMainComponentWithoutCrossingDisconnectedGeometry()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        // This is a generic graph test; the connector is not a claim about an original field route.
        planner.Observe(new("168:142", "garden"), [new("connector", AutoPlayEventKind.Transition, ConditionSignature: "connector")]);
        planner.ObserveNavigation("168:142", "outside", [Event(planner, "168:142", "connector")]);
        planner.Observe(new("connector:142", "garden"), [new("other-ingress", AutoPlayEventKind.Transition, ConditionSignature: "ingress")]);
        planner.ObserveNavigation("connector:142", "connector", [Event(planner, "connector:142", "other-ingress")]);
        Connect(planner, "168:142", "connector", "connector:142", "outside", "connector");
        Connect(planner, "connector:142", "other-ingress", "165:142", "connector", "main");

        AutoPlayPlanStep first = planner.PlanNavigation("west")!;

        Assert.Equal("165:gateway:3", first.Event.Id);
        Assert.Equal("168:142", first.Route!.DestinationVisitKey);
        Assert.Equal("connector", planner.PlanNavigation("outside")!.Event.Id);
        Assert.Equal("other-ingress", planner.PlanNavigation("connector")!.Event.Id);
        Assert.Equal("165:gateway:5", planner.PlanNavigation("main")!.Event.Id);
        Assert.Equal(1, planner.ReachableNavigationFrontierCount("west"));
    }

    [Fact]
    public void EventCompletionAndChoiceCoverageRemainSharedAcrossNavigationObservations()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayEvent mainExit = Event(planner, "165:142", "165:gateway:5");
        AutoPlayEvent westExit = Event(planner, "165:142", "165:gateway:3");
        AutoPlayEvent talk = new("shared-talk", AutoPlayEventKind.Interaction, ConditionSignature: "same-input");
        planner.Observe(new("165:142", "garden"), [mainExit, westExit, talk]);
        planner.ObserveNavigation("165:142", "main", [mainExit, talk]);
        planner.ObserveNavigation("165:142", "west", [westExit, talk]);
        planner.ObserveChoice("one-native-prompt", 2);
        Assert.Equal("shared-talk", planner.PlanNavigation("west")!.Event.Id);

        planner.Completed("165:142", "shared-talk");
        planner.ObserveNavigation("165:142", "main", [mainExit, talk]);
        planner.ObserveNavigation("165:142", "west", [westExit, talk]);

        Assert.Null(planner.PlanNavigation("west"));
        Assert.Equal("165:gateway:5", planner.PlanNavigation("main")!.Event.Id);
        Assert.Single(planner.State.Choices);
        Assert.Single(planner.State.Visits.Single(visit => visit.Visit.Key == "165:142").Events, item => item.Event.Id == "shared-talk");
    }

    [Fact]
    public void RetiringTheAggregateRouteAlsoRemovesItsNavigationEdgeFromTraversal()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayEventCoverage entry = planner.State.Visits.Single(visit => visit.Visit.Key == "165:142").Events.Single(item => item.Event.Id == "165:gateway:5");
        planner.RetireTravelRoute("165:142", entry.Event.Id, Assert.Single(entry.Routes), "unexpected-prompt", 0, 1, []);
        string before = JsonSerializer.Serialize(planner.State);

        AutoPlayPlanStep rediscover = planner.PlanNavigation("main")!;

        Assert.False(rediscover.IsTravel);
        Assert.Null(rediscover.Route);
        Assert.Equal(entry.Event.Id, rediscover.Event.Id);
        Assert.Equal(1, planner.ReachableNavigationFrontierCount("main"));
        Assert.Null(planner.PlanNavigation("west"));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Fact]
    public void AnUnconfirmedLegacyRouteIsProbedWithoutInventingADestinationComponent()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        planner.State.NavigationRoutes.RemoveAll(edge => edge.SourceKey == "main");
        string before = JsonSerializer.Serialize(planner.State);

        AutoPlayPlanStep probe = planner.PlanNavigation("main")!;

        Assert.True(probe.IsTravel);
        Assert.Equal("165:142", probe.VisitKey);
        Assert.Equal("172:142", probe.Route!.DestinationVisitKey);
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("main"));
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("main", "165:142", "165:gateway:5"));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
        planner.Completed("172:142", "pending");
        planner.RecordNavigationRoute("main", "destination", "165:gateway:5", probe.Route);
        Assert.Null(planner.PlanNavigation("main"));
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("main"));
    }

    [Fact]
    public void ANewOrdinaryInteractionPrecedesAnEarlierSortedCompletedRouteProbe()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayEvent earlier = Event(planner, "165:142", "165:gateway:5") with { Kind = AutoPlayEventKind.Interaction };
        AutoPlayEvent ordinary = new("z-new-interaction", AutoPlayEventKind.Interaction, ConditionSignature: "new-native-input");
        planner.Observe(new("165:142", "garden"), [earlier, Event(planner, "165:142", "165:gateway:3"), ordinary]);
        planner.ObserveNavigation("165:142", "main", [earlier, ordinary]);
        planner.State.NavigationRoutes.RemoveAll(edge => edge.SourceKey == "main");
        string before = JsonSerializer.Serialize(planner.State);

        AutoPlayPlanStep selected = planner.PlanNavigation("main")!;

        Assert.Equal(ordinary.Id, selected.Event.Id);
        Assert.False(selected.IsTravel);
        Assert.Null(selected.Route);
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Fact]
    public void ALegacyRouteWithoutAnObservedDestinationAggregateDoesNotProduceAProbe()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        planner.State.NavigationRoutes.Clear();
        planner.State.NavigationVisits.RemoveAll(visit => visit.Key != "main");
        planner.State.Visits.RemoveAll(visit => visit.Visit.Key != "165:142");
        string before = JsonSerializer.Serialize(planner.State);

        Assert.Null(planner.PlanNavigation("main"));
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("main"));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Theory]
    [InlineData("blocked")]
    [InlineData("native-unavailable")]
    [InlineData("participant-deferred")]
    [InlineData("changed-condition")]
    [InlineData("changed-story")]
    public void NavigationNeverClearsSharedGuardsOrProbesAnIneligibleRoute(string reason)
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayEventCoverage entry = planner.State.Visits.Single(visit => visit.Visit.Key == "165:142").Events.Single(item => item.Event.Id == "165:gateway:5");
        switch (reason)
        {
            case "blocked":
                planner.Block("165:142", entry.Event.Id, "The actual native entrance is unavailable.");
                break;
            case "native-unavailable":
                entry.Unavailable = true;
                break;
            case "participant-deferred":
                entry.PolicyDeferred = true;
                break;
            case "changed-condition":
                entry.Event = entry.Event with { ConditionSignature = "new-input" };
                break;
            case "changed-story":
                entry.Routes[0] = entry.Routes[0] with { ObservedStoryChanged = true };
                break;
        }
        string before = JsonSerializer.Serialize(planner.State);

        Assert.Null(planner.PlanNavigation("main"));
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("main"));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Fact]
    public void AChangedResourceExcludesItsVertexButKeepsHistoricalMetadataForRediscovery()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        string before = JsonSerializer.Serialize(planner.State);
        bool Current(AutoPlayNavigationVisit visit) => visit.Key != "destination";

        AutoPlayPlanStep probe = planner.PlanNavigation("main", isCurrent: Current)!;

        Assert.True(probe.IsTravel);
        Assert.Equal("172:142", probe.Route!.DestinationVisitKey);
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("main", isCurrent: Current));
        Assert.Null(planner.PlanNavigation("main", isCurrent: visit => visit.Key != "main"));
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("main", isCurrent: visit => visit.Key != "main"));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Fact]
    public void RefreshCallbacksUseNavigationIdentityWhilePolicyAndReturnedActionsUseAggregateIdentity()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        planner.Completed("172:142", "pending");
        HashSet<string> policyVisits = [];
        planner = new(planner.State, phaseOrder: (visit, _) =>
        {
            policyVisits.Add(visit.Key);
            return 0;
        });

        AutoPlayPlanStep refresh = planner.PlanNavigation("main", visit => visit.Key == "destination")!;

        Assert.Equal("destination", refresh.RefreshVisitKey);
        Assert.Equal("165:142", refresh.VisitKey);
        Assert.Equal("172:142", refresh.Route!.DestinationVisitKey);
        Assert.Contains("172:142", policyVisits);
        Assert.All(policyVisits, key => Assert.Contains(key, new[] { "165:142", "168:142", "172:142" }));
        Assert.Equal(1, planner.ReachableNavigationFrontierCount("main", needsRefresh: visit => visit.Key == "destination"));
    }

    [Fact]
    public void AnActualWithinFieldRouteCanConnectTwoDifferentComponents()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayEvent westExit = Event(planner, "165:142", "165:gateway:3");
        AutoPlayEvent ladder = new("native-ladder", AutoPlayEventKind.Interaction, ConditionSignature: "ladder-input");
        planner.Observe(new("165:142", "garden"), [Event(planner, "165:142", "165:gateway:5"), westExit, ladder]);
        planner.ObserveNavigation("165:142", "west", [westExit, ladder]);
        planner.Completed("165:142", ladder.Id, "165:142", observedStoryChanged: false, selections: []);
        planner.RecordNavigationRoute("west", "main", ladder.Id, planner.State.Visits.Single(visit => visit.Visit.Key == "165:142").Events.Single(item => item.Event.Id == ladder.Id).Routes.Single());

        AutoPlayPlanStep step = planner.PlanNavigation("west")!;

        Assert.Equal(ladder.Id, step.Event.Id);
        Assert.True(step.IsTravel);
        Assert.Equal("165:142", step.Route!.DestinationVisitKey);
    }

    [Fact]
    public void ProbePriorityDoesNotMakeAnUnknownStoryChangingRouteTraversable()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayEventCoverage entry = planner.State.Visits.Single(visit => visit.Visit.Key == "165:142").Events.Single(item => item.Event.Id == "165:gateway:5");
        entry.Event = entry.Event with { ChangesStory = true };
        planner.ObserveNavigation("165:142", "main", [entry.Event]);
        planner.State.NavigationRoutes.RemoveAll(edge => edge.SourceKey == "main");
        planner.Observe(new("other:142", "garden"), [new("other-pending", AutoPlayEventKind.Interaction)]);
        planner.ObserveNavigation("other:142", "other", [Event(planner, "other:142", "other-pending")]);
        AutoPlayEventRoute unknown = new("other:142", entry.Event.ConditionSignature, null, []);
        entry.Routes.Add(unknown);
        planner.RecordNavigationRoute("main", "other", entry.Event.Id, unknown);

        AutoPlayPlanStep probe = planner.PlanNavigation("main")!;

        Assert.Equal("172:142", probe.Route!.DestinationVisitKey);
        Assert.True(probe.Event.ChangesStory);
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("main"));
    }

    [Fact]
    public void NavigationReceiptsSurviveColdMetadataRestoreWithoutDuplicatingCoverage()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        planner.Observe(new("165:142", "garden"), [Event(planner, "165:142", "165:gateway:3")]);
        string before = JsonSerializer.Serialize(planner.State);
        AutoPlayPlanner restored = new(JsonSerializer.Deserialize<AutoPlayPlannerState>(before)!);

        Assert.Equal("165:gateway:5", restored.PlanNavigation("main")!.Event.Id);
        Assert.Null(restored.PlanNavigation("west"));
        Assert.Equal(before, JsonSerializer.Serialize(restored.State));
    }

    [Fact]
    public void AnActualNewEndpointMakesTheOldStickyGoalUnreachableWithoutChangingAggregateCoverage()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        planner.Completed("172:142", "pending");
        planner.ObserveNavigation("172:142", "destination-new", [Event(planner, "172:142", "pending")]);
        AutoPlayNavigationRoute previous = planner.State.NavigationRoutes.Single(edge => edge.SourceKey == "main");
        string aggregateBefore = JsonSerializer.Serialize(planner.State.Visits);
        Assert.Equal("destination", planner.PlanNavigation("main", visit => visit.Key == "destination", "destination")!.RefreshVisitKey);

        planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route);

        Assert.DoesNotContain(planner.State.NavigationRoutes, edge => edge.SourceKey == "main" && edge.DestinationKey == "destination");
        AutoPlayRetiredNavigationRoute retired = Assert.Single(planner.State.NavigationRetirements);
        Assert.Equal(previous, retired.Route);
        Assert.Equal("destination-new", retired.ActualDestinationKey);
        AutoPlayPlanStep refresh = planner.PlanNavigation("main", visit => visit.Key is "destination" or "destination-new", "destination")!;
        Assert.Equal("destination-new", refresh.RefreshVisitKey);
        Assert.Equal("172:142", refresh.Route!.DestinationVisitKey);
        Assert.Equal(1, planner.ReachableNavigationFrontierCount("main", needsRefresh: visit => visit.Key is "destination" or "destination-new"));
        Assert.Equal(aggregateBefore, JsonSerializer.Serialize(planner.State.Visits));
    }

    [Fact]
    public void EndpointReplacementPreservesOtherSourceComponentsAndRecordedSelections()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayNavigationRoute previous = planner.State.NavigationRoutes.Single(edge => edge.SourceKey == "main");
        planner.ObserveNavigation("165:142", "other-source", [Event(planner, "165:142", previous.EventId)]);
        planner.ObserveNavigation("172:142", "destination-new", [Event(planner, "172:142", "pending")]);
        planner.RecordNavigationRoute("other-source", "destination", previous.EventId, previous.Route);
        planner.Completed("165:142", previous.EventId, "172:142", observedStoryChanged: false, selections: [new("actual-choice", 2)]);
        AutoPlayEventRoute otherSelection = planner.State.Visits.Single(visit => visit.Visit.Key == "165:142").Events.Single(item => item.Event.Id == previous.EventId).Routes.Single(route => route.Selections!.Count == 1);
        planner.RecordNavigationRoute("main", "destination", previous.EventId, otherSelection);

        planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route);

        Assert.Contains(planner.State.NavigationRoutes, edge => edge.SourceKey == "other-source" && edge.DestinationKey == "destination");
        Assert.Contains(planner.State.NavigationRoutes, edge => edge.SourceKey == "main" && edge.DestinationKey == "destination" && edge.Route.Selections!.SequenceEqual(otherSelection.Selections!));
        Assert.Contains(planner.State.NavigationRoutes, edge => edge.SourceKey == "west" && edge.DestinationKey == "outside");
        Assert.Single(planner.State.NavigationRetirements);
    }

    [Fact]
    public void AnAlreadyKnownActualEndpointStillRetiresContradictionsWithoutDuplicatingHistory()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayNavigationRoute previous = planner.State.NavigationRoutes.Single(edge => edge.SourceKey == "main");
        planner.ObserveNavigation("172:142", "destination-new", [Event(planner, "172:142", "pending")]);
        planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route);
        planner.State.NavigationRoutes.Add(previous);

        planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route);
        planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route);

        Assert.Single(planner.State.NavigationRetirements);
        Assert.Single(planner.State.NavigationRoutes, edge => edge.SourceKey == "main");
        Assert.DoesNotContain(planner.State.NavigationRoutes, edge => edge.SourceKey == "main" && edge.DestinationKey == "destination");
    }

    [Fact]
    public void RepeatingAnAuthenticatedEdgeAfterSourceObservationChangesIsIdempotentAndDoesNotMakeItTraversable()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayNavigationRoute previous = planner.State.NavigationRoutes.Single(edge => edge.SourceKey == "main");
        AutoPlayEvent changed = Event(planner, "165:142", previous.EventId) with { ConditionSignature = "changed-input" };
        planner.Observe(new("165:142", "garden"), [changed, Event(planner, "165:142", "165:gateway:3")]);
        planner.ObserveNavigation("165:142", "main", [changed]);
        planner.Completed("165:142", previous.EventId);
        string before = JsonSerializer.Serialize(planner.State);

        planner.RecordNavigationRoute(previous.SourceKey, previous.DestinationKey, previous.EventId, previous.Route);
        planner.RecordNavigationRoute(previous.SourceKey, previous.DestinationKey, previous.EventId, previous.Route);

        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
        Assert.Null(planner.PlanNavigation("main"));
        Assert.Equal(0, planner.ReachableNavigationFrontierCount("main"));
        Assert.Equal("changed-input", Event(planner, "165:142", previous.EventId).ConditionSignature);
        Assert.Equal("changed-input", planner.State.NavigationVisits.Single(visit => visit.Key == "main").Events.Single().ConditionSignature);
    }

    [Fact]
    public void AChangedSourceObservationCannotAuthenticateANewEndpointForAnOldCondition()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayNavigationRoute previous = planner.State.NavigationRoutes.Single(edge => edge.SourceKey == "main");
        AutoPlayEvent changed = Event(planner, "165:142", previous.EventId) with { ConditionSignature = "changed-input" };
        planner.ObserveNavigation("165:142", "main", [changed]);
        planner.ObserveNavigation("172:142", "destination-new", [Event(planner, "172:142", "pending")]);
        string before = JsonSerializer.Serialize(planner.State);

        Assert.Throws<InvalidOperationException>(() => planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route));

        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Fact]
    public void AnAuthenticatedDuplicateWithChangedSourceStillRetiresContradictoryEndpoints()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayNavigationRoute previous = planner.State.NavigationRoutes.Single(edge => edge.SourceKey == "main");
        planner.ObserveNavigation("172:142", "destination-new", [Event(planner, "172:142", "pending")]);
        planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route);
        planner.State.NavigationRoutes.Add(previous);
        planner.ObserveNavigation("165:142", "main", [Event(planner, "165:142", previous.EventId) with { ConditionSignature = "changed-input" }]);
        string aggregateBefore = JsonSerializer.Serialize(planner.State.Visits);

        planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route);
        string after = JsonSerializer.Serialize(planner.State);
        planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route);

        Assert.Equal(after, JsonSerializer.Serialize(planner.State));
        Assert.Equal(aggregateBefore, JsonSerializer.Serialize(planner.State.Visits));
        Assert.Single(planner.State.NavigationRetirements);
        Assert.Single(planner.State.NavigationRoutes, edge => edge.SourceKey == "main");
        Assert.DoesNotContain(planner.State.NavigationRoutes, edge => edge.SourceKey == "main" && edge.DestinationKey == "destination");
    }

    [Fact]
    public void AReplacedNavigationEndpointAndItsHistorySurviveColdRestore()
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayNavigationRoute previous = planner.State.NavigationRoutes.Single(edge => edge.SourceKey == "main");
        planner.ObserveNavigation("172:142", "destination-new", [Event(planner, "172:142", "pending")]);
        planner.RecordNavigationRoute("main", "destination-new", previous.EventId, previous.Route);
        planner.Completed("172:142", "pending");
        string before = JsonSerializer.Serialize(planner.State);

        AutoPlayPlanner restored = new(JsonSerializer.Deserialize<AutoPlayPlannerState>(before)!);

        Assert.Single(restored.State.NavigationRetirements);
        Assert.Null(restored.PlanNavigation("main", visit => visit.Key == "destination", "destination"));
        Assert.Equal("destination-new", restored.PlanNavigation("main", visit => visit.Key == "destination-new", "destination")!.RefreshVisitKey);
        Assert.Equal(before, JsonSerializer.Serialize(restored.State));
    }

    [Theory]
    [InlineData("wrong-source")]
    [InlineData("wrong-destination")]
    [InlineData("unrecorded-route")]
    public void InvalidActualEndpointProofDoesNotRetireOrChangeAnyNavigationHistory(string reason)
    {
        AutoPlayPlanner planner = CreateTwoComponents();
        AutoPlayNavigationRoute previous = planner.State.NavigationRoutes.Single(edge => edge.SourceKey == "main");
        planner.ObserveNavigation("172:142", "destination-new", [Event(planner, "172:142", "pending")]);
        string source = reason == "wrong-source" ? "west" : "main";
        string destination = reason == "wrong-destination" ? "outside" : "destination-new";
        AutoPlayEventRoute route = reason == "unrecorded-route" ? previous.Route with { Selections = [new("unknown-choice", 1)] } : previous.Route;
        string before = JsonSerializer.Serialize(planner.State);

        Assert.Throws<InvalidOperationException>(() => planner.RecordNavigationRoute(source, destination, previous.EventId, route));

        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    private static AutoPlayEvent Event(AutoPlayPlanner planner, string visit, string eventId)
    {
        return planner.State.Visits.Single(item => item.Visit.Key == visit).Events.Single(item => item.Event.Id == eventId).Event;
    }

    private static void Connect(AutoPlayPlanner planner, string source, string eventId, string destination, string sourceNavigation, string destinationNavigation)
    {
        planner.Completed(source, eventId, destination, observedStoryChanged: false, selections: []);
        AutoPlayEventRoute route = planner.State.Visits.Single(visit => visit.Visit.Key == source).Events.Single(item => item.Event.Id == eventId).Routes.Last();
        planner.RecordNavigationRoute(sourceNavigation, destinationNavigation, eventId, route);
    }

    private static AutoPlayPlanner CreateTwoComponents()
    {
        AutoPlayPlanner planner = new();
        AutoPlayEvent main = new("165:gateway:5", AutoPlayEventKind.Transition, ConditionSignature: "main-input");
        AutoPlayEvent west = new("165:gateway:3", AutoPlayEventKind.Transition, ConditionSignature: "west-input");
        planner.Observe(new("165:142", "garden"), [main, west]);
        planner.Observe(new("172:142", "garden"), [new("pending", AutoPlayEventKind.Interaction)]);
        planner.Observe(new("168:142", "garden"), []);
        planner.ObserveNavigation("165:142", "main", [main]);
        planner.ObserveNavigation("165:142", "west", [west]);
        planner.ObserveNavigation("172:142", "destination", [Event(planner, "172:142", "pending")]);
        planner.ObserveNavigation("168:142", "outside", []);
        Connect(planner, "165:142", main.Id, "172:142", "main", "destination");
        Connect(planner, "165:142", west.Id, "168:142", "west", "outside");
        return planner;
    }
}
