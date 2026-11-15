using System.Text.Json;
using Memoria.AutoPlay;

internal static class AutoPlayPlannerTests
{
    public static void Run()
    {
        AutoPlayPlanner<Deferral> planner = new();
        planner.Observe(new("lobby", "region"), [new("exit", AutoPlayEventKind.Transition, ConditionSignature: "entry")]);
        planner.ObserveNavigation("lobby", "lobby-component", [new("exit", AutoPlayEventKind.Transition, ConditionSignature: "entry")]);
        planner.BeginEvent("lobby", "exit");
        planner.Completed("lobby", "exit", "annex", observedStoryChanged: false);
        planner.Observe(new("annex", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        planner.ObserveNavigation("annex", "annex-component", [new("talk", AutoPlayEventKind.Interaction)]);
        AutoPlayEventRoute route = planner.State.Visits[0].Events[0].Routes.Single();
        planner.RecordNavigationRoute("lobby-component", "annex-component", "exit", route);
        Deferral deferral = new("talk", "predicate", "guard", [], Receipt: "game-owned-payload");
        Check(planner.DeferParticipantAction("annex", "talk", deferral), "The first participant deferral must be recorded.");
        Check(!planner.DeferParticipantAction("annex", "talk", deferral), "An identical participant deferral must not be recorded twice.");
        planner.ObserveChoice("answer", 2);
        planner.State.Choices.Single().BeforeContext = new() { Visit = "annex", NavigationSource = new("annex-component", "talk", "predicate", "talk"), SceneChoices = ["prior-choice"] };
        string json = JsonSerializer.Serialize(planner.State);
        planner = new(JsonSerializer.Deserialize<AutoPlayPlannerState<Deferral>>(json)!);
        Check(JsonSerializer.Serialize(planner.State) == json, "Planner restoration changed its serialized state.");
        Check(planner.State.Visits[1].Events[0].ParticipantPolicyDeferrals.Single().Receipt == "game-owned-payload", "Planner restoration discarded a concrete game-owned deferral payload.");
        Check(planner.State.Choices.Single().BeforeContext?.SceneChoices.Single() == "prior-choice", "Planner restoration discarded a choice input context.");
        AutoPlayPlanStep? next = planner.PlanNavigation("lobby-component");
        Check(next is { IsTravel: true, Event.Id: "exit", Route.DestinationVisitKey: "annex" }, "Restored navigation did not travel to its reachable gameplay frontier.");
        Check(planner.ReachableNavigationFrontierCount("lobby-component") == 1, "A recorded travel edge must not count as an unconsumed gameplay frontier.");
        Check(planner.PlanChoice("answer").Kind == AutoPlayChoiceStepKind.SaveBeforeChoice, "An unexplored choice requires its original checkpoint.");
        planner.RegisterChoiceCheckpoint("answer", "before");
        planner.BeginChoiceOption("answer", 0);
        planner.CompleteChoiceAtControlReturn("answer", "continuation");
        Check(planner.PlanChoice("answer") is { Kind: AutoPlayChoiceStepKind.Replay, Selection: 1, Checkpoint: "before" }, "Choice coverage must replay the remaining alternative from its original checkpoint.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    public sealed record Deferral(string TargetId, string SourceConditionSignature, string GuardFingerprint, IReadOnlyList<AutoPlayChoiceSelection> Selections, string? ChoiceKey = null, int FirstOption = 0, int OptionCount = 0, string? Receipt = null) : IAutoPlayParticipantPolicyDeferral;
}
