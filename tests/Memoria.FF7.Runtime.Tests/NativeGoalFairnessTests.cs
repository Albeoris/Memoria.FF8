using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeGoalFairnessTests
{
    private sealed record Fixture(Ff7AutoPlayState State, AutoPlayPlanner<Ff7AutoPlayDeferral> Planner, Ff7AutoPlayObservation Observation, Ff7NativeSiblingCandidate Sibling, Ff7AutoPlayNativeRoute Proof);

    internal static void Run()
    {
        Fixture fixture = Create();
        CheckSelectionAndAdoption(fixture);
        CheckFairPendingWinner();
        CheckStaleWinner();
        CheckIntentFences();
        Console.WriteLine("Native multi-goal selection, retained fairness, atomic owner/goal/proof and intent checks passed.");
    }

    private static Fixture Create()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent slow = new("slow", AutoPlayEventKind.Transition, Order: 0, ConditionSignature: "slow-condition");
        AutoPlayEvent bridge = new("bridge", AutoPlayEventKind.Interaction, Order: 2, ConditionSignature: "bridge-condition");
        AutoPlayEvent frontier = new("frontier", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "fresh");
        planner.Observe(new("visit", "program"), [slow, bridge, frontier]);
        state.VisitGuards["visit"] = new([], [], "facts", Ff7AutoPlayPolicy.CurrentDiscoveryRevision);
        planner.ObserveNavigation("visit", "left", [slow, bridge, frontier]);
        planner.ObserveNavigation("visit", "remote", [frontier]);
        planner.Completed("visit", bridge.Id, "visit", observedStoryChanged: false, entrySignature: bridge.ConditionSignature, entrySignatureAuthoritative: true, targetId: bridge.Id);
        AutoPlayEventRoute route = state.Coverage.Visits[0].Events.Single(item => item.Event.Id == bridge.Id).Routes.Single();
        planner.RecordNavigationRoute("left", "remote", bridge.Id, route);
        state.NavigationGuards["absent"] = new("visit", 3, EventId: frontier.Id, NavigationKey: "left", ObservedFingerprint: "absent");
        planner.MarkUnavailable("visit", frontier.Id, new("fresh", "visit", "absent", "absent", frontier.Id));
        state.NavigationGoal = new("left", slow.Id, slow.ConditionSignature);
        state.Active = new("visit", slow.Id, slow.ConditionSignature!, 1, -1, 0, Ff7AutoPlayTargetKind.Gateway, 10, 1, "before", NavigationKey: "left");
        planner.BeginEvent("visit", slow.Id);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [new(slow.Id, -1, 0, Ff7AutoPlayTargetKind.Gateway, slow.ConditionSignature!, false, default, GatewayIndex: 1, DestinationField: 10), new(bridge.Id, 2, 3, Ff7AutoPlayTargetKind.LineCross, bridge.ConditionSignature!, false, default, MayChangeNavigation: true), new(frontier.Id, 3, 1, Ff7AutoPlayTargetKind.ActorTalk, "fresh", false, default)], true, true) { NavigationKey = "left" };
        Ff7NativeSiblingCandidate sibling = Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(state, planner, observation).Single();
        Ff7AutoPlayNativeRoute proof = new(bridge.Id, bridge.ConditionSignature!, 1, [new(new('A', 64), new(1, 0))], new('B', 64));
        return new(state, planner, observation, sibling, proof);
    }

    private static AutoPlayPlanStep? Select(Fixture fixture)
    {
        return fixture.Planner.PlanNavigation("left", allowRemoteConditionRoutes: true, avoidFirstEvents: fixture.Sibling.AvoidFirstEvents.ToHashSet(StringComparer.Ordinal), preferredGoal: fixture.State.NavigationGoal, readAvailability: Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(fixture.State));
    }

    private static bool Adopt(Fixture fixture, AutoPlayPlanStep? fresh = null, Ff7AutoPlayNativeRoute? proof = null)
    {
        return Ff7AutoPlayPolicy.TryAdoptSiblingNativeRoute(fixture.State, fixture.State.Active!, fixture.State.NavigationGoal, fixture.Observation, fixture.Sibling, fresh ?? Select(fixture), proof ?? fixture.Proof, new('A', 64), new(false, [], "actual-entry"), new Dictionary<string, string>(), 1, "current-source");
    }

    private static void CheckSelectionAndAdoption(Fixture fixture)
    {
        string original = JsonSerializer.Serialize(fixture.State);
        Check(fixture.Sibling.Target.Id == "bridge" && fixture.Sibling.Plan is { IsTravel: true, Goal.NavigationKey: "remote", Goal.EventId: "frontier" }, "A guarded-only filter lost an unguarded consumed transport toward a real shared frontier.");
        Check(Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(fixture.State, fixture.Planner, fixture.Observation).Length == 1 && original == JsonSerializer.Serialize(fixture.State), "Sibling discovery changed coverage, selected owner, goal or native evidence.");
        string guards = JsonSerializer.Serialize(fixture.State.NavigationGuards);
        Check(Adopt(fixture), "A complete freshly selected sibling proof was not adopted.");
        Check(fixture.State.Active is { EventId: "bridge", Accepted: false, Returned: false, EntryAvailabilityFingerprint: "actual-entry", TravelRoute: not null } && fixture.State.NavigationGoal == fixture.Sibling.Plan.Goal && fixture.State.NativeRoute == fixture.Proof, "Sibling admission lost its exact shared goal, fresh owner, route, entry availability or proof.");
        Check(fixture.State.Coverage.Visits[0].Events.Single(item => item.Event.Id == "slow").Conditions is { ActiveEntry: null } old && !old.HasExecutedEntry("slow-condition"), "Switching an unaccepted owner fabricated completion or retained its pending entry.");
        Check(guards == JsonSerializer.Serialize(fixture.State.NavigationGuards), "A sibling proof cleared refusal history.");
        Fixture required = Create();
        required.State.NativeRevalidation = new("left", "slow", "slow-condition", new('C', 64), true);
        Check(Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(required.State, required.Planner, required.Observation).Length == 0 && !Adopt(required), "An arrived local validation obligation was bypassed by sibling work.");
    }

    private static void CheckFairPendingWinner()
    {
        Fixture fixture = Create();
        int slow = 0;
        int fast = 0;
        bool disposed = false;
        IEnumerable<int> Slow()
        {
            try
            {
                while (true)
                {
                    slow++;
                    yield return 0;
                }
            }
            finally
            {
                disposed = true;
            }
        }
        IEnumerable<int> Fast()
        {
            for (int step = 0; step < 3; step++)
            {
                fast++;
                Check(!disposed && fixture.State.Active?.EventId == "slow", "Pending sibling work abandoned the selected workset or owner.");
                yield return 0;
            }
            Check(Adopt(fixture), "A finite sibling was starved by the retained infinite native job.");
        }
        foreach (int _ in Ff7AutoplayDriver.EnumerateFairNativeCandidates([Slow(), Fast()], () => fixture.State.NativeRoute is not null))
        {
            if (fixture.State.NativeRoute is null)
            {
                Ff7AutoPlayActiveEvent owner = fixture.State.Active!;
                Ff7AutoPlayMovementFailure region = new(0, 10, 0, 10);
                string key = Ff7AutoPlayPolicy.ReadMovementFailureGuardKey("left", "slow", "physical", region);
                fixture.State.NavigationGuards[key] = new("visit", -1, EventId: "slow", NavigationKey: "left", MovementFailure: region, MovementSourceInside: true);
                fixture.Planner.MarkUnavailable("visit", "slow", new("slow-condition", "visit", key, "same-local-failure", "slow"));
                Ff7AutoPlayPolicy.PreservePendingPhysicalOwner(fixture.State, owner, fixture.Observation, fixture.Observation.Events.Single(target => target.Id == "slow"), new Dictionary<string, string> { [key] = "same-local-failure" });
                Ff7NativePhysicalFailureFence fence = new(owner, fixture.State.NavigationGoal, new('A', 64), 4096, 1024);
                Check(fence.Matches(fixture.State, new('A', 64), 4096, 1024) && !fence.Matches(fixture.State, new('B', 64), 4096, 1024) && !fence.Matches(fixture.State, new('A', 64), 4096, 512), "Repeated local exhaustion lost its exact owner/source/mode fence.");
                Check(!fixture.State.Coverage.Visits[0].Events.Single(item => item.Event.Id == "slow").Unavailable, "A repeated local search failure became event-level denial while sibling work was pending.");
            }
        }
        Check(slow >= 3 && fast == 3 && disposed && fixture.State.NavigationGoal == fixture.Sibling.Plan.Goal, "Native multi-goal fairness or winner cancellation lost an open workset/goal.");
    }

    private static void CheckStaleWinner()
    {
        foreach (Func<Fixture, bool> stale in new Func<Fixture, bool>[] { fixture => Adopt(fixture, fixture.Sibling.Plan with { Goal = new("elsewhere") }), fixture => Adopt(fixture, proof: fixture.Proof with { ConditionFingerprint = "changed" }), fixture => Adopt(fixture, proof: fixture.Proof with { Steps = [new(new('C', 64), default)] }) })
        {
            Fixture fixture = Create();
            string before = JsonSerializer.Serialize(fixture.State);
            Check(!stale(fixture) && before == JsonSerializer.Serialize(fixture.State), "A stale candidate/goal/source changed owner or consumed coverage.");
        }
    }

    private static void CheckIntentFences()
    {
        Fixture fixture = Create();
        Ff7NativePlanningHeader header = Ff7AutoPlayPolicy.ReadNativePlanningHeader(fixture.State);
        string full = Ff7AutoPlayPolicy.ReadNativePlanningIntent(fixture.State);
        fixture.State.Status = "Planning is pending";
        Check(header == Ff7AutoPlayPolicy.ReadNativePlanningHeader(fixture.State) && full == Ff7AutoPlayPolicy.ReadNativePlanningIntent(fixture.State), "Idempotent planning status invalidated a retained native workset.");
        fixture.State.Coverage.Visits[0].Events[0].Conditions!.ConsumedEntries.Add("external-change");
        Check(header == Ff7AutoPlayPolicy.ReadNativePlanningHeader(fixture.State) && full != Ff7AutoPlayPolicy.ReadNativePlanningIntent(fixture.State), "The cheap header was mistaken for proof of nested ledger equivalence.");
        fixture.State.NavigationGoal = new("other");
        Check(header != Ff7AutoPlayPolicy.ReadNativePlanningHeader(fixture.State), "An explicit goal change retained the cheap intent fence.");
        Ff7AutoPlayState observed = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(observed);
        AutoPlayEvent native = new("observed", AutoPlayEventKind.Interaction, ConditionSignature: "condition");
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [new(native.Id, 2, 3, Ff7AutoPlayTargetKind.LineCross, "condition", false, default, MayChangeNavigation: true)], true, true) { NavigationKey = "left" };
        planner.Observe(new("visit", "program"), [native]);
        planner.ObserveNavigation("visit", "left", [native]);
        Ff7AutoPlayPolicy.ObserveNavigationProbes(observed, observation);
        Check(!Ff7AutoPlayPolicy.NativePlannerObservationChanged(observed, observation, [native]), "An idempotent native observation discarded retained sibling worksets.");
        Check(Ff7AutoPlayPolicy.NativePlannerObservationChanged(observed, observation, [native with { ConditionSignature = "changed" }]), "A supported native condition change did not invalidate the pending candidate pool.");
        observed.Coverage.Visits[0].Events[0].Completed = true;
        Check(Ff7AutoPlayPolicy.NativePlannerObservationChanged(observed, observation, [native]), "New topology rediscovery failed to invalidate the pending candidate pool.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
