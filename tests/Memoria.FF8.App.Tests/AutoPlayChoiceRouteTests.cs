using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayChoiceRouteTests
{
    [Fact]
    public void UnexpectedTravelDestinationCannotChangeCoverageOrRequestResources()
    {
        string output = Path.Combine(Path.GetTempPath(), "ff8-route-validation-" + Guid.NewGuid().ToString("N"));
        GameSession session = GameSession.CreateColdProcess();
        int resources = 0;
        AutoPlayController controller = new(session, () =>
        {
            resources++;
            throw new InvalidOperationException("No resource lookup is permitted before route validation.");
        }, session.Restore, output);
        try
        {
            controller.Start();
            AutoPlayPlanner planner = new(controller.State.Coverage);
            planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Transition, ConditionSignature: "entry")]);
            planner.BeginEvent("source", "door");
            controller.State.ActiveVisit = "source";
            controller.State.ActiveEvent = "door";
            controller.State.ActiveSelections = [];
            controller.State.ActiveTravelRoute = new("expected-destination", "entry", false, []);
            string before = JsonSerializer.Serialize(controller.State.Coverage);
            NotSupportedException error = Assert.Throws<NotSupportedException>(controller.RecordActiveControlReturn);
            Assert.Contains("different destination", error.Message);
            Assert.Equal(before, JsonSerializer.Serialize(controller.State.Coverage));
            Assert.Equal(0, resources);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [Theory]
    [InlineData(false, "authentic-entry", "authentic-entry")]
    [InlineData(false, null, null)]
    [InlineData(true, "authentic-entry", null)]
    public void LegacyResumeRecoversOnlyThePersistedActiveEntryOnceAndPreservesExplicitUnknown(bool explicitUnknown, string? activeEntry, string? expected)
    {
        string output = Path.Combine(Path.GetTempPath(), "ff8-route-entry-" + Guid.NewGuid().ToString("N"));
        GameSession session = GameSession.CreateColdProcess();
        AutoPlayController controller = new(session, () => throw new InvalidOperationException("This metadata restore needs no game resources."), session.Restore, output);
        try
        {
            controller.Start();
            string manifest = Assert.Single(Directory.GetFiles(output, "start.run.json", SearchOption.AllDirectories));
            AutoPlayRunState state = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
            state.ActiveVisit = "source";
            state.ActiveEvent = "door";
            state.Coverage.Visits.Add(new AutoPlayVisitCoverage { Visit = new("source", "region"), Events = [new() { Event = new("door", AutoPlayEventKind.Interaction, ConditionSignature: "later-source"), Conditions = new() { ActiveEntry = activeEntry } }] });
            JsonObject json = JsonSerializer.SerializeToNode(state, AutoPlayController.JsonOptions)!.AsObject();
            if (!explicitUnknown)
            {
                json.Remove(nameof(AutoPlayRunState.ActiveConditionSignature));
            }
            File.WriteAllText(manifest, json.ToJsonString());
            controller.ResumeFrom(manifest);
            Assert.Equal(expected, controller.State.ActiveConditionSignature);
            controller.State.Coverage.Visits.Single().Events.Single().Conditions!.ActiveEntry = "another-alternative";
            controller.State.Snapshot = state.Snapshot;
            File.WriteAllText(manifest, JsonSerializer.Serialize(controller.State, AutoPlayController.JsonOptions));
            controller.ResumeFrom(manifest);
            Assert.Equal(expected, controller.State.ActiveConditionSignature);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [Fact]
    public void AnAuthoritativelyUnknownEntryNeverUsesTheCurrentOrAnEarlierAlternativeSignature()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Interaction, ConditionSignature: "current-source")]);
        planner.BeginEvent("source", "door");
        planner.Completed("source", "door", "destination", exitSignature: "actual-exit", observedStoryChanged: false, selections: [], entrySignature: null, entrySignatureAuthoritative: true);
        planner.Completed("source", "door", "other", observedStoryChanged: false, selections: [], entrySignature: null, entrySignatureAuthoritative: true);
        AutoPlayEventCoverage coverage = planner.State.Visits.Single().Events.Single();
        Assert.Empty(coverage.Conditions!.ConsumedEntries);
        Assert.Equal(new[] { "actual-exit" }, coverage.Conditions.ConsumedExits);
        Assert.All(coverage.Routes, route => Assert.Null(route.ConditionSignature));
        Assert.Null(coverage.ObservedConditionSignature);
        Assert.False(planner.Plan("source")!.IsTravel);
    }

    [Fact]
    public void ActualDescentAndStayBothFinishWithoutLosingTheDescentRouteOrChoosingTheDeadEnd()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayChoiceRouteTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        string output = Path.Combine(Path.GetTempPath(), "ff8-choice-routes-" + Guid.NewGuid().ToString("N"));
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        try
        {
            GameWorld GetWorld()
            {
                return world ??= new(session, TestResources.GameDirectory, validateWorldPresentation: false);
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output);
            controller.ResumeFrom(manifest);
            string key = "312:125:8:50:978:0:2:3";
            bool descended = false;
            bool stayed = false;
            bool finished = false;
            for (int tick = 0; tick < 96 && controller.Error is null; tick++)
            {
                controller.Advance(1);
                AutoPlayChoiceCoverage? choice = controller.State.Coverage.Choices.SingleOrDefault(item => item.Key == key);
                descended |= session.State.FieldId == 310 && choice?.ActiveOption == 2;
                stayed |= session.State.FieldId == 312 && choice?.ActiveOption == 3 && GetWorld().Events.IsUserControlEnabled;
                if (choice?.Finished == true && controller.State.PrimaryContinuation is null)
                {
                    finished = true;
                    break;
                }
            }
            Assert.Null(controller.Error);
            Assert.True(descended);
            Assert.True(stayed);
            Assert.True(finished);
            Assert.Equal(310, session.State.FieldId);
            AutoPlayChoiceCoverage completed = controller.State.Coverage.Choices.Single(item => item.Key == key);
            Assert.Equal(new[] { 2, 3 }, completed.CompletedOptions);
            Assert.Empty(completed.BeforeSelections!);
            AutoPlayEventCoverage exit = controller.State.Coverage.Visits.Single(item => item.Visit.Key == "312:125").Events.Single(item => item.Event.Id == "312:line:9");
            AutoPlayEventRoute descent = Assert.Single(exit.Routes);
            Assert.Equal("310:125", descent.DestinationVisitKey);
            Assert.False(descent.ObservedStoryChanged);
            Assert.Equal(new AutoPlayChoiceSelection(key, 2), Assert.Single(descent.Selections!));
            AutoPlayPlanner planner = new(JsonSerializer.Deserialize<AutoPlayPlannerState>(JsonSerializer.Serialize(controller.State.Coverage))!);
            AutoPlayPlanStep route = planner.Plan("312:125")!;
            Assert.True(route.IsTravel);
            Assert.Equal("310:125", route.Route!.DestinationVisitKey);
            Assert.Equal(2, AutoPlayController.SelectRecordedTravelChoice(route.Route, [], key, 2, 3));
            Assert.True(planner.ReachableFrontierCount("310:125") > 0);
            Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
            Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));

            // Independently cold-load the unchanged original boundary with the routes just observed.
            AutoPlayRunState travel = JsonSerializer.Deserialize<AutoPlayRunState>(JsonSerializer.Serialize(controller.State, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
            travel.ActiveVisit = null;
            travel.ActiveEvent = null;
            travel.ActiveStory = null;
            travel.ActiveTravelRoute = null;
            travel.ActiveBeforeCheckpoint = null;
            travel.ActiveTargetId = null;
            travel.ActiveSelections = [];
            travel.ActiveObservedInput = null;
            travel.ObserveContactInput = false;
            travel.SceneChoices.Clear();
            travel.PrimaryContinuation = null;
            travel.Snapshot = "travel-source.json";
            File.WriteAllBytes(Path.Combine(output, travel.Snapshot), originalSnapshot);
            string travelManifest = Path.Combine(output, "travel.run.json");
            File.WriteAllText(travelManifest, JsonSerializer.Serialize(travel, AutoPlayController.JsonOptions));
            controller.Stop();
            AutoPlayController traveler = new(session, GetWorld, Restore, output);
            traveler.ResumeFrom(travelManifest);
            for (int tick = 0; tick < 96 && session.State.FieldId != 310 && traveler.Error is null; tick++)
            {
                traveler.Advance(1);
            }
            Assert.Null(traveler.Error);
            Assert.Equal(310, session.State.FieldId);
            Assert.Equal(new AutoPlayChoiceSelection(key, 2), Assert.Single(traveler.State.ActiveSelections!));
            Assert.Empty(traveler.State.SceneChoices);
            Assert.Equal(new[] { 2, 3 }, traveler.State.Coverage.Choices.Single(item => item.Key == key).CompletedOptions);
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [Fact]
    public void WorldStayOutcomeDoesNotBlockRemainingAlternativesOrAnActuallyObservedFieldRoute()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("world:40", "world"), [new("entrance", AutoPlayEventKind.Transition, ConditionSignature: "entry")]);
        planner.BeginEvent("world:40", "entrance");
        AutoPlayController.RecordWorldControlReturn(planner, "world:40", "entrance", "world:40", 40, 40, selections: [new("prompt", 1)], entrySignature: "entry", hasChoiceOutcome: true);
        AutoPlayEventCoverage entrance = planner.State.Visits.Single().Events.Single();
        Assert.Null(entrance.BlockedReason);
        planner.Completed("world:40", "entrance", "field:40", observedStoryChanged: false, selections: [new("prompt", 0)], entrySignature: "entry");
        planner.Observe(new("field:40", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        AutoPlayController.RecordWorldControlReturn(planner, "world:40", "entrance", "world:40", 40, 40);
        Assert.Null(entrance.BlockedReason);
        Assert.Equal("field:40", planner.Plan("world:40")!.Route!.DestinationVisitKey);
        Assert.Equal(new AutoPlayChoiceSelection("prompt", 0), Assert.Single(Assert.Single(entrance.Routes).Selections!));
    }

    [Fact]
    public void ActualSuccessfulCompletionClearsAPreviousBlockedAttempt()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Transition)]);
        planner.Block("source", "door", "No native contact in the earlier attempt.");
        planner.Completed("source", "door", "destination", observedStoryChanged: false, selections: []);
        planner.Observe(new("destination", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        Assert.True(planner.Plan("source")!.IsTravel);
    }

    [Fact]
    public void MultipleActualDestinationsAndNestedSelectionPathsSurviveRestoreWithoutCombiningAlternatives()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Interaction, ChangesStory: true, ConditionSignature: "entry")]);
        AutoPlayChoiceSelection[] first = [new("outer", 0), new("inner", 1)];
        AutoPlayChoiceSelection[] second = [new("outer", 1)];
        planner.BeginEvent("source", "door");
        planner.Completed("source", "door", "annex", observedStoryChanged: false, selections: first, entrySignature: "entry");
        planner.Completed("source", "door", "garden", observedStoryChanged: false, selections: second, entrySignature: "entry");
        planner.Completed("source", "door", observedStoryChanged: false, selections: [new("outer", 2)], entrySignature: "entry");
        planner.Observe(new("annex", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        planner.Observe(new("garden", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        planner = new(JsonSerializer.Deserialize<AutoPlayPlannerState>(JsonSerializer.Serialize(planner.State))!);
        AutoPlayEventCoverage coverage = planner.State.Visits.Single(item => item.Visit.Key == "source").Events.Single();
        Assert.Equal(2, coverage.Routes.Count);
        Assert.Equal(first, coverage.Routes.Single(route => route.DestinationVisitKey == "annex").Selections);
        Assert.Equal(second, coverage.Routes.Single(route => route.DestinationVisitKey == "garden").Selections);
        AutoPlayPlanStep step = planner.Plan("source")!;
        Assert.Equal("annex", step.Route!.DestinationVisitKey);
        Assert.Equal(0, AutoPlayController.SelectRecordedTravelChoice(step.Route, [], "outer", 0, 2));
        Assert.Equal(1, AutoPlayController.SelectRecordedTravelChoice(step.Route, [new("outer", 0)], "inner", 0, 1));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.SelectRecordedTravelChoice(step.Route, [new("outer", 1)], "inner", 0, 1));
    }

    [Fact]
    public void RepeatedPromptKeysRetainTheirActualOrderedSelections()
    {
        AutoPlayEventRoute route = new("destination", "entry", false, [new("prompt", 2), new("prompt", 3)]);
        Assert.Equal(2, AutoPlayController.SelectRecordedTravelChoice(route, [], "prompt", 2, 3));
        Assert.Equal(3, AutoPlayController.SelectRecordedTravelChoice(route, [new("prompt", 2)], "prompt", 2, 3));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.SelectRecordedTravelChoice(route, [new("prompt", 2), new("prompt", 3)], "prompt", 2, 3));
    }

    [Fact]
    public void UnknownChangedOrUnavailableChoicePathsRefuseBeforeSelectingAnyOption()
    {
        AutoPlayEventRoute route = new("destination", "entry", false, [new("prompt", 2)]);
        Assert.Throws<NotSupportedException>(() => AutoPlayController.SelectRecordedTravelChoice(route with { Selections = null }, [], "prompt", 2, 3));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.SelectRecordedTravelChoice(route, null, "prompt", 2, 3));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.SelectRecordedTravelChoice(route, [], "other-prompt", 2, 3));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.SelectRecordedTravelChoice(route, [], "prompt", 0, 1));
    }

    [Fact]
    public void LegacySingleDestinationRetainsNoInventedChoiceProvenance()
    {
        AutoPlayPlannerState state = JsonSerializer.Deserialize<AutoPlayPlannerState>("{\"Visits\":[{\"Visit\":{\"Key\":\"source\",\"Region\":\"region\"},\"Events\":[{\"Event\":{\"Id\":\"door\",\"Kind\":1,\"DestinationVisitKey\":\"destination\"},\"Available\":true,\"Completed\":true}]},{\"Visit\":{\"Key\":\"destination\",\"Region\":\"region\"},\"Events\":[{\"Event\":{\"Id\":\"talk\",\"Kind\":0},\"Available\":true}]}]}")!;
        AutoPlayPlanner planner = new(state);
        AutoPlayPlanStep step = planner.Plan("source")!;
        Assert.True(step.IsTravel);
        Assert.Null(step.Route!.Selections);
        Assert.Throws<NotSupportedException>(() => AutoPlayController.SelectRecordedTravelChoice(step.Route, [], "prompt", 0, 1));
    }

    [Fact]
    public void EachRouteKeepsItsOwnStoryObservationAndConditionSignature()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Interaction, ChangesStory: true, ConditionSignature: "entry")]);
        planner.Completed("source", "door", "advanced", observedStoryChanged: true, selections: [new("prompt", 0)]);
        planner.Completed("source", "door", "annex", observedStoryChanged: false, selections: [new("prompt", 1)]);
        planner.Observe(new("advanced", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        planner.Observe(new("annex", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        Assert.Equal("annex", planner.Plan("source")!.Route!.DestinationVisitKey);
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Interaction, ChangesStory: true, ConditionSignature: "changed")]);
        Assert.False(planner.Plan("source")!.IsTravel);
    }

    [Fact]
    public void ContinuationScoreExcludesTheActiveInteractionAndCountsActualReachableFrontier()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "region"), [new("active", AutoPlayEventKind.Interaction)]);
        Assert.Equal(0, planner.ReachableFrontierCount("source", "source", "active"));
        planner.Completed("source", "active", "hall", observedStoryChanged: false, selections: [new("prompt", 0)]);
        planner.Observe(new("hall", "region"), [new("exit", AutoPlayEventKind.Transition)]);
        planner.Completed("hall", "exit", "frontier", observedStoryChanged: false, selections: []);
        planner.Observe(new("frontier", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        Assert.Equal(1, planner.ReachableFrontierCount("hall", "source", "active"));
        Assert.Equal(1, planner.ReachableFrontierCount("source", "source", "active"));
    }
}
