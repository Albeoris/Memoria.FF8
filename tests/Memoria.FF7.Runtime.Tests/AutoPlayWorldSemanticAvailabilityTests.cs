using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldSemanticAvailabilityTests
{
    public static void Run()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent entry = new("mesh:0:0:script:7", AutoPlayEventKind.Transition, ConditionSignature: "predicate:geometry:model");
        planner.Observe(new("world", "resource"), [entry]);
        planner.ObserveNavigation("world", "world-nav", [entry]);
        planner.Completed("world", entry.Id, entrySignature: entry.ConditionSignature, entrySignatureAuthoritative: true);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single().Events.Single();
        coverage.CompletedAvailabilityFingerprints.Add("legacy-pose");
        string pose = "first-pose";
        AutoPlayEventAvailability Read(AutoPlayNavigationVisit navigation, AutoPlayEvent observed)
        {
            if (navigation.VisitKey != "world")
            {
                return null!;
            }
            return Ff7AutoPlayPolicy.ReadWorldSemanticAvailability(coverage, observed, new(false, [], pose));
        }
        string before = JsonSerializer.Serialize(state.Coverage);
        Check(planner.PlanNavigation("world-nav", readAvailability: Read) is null, "A consumed world entry reopened at its first physical pose.");
        pose = "second-pose";
        Check(planner.PlanNavigation("world-nav", readAvailability: Read) is null, "A consumed world entry reopened solely because its physical pose changed.");
        Check(JsonSerializer.Serialize(state.Coverage) == before, "Reading semantic completion changed historical execution or availability coverage.");
        foreach (string condition in new[] { "changed-predicate:geometry:model", "predicate:changed-geometry:model", "predicate:geometry:changed-model" })
        {
            AutoPlayEvent changed = entry with { ConditionSignature = condition };
            planner.Observe(new("world", "resource"), [changed]);
            planner.ObserveNavigation("world", "world-nav", [changed]);
            Check(planner.PlanNavigation("world-nav", readAvailability: Read)?.Event.Id == entry.Id, "A changed native predicate, geometry or model source lost its unexplored frontier.");
        }
        planner.Observe(new("world", "resource"), [entry]);
        planner.ObserveNavigation("world", "world-nav", [entry]);
        AutoPlayEvent remote = new("field-frontier", AutoPlayEventKind.Interaction, ConditionSignature: "field-entry");
        planner.Observe(new("field", "field-resource"), [remote]);
        planner.ObserveNavigation("field", "field-nav", [remote]);
        planner.Completed("world", entry.Id, "field", observedStoryChanged: false, selections: [], entrySignature: entry.ConditionSignature, entrySignatureAuthoritative: true);
        AutoPlayEventRoute route = coverage.Routes.Single();
        planner.RecordNavigationRoute("world-nav", "field-nav", entry.Id, route);
        Check(planner.PlanNavigation("world-nav", readAvailability: Read) is { IsTravel: true, Goal.EventId: "field-frontier" }, "Suppressing pose completion prevented authenticated native travel to a real frontier.");
        planner.MarkUnavailable("world", entry.Id, new(entry.ConditionSignature!, "world", "physical", "blocked-pose", entry.Id));
        string guard = "blocked-pose";
        AutoPlayEventAvailability FailureRead(AutoPlayNavigationVisit navigation, AutoPlayEvent observed)
        {
            return navigation.VisitKey == "world" ? Ff7AutoPlayPolicy.ReadWorldSemanticAvailability(coverage, observed, AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(coverage, new Dictionary<string, string> { ["physical"] = guard })) : null!;
        }
        Check(planner.PlanNavigation("world-nav", readAvailability: FailureRead) is null, "Semantic completion bypassed a matching native physical failure.");
        guard = "changed-native-facts";
        Check(planner.PlanNavigation("world-nav", readAvailability: FailureRead) is { IsTravel: true }, "A changed native physical proof did not restore authenticated travel.");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> unexplored = new() { Event = entry, Available = true };
        unexplored.UnavailableAttempts.Add(new(entry.ConditionSignature!, "world", "approach", "original-facts", entry.Id));
        var blocked = Ff7AutoPlayPolicy.ReadWorldSemanticAvailability(unexplored, entry, AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(unexplored, new Dictionary<string, string> { ["approach"] = "original-facts" }));
        Check(blocked.Unavailable, "A failed unexplored approach ignored its unchanged native proof.");
        var retry = Ff7AutoPlayPolicy.ReadWorldSemanticAvailability(unexplored, entry, AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(unexplored, new Dictionary<string, string> { ["approach"] = "changed-native-facts" }));
        Check(!retry.Unavailable && retry.Fingerprint == entry.ConditionSignature, "An unexplored entry lost its semantic retry identity after a failed approach changed.");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> legacy = new() { Event = entry, Completed = true };
        Check(Ff7AutoPlayPolicy.ReadWorldSemanticAvailability(legacy, entry, new(false, [], "legacy-position")).Fingerprint is null, "An older completed observation requires new completion metadata.");
        legacy.ObservedConditionSignature = "older-native-context";
        Check(Ff7AutoPlayPolicy.ReadWorldSemanticAvailability(legacy, entry, new(false, [], "position")).Fingerprint == entry.ConditionSignature, "A recorded change in a legacy native context was hidden.");
        CheckStaleSelection(state, coverage, entry, route);
    }

    private static void CheckStaleSelection(Ff7AutoPlayState state, AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage, AutoPlayEvent entry, AutoPlayEventRoute route)
    {
        Ff7AutoPlayActiveEvent active = new("world", entry.Id, entry.ConditionSignature!, 1, -1, 7, Ff7AutoPlayTargetKind.WorldMesh, null, 1, "native-witness", NavigationKey: "world-nav");
        string before = JsonSerializer.Serialize(state.Coverage);
        state.Active = active with { Accepted = true };
        Check(!Ff7AutoPlayPolicy.RetireConsumedWorldFrontier(state), "An accepted native dispatch was canceled as a stale local frontier.");
        state.Active = active with { TravelRoute = route };
        Check(!Ff7AutoPlayPolicy.RetireConsumedWorldFrontier(state), "An authenticated travel step was canceled as a stale local frontier.");
        state.Active = active;
        state.NavigationGoal = new("field-nav", "field-frontier");
        Check(!Ff7AutoPlayPolicy.RetireConsumedWorldFrontier(state), "An explicit travel goal was canceled as a stale local frontier.");
        state.NavigationGoal = null;
        coverage.RouteNeedsRediscovery = true;
        Check(!Ff7AutoPlayPolicy.RetireConsumedWorldFrontier(state), "An explicit native route rediscovery was canceled.");
        coverage.RouteNeedsRediscovery = false;
        state.Choice = new("native-choice", active);
        Check(!Ff7AutoPlayPolicy.RetireConsumedWorldFrontier(state), "A native choice continuation was canceled as a completed frontier.");
        state.Choice = null;
        Check(Ff7AutoPlayPolicy.RetireConsumedWorldFrontier(state) && state.Active is null, "A restored unaccepted local selection survived its already consumed native entry.");
        Check(JsonSerializer.Serialize(state.Coverage) == before, "Retiring disposable selection bookkeeping erased native execution history.");
        Ff7AutoPlayState cold = state.Clone();
        Check(cold.Coverage.Visits.Single(visit => visit.Visit.Key == "world").Events.Single().CompletedAvailabilityFingerprints.Contains("legacy-pose"), "Cold compatibility discarded existing completion evidence.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
