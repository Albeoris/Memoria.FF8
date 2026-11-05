using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayExhaustedSurveyTests
{
    [Fact]
    public void ExhaustionReinspectsEachExactInputOnceEvenWhenInitializationRestoresAnotherObservedInput()
    {
        var test = Create();
        AutoPlayVisitGuard guard = test.State.VisitGuards["remote-nav"];
        int score = AutoPlayController.ProgressScore(test.State);
        string before = JsonSerializer.Serialize(test.State.Coverage);
        Assert.Null(test.Planner.PlanNavigation("source-nav", _ => false));

        AutoPlayPlanStep next = Plan(test, "B")!;

        Assert.Equal("door", next.Event.Id);
        Assert.Equal("remote-nav", next.RefreshVisitKey);
        Assert.Equal("remote-nav", test.State.RefreshTargetVisit);
        Assert.NotNull(test.State.PendingExhaustedSurvey);
        string requested = AutoPlayController.ExhaustedSurveySignature("remote-nav", guard, "B");
        Assert.DoesNotContain(requested, test.State.ExhaustedSurveyInspections);
        Assert.False(AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "source-nav", _ => "A"));
        Assert.NotNull(test.State.PendingExhaustedSurvey);

        Assert.True(AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "remote-nav", _ => "A"));

        Assert.Null(test.State.PendingExhaustedSurvey);
        Assert.Null(test.State.RefreshTargetVisit);
        Assert.Contains(requested, test.State.ExhaustedSurveyInspections);
        Assert.Null(Plan(test, "B"));
        Assert.Null(Plan(test, "A"));
        Assert.Equal(2, test.State.ExhaustedSurveyInspections.Count);
        Assert.Equal(score, AutoPlayController.ProgressScore(test.State));
        Assert.Equal(before, JsonSerializer.Serialize(test.State.Coverage));
        Assert.Equal(new[] { "remote-nav" }, test.State.InspectedVisitsSinceProgress);
        Assert.False(AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "remote-nav", _ => "A"));
        Assert.NotNull(Plan(test, "C"));
    }

    [Fact]
    public void ActualInspectionAlsoRecordsThePostInitializationFingerprint()
    {
        var test = Create();
        Assert.NotNull(Plan(test, "requested"));

        Assert.True(AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "remote-nav", _ => "post-init"));

        Assert.Null(Plan(test, "post-init"));
        Assert.Equal(3, test.State.ExhaustedSurveyInspections.Count);
    }

    [Fact]
    public void AnAbortedOrChangedTargetDoesNotConsumeItsRequestedSignature()
    {
        var test = Create();
        Plan(test, "B");
        string requested = test.State.PendingExhaustedSurvey!.Signature;

        AutoPlayController.UpdateExhaustedSurveyTarget(test.State, "another-target");

        Assert.Null(test.State.PendingExhaustedSurvey);
        Assert.DoesNotContain(requested, test.State.ExhaustedSurveyInspections);
        Assert.False(AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "remote-nav", _ => "B"));
        AutoPlayController.UpdateExhaustedSurveyTarget(test.State, null);
        Assert.NotNull(Plan(test, "B"));
    }

    [Fact]
    public void AnUnreachableTargetDoesNotReceiveAnInspectionReceipt()
    {
        var test = Create();
        Plan(test, "B");
        string requested = test.State.PendingExhaustedSurvey!.Signature;
        AutoPlayEvent door = test.State.Coverage.Visits.Single(visit => visit.Visit.Key == "source").Events.Single().Event;
        test.Planner.ObserveNavigation("source", "source-nav", []);

        Assert.Null(Plan(test, "B"));

        Assert.Null(test.State.PendingExhaustedSurvey);
        Assert.Null(test.State.RefreshTargetVisit);
        Assert.DoesNotContain(requested, test.State.ExhaustedSurveyInspections);
        test.Planner.ObserveNavigation("source", "source-nav", [door]);
        Assert.NotNull(Plan(test, "B"));
    }

    [Fact]
    public void AChangedExactReadDomainIsNotSuppressedByAnOlderSignature()
    {
        var test = Create();
        Plan(test, "B");
        AutoPlayController.CompleteExhaustedSurveyInspection(test.State, "remote-nav", _ => "A");
        Assert.Null(Plan(test, "B"));
        AutoPlayVisitGuard original = test.State.VisitGuards["remote-nav"];
        test.State.VisitGuards["remote-nav"] = original with { Reads = ImmutableArray.Create(new NativePersistentPredicateRead("PSHM_B", 1301, 1)) };

        Assert.NotNull(Plan(test, "B"));

        Assert.NotEqual(AutoPlayController.ExhaustedSurveySignature("remote-nav", original, "B"), test.State.PendingExhaustedSurvey!.Signature);
        Assert.NotEqual(AutoPlayController.ExhaustedSurveySignature("remote-nav", original, "B"), AutoPlayController.ExhaustedSurveySignature("different-nav", original, "B"));
        Assert.NotEqual(AutoPlayController.ExhaustedSurveySignature("remote-nav", original, "B"), AutoPlayController.ExhaustedSurveySignature("remote-nav", original with { ProgramHash = "different-program" }, "B"));
        Assert.NotEqual(AutoPlayController.ExhaustedSurveySignature("remote-nav", original, "B"), AutoPlayController.ExhaustedSurveySignature("remote-nav", original with { Version = original.Version + 1 }, "B"));
    }

    [Fact]
    public void ARealFrontierKeepsItsPriorityAndAbortsAnUncompletedResurvey()
    {
        var test = Create();
        Plan(test, "B");
        string requested = test.State.PendingExhaustedSurvey!.Signature;
        AutoPlayEvent door = test.State.Coverage.Visits.Single(visit => visit.Visit.Key == "source").Events.Single().Event;
        AutoPlayEvent fresh = new("fresh", AutoPlayEventKind.Interaction, ConditionSignature: "fresh-condition");
        test.Planner.Observe(new("source", "region"), [door, fresh]);
        test.Planner.ObserveNavigation("source", "source-nav", [door, fresh]);

        AutoPlayPlanStep next = AutoPlayController.PlanWithExhaustedSurvey(test.State, test.Planner, "source-nav", _ => false, _ => true, _ => throw new InvalidOperationException("Exhaustion fallback must not run while a genuine frontier exists."))!;

        Assert.Equal("fresh", next.Event.Id);
        Assert.Null(test.State.PendingExhaustedSurvey);
        Assert.Null(test.State.RefreshTargetVisit);
        Assert.DoesNotContain(requested, test.State.ExhaustedSurveyInspections);
    }

    [Fact]
    public void PendingAndCompletedSignaturesPersistWithoutChangingOlderManifests()
    {
        var test = Create();
        Plan(test, "B");
        AutoPlayRunState restored = JsonSerializer.Deserialize<AutoPlayRunState>(JsonSerializer.Serialize(test.State, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
        Assert.Equal(test.State.PendingExhaustedSurvey, restored.PendingExhaustedSurvey);
        Assert.Equal(test.State.RefreshTargetVisit, restored.RefreshTargetVisit);
        Assert.True(restored.ExhaustedSurveyInspections.SetEquals(test.State.ExhaustedSurveyInspections));
        Assert.True(AutoPlayController.CompleteExhaustedSurveyInspection(restored, "remote-nav", _ => "A"));
        AutoPlayRunState completed = JsonSerializer.Deserialize<AutoPlayRunState>(JsonSerializer.Serialize(restored, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
        Assert.Null(AutoPlayController.PlanWithExhaustedSurvey(completed, new(completed.Coverage), "source-nav", _ => false, _ => true, _ => "B"));
        AutoPlayRunState legacy = JsonSerializer.Deserialize<AutoPlayRunState>("{}", AutoPlayController.JsonOptions)!;
        Assert.Empty(legacy.ExhaustedSurveyInspections);
        Assert.Null(legacy.PendingExhaustedSurvey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnUnknownFingerprintOrLegacyGuardDoesNotAuthorizeAnExactResurvey(bool legacy)
    {
        var test = Create();
        if (legacy)
        {
            test.State.VisitGuards["remote-nav"] = test.State.VisitGuards["remote-nav"] with { Version = AutoPlayVisitGuard.CurrentVersion - 1 };
        }
        Assert.Null(AutoPlayController.PlanWithExhaustedSurvey(test.State, test.Planner, "source-nav", _ => false, _ => true, _ => legacy ? "B" : null));
        Assert.Null(test.State.PendingExhaustedSurvey);
    }

    private static AutoPlayPlanStep? Plan((AutoPlayRunState State, AutoPlayPlanner Planner) test, string fingerprint)
    {
        return AutoPlayController.PlanWithExhaustedSurvey(test.State, test.Planner, "source-nav", _ => false, _ => true, _ => fingerprint);
    }

    private static (AutoPlayRunState State, AutoPlayPlanner Planner) Create()
    {
        AutoPlayRunState state = new();
        AutoPlayPlanner planner = new(state.Coverage);
        AutoPlayEvent door = new("door", AutoPlayEventKind.Transition, ConditionSignature: "source-condition");
        AutoPlayEvent observed = new("observed", AutoPlayEventKind.Interaction, ConditionSignature: "remote-condition");
        planner.Observe(new("source", "region"), [door]);
        planner.Observe(new("remote", "region"), [observed]);
        planner.Completed("remote", "observed");
        planner.Completed("source", "door", "remote", observedStoryChanged: false, selections: []);
        planner.ObserveNavigation("source", "source-nav", [door]);
        planner.ObserveNavigation("remote", "remote-nav", [observed]);
        AutoPlayEventRoute route = state.Coverage.Visits.Single(visit => visit.Visit.Key == "source").Events.Single().Routes.Single();
        planner.RecordNavigationRoute("source-nav", "remote-nav", "door", route);
        AutoPlayVisitGuard guard = new(AutoPlayVisitGuard.CurrentVersion, 1, "program", ImmutableArray.Create(new NativePersistentPredicateRead("PSHM_B", 1300, 1)), "A");
        state.VisitGuards["remote-nav"] = guard;
        state.InspectedVisitsSinceProgress.Add("remote-nav");
        AutoPlayController.SeedExhaustedSurveyInspection(state, "remote-nav", guard);
        return (state, planner);
    }
}
