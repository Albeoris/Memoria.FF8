using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNavigationExcursionTests
{
    [Fact]
    public void OnlyAuthenticatedCurrentRoutesProveReturnFromAnInternalDeadEndComponent()
    {
        AutoPlayPlanner planner = Graph();
        Assert.True(planner.CanReachNavigation("source-nav", "annex-nav"));
        Assert.False(planner.CanReachNavigation("annex-nav", "source-nav"));
        Assert.False(planner.CanReachNavigation("source-nav", "annex-nav", vertex => vertex.Key != "room-nav"));
        Connect(planner, "annex", "return", "source");
        Assert.True(planner.CanReachNavigation("annex-nav", "source-nav"));
        AutoPlayEventCoverage item = planner.State.Visits.Single(visit => visit.Visit.Key == "annex").Events.Single(candidate => candidate.Event.Id == "return");
        item.Event = item.Event with { ConditionSignature = "changed-condition" };
        Assert.False(planner.CanReachNavigation("annex-nav", "source-nav"));
    }

    [Fact]
    public void AScopedDeadEndBlocksOnlyItsSourceComponentAndExactConditionWithoutChangingCompletion()
    {
        AutoPlayPlanner planner = Graph();
        AutoPlayEventCoverage shared = planner.State.Visits.Single(visit => visit.Visit.Key == "source").Events.Single();
        planner.ObserveNavigation("source", "alternate-nav", [shared.Event]);
        planner.RecordNavigationRoute("alternate-nav", "room-nav", "out", shared.Routes.Single());
        string before = JsonSerializer.Serialize(shared);
        planner.SetNavigationExcursionBlocks([new("source-nav", "source", "out", "out-left", "source-condition")]);

        Assert.False(planner.CanReachNavigation("source-nav", "room-nav"));
        Assert.True(planner.CanReachNavigation("alternate-nav", "room-nav"));
        Assert.Equal("out", planner.PlanNavigation("source-nav")!.Event.Id);
        Assert.Equal(before, JsonSerializer.Serialize(shared));
        Assert.Null(shared.BlockedReason);
        planner.SetNavigationExcursionBlocks([new("source-nav", "source", "out", "out-left", "source-condition"), new("source-nav", "source", "out", "out-right", "source-condition")]);
        Assert.Null(planner.PlanNavigation("source-nav"));
        AutoPlayEvent changed = shared.Event with { ConditionSignature = "new-condition" };
        planner.Observe(new("source", "region"), [changed]);
        planner.ObserveNavigation("source", "source-nav", [changed]);
        planner.Completed("source", "out", "room", observedStoryChanged: false, selections: []);
        planner.RecordNavigationRoute("source-nav", "room-nav", "out", shared.Routes.Single(route => route.ConditionSignature == "new-condition"));
        Assert.True(planner.CanReachNavigation("source-nav", "room-nav"));
    }

    [Fact]
    public void TheSingleAnchorSurvivesInternalTravelButNotReturnStoryProgressChoicesOrAnUnissuedAttempt()
    {
        AutoPlayRunState state = new() { NavigationExcursion = Excursion(State()) with { Departed = false } };
        AutoPlayController.ObserveNavigationExcursionBoundary(state, 1, "room-nav", canReturn: false);
        Assert.True(state.NavigationExcursion!.Departed);
        AutoPlayController.ObserveNavigationExcursionBoundary(state, 1, "annex-nav", canReturn: false);
        Assert.Equal("source.json", state.NavigationExcursion!.Checkpoint);
        AutoPlayController.ObserveNavigationExcursionBoundary(state, 1, "source-nav", canReturn: false);
        Assert.Null(state.NavigationExcursion);
        state.NavigationExcursion = Excursion(State());
        AutoPlayController.ObserveNavigationExcursionBoundary(state, 1, "room-nav", canReturn: true);
        Assert.Null(state.NavigationExcursion);
        state.NavigationExcursion = Excursion(State());
        AutoPlayController.ObserveNavigationExcursionBoundary(state, 2, "room-nav", canReturn: false);
        Assert.Null(state.NavigationExcursion);
        state.NavigationExcursion = Excursion(State());
        state.PrimaryContinuation = "choice.json";
        AutoPlayController.ObserveNavigationExcursionBoundary(state, 1, "room-nav", canReturn: false);
        Assert.Null(state.NavigationExcursion);
        state.PrimaryContinuation = null;
        state.NavigationExcursion = Excursion(State()) with { Issued = false };
        AutoPlayController.ObserveNavigationExcursionBoundary(state, 1, "room-nav", canReturn: false);
        Assert.Null(state.NavigationExcursion);
    }

    [Fact]
    public void RollbackSafetyRejectsQuestBytesStoryAndRewardsWhileIgnoringLocalAndPresentationMemory()
    {
        GameState source = State();
        AutoPlayNavigationExcursion excursion = Excursion(source);
        Assert.True(AutoPlayController.HasUnchangedNavigationExcursionProgress(excursion, source, "persistent"));
        Assert.False(AutoPlayController.HasUnchangedNavigationExcursionProgress(excursion, source, null));
        Assert.False(AutoPlayController.HasUnchangedNavigationExcursionProgress(excursion, source with { EventMemory = source.EventMemory.Write(300, 1, NativeScalarValue.Exact(7)) }, "persistent"));
        Assert.False(AutoPlayController.HasUnchangedNavigationExcursionProgress(excursion, source with { EventMemory = source.EventMemory.Write(256, 2, NativeScalarValue.Exact(2)) }, "persistent"));
        Assert.False(AutoPlayController.HasUnchangedNavigationExcursionProgress(excursion, source with { AppliedBattleResultIds = ["new-result"] }, "persistent"));
        Assert.True(AutoPlayController.HasUnchangedNavigationExcursionProgress(excursion, source with { EventMemory = source.EventMemory.Write(1300, 1, NativeScalarValue.Exact(7)).Write(12, 1, NativeScalarValue.Exact(9)) }, "persistent"));
        Assert.Equal(AutoPlayController.NavigationExcursionProgressFingerprint(source), AutoPlayController.NavigationExcursionProgressFingerprint(source with { EventMemory = source.EventMemory.Write(300, 1, NativeScalarValue.Exact(0)) }));
    }

    [Fact]
    public void FieldLocalPartyBindingsMayChangeButCanonicalPartyIdentityCannotBeRolledBack()
    {
        GameState source = State() with { EventState = JsonSerializer.SerializeToElement(new { Scene = new { Party = new[] { 0, 4, 1 }, PartyBindingMemory = new { Version = 1, First = 0, Second = 2, Third = 3 } } }) };
        GameState remapped = source with { EventState = JsonSerializer.SerializeToElement(new { Scene = new { Party = new[] { 0, 4, 1 }, PartyBindingMemory = new { Version = 1, First = 0, Second = 4, Third = 1 } } }) };
        GameState changedParty = source with { EventState = JsonSerializer.SerializeToElement(new { Scene = new { Party = new[] { 0, 5, 1 }, PartyBindingMemory = new { Version = 1, First = 0, Second = 4, Third = 1 } } }) };

        Assert.Equal(AutoPlayController.NavigationExcursionProgressFingerprint(source), AutoPlayController.NavigationExcursionProgressFingerprint(remapped));
        Assert.NotEqual(AutoPlayController.NavigationExcursionProgressFingerprint(source), AutoPlayController.NavigationExcursionProgressFingerprint(changedParty));
    }

    [Fact]
    public void AnchorAndScopedBlocksPersistAndRetainOnlyTheSingleStandaloneDependency()
    {
        AutoPlayRunState state = new() { NavigationExcursion = Excursion(State()), NavigationExcursionBlocks = [new("source-nav", "source", "out", "out", "source-condition")] };
        AutoPlayRunState restored = JsonSerializer.Deserialize<AutoPlayRunState>(JsonSerializer.Serialize(state, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
        Assert.Equal(state.NavigationExcursion!.Checkpoint, restored.NavigationExcursion!.Checkpoint);
        Assert.Equal(state.NavigationExcursion.ProgressFingerprint, restored.NavigationExcursion.ProgressFingerprint);
        Assert.Equal(state.NavigationExcursionBlocks, restored.NavigationExcursionBlocks);
        Assert.Equal(new[] { "source.json" }, AutoPlayController.CaptureDependencies(restored));
        AutoPlayRunState legacy = JsonSerializer.Deserialize<AutoPlayRunState>("{}", AutoPlayController.JsonOptions)!;
        Assert.Null(legacy.NavigationExcursion);
        Assert.Empty(legacy.NavigationExcursionBlocks);
    }

    private static GameState State() => new() { EventMemory = NativeEventMemory.Empty.Write(256, 2, NativeScalarValue.Exact(1)) };

    private static AutoPlayNavigationExcursion Excursion(GameState source) => new("source.json", "source-nav", "source", 1, "out", "out", -1, "source-condition", 1, [], "persistent", AutoPlayController.NavigationExcursionProgressFingerprint(source)!, Departed: true, Issued: true);

    private static AutoPlayPlanner Graph()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "region"), [new("out", AutoPlayEventKind.Transition, ConditionSignature: "source-condition", Variants: ["out-left", "out-right"])]);
        planner.Observe(new("room", "region"), [new("internal", AutoPlayEventKind.Transition, ConditionSignature: "room-condition")]);
        planner.Observe(new("annex", "region"), [new("return", AutoPlayEventKind.Transition, ConditionSignature: "annex-condition")]);
        foreach (AutoPlayVisitCoverage visit in planner.State.Visits)
        {
            planner.ObserveNavigation(visit.Visit.Key, visit.Visit.Key + "-nav", visit.Events.Select(item => item.Event).ToArray());
        }
        Connect(planner, "source", "out", "room");
        Connect(planner, "room", "internal", "annex");
        return planner;
    }

    private static void Connect(AutoPlayPlanner planner, string source, string eventId, string destination)
    {
        AutoPlayEventCoverage item = planner.State.Visits.Single(visit => visit.Visit.Key == source).Events.Single(candidate => candidate.Event.Id == eventId);
        planner.Completed(source, eventId, destination, observedStoryChanged: false, selections: [], targetId: item.Event.Variants?.FirstOrDefault());
        planner.RecordNavigationRoute(source + "-nav", destination + "-nav", eventId, item.Routes.Single());
    }
}
