using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayOrdinaryChoiceScopeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OrdinaryChoiceCheckpointsAndCompletionSurviveUnrelatedAncestorPrefixChanges(bool input, bool finished)
    {
        GameSession session = GameSession.CreateColdProcess();
        GameState nativeState = session.State;
        int worldLookups = 0;
        AutoPlayController controller = new(session, () =>
        {
            worldLookups++;
            throw new InvalidOperationException("An ordinary choice without participant-policy receipts cannot require a native allocation.");
        }, _ => throw new InvalidOperationException("Metadata refresh cannot restore gameplay."), Path.GetTempPath());
        AutoPlayPlanner planner = new(controller.State.Coverage);
        planner.Observe(new("600:0", "region"), [new("600:line:1", AutoPlayEventKind.Interaction, ConditionSignature: "source", Variants: ["600:line:1:0:0", "600:line:1:0:1"])]);
        string key = input ? "input:600:original-query" : "600:0:1:3:10:5:0:1";
        planner.ObserveChoice(key, 2);
        planner.RegisterChoiceCheckpoint(key, "before-choice.json");
        AutoPlayChoiceCoverage choice = Assert.Single(planner.State.Choices);
        choice.InputCheckpointPrepared = input;
        choice.BeforeSelections = [new("ancestor", 0)];
        planner.BeginChoiceOption(key, 0);
        planner.CompleteChoiceAtControlReturn(key, "actual-control-return.json");
        if (finished)
        {
            planner.BeginChoiceOption(key, 1);
            planner.CompleteChoiceAtControlReturn(key);
            planner.FinishChoice(key);
        }
        controller.State.ActiveVisit = "600:0";
        controller.State.ActiveEvent = "600:line:1";
        controller.State.ActiveConditionSignature = "source";
        string before = JsonSerializer.Serialize(choice, AutoPlayController.JsonOptions);
        MethodInfo refresh = typeof(AutoPlayController).GetMethod("RefreshParticipantChoice", BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (int prefix in new[] { 0, 1, 2 })
        {
            controller.State.ActiveTargetId = $"600:line:1:0:{prefix % 2}";
            controller.State.ActiveSelections = [new("ancestor", prefix)];
            refresh.Invoke(controller, [choice]);

            Assert.Equal(before, JsonSerializer.Serialize(choice, AutoPlayController.JsonOptions));
            Assert.Equal(finished ? AutoPlayChoiceStepKind.Complete : AutoPlayChoiceStepKind.Replay, planner.PlanChoice(key).Kind);
            Assert.Equal(finished, choice.Finished);
            Assert.False(choice.PolicyExhausted);
            Assert.Null(choice.PolicyScope);
            Assert.Empty(choice.PolicyCheckpoints);
            Assert.Empty(choice.PolicyDeferredOptions);
        }
        Assert.Equal(0, worldLookups);
        Assert.Same(nativeState, session.State);
        Assert.Empty(controller.State.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.ParticipantPolicyDeferrals));
    }

    [Fact]
    public void RealControlReturnsForEveryOptionRestoreTheContinuationAndFinishWithoutPolicyExhaustion()
    {
        AutoPlayPlanner planner = new();
        planner.ObserveChoice("ordinary", 2);
        planner.RegisterChoiceCheckpoint("ordinary", "before-choice.json");
        planner.BeginChoiceOption("ordinary", 0);
        planner.CompleteChoiceAtControlReturn("ordinary", "actual-control-return.json");
        planner.BeginChoiceOption("ordinary", 1);
        planner.CompleteChoiceAtControlReturn("ordinary");

        planner.RefreshParticipantChoice("ordinary", "unchanged-participant", []);

        AutoPlayChoiceCoverage choice = Assert.Single(planner.State.Choices);
        Assert.False(choice.PolicyExhausted);
        Assert.Empty(choice.PolicyDeferredOptions);
        Assert.Equal(new[] { 0, 1 }, choice.CompletedOptions);
        Assert.Equal(new AutoPlayChoiceStep(AutoPlayChoiceStepKind.RestoreContinuation, Checkpoint: "actual-control-return.json"), planner.PlanChoice("ordinary"));
        planner.FinishChoice("ordinary");
        Assert.True(choice.Finished);
        Assert.False(choice.PolicyExhausted);
        Assert.Equal(AutoPlayChoiceStepKind.Complete, planner.PlanChoice("ordinary").Kind);
        Assert.Equal("before-choice.json", choice.BeforeCheckpoint);
        Assert.Equal("actual-control-return.json", choice.ContinuationCheckpoint);
    }
}
