using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeRearmedStoryPriorityTests
{
    private sealed record Fixture(Ff7AutoPlayState State, AutoPlayPlanner<Ff7AutoPlayDeferral> Planner, AutoPlayEvent Story);

    internal static void Run()
    {
        Fixture rearmed = Create();
        var storyCoverage = rearmed.State.Coverage.Visits.Single(visit => visit.Visit.Key == "here").Events.Single(item => item.Event.Id == "story");
        Check(storyCoverage.Completed && storyCoverage.Conditions!.ConsumedEntries.SetEquals(["old"]) && !storyCoverage.Conditions.HasExecutedEntry("new"), "The regression fixture lost historical completion or fabricated execution of the newly observed condition.");
        string history = JsonSerializer.Serialize(rearmed.State.Coverage);
        AutoPlayPlanStep? selected = rearmed.Planner.PlanNavigation("here-nav");
        Check(selected is { IsTravel: false, Event.Id: "story", Event.ConditionSignature: "new" }, "A freshly rearmed local story condition lost priority to an unrelated remote ordinary frontier.");
        Check(history == JsonSerializer.Serialize(rearmed.State.Coverage), "Planning credited story execution or changed existing route history.");
        storyCoverage.UnavailableAttempts.Add(new("new", "here", "guard", "physical", "story"));
        Check(rearmed.Planner.PlanNavigation("here-nav") is { IsTravel: true, Goal.EventId: "remote" }, "An attempted story condition bypassed the existing failure/fairness demotion.");

        Fixture consumed = Create();
        consumed.Planner.Completed("here", "story", observedStoryChanged: true, entrySignature: "new", entrySignatureAuthoritative: true);
        string consumedHistory = JsonSerializer.Serialize(consumed.State.Coverage);
        Check(consumed.Planner.PlanNavigation("here-nav") is { IsTravel: true, Goal.EventId: "remote" }, "A consumed current story condition retained fresh-story priority.");
        Check(consumedHistory == JsonSerializer.Serialize(consumed.State.Coverage), "Consumed-story selection changed the ledger.");

        Ff7Progress progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [], FieldRandomIndex = 7, FieldRandomStride = 1 };
        FieldHostRead[] reads = [new(FieldHostReadKind.Random)];
        var beforeRandom = Ff7AutoPlayDiscovery.ReadHostConditions(reads, progress, new(), nativeRandomDomain: "native-domain-a");
        var afterRandom = Ff7AutoPlayDiscovery.ReadHostConditions(reads, progress with { FieldRandomIndex = 8, FieldRandomStride = 2 }, new(), nativeRandomDomain: "native-domain-b");
        Check(beforeRandom.Complete && afterRandom.Complete && beforeRandom.Fingerprint == afterRandom.Fingerprint, "RNG-only execution metadata changed intrinsic condition novelty.");
        Fixture rngOnly = Create(rearmed: false, oldCondition: beforeRandom.Fingerprint);
        string rngHistory = JsonSerializer.Serialize(rngOnly.State.Coverage);
        Check(rngOnly.Planner.PlanNavigation("here-nav") is { IsTravel: true, Goal.EventId: "remote" }, "RNG-only metadata rearmed an already consumed story source.");
        Check(rngHistory == JsonSerializer.Serialize(rngOnly.State.Coverage), "RNG-only selection rewrote consumed story or route history.");
        Console.WriteLine("Fresh local story conditions retain priority while consumed, attempted and RNG-only conditions preserve existing fairness.");
    }

    private static Fixture Create(bool rearmed = true, string oldCondition = "old")
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent story = new("story", AutoPlayEventKind.Interaction, ChangesStory: true, Order: 99, ConditionSignature: oldCondition);
        AutoPlayEvent exit = new("exit", AutoPlayEventKind.Transition, ConditionSignature: "route");
        AutoPlayEvent remote = new("remote", AutoPlayEventKind.Interaction, ConditionSignature: "remote-condition");
        planner.Observe(new("here", "program-here"), [story, exit]);
        planner.Observe(new("there", "program-there"), [remote]);
        planner.ObserveNavigation("here", "here-nav", [story, exit]);
        planner.ObserveNavigation("there", "there-nav", [remote]);
        planner.Completed("here", story.Id, observedStoryChanged: true, entrySignature: oldCondition, entrySignatureAuthoritative: true);
        planner.Completed("here", exit.Id, "there", observedStoryChanged: false, entrySignature: exit.ConditionSignature, entrySignatureAuthoritative: true, targetId: exit.Id);
        var route = state.Coverage.Visits.Single(visit => visit.Visit.Key == "here").Events.Single(item => item.Event.Id == exit.Id).Routes.Single();
        planner.RecordNavigationRoute("here-nav", "there-nav", exit.Id, route);
        story = story with { ConditionSignature = rearmed ? "new" : oldCondition };
        planner.Observe(new("here", "program-here"), [story, exit]);
        planner.ObserveNavigation("here", "here-nav", [story, exit]);
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
