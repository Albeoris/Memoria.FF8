using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayPlannerTests
{
    [Fact]
    public void Observed_script_transition_becomes_a_travel_edge_even_when_destination_was_dynamic()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("room", "garden"), [new("door-action", AutoPlayEventKind.Interaction)]);
        planner.Completed("room", "door-action", "hall");
        planner.Observe(new("room", "garden"), [new("door-action", AutoPlayEventKind.Interaction)]);
        planner.Observe(new("hall", "garden"), [new("talk", AutoPlayEventKind.Interaction)]);
        AutoPlayPlanStep travel = planner.Plan("room")!;
        Assert.True(travel.IsTravel);
        Assert.Equal("door-action", travel.Event.Id);
        Assert.Equal(AutoPlayEventKind.Transition, travel.Event.Kind);
    }

    [Fact]
    public void Ordering_is_stable_and_story_changes_wait_for_all_reachable_nonstory_frontiers()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new AutoPlayVisit("garden", "garden"), [new("story", AutoPlayEventKind.Interaction, ChangesStory: true, Order: -100), new("exit", AutoPlayEventKind.Transition, "town"), new("talk-b", AutoPlayEventKind.Interaction), new("talk-a", AutoPlayEventKind.Interaction)]);
        Assert.Equal("talk-a", planner.Plan("garden")!.Event.Id);
        planner.Completed("garden", "talk-a");
        Assert.Equal("talk-b", planner.Plan("garden")!.Event.Id);
        planner.Completed("garden", "talk-b");
        Assert.Equal("exit", planner.Plan("garden")!.Event.Id);
        planner.Completed("garden", "exit");
        planner.Observe(new AutoPlayVisit("town", "town"), [new("return", AutoPlayEventKind.Transition, "garden"), new("shop", AutoPlayEventKind.Interaction)]);
        AutoPlayPlanStep travel = planner.Plan("garden")!;
        Assert.True(travel.IsTravel);
        Assert.Equal("garden", travel.VisitKey);
        Assert.Equal("exit", travel.Event.Id);
        Assert.Equal("shop", planner.Plan("town")!.Event.Id);
        planner.Completed("town", "shop");
        planner.Completed("town", "return");
        Assert.Equal("story", planner.Plan("garden")!.Event.Id);
    }

    [Fact]
    public void Current_region_frontiers_precede_other_regions_and_overrides_order_equal_region_events()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new AutoPlayVisit("hub", "garden"), [new("hall", AutoPlayEventKind.Transition, "hall"), new("town", AutoPlayEventKind.Transition, "town")]);
        planner.Completed("hub", "hall");
        planner.Completed("hub", "town");
        planner.Observe(new AutoPlayVisit("hall", "garden"), [new("z", AutoPlayEventKind.Interaction, Order: -1), new("a", AutoPlayEventKind.Interaction)]);
        planner.Observe(new AutoPlayVisit("town", "town"), [new("a", AutoPlayEventKind.Interaction, Order: -100)]);
        Assert.Equal("hall", planner.Plan("hub")!.Event.Id);
        Assert.Equal("z", planner.Plan("hall")!.Event.Id);
    }

    [Fact]
    public void Completed_story_edges_never_provide_reversible_travel_routes()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new AutoPlayVisit("before", "garden"), [new("story", AutoPlayEventKind.Transition, "after", true)]);
        planner.Completed("before", "story");
        planner.Observe(new AutoPlayVisit("after", "garden"), [new("talk", AutoPlayEventKind.Interaction)]);
        Assert.Null(planner.Plan("before"));
        Assert.Equal("talk", planner.Plan("after")!.Event.Id);
    }

    [Fact]
    public void Unavailable_and_blocked_events_do_not_form_frontiers_or_travel_routes()
    {
        AutoPlayPlanner planner = new();
        AutoPlayVisit visit = new("hub", "garden");
        planner.Observe(visit, [new("gone", AutoPlayEventKind.Interaction), new("blocked", AutoPlayEventKind.Interaction)]);
        planner.Observe(visit, [new("blocked", AutoPlayEventKind.Interaction)]);
        planner.Block("hub", "blocked", "No reachable navigation point.");
        Assert.Null(planner.Plan("hub"));
        Assert.Single(planner.State.Visits);
        Assert.Equal(2, planner.State.Visits[0].Events.Count);
    }

    [Fact]
    public void Completion_is_preserved_across_observation_and_serialization_but_not_distinct_story_states()
    {
        AutoPlayPlanner planner = new();
        AutoPlayEvent talk = new("talk", AutoPlayEventKind.Interaction);
        planner.Observe(new AutoPlayVisit("hall:0", "garden"), [talk]);
        planner.Completed("hall:0", "talk");
        string json = JsonSerializer.Serialize(planner.State);
        planner = new AutoPlayPlanner(JsonSerializer.Deserialize<AutoPlayPlannerState>(json)!);
        planner.Observe(new AutoPlayVisit("hall:0", "garden"), [talk]);
        Assert.Null(planner.Plan("hall:0"));
        planner.Observe(new AutoPlayVisit("hall:1", "garden"), [talk]);
        Assert.Equal("talk", planner.Plan("hall:1")!.Event.Id);
    }

    [Fact]
    public void Choice_options_replay_the_original_checkpoint_and_restore_primary_continuation()
    {
        AutoPlayPlanner planner = new();
        planner.ObserveChoice("greeting", 3, 2);
        Assert.Equal(AutoPlayChoiceStepKind.SaveBeforeChoice, planner.PlanChoice("greeting").Kind);
        planner.RegisterChoiceCheckpoint("greeting", "before");
        Assert.Equal(new AutoPlayChoiceStep(AutoPlayChoiceStepKind.Select, 2), planner.PlanChoice("greeting"));
        planner.BeginChoiceOption("greeting", 2);
        Assert.Throws<InvalidOperationException>(() => planner.PlanChoice("greeting"));
        Assert.Throws<ArgumentNullException>(() => planner.CompleteChoiceAtControlReturn("greeting"));
        planner.CompleteChoiceAtControlReturn("greeting", "primary");
        Assert.Equal(new AutoPlayChoiceStep(AutoPlayChoiceStepKind.Replay, 3, "before"), planner.PlanChoice("greeting"));
        planner.BeginChoiceOption("greeting", 3);
        planner.CompleteChoiceAtControlReturn("greeting");
        planner.BeginChoiceOption("greeting", 4);
        planner.CompleteChoiceAtControlReturn("greeting");
        Assert.Equal(new AutoPlayChoiceStep(AutoPlayChoiceStepKind.RestoreContinuation, Checkpoint: "primary"), planner.PlanChoice("greeting"));
        planner.FinishChoice("greeting");
        Assert.Equal(AutoPlayChoiceStepKind.Complete, planner.PlanChoice("greeting").Kind);
        Assert.Throws<InvalidOperationException>(() => planner.RegisterChoiceCheckpoint("greeting", "replacement"));
    }

    [Fact]
    public void Nested_choices_have_linear_option_coverage_and_survive_state_round_trip()
    {
        AutoPlayPlanner planner = new();
        planner.ObserveChoice("outer", 2);
        planner.RegisterChoiceCheckpoint("outer", "outer-before");
        planner.BeginChoiceOption("outer", 0);
        planner.ObserveChoice("inner", 3);
        planner.RegisterChoiceCheckpoint("inner", "inner-before");
        for (int option = 0; option < 3; option++)
        {
            planner.BeginChoiceOption("inner", option);
            planner.CompleteChoiceAtControlReturn("inner", option == 0 ? "inner-primary" : null);
        }
        planner.FinishChoice("inner");
        planner.CompleteChoiceAtControlReturn("outer", "outer-primary");
        planner = new AutoPlayPlanner(JsonSerializer.Deserialize<AutoPlayPlannerState>(JsonSerializer.Serialize(planner.State))!);
        planner.BeginChoiceOption("outer", 1);
        planner.ObserveChoice("inner", 3);
        Assert.Equal(AutoPlayChoiceStepKind.Complete, planner.PlanChoice("inner").Kind);
        planner.CompleteChoiceAtControlReturn("outer");
        Assert.Equal("outer-primary", planner.PlanChoice("outer").Checkpoint);
        planner.FinishChoice("outer");
        Assert.Equal(5, planner.State.Choices.Sum(choice => choice.CompletedOptions.Count));
    }

    [Fact]
    public void Changed_choice_shape_and_premature_completion_fail_fast()
    {
        AutoPlayPlanner planner = new();
        planner.ObserveChoice("choice", 2);
        Assert.Throws<InvalidOperationException>(() => planner.ObserveChoice("choice", 3));
        Assert.Throws<InvalidOperationException>(() => planner.FinishChoice("choice"));
        Assert.Throws<InvalidOperationException>(() => planner.BeginChoiceOption("choice", 1));
    }
}
