using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayParticipantPolicyPlannerTests
{
    [Fact]
    public void ADeferredTargetDoesNotCompleteOrSuppressAnotherActualGeometryVariant()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("field:1", "region"), [new("entry", AutoPlayEventKind.Interaction, ConditionSignature: "source", Variants: ["left", "right"]), new("other", AutoPlayEventKind.Interaction)]);
        planner.DeferParticipantAction("field:1", "entry", Deferral("left", []));
        planner.RefreshParticipantPolicy("field:1", _ => true);
        AutoPlayEventCoverage entry = planner.State.Visits[0].Events.Single(item => item.Event.Id == "entry");

        Assert.False(entry.PolicyDeferred);
        Assert.False(entry.Completed);
        Assert.False(entry.Unavailable);
        Assert.Empty(entry.UnavailableAttempts);
        Assert.Equal(new[] { "left" }, entry.PolicyDeferredVariants);
        Assert.Equal("entry", planner.Plan("field:1")!.Event.Id);

        planner.DeferParticipantAction("field:1", "entry", Deferral("right", []));
        planner.RefreshParticipantPolicy("field:1", _ => true);
        Assert.True(entry.PolicyDeferred);
        Assert.False(entry.Completed);
        Assert.Equal("other", planner.Plan("field:1")!.Event.Id);
        planner.RefreshParticipantPolicy("field:1", _ => false);
        Assert.False(entry.PolicyDeferred);
        Assert.Empty(entry.PolicyDeferredVariants);
        Assert.Equal("entry", planner.Plan("field:1")!.Event.Id);
    }

    [Fact]
    public void ADeferredOptionLeavesAnotherOptionAvailableAndOnlyRealControlReturnEarnsCredit()
    {
        AutoPlayPlanner planner = new();
        planner.ObserveChoice("choice", 2);
        planner.RegisterChoiceCheckpoint("choice", "before.json");
        planner.RefreshParticipantChoice("choice", "party-a", [0]);

        Assert.Equal(1, planner.PlanChoice("choice").Selection);
        planner.BeginChoiceOption("choice", 1);
        planner.CompleteChoiceAtControlReturn("choice", "actual-return.json");
        Assert.Equal(AutoPlayChoiceStepKind.RestoreContinuation, planner.PlanChoice("choice").Kind);
        planner.FinishChoice("choice");
        AutoPlayChoiceCoverage choice = Assert.Single(planner.State.Choices);
        Assert.Equal(new[] { 1 }, choice.CompletedOptions);
        Assert.Equal(new[] { 0 }, choice.PolicyDeferredOptions);
        Assert.True(choice.PolicyExhausted);
        Assert.False(choice.Finished);
        Assert.Equal("actual-return.json", choice.ContinuationCheckpoint);
    }

    [Fact]
    public void ChangedPolicyGuardsReopenOptionsAndPreserveEachAuthenticCheckpoint()
    {
        AutoPlayPlanner planner = new();
        planner.ObserveChoice("input", 2);
        planner.RefreshParticipantChoice("input", "party-a", []);
        planner.RegisterChoiceCheckpoint("input", "party-a-before.json");
        planner.RefreshParticipantChoice("input", "party-a", [0, 1]);
        AutoPlayChoiceCoverage choice = Assert.Single(planner.State.Choices);
        choice.PolicyExhausted = false;
        Assert.Equal(AutoPlayChoiceStepKind.PolicyDeferred, planner.PlanChoice("input").Kind);
        planner.FinishPolicyDeferredChoice("input");

        planner.RefreshParticipantChoice("input", "party-b", []);

        Assert.False(choice.PolicyExhausted);
        Assert.False(choice.Finished);
        Assert.Empty(choice.CompletedOptions);
        Assert.Empty(choice.PolicyDeferredOptions);
        Assert.Equal("party-a-before.json", choice.PolicyCheckpoints["party-a"].BeforeCheckpoint);
        Assert.Equal(AutoPlayChoiceStepKind.SaveBeforeChoice, planner.PlanChoice("input").Kind);
        planner.RegisterChoiceCheckpoint("input", "party-b-before.json");
        planner.RefreshParticipantChoice("input", "party-a", [0]);
        Assert.Equal("party-a-before.json", choice.BeforeCheckpoint);
        Assert.Equal("party-b-before.json", choice.PolicyCheckpoints["party-b"].BeforeCheckpoint);
        Assert.Equal(1, planner.PlanChoice("input").Selection);
    }

    [Fact]
    public void OneObservedChoicePrefixDoesNotDeferUnobservedOptionsOrDifferentPrefixes()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("field:1", "region"), [new("entry", AutoPlayEventKind.Interaction, ConditionSignature: "source", Variants: ["target"])]);
        planner.DeferParticipantAction("field:1", "entry", Deferral("target", [new("choice", 0)], "choice", 2));
        planner.RefreshParticipantPolicy("field:1", _ => true);
        AutoPlayEventCoverage entry = planner.State.Visits[0].Events[0];
        Assert.False(entry.PolicyDeferred);
        planner.DeferParticipantAction("field:1", "entry", Deferral("target", [new("ancestor", 1), new("choice", 1)], "choice", 2));
        planner.RefreshParticipantPolicy("field:1", _ => true);
        Assert.False(entry.PolicyDeferred);
        planner.DeferParticipantAction("field:1", "entry", Deferral("target", [new("choice", 1)], "choice", 2));
        planner.RefreshParticipantPolicy("field:1", _ => true);
        Assert.True(entry.PolicyDeferred);
        Assert.False(entry.Completed);
        Assert.Equal(3, entry.ParticipantPolicyDeferrals.Count);
    }

    private static AutoPlayParticipantPolicyDeferral Deferral(string target, IReadOnlyList<AutoPlayChoiceSelection> selections, string? choice = null, int count = 0)
    {
        AutoPlayParticipantPolicyGuard guard = new(1, 600, "program", [], "party-a-guard", [0, 1, 2], [0, 1, 2], [8, 9, 10]);
        NativePartyActionRequest request = new(600, "program", 1, 3, 4, 5, 1, 0, 1, 6, 0, 8, 0, 5, 6, 1, 6, 20);
        return new(target, "source", guard, selections, request, choice, 0, count);
    }
}
