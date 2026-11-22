using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeComponentAttemptPriorityTests
{
    private sealed record Fixture(Ff7AutoPlayState State, AutoPlayPlanner<Ff7AutoPlayDeferral> Planner, AutoPlayEvent Story);

    internal static void Run()
    {
        Fixture fresh = Create();
        string history = JsonSerializer.Serialize(fresh.State.Coverage);
        AutoPlayEventAvailability Read(AutoPlayNavigationVisit navigation, AutoPlayEvent target) => Ff7AutoPlayPolicy.ReadComponentAvailability(fresh.State, navigation, target, Facts());
        AutoPlayNavigationVisit upper = fresh.State.Coverage.NavigationVisits.Single(visit => visit.Key == "upper");
        Check(Read(upper, fresh.Story).AttemptedConditions is { Count: 0 }, "A lower-component refusal was projected into the upper component's attempt metadata.");
        Check(fresh.Planner.PlanNavigation("lower", readAvailability: Read) is { IsTravel: true, Event.Id: "bridge", Goal.EventId: "story" }, "A fresh upper-component story source lost priority because the same condition was attempted below.");
        Check(history == JsonSerializer.Serialize(fresh.State.Coverage), "Component-aware priority planning altered physical failure or route history.");
        AutoPlayEventAvailability Unknown(AutoPlayNavigationVisit navigation, AutoPlayEvent target) => Read(navigation, target) with { AttemptedConditions = null };
        Check(fresh.Planner.PlanNavigation("lower", readAvailability: Unknown) is { IsTravel: true, Goal.EventId: "remote" }, "Absent optional component metadata stopped conservatively retaining aggregate attempts.");
        Fixture attempted = Create(upperAttempt: true);
        AutoPlayEventAvailability ReadAttempted(AutoPlayNavigationVisit navigation, AutoPlayEvent target) => Ff7AutoPlayPolicy.ReadComponentAvailability(attempted.State, navigation, target, Facts());
        Check(attempted.Planner.PlanNavigation("lower", readAvailability: ReadAttempted) is { IsTravel: true, Goal.EventId: "remote" }, "An actual same-component story attempt escaped normal fairness demotion.");
        Fixture consumed = Create();
        consumed.Planner.Completed("here", "story", observedStoryChanged: true, entrySignature: "new", entrySignatureAuthoritative: true);
        AutoPlayEventAvailability ReadConsumed(AutoPlayNavigationVisit navigation, AutoPlayEvent target) => Ff7AutoPlayPolicy.ReadComponentAvailability(consumed.State, navigation, target, Facts());
        string consumedHistory = JsonSerializer.Serialize(consumed.State.Coverage);
        Check(consumed.Planner.PlanNavigation("lower", readAvailability: ReadConsumed) is { IsTravel: true, Goal.EventId: "remote" }, "Component attempt projection rearmed a consumed story condition.");
        Check(consumedHistory == JsonSerializer.Serialize(consumed.State.Coverage), "Consumed-condition planning changed the durable ledger.");
        Console.WriteLine("Component-scoped attempt metadata preserves fresh story priority across known local bridges without changing history.");
    }

    private static Dictionary<string, string> Facts() => new() { ["lower-failure"] = "updated-lower", ["upper-failure"] = "updated-upper" };

    private static Fixture Create(bool upperAttempt = false)
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent story = new("story", AutoPlayEventKind.Interaction, ChangesStory: true, Order: 99, ConditionSignature: "old");
        AutoPlayEvent bridge = new("bridge", AutoPlayEventKind.Transition, ConditionSignature: "bridge-condition");
        AutoPlayEvent exit = new("exit", AutoPlayEventKind.Transition, ConditionSignature: "exit-condition");
        AutoPlayEvent remote = new("remote", AutoPlayEventKind.Interaction, ConditionSignature: "remote-condition");
        planner.Observe(new("here", "program"), [story, bridge, exit]);
        planner.Observe(new("there", "remote-program"), [remote]);
        planner.ObserveNavigation("here", "lower", [bridge, exit]);
        planner.ObserveNavigation("here", "upper", [story]);
        planner.ObserveNavigation("there", "remote-nav", [remote]);
        planner.Completed("here", "story", observedStoryChanged: true, entrySignature: "old", entrySignatureAuthoritative: true);
        planner.Completed("here", "bridge", "here", observedStoryChanged: false, entrySignature: bridge.ConditionSignature, entrySignatureAuthoritative: true, targetId: bridge.Id);
        planner.Completed("here", "exit", "there", observedStoryChanged: false, entrySignature: exit.ConditionSignature, entrySignatureAuthoritative: true, targetId: exit.Id);
        var here = state.Coverage.Visits.Single(visit => visit.Visit.Key == "here");
        planner.RecordNavigationRoute("lower", "upper", bridge.Id, here.Events.Single(item => item.Event.Id == bridge.Id).Routes.Single());
        planner.RecordNavigationRoute("lower", "remote-nav", exit.Id, here.Events.Single(item => item.Event.Id == exit.Id).Routes.Single());
        story = story with { ConditionSignature = "new" };
        planner.Observe(new("here", "program"), [story, bridge, exit]);
        planner.ObserveNavigation("here", "upper", [story]);
        var coverage = here.Events.Single(item => item.Event.Id == story.Id);
        state.NavigationGuards["lower-failure"] = new("here", 29, EventId: story.Id, NavigationKey: "lower");
        coverage.UnavailableAttempts.Add(new("new", "here", "lower-failure", "physical", story.Id));
        if (upperAttempt)
        {
            state.NavigationGuards["upper-failure"] = new("here", 29, EventId: story.Id, NavigationKey: "upper");
            coverage.UnavailableAttempts.Add(new("new", "here", "upper-failure", "physical", story.Id));
        }
        return new(state, planner, story);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
