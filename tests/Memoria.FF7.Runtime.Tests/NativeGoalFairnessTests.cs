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
        CheckFamilyFairness();
        CheckStaleWinner();
        CheckIntentFences();
        CheckOwnedNativeSiblingCommitments();
        Console.WriteLine("Native multi-goal selection, retained fairness, atomic owner/goal/proof and intent checks passed.");
    }

    private static Fixture Create(bool actor = false)
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent slow = new("slow", actor ? AutoPlayEventKind.Interaction : AutoPlayEventKind.Transition, Order: 0, ConditionSignature: "slow-condition");
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
        state.Active = new("visit", slow.Id, slow.ConditionSignature!, 1, actor ? 4 : -1, actor ? 1 : 0, actor ? Ff7AutoPlayTargetKind.ActorTalk : Ff7AutoPlayTargetKind.Gateway, actor ? null : (ushort)10, 1, "before", NavigationKey: "left");
        planner.BeginEvent("visit", slow.Id);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [new(slow.Id, actor ? 4 : -1, actor ? 1 : 0, actor ? Ff7AutoPlayTargetKind.ActorTalk : Ff7AutoPlayTargetKind.Gateway, slow.ConditionSignature!, false, default, GatewayIndex: actor ? null : 1, DestinationField: actor ? null : (ushort)10), new(bridge.Id, 2, 3, Ff7AutoPlayTargetKind.LineCross, bridge.ConditionSignature!, false, default, MayChangeNavigation: true), new(frontier.Id, 3, 1, Ff7AutoPlayTargetKind.ActorTalk, "fresh", false, default)], true, true) { NavigationKey = "left" };
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
        Check(fixture.Sibling.Target.Id == "bridge" && fixture.Sibling.Plan is { IsTravel: true, Goal.NavigationKey: "remote", Goal.EventId: "frontier", DestinationNavigationKey: "remote", Route.DestinationVisitKey: "visit" }, "A guarded-only filter lost an unguarded consumed transport toward a real shared frontier.");
        Check(Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(fixture.State, fixture.Planner, fixture.Observation).Length == 1 && original == JsonSerializer.Serialize(fixture.State), "Sibling discovery changed coverage, selected owner, goal or native evidence.");
        Check(Ff7AutoPlayPolicy.ReadExpectedNativeTransportNavigation(fixture.State, fixture.Planner, fixture.Observation, fixture.State.Active!, fixture.Sibling) == "remote", "A native transport proof received its aggregate field key instead of exact component destination.");
        string guards = JsonSerializer.Serialize(fixture.State.NavigationGuards);
        Check(Adopt(fixture), "A complete freshly selected sibling proof was not adopted.");
        Check(fixture.State.Active is { EventId: "bridge", Accepted: false, Returned: false, EntryAvailabilityFingerprint: "actual-entry", TravelRoute: not null } && fixture.State.NavigationGoal == fixture.Sibling.Plan.Goal && fixture.State.NativeRoute == fixture.Proof, "Sibling admission lost its exact shared goal, fresh owner, route, entry availability or proof.");
        Check(fixture.State.Coverage.Visits[0].Events.Single(item => item.Event.Id == "slow").Conditions is { ActiveEntry: null } old && !old.HasExecutedEntry("slow-condition"), "Switching an unaccepted owner fabricated completion or retained its pending entry.");
        Check(guards == JsonSerializer.Serialize(fixture.State.NavigationGuards), "A sibling proof cleared refusal history.");
        Fixture required = Create();
        required.State.NativeRevalidation = new("left", "slow", "slow-condition", new('C', 64), true);
        Check(Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(required.State, required.Planner, required.Observation).Length > 0 && Adopt(required) && required.State.NativeRevalidation is null && required.State.SourceRevalidationAttempts.Count == 0, "An optional arrived maintenance task excluded another genuine frontier or fabricated completion when replaced.");
    }

    private static void CheckFairPendingWinner()
    {
        Fixture fixture = Create(actor: true);
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
                Ff7AutoPlayMovementFailure? region = slow == 1 ? null : new(0, 10, 0, 10);
                string key = Ff7AutoPlayPolicy.ReadMovementFailureGuardKey("left", "slow", "physical", region);
                fixture.State.NavigationGuards[key] = new("visit", -1, EventId: "slow", NavigationKey: "left", MovementFailure: region, MovementSourceInside: true);
                fixture.Planner.MarkUnavailable("visit", "slow", new("slow-condition", "visit", key, "same-local-failure", "slow"));
                Check(Ff7AutoPlayPolicy.TryRetainFailedOrdinaryTarget(fixture.State, owner, fixture.Observation, fixture.Observation.Events.Single(target => target.Id == "slow"), new Dictionary<string, string> { [key] = "same-local-failure" }), "A synchronous regionless ordinary failure dropped its owner before native alternatives could start.");
                Ff7NativePhysicalFailureFence fence = new(owner, fixture.State.NavigationGoal, new('A', 64), 4096, 1024);
                Check(fence.Matches(fixture.State, new('A', 64), 4096, 1024) && !fence.Matches(fixture.State, new('B', 64), 4096, 1024) && !fence.Matches(fixture.State, new('A', 64), 4096, 512), "Repeated local exhaustion lost its exact owner/source/mode fence.");
                Check(!fixture.State.Coverage.Visits[0].Events.Single(item => item.Event.Id == "slow").Unavailable, "A repeated local search failure became event-level denial while sibling work was pending.");
            }
        }
        Check(slow >= 3 && fast == 3 && disposed && fixture.State.NavigationGoal == fixture.Sibling.Plan.Goal, "Native multi-goal fairness or winner cancellation lost an open workset/goal.");
    }

    private static void CheckFamilyFairness()
    {
        int current = 0;
        int[] siblings = new int[20];
        int disposed = 0;
        IEnumerable<int> Search(int index)
        {
            try
            {
                while (true)
                {
                    if (index < 0)
                    {
                        current++;
                    }
                    else
                    {
                        siblings[index]++;
                    }
                    yield return 0;
                }
            }
            finally
            {
                disposed++;
            }
        }
        using (IEnumerator<int> cursor = Ff7AutoPlayPolicy.EnumerateNativeGoalFamilies(Search(-1), Enumerable.Range(0, siblings.Length).Select(Search), () => false).GetEnumerator())
        {
            for (int turn = 0; turn < 40; turn++)
            {
                Check(cursor.MoveNext(), "An infinite family unexpectedly exhausted.");
            }
            Check(current == 20 && siblings.Sum() == 20 && siblings.All(count => count == 1), "The current goal lost its half share or a sibling was starved by grouped native scheduling.");
        }
        Check(disposed == 21, "Cancelling grouped native families retained a current or sibling cursor.");
    }

    private static void CheckStaleWinner()
    {
        foreach (Func<Fixture, bool> stale in new Func<Fixture, bool>[] { fixture => Adopt(fixture, fixture.Sibling.Plan with { Goal = new("elsewhere") }), fixture => Adopt(fixture, fixture.Sibling.Plan with { DestinationNavigationKey = "wrong-component" }), fixture => Adopt(fixture, proof: fixture.Proof with { ConditionFingerprint = "changed" }), fixture => Adopt(fixture, proof: fixture.Proof with { Steps = [new(new('C', 64), default)] }) })
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

    private static Fixture OwnedFixture()
    {
        Fixture fixture = Create();
        AutoPlayTravelGoal goal = new("remote", "frontier", "fresh");
        fixture.State.NavigationGoal = goal;
        fixture.State.TravelCommitment = new(goal, fixture.Observation.NavigationKey, fixture.State.DestinationField);
        return fixture;
    }

    private static void CheckOwnedNativeSiblingCommitments()
    {
        Fixture owned = OwnedFixture();
        AutoPlayTravelGoal goal = owned.State.NavigationGoal!;
        AutoPlayPlanStep same = owned.Sibling.Plan with { Goal = goal };
        AutoPlayPlanStep different = same with { Goal = new("left", "slow", "slow-condition") };
        string before = JsonSerializer.Serialize(owned.State);
        Check(Ff7AutoPlayPolicy.MatchesCommittedNativeSibling(owned.State, owned.Observation, same), "An authenticated native sibling toward the same ultimate goal was excluded.");
        Check(!Ff7AutoPlayPolicy.MatchesCommittedNativeSibling(owned.State, owned.Observation, different), "A native sibling stole an authenticated trip for a different ultimate goal.");
        Ff7NativeSiblingCandidate[] candidates = Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(owned.State, owned.Planner, owned.Observation);
        Check(candidates.Length > 0 && candidates.All(candidate => candidate.Plan.Goal == goal), "Native candidate enumeration ignored its owned ultimate goal.");
        Check(Ff7AutoPlayPolicy.ReselectNativeSiblingPlan(owned.State, owned.Planner, owned.Observation, owned.Sibling)?.Goal == goal, "Fresh native sibling reselection lost the authenticated ultimate goal.");
        Check(before == JsonSerializer.Serialize(owned.State), "Owned native sibling filtering changed state or coverage.");
        Check(Adopt(owned, same), "A freshly revalidated same-goal native sibling could not be adopted.");

        Fixture race = Create();
        race.State.TravelCommitment = new(race.State.NavigationGoal!, race.Observation.NavigationKey, race.State.DestinationField);
        before = JsonSerializer.Serialize(race.State);
        Check(!Adopt(race, race.Sibling.Plan) && before == JsonSerializer.Serialize(race.State), "A proof selected before commitment stole the current trip at publication.");

        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            fixture => fixture.State.TravelCommitment = null,
            fixture => fixture.State.TravelCommitment = fixture.State.TravelCommitment! with { NavigationKey = "another-source" },
            fixture => fixture.State.NavigationGoal = new("left", "slow", "slow-condition"),
            fixture => fixture.State.DestinationField = 10,
            fixture =>
            {
                var coverage = fixture.State.Coverage.Visits[0].Events.Single(item => item.Event.Id == "frontier");
                coverage.Conditions ??= new();
                coverage.Conditions.ConsumedEntries.Add("fresh");
            },
            fixture =>
            {
                int index = fixture.State.Coverage.NavigationVisits.FindIndex(visit => visit.Key == "remote");
                AutoPlayNavigationVisit remote = fixture.State.Coverage.NavigationVisits[index];
                fixture.State.Coverage.NavigationVisits[index] = remote with { Events = remote.Events.Select(item => item with { ConditionSignature = "changed" }).ToArray() };
            }
        })
        {
            Fixture released = OwnedFixture();
            invalidate(released);
            Check(Ff7AutoPlayPolicy.MatchesCommittedNativeSibling(released.State, released.Observation, different), "A released, stale, consumed or superseded commitment permanently pinned native siblings.");
        }

        Fixture sourceOnly = Create();
        AutoPlayTravelGoal pendingSource = new("remote");
        sourceOnly.State.NavigationGoal = pendingSource;
        sourceOnly.State.TravelCommitment = new(pendingSource, sourceOnly.Observation.NavigationKey, sourceOnly.State.DestinationField);
        AutoPlayPlanStep sourcePlan = sourceOnly.Sibling.Plan with { Goal = pendingSource };
        Check(Ff7AutoPlayPolicy.MatchesCommittedNativeSibling(sourceOnly.State, sourceOnly.Observation, sourcePlan, sourceGoalPending: true), "A proved pending source-only trip lost its native sibling.");
        Check(!Ff7AutoPlayPolicy.MatchesCommittedNativeSibling(sourceOnly.State, sourceOnly.Observation, sourceOnly.Sibling.Plan, sourceGoalPending: true), "A native event goal replaced an authenticated pending source-only trip.");
        Check(Ff7AutoPlayPolicy.MatchesCommittedNativeSibling(sourceOnly.State, sourceOnly.Observation, sourceOnly.Sibling.Plan, sourceGoalPending: false), "An unproved or already refreshed source-only goal prevented ordinary fairness.");
        bool NeedsRefresh(AutoPlayVisit visit) => visit.Key == pendingSource.NavigationKey;
        Ff7NativeSiblingCandidate[] sourceCandidates = Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(sourceOnly.State, sourceOnly.Planner, sourceOnly.Observation, NeedsRefresh, sourceGoalPending: true);
        Check(sourceCandidates.Length > 0 && sourceCandidates.All(candidate => candidate.Plan.Goal == pendingSource), "Source-only native enumeration did not preserve its authenticated refresh goal.");
        Ff7NativeSiblingCandidate candidate = sourceCandidates.Single(item => item.Target.Id == sourceOnly.Sibling.Target.Id);
        AutoPlayPlanStep? fresh = Ff7AutoPlayPolicy.ReselectNativeSiblingPlan(sourceOnly.State, sourceOnly.Planner, sourceOnly.Observation, candidate, NeedsRefresh, sourceGoalPending: true);
        Check(fresh?.Goal == pendingSource, "Source-only native reselection lost the pending-source fence.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
