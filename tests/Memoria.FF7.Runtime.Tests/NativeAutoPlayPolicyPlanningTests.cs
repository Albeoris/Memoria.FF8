using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayPolicyPlanningTests
{
    public static void Run()
    {
        AutoPlayNavigationRefreshTests.Run();
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayVisit visit = new("current", "region");
        planner.Observe(visit, [new("repeatable", AutoPlayEventKind.Interaction, ConditionSignature: "before"), new("unseen", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "ready")]);
        planner.Completed(visit.Key, "repeatable", entrySignature: "before", entrySignatureAuthoritative: true);
        planner.Observe(visit, [new("repeatable", AutoPlayEventKind.Interaction, ConditionSignature: "changed"), new("unseen", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "ready")]);
        Check(planner.Plan(visit.Key)?.Event.Id == "unseen", "A changed repeatable event must not starve an unexecuted frontier.");
        planner.Completed(visit.Key, "unseen", entrySignature: "ready", entrySignatureAuthoritative: true);
        Check(planner.Plan(visit.Key)?.Event.Id == "repeatable", "A changed predicate must remain eligible after the unexecuted frontier is covered.");
        Ff7AutoPlayState restored = state.Clone();
        Check(Ff7AutoPlayPolicy.CreatePlanner(restored).Plan(visit.Key)?.Event.Id == "repeatable", "Cold planner restoration lost the changed predicate.");
        restored.Coverage.Visits.Single().Events.Single(entry => entry.Event.Id == "unseen").RouteNeedsRediscovery = true;
        Check(Ff7AutoPlayPolicy.CreatePlanner(restored).Plan(visit.Key)?.Event.Id == "unseen", "A consumed transport awaiting native connectivity evidence must precede a changed repeatable event.");
        Ff7AutoPlayState fair = new();
        var fairPlanner = Ff7AutoPlayPolicy.CreatePlanner(fair);
        AutoPlayEvent[] repeated = [new("first", AutoPlayEventKind.Interaction, ConditionSignature: "one"), new("second", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "one")];
        fairPlanner.Observe(visit, repeated);
        fairPlanner.Completed(visit.Key, "first", entrySignature: "one", entrySignatureAuthoritative: true);
        fairPlanner.Completed(visit.Key, "first", entrySignature: "two", entrySignatureAuthoritative: true);
        fairPlanner.Completed(visit.Key, "second", entrySignature: "one", entrySignatureAuthoritative: true);
        fairPlanner.Observe(visit, repeated.Select(item => item with { ConditionSignature = "three" }).ToArray());
        Check(fairPlanner.Plan(visit.Key)?.Event.Id == "second", "Repeated changing predicates on an earlier event must not starve a less explored native frontier.");
        CheckComponentProbeScope();
        CheckEntryAvailability();
    }

    private static void CheckEntryAvailability()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new("field", "resource"), [new("ladder", AutoPlayEventKind.Interaction, ConditionSignature: "entry")]);
        planner.MarkUnavailable("field", "ladder", new("entry", "field", "physical", "blocked", "ladder"));
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "source-open" });
        var coverage = state.Coverage.Visits.Single().Events.Single();
        string? source = coverage.AvailabilityFingerprint;
        Ff7AutoPlayActiveEvent active = new("field", "ladder", "entry", 1, 1, 4, Ff7AutoPlayTargetKind.LineTouch, null, 0, "stable", EntryAvailabilityFingerprint: source);
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "arrival-open" });
        string? arrival = coverage.AvailabilityFingerprint;
        Ff7AutoPlayObservation observation = new("field", "resource", 1, [], true, true);
        Ff7AutoPlayPolicy.RecordControlReturn(planner, active, observation, false, "entry", false, []);
        Check(source is not null && coverage.CompletedAvailabilityFingerprints.Contains(source), "Native completion lost the source availability under which the ladder was admitted.");
        Check(arrival is not null && !coverage.CompletedAvailabilityFingerprints.Contains(arrival), "Native completion consumed arrival availability as if it were the executed source.");
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "source-open" });
        Check(planner.Plan("field") is null, "Returning to an already consumed source availability reopened the same native ladder.");
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "changed-open" });
        Check(planner.Plan("field")?.Event.Id == "ladder", "An actually changed physical admission must retain its normal shared-planner retry.");
        state.NavigationGuards["physical"] = new("field", 1, EventId: "ladder", NavigationKey: "lower", SourceTriangle: 0);
        planner.MarkUnavailable("field", "ladder", new("entry", "field", "physical", "changed-open", "ladder"));
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "changed-open" });
        planner.ObserveNavigation("field", "upper", [coverage.Event]);
        Check(Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource")), "A consumed script entry hid an unconsumed physical-availability frontier in another component.");
    }

    private static void CheckComponentProbeScope()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayVisit visit = new("field", "resource");
        planner.Observe(visit, [new("ladder", AutoPlayEventKind.Interaction, ConditionSignature: "current")]);
        planner.Completed(visit.Key, "ladder");
        Ff7AutoPlayTarget target = new("ladder", 1, 4, Ff7AutoPlayTargetKind.LineTouch, "current", false, default, MayChangeNavigation: true, EntryInputContract: "native-entry");
        Ff7AutoPlayObservation lower = new(visit.Key, "resource", 1, [target], true, true) { NavigationKey = "lower" };
        Ff7AutoPlayPolicy.ObserveNavigationProbes(state, lower);
        var coverage = state.Coverage.Visits.Single().Events.Single();
        Check(coverage.RouteNeedsRediscovery, "A legacy completion must not invent component connectivity evidence.");
        state.NavigationProbes.Add(Ff7AutoPlayPolicy.NavigationProbeKey(lower.NavigationKey, target));
        Ff7AutoPlayPolicy.ObserveNavigationProbes(state, lower with { NavigationKey = "upper" });
        Check(coverage.RouteNeedsRediscovery, "Inspecting a transport from another floor lost its scoped probe.");
        Ff7AutoPlayPolicy.ObserveNavigationProbes(state, lower);
        Check(!coverage.RouteNeedsRediscovery, "An uninspected remote floor left a sticky probe on an already inspected floor.");
        Ff7AutoPlayState cold = state.Clone();
        Ff7AutoPlayPolicy.ObserveNavigationProbes(cold, lower);
        Check(!cold.Coverage.Visits.Single().Events.Single().RouteNeedsRediscovery, "Cold restoration lost the exact component probe receipt.");
        Ff7AutoPlayPolicy.ObserveNavigationProbes(state, lower with { Events = [target with { EntryInputContract = "corrected-native-entry" }] });
        Check(coverage.RouteNeedsRediscovery, "A corrected native admission contract reused an obsolete optional probe receipt.");
        Ff7AutoPlayNavigationGuard legacy = new(visit.Key, 1, EventId: target.Id);
        Ff7AutoPlayNavigationGuard restored = Ff7AutoPlayPolicy.RestorePhysicalGuard(legacy, "lower", 4);
        Check(restored.NavigationKey == "lower" && restored.SourceTriangle == 4, "A revalidated legacy guard did not recover optional component facts from the live native source.");
        Check(Ff7AutoPlayPolicy.RestorePhysicalGuard(restored, "upper", 8) == restored, "Legacy guard restoration silently reassigned a different component's physical evidence.");
        state.NavigationGoal = new("upper", "ladder", "current");
        Check(state.Clone().NavigationGoal == state.NavigationGoal, "Cold restoration lost the shared planner's multi-edge travel goal.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
