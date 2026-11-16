using System.Text.Json;
using Memoria.AutoPlay;
using Deferral = AutoPlayPlannerTests.Deferral;

internal static class AutoPlayComponentAvailabilityTests
{
    public static void Run()
    {
        AutoPlayPlanner<Deferral> planner = new();
        AutoPlayEvent ladder = new("ladder", AutoPlayEventKind.Interaction, ConditionSignature: "entry");
        planner.Observe(new("field", "resource"), [ladder]);
        planner.ObserveNavigation("field", "current", [ladder]);
        planner.ObserveNavigation("field", "remote", [ladder]);
        planner.MarkUnavailable("field", ladder.Id, new("entry", "field", "physical-guard", "blocked", ladder.Id));
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical-guard"] = "current-free" });
        planner.Completed("field", ladder.Id, "field", observedStoryChanged: false, entrySignature: "entry", entrySignatureAuthoritative: true, targetId: ladder.Id);
        AutoPlayEventCoverage<Deferral> coverage = planner.State.Visits.Single().Events.Single();
        planner.RecordNavigationRoute("current", "remote", ladder.Id, coverage.Routes.Single());
        string original = JsonSerializer.Serialize(planner.State);
        AutoPlayPlanStep? legacy = planner.PlanNavigation("current");
        Check(legacy is not null, "The fixture did not expose a remote connectivity probe under aggregate current-position availability.");
        Check(JsonSerializer.Serialize(planner.PlanNavigation("current", readAvailability: (_, _) => null)) == JsonSerializer.Serialize(legacy), "An absent component projection changed the existing navigation contract.");
        bool remoteReleased = false;
        AutoPlayEventAvailability ReadAvailability(AutoPlayNavigationVisit visit, AutoPlayEvent observed)
        {
            Check(observed.Id == ladder.Id, "Availability was requested for an unknown observed native event.");
            string fingerprint = visit.Key == "current" ? "current-free" : remoteReleased ? "remote-free" : "blocked";
            return AutoPlayPlanner<Deferral>.ReadAvailability(coverage, new Dictionary<string, string> { ["physical-guard"] = fingerprint });
        }
        Check(planner.PlanNavigation("current", readAvailability: ReadAvailability) is null, "Current-position availability reopened a blocked remote ladder frontier.");
        Check(planner.ReachableNavigationFrontierCount("current", readAvailability: ReadAvailability) == 0, "A blocked remote component contributed a phantom gameplay frontier.");
        Check(JsonSerializer.Serialize(planner.State) == original, "Projecting component eligibility mutated aggregate completion, guards or routes.");
        remoteReleased = true;
        AutoPlayPlanStep? reopened = planner.PlanNavigation("current", readAvailability: ReadAvailability);
        Check(reopened is { IsTravel: true, Event.Id: "ladder", Goal.NavigationKey: "remote", Goal.EventId: "ladder" }, "Changed remote eligibility failed to reopen its genuine source frontier through the proved route.");
        Check(planner.ReachableNavigationFrontierCount("current", readAvailability: ReadAvailability) == 1, "Changed remote eligibility did not contribute exactly one genuine gameplay frontier.");
        Check(JsonSerializer.Serialize(planner.State) == original, "Reopening a projected remote frontier mutated recorded aggregate evidence.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
