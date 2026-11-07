using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlaySoftWalkBlockTests
{
    [Fact]
    public void ARecordedTravelApproachKeepsItsPriorityWithoutRemovingPhysicalAlternatives()
    {
        AutoPlayTarget first = new("gate:first", AutoPlayTargetKind.Gateway, 0, 0, 0, 0, Order: 0);
        AutoPlayTarget second = first with { Id = "gate:second", Order = 1 };
        AutoPlayTarget recorded = first with { Id = "gate:recorded", Order = 2 };

        Assert.Equal(new[] { recorded, first, second }, AutoPlayController.OrderAutoPlayWalkingVariants([second, recorded, first], recorded.Id));
        Assert.Equal(new[] { first, second, recorded }, AutoPlayController.OrderAutoPlayWalkingVariants([second, recorded, first], "missing-approach"));
    }

    [Fact]
    public void ExhaustionRevisitsTheBlockedRouteOnceAndPreservesItsActualTravelEvidence()
    {
        var test = Create();
        string routes = JsonSerializer.Serialize(test.Blocked.Routes);
        string[] consumed = test.Blocked.Conditions!.ConsumedEntries.ToArray();
        Assert.Null(test.Planner.PlanNavigation("source-nav", _ => false));

        AutoPlayPlanStep next = Plan(test)!;

        Assert.Equal("to-hub", next.Event.Id);
        Assert.Equal("hub-nav", next.RefreshVisitKey);
        Assert.True(test.State.PendingExhaustedSurvey!.RetryWalkingBlocks);
        string signature = test.State.PendingExhaustedSurvey.Signature;
        Assert.NotEqual(AutoPlayController.ExhaustedSurveySignature("hub-nav", test.State.VisitGuards["hub-nav"], "A"), signature);
        Assert.False(AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "source-nav", _ => "A"));
        Assert.NotNull(test.Blocked.BlockedReason);
        Assert.DoesNotContain(signature, test.State.ExhaustedSurveyInspections);

        Assert.True(AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "hub-nav", _ => "A"));

        Assert.Null(test.Blocked.BlockedReason);
        Assert.Null(test.Blocked.BlockKind);
        Assert.True(test.Blocked.Completed);
        Assert.Equal(routes, JsonSerializer.Serialize(test.Blocked.Routes));
        Assert.Equal(consumed, test.Blocked.Conditions.ConsumedEntries);
        Assert.Equal("to-frontier", test.Planner.PlanNavigation("hub-nav", _ => false)!.Event.Id);
        test.Planner.Block("hub", "to-frontier", "search failed again", AutoPlayBlockKind.Walking);
        Assert.Null(Plan(test));
        Assert.Contains(signature, test.State.ExhaustedSurveyInspections);
    }

    [Fact]
    public void OnlyTheFailedCurrentEntryIsReleasedForAnUncompletedEvent()
    {
        var test = Create();
        test.Blocked.Completed = false;
        test.Blocked.Routes.Clear();
        test.Blocked.Conditions!.ConsumedEntries.Add("older-entry");
        Assert.NotNull(Plan(test));

        AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "hub-nav", _ => "A");

        Assert.False(test.Blocked.Completed);
        Assert.DoesNotContain("hub-condition", test.Blocked.Conditions.ConsumedEntries);
        Assert.Contains("older-entry", test.Blocked.Conditions.ConsumedEntries);
        Assert.Equal("to-frontier", test.Planner.PlanNavigation("hub-nav", _ => false)!.Event.Id);
    }

    [Theory]
    [InlineData("No discovered approach selects this actor with the ordinary talk query.")]
    [InlineData("None of the discovered coordinate approaches activated a native contact callback.")]
    [InlineData("No native walking approach reached and selected this actor with the ordinary talk query.")]
    [InlineData("No native walking approach activated the intended contact callback.")]
    public void OlderMissingKindWalkingReasonsRetainTheNarrowRetryClassification(string reason)
    {
        string json = JsonSerializer.Serialize(new { Event = new { Id = "gate", Kind = "Transition", ConditionSignature = "condition" }, BlockedReason = reason });
        AutoPlayEventCoverage legacy = JsonSerializer.Deserialize<AutoPlayEventCoverage>(json, AutoPlayController.JsonOptions)!;

        Assert.Null(legacy.BlockKind);
        Assert.True(AutoPlayPlanner.IsSoftWalkingBlock(legacy));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("near-match")]
    [InlineData("native-unavailable")]
    [InlineData("proved-attempt")]
    public void OtherBlocksAndNativeUnavailableEvidenceAreNeverReleased(string kind)
    {
        var test = Create();
        if (kind == "unknown")
        {
            test.Planner.Block("hub", "to-frontier", "An unsupported native boundary.");
        }
        else if (kind == "near-match")
        {
            test.Planner.Block("hub", "to-frontier", "No native walking approach activated the intended contact callback. Extra context");
        }
        else if (kind == "native-unavailable")
        {
            test.Blocked.Unavailable = true;
        }
        else
        {
            test.Blocked.UnavailableAttempts.Add(new("hub-condition", "frontier", "guard", "fingerprint"));
        }
        string before = JsonSerializer.Serialize(test.State.Coverage);

        Assert.False(AutoPlayPlanner.IsSoftWalkingBlock(test.Blocked));
        Assert.Null(Plan(test));
        Assert.Equal(before, JsonSerializer.Serialize(test.State.Coverage));
    }

    [Fact]
    public void AbortingARefreshDoesNotClearOrConsumeItsBlockedTarget()
    {
        var test = Create();
        Assert.NotNull(Plan(test));
        string requested = test.State.PendingExhaustedSurvey!.Signature;

        AutoPlayController.UpdateExhaustedSurveyTarget(test.State, null);

        Assert.False(AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "hub-nav", _ => "A"));
        Assert.NotNull(test.Blocked.BlockedReason);
        Assert.DoesNotContain(requested, test.State.ExhaustedSurveyInspections);
        Assert.NotNull(Plan(test));
    }

    [Fact]
    public void ActualInspectionStoresThePostInitializationSoftSignatureToo()
    {
        var test = Create();
        Assert.NotNull(Plan(test));

        Assert.True(AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "hub-nav", _ => "post-init"));
        test.Planner.Block("hub", "to-frontier", "search failed again", AutoPlayBlockKind.Walking);

        Assert.Null(Plan(test, "post-init"));
        Assert.NotNull(Plan(test, "new-input"));
        Assert.False(test.State.PendingExhaustedSurvey!.RetryWalkingBlocks);
    }

    [Fact]
    public void ARealFrontierKeepsPriorityOverRetryingARecordedSearchFailure()
    {
        var test = Create();
        AutoPlayEvent previous = test.State.Coverage.Visits.Single(visit => visit.Visit.Key == "source").Events.Single().Event;
        AutoPlayEvent fresh = new("fresh", AutoPlayEventKind.Interaction, ConditionSignature: "fresh");
        test.Planner.Observe(new("source", "region"), [previous, fresh]);
        test.Planner.ObserveNavigation("source", "source-nav", [previous, fresh]);

        AutoPlayPlanStep next = Plan(test)!;

        Assert.Equal("fresh", next.Event.Id);
        Assert.Null(test.State.PendingExhaustedSurvey);
        Assert.NotNull(test.Blocked.BlockedReason);
    }

    [Fact]
    public void SoftRetryRequestsAndTheirFiniteReceiptsSurviveManifestRoundTrips()
    {
        var test = Create();
        Plan(test);
        AutoPlayRunState restored = JsonSerializer.Deserialize<AutoPlayRunState>(JsonSerializer.Serialize(test.State, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
        Assert.True(restored.PendingExhaustedSurvey!.RetryWalkingBlocks);
        Assert.True(AutoPlayController.CompleteExhaustedSurveyInspection(restored, "hub-nav", _ => "A"));
        AutoPlayPlanner planner = new(restored.Coverage);
        planner.Block("hub", "to-frontier", "search failed again", AutoPlayBlockKind.Walking);

        Assert.Null(AutoPlayController.PlanWithExhaustedSurvey(restored, planner, "source-nav", _ => false, _ => true, _ => "A"));
        AutoPlayExhaustedSurveyRequest legacy = JsonSerializer.Deserialize<AutoPlayExhaustedSurveyRequest>("{\"NavigationKey\":\"nav\",\"Signature\":\"signature\"}")!;
        Assert.False(legacy.RetryWalkingBlocks);
    }

    [Fact]
    public void ANewWalkingSearchOnlyReopensObsoleteBlocksInTheActuallyObservedNavigation()
    {
        var test = Create();
        test.Blocked.WalkingSearchRevision = null;
        string routes = JsonSerializer.Serialize(test.Blocked.Routes);
        string[] consumed = test.Blocked.Conditions!.ConsumedEntries.ToArray();

        AutoPlayController.RefreshWalkingSearchRevision(test.State, "source-nav");
        Assert.NotNull(test.Blocked.BlockedReason);
        AutoPlayController.RefreshWalkingSearchRevision(test.State, "hub-nav");

        Assert.Null(test.Blocked.BlockedReason);
        Assert.True(test.Blocked.Completed);
        Assert.Equal(routes, JsonSerializer.Serialize(test.Blocked.Routes));
        Assert.Equal(consumed, test.Blocked.Conditions.ConsumedEntries);
        test.Planner.Block("hub", "to-frontier", "search failed again", AutoPlayBlockKind.Walking);
        AutoPlayController.RefreshWalkingSearchRevision(test.State, "hub-nav");
        Assert.NotNull(test.Blocked.BlockedReason);
        Assert.Equal(AutoPlayPlanner.CurrentWalkingSearchRevision, test.Blocked.WalkingSearchRevision);
    }

    [Fact]
    public void SearchRevisionReleasesOnlyTheUncompletedFailedEntry()
    {
        var test = Create();
        test.Blocked.WalkingSearchRevision = 0;
        test.Blocked.Completed = false;
        test.Blocked.Routes.Clear();
        test.Blocked.Conditions!.ConsumedEntries.Add("previous-entry");

        AutoPlayController.RefreshWalkingSearchRevision(test.State, "hub-nav");

        Assert.Null(test.Blocked.BlockedReason);
        Assert.False(test.Blocked.Completed);
        Assert.DoesNotContain("hub-condition", test.Blocked.Conditions.ConsumedEntries);
        Assert.Contains("previous-entry", test.Blocked.Conditions.ConsumedEntries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SearchRevisionCannotReleaseNativeUnavailabilityOrUnknownFailures(bool nativeUnavailable)
    {
        var test = Create();
        test.Blocked.WalkingSearchRevision = null;
        if (nativeUnavailable)
        {
            test.Blocked.Unavailable = true;
        }
        else
        {
            test.Planner.Block("hub", "to-frontier", "Unsupported execution contract.");
        }
        string before = JsonSerializer.Serialize(test.State.Coverage);

        AutoPlayController.RefreshWalkingSearchRevision(test.State, "hub-nav");

        Assert.Equal(before, JsonSerializer.Serialize(test.State.Coverage));
    }

    private static AutoPlayPlanStep? Plan((AutoPlayRunState State, AutoPlayPlanner Planner, AutoPlayEventCoverage Blocked) test, string fingerprint = "A")
    {
        return AutoPlayController.PlanWithExhaustedSurvey(test.State, test.Planner, "source-nav", _ => false, _ => true, _ => fingerprint);
    }

    private static (AutoPlayRunState State, AutoPlayPlanner Planner, AutoPlayEventCoverage Blocked) Create()
    {
        AutoPlayRunState state = new();
        AutoPlayPlanner planner = new(state.Coverage);
        AutoPlayEvent toHub = new("to-hub", AutoPlayEventKind.Transition, ConditionSignature: "source-condition");
        AutoPlayEvent toSource = new("to-source", AutoPlayEventKind.Transition, ConditionSignature: "return-condition");
        AutoPlayEvent toFrontier = new("to-frontier", AutoPlayEventKind.Transition, ConditionSignature: "hub-condition");
        AutoPlayEvent fresh = new("undiscovered", AutoPlayEventKind.Interaction, ConditionSignature: "frontier-condition");
        planner.Observe(new("source", "region"), [toHub]);
        planner.Observe(new("hub", "region"), [toSource, toFrontier]);
        planner.Observe(new("frontier", "region"), [fresh]);
        planner.Completed("source", "to-hub", "hub", observedStoryChanged: false, selections: []);
        planner.Completed("hub", "to-source", "source", observedStoryChanged: false, selections: []);
        planner.Completed("hub", "to-frontier", "frontier", observedStoryChanged: false, selections: []);
        planner.ObserveNavigation("source", "source-nav", [toHub]);
        planner.ObserveNavigation("hub", "hub-nav", [toSource, toFrontier]);
        planner.ObserveNavigation("frontier", "frontier-nav", [fresh]);
        foreach ((string visit, string navigation, string id, string destination) in new[] { ("source", "source-nav", "to-hub", "hub-nav"), ("hub", "hub-nav", "to-source", "source-nav"), ("hub", "hub-nav", "to-frontier", "frontier-nav") })
        {
            AutoPlayEventRoute route = state.Coverage.Visits.Single(item => item.Visit.Key == visit).Events.Single(item => item.Event.Id == id).Routes.Single();
            planner.RecordNavigationRoute(navigation, destination, id, route);
        }
        AutoPlayVisitGuard guard = new(AutoPlayVisitGuard.CurrentVersion, 1, "program", ImmutableArray.Create(new NativePersistentPredicateRead("PSHM_B", 720, 1)), "A");
        state.VisitGuards["hub-nav"] = guard;
        AutoPlayController.SeedExhaustedSurveyInspection(state, "hub-nav", guard);
        planner.Block("hub", "to-frontier", "search failed", AutoPlayBlockKind.Walking);
        return (state, planner, state.Coverage.Visits.Single(visit => visit.Visit.Key == "hub").Events.Single(item => item.Event.Id == "to-frontier"));
    }
}
