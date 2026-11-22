using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeRearmedExplorationPriorityTests
{
    private sealed record Fixture(Ff7AutoPlayState State, AutoPlayPlanner<Ff7AutoPlayDeferral> Planner, AutoPlayEventCoverage<Ff7AutoPlayDeferral> Npc);

    internal static void Run()
    {
        Fixture fresh = Create("new");
        Check(fresh.Npc.Completed && fresh.Npc.Conditions!.ConsumedEntries.SetEquals(["old"]), "The authored NPC lost its historical condition.");
        AssertPlan(fresh, "npc", "A known source's genuinely new condition lost priority to a new lower-order exit.");
        Fixture attempted = Create("new");
        attempted.Npc.UnavailableAttempts.Add(new("new", "visit", "guard", "physical", "npc"));
        AssertPlan(attempted, "exit", "An attempted condition bypassed existing fairness demotion.");
        Fixture consumed = Create("new");
        consumed.Planner.Completed("visit", "npc", entrySignature: "new", entrySignatureAuthoritative: true);
        AssertPlan(consumed, "exit", "A consumed condition retained fresh exploration priority.");
        AssertPlan(Create("old"), "exit", "The same executed condition was treated as newly rearmed.");
        Fixture legacy = Create("new");
        legacy.Npc.Conditions = null;
        AssertPlan(legacy, "exit", "Legacy completion without condition history acquired novelty priority.");
        Fixture empty = Create("new");
        empty.Npc.Conditions!.ConsumedEntries.Clear();
        AssertPlan(empty, "exit", "An empty ledger acquired novelty priority without a consumed source.");
        AssertPlan(Create(null), "exit", "An absent authoritative current condition acquired novelty priority.");
        Fixture localRoute = Create("new", localRouteCompetitor: true);
        Check(localRoute.State.Coverage.Visits.Single().Events.Single(item => item.Event.Id == "local-route").Routes.Count == 1, "The competitor lost its real previously recorded local navigation route.");
        AssertPlan(localRoute, "npc", "A rearmed known local travel producer preempted the fresh nontravel NPC branch.");
        Console.WriteLine("Rearmed known nonstory sources outrank new exits while attempted, consumed and unknown conditions retain fairness.");
    }

    private static Fixture Create(string? current, bool localRouteCompetitor = false)
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent npc = new("npc", AutoPlayEventKind.Interaction, Order: 99, ConditionSignature: "old");
        AutoPlayEvent exit = new("exit", AutoPlayEventKind.Transition, Order: 0, ConditionSignature: "exit-condition");
        planner.Observe(new("visit", "program"), [npc, exit]);
        planner.ObserveNavigation("visit", "navigation", [npc, exit]);
        planner.Completed("visit", npc.Id, entrySignature: "old", entrySignatureAuthoritative: true);
        npc = npc with { ConditionSignature = current };
        planner.Observe(new("visit", "program"), [npc, exit]);
        planner.ObserveNavigation("visit", "navigation", [npc, exit]);
        if (localRouteCompetitor)
        {
            AutoPlayEvent route = new("local-route", AutoPlayEventKind.Interaction, Order: 0, ConditionSignature: "route-old");
            planner.Observe(new("visit", "program"), [npc, exit, route]);
            planner.ObserveNavigation("visit", "navigation", [npc, exit, route]);
            planner.Completed("visit", route.Id, "visit", observedStoryChanged: false, entrySignature: "route-old", entrySignatureAuthoritative: true, targetId: route.Id);
            AutoPlayEventRoute recorded = state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == route.Id).Routes.Single();
            planner.RecordNavigationRoute("navigation", "navigation", route.Id, recorded);
            route = route with { ConditionSignature = "route-new" };
            planner.Observe(new("visit", "program"), [npc, exit, route]);
            planner.ObserveNavigation("visit", "navigation", [npc, exit, route]);
        }
        return new(state, planner, state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == npc.Id));
    }

    private static void AssertPlan(Fixture source, string expected, string failure)
    {
        string before = JsonSerializer.Serialize(source.State.Coverage);
        if (source.Planner.PlanNavigation("navigation") is not { IsTravel: false } selected || selected.Event.Id != expected)
        {
            throw new InvalidOperationException(failure);
        }
        Check(before == JsonSerializer.Serialize(source.State.Coverage), "Exploration ranking changed consumed conditions, attempts or durable history.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
