using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class AutoPlayNavigationRefreshTests
{
    public static void Run()
    {
        CheckCompletedRemoteAvailability();
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
        AutoPlayEvent changedActor = actor with { ConditionSignature = "changed-scratch" };
        planner.Observe(new("field", "resource"), [ladder, changedActor]);
        planner.ObserveNavigation("field", "lower", [ladder, changedActor]);
        planner.ObserveNavigation("field", "upper", [changedActor]);
        state.NavigationGuards["lower-guard"] = state.NavigationGuards["lower-guard"] with { SourceTriangle = 0 };
        state.NavigationGuards["upper-guard"] = state.NavigationGuards["upper-guard"] with { SourceTriangle = 1 };
        planner.MarkUnavailable("field", "actor", new("changed-scratch", "field", "lower-guard", "lower-physical", "actor"));
        Dictionary<string, string> validatedPhysicalGuards = new(StringComparer.Ordinal) { ["lower-guard"] = "lower-physical", ["upper-guard"] = "upper-physical" };
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), validatedPhysicalGuards), "A changed raw script condition must not invalidate an unchanged source-component physical absence proof.");
        Check(Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource")), "Without revalidated native physical facts, a legacy receipt must retain exact-condition eligibility.");
        Check(planner.PlanNavigation("lower", visit => Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, visit, validatedPhysicalGuards)) is null, "Unchanged physical proofs must prevent repeating an already consumed ladder after scratch predicates change.");
        validatedPhysicalGuards["upper-guard"] = "moved-target";
        Check(Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), validatedPhysicalGuards), "A changed target fingerprint must invalidate its old component absence proof.");
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("lower", "resource"), validatedPhysicalGuards), "Invalidating a remote target proof must preserve the unchanged local component bound.");
        Check(planner.PlanNavigation("lower", visit => Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, visit, validatedPhysicalGuards)) is { IsTravel: true, RefreshVisitKey: "upper" }, "A genuinely changed remote physical proof must permit the existing shared refresh route.");
        planner.MarkUnavailable("field", "actor", new("changed-scratch", "field", "upper-guard", "moved-target", "actor"));
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), validatedPhysicalGuards), "A new physical failure must bound the refreshed component again.");
        validatedPhysicalGuards["upper-guard"] = "moved-again";
        Check(Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), validatedPhysicalGuards), "A supplied changed physical proof must invalidate even an exact-condition failure receipt.");
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["lower-guard"] = "changed", ["upper-guard"] = "changed" });
        planner.Completed("field", "actor", entrySignature: "changed-scratch", entrySignatureAuthoritative: true);
        planner.MarkUnavailable("field", "actor", new("changed-scratch", "field", "lower-guard", "lower-physical", "actor"));
        state.NavigationGuards.Remove("upper-guard");
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource")), "An already consumed condition must not create a perpetual remote inspection frontier.");

        bool NeedsRefresh(AutoPlayVisit visit)
        {
            return Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, visit);
        }
    }

    private static void CheckCompletedRemoteAvailability()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent actor = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "entry");
        planner.Observe(new("field", "resource"), [actor]);
        planner.ObserveNavigation("field", "upper", [actor]);
        state.NavigationGuards["physical"] = new("field", 1, EventId: "actor", NavigationKey: "lower", SourceTriangle: 0);
        planner.MarkUnavailable("field", "actor", new("entry", "field", "physical", "lower-blocked", "actor"));
        Dictionary<string, string> upper = new() { ["physical"] = "upper-open" };
        planner.RefreshUnavailable("field", upper);
        planner.Completed("field", "actor", entrySignature: "entry", entrySignatureAuthoritative: true);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> item = state.Coverage.Visits.Single().Events.Single();
        string? completed = item.AvailabilityFingerprint;
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "lower-blocked" });
        item.RouteNeedsRediscovery = true;
        string? current = item.AvailabilityFingerprint;
        Check(AutoPlayPlanner<Ff7AutoPlayDeferral>.NeedsExecution(item), "The current blocked availability must differ from the executed upper source.");
        AutoPlayEventAvailability projected = AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(item, upper);
        Check(projected.Fingerprint == completed && !projected.Unavailable && item.AvailabilityFingerprint == current && item.Unavailable, "Pure component projection must preserve exact shared availability identity without changing live coverage.");
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), componentPhysicalGuards: upper), "Current-component availability and route probes must not reopen a consumed remote component.");
        Dictionary<string, string> moved = new() { ["physical"] = "upper-changed-open" };
        Check(Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), componentPhysicalGuards: moved), "A genuinely unconsumed remote availability must retain its refresh frontier.");
        Ff7AutoPlayState cold = state.Clone();
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(cold, new("upper", "resource"), componentPhysicalGuards: upper), "Cold restoration lost consumption of the remote component's actual availability.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
