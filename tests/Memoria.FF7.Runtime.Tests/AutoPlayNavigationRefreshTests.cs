using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class AutoPlayNavigationRefreshTests
{
    public static void Run()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent ladder = new("ladder", AutoPlayEventKind.Interaction, ConditionSignature: "ladder-entry");
        AutoPlayEvent actor = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "actor-entry");
        planner.Observe(new("field", "resource"), [ladder, actor]);
        planner.ObserveNavigation("field", "lower", [ladder, actor]);
        planner.BeginEvent("field", "ladder");
        planner.Completed("field", "ladder", "field", observedStoryChanged: false);
        planner.ObserveNavigation("field", "upper", [actor]);
        planner.RecordNavigationRoute("lower", "upper", "ladder", planner.State.Visits.Single().Events.Single(item => item.Event.Id == "ladder").Routes.Single());
        state.NavigationGuards["lower-guard"] = new("field", 0, EventId: "actor", NavigationKey: "lower");
        state.NavigationGuards["upper-guard"] = new("field", 0, EventId: "actor", NavigationKey: "upper");
        planner.MarkUnavailable("field", "actor", new("actor-entry", "field", "lower-guard", "lower-physical", "actor"));
        Check(planner.PlanNavigation("lower") is null, "An aggregate unavailable actor should not be counted as a currently executable remote frontier.");
        AutoPlayPlanStep? refresh = planner.PlanNavigation("lower", NeedsRefresh);
        Check(refresh is { IsTravel: true, Event.Id: "ladder", RefreshVisitKey: "upper", Goal.NavigationKey: "upper" }, "A component refresh must use the shared planner's recorded first-hop route despite aggregate event unavailability.");
        Check(planner.ReachableNavigationFrontierCount("lower", needsRefresh: NeedsRefresh) == 1, "A required remote inspection must count as one reachable refresh frontier.");
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["lower-guard"] = "upper-physical" });
        Check(planner.PlanNavigation("upper") is { IsTravel: false, Event.Id: "actor" }, "The upper component's changed physical guard must permit its normal native actor retry.");
        planner.MarkUnavailable("field", "actor", new("actor-entry", "field", "upper-guard", "upper-physical", "actor"));
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["lower-guard"] = "upper-physical", ["upper-guard"] = "upper-physical" });
        Check(planner.PlanNavigation("lower", NeedsRefresh) is null, "Physical failures recorded in both components must bound refresh travel instead of cycling.");
        Ff7AutoPlayState cold = state.Clone();
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(cold, new("upper", "resource")), "Cold restoration lost the component-specific failure inspection receipt.");
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["lower-guard"] = "changed", ["upper-guard"] = "changed" });
        planner.Completed("field", "actor", entrySignature: "actor-entry", entrySignatureAuthoritative: true);
        planner.MarkUnavailable("field", "actor", new("actor-entry", "field", "lower-guard", "lower-physical", "actor"));
        state.NavigationGuards.Remove("upper-guard");
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource")), "An already consumed condition must not create a perpetual remote inspection frontier.");

        bool NeedsRefresh(AutoPlayVisit visit)
        {
            return Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, visit);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
