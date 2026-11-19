using Memoria.AutoPlay;
using Memoria.FF7.Runtime;
using System.Text.Json;

internal static class NativeSourceGoalFairnessTests
{
    private sealed record Fixture(Ff7AutoPlayState State, AutoPlayPlanner<Ff7AutoPlayDeferral> Planner, Ff7AutoPlayObservation Observation);

    internal static void Run()
    {
        Fixture fixture = Create();
        string before = JsonSerializer.Serialize(fixture.State);
        Ff7NativeSiblingCandidate[] candidates = Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(fixture.State, fixture.Planner, fixture.Observation);
        Ff7NativeSiblingCandidate transport = candidates.Single(item => item.Target.Id == "bridge" && item.Revalidation is not null);
        Ff7NativeSiblingCandidate local = candidates.Single(item => item.Target.Id == "local");
        Check(transport.Revalidation is { EventId: "repair", NavigationKey: "remote" } && transport.Plan is { RefreshVisitKey: "remote", Goal.NavigationKey: "remote", Goal.EventId: null }, "A guarded remote actor source did not enter the fair pool as an exact shared refresh transport/task.");
        Check(local.Guarded && local.Revalidation is null && local.Target.Id == "local", "Current guarded native probe was starved by the active ordinary goal.");
        Check(before == JsonSerializer.Serialize(fixture.State), "Candidate discovery installed capability/goal/task or changed historical records.");
        CheckFairWinner(fixture, transport);
        CheckStaleTasks();
        Fixture arrived = Create();
        arrived.State.NativeRevalidation = new("left", "local", "local-condition", new('C', 64), true);
        Check(Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(arrived.State, arrived.Planner, arrived.Observation).Length == 0, "An arrived exact-source task lost exclusive local service.");
        Fixture changed = Create();
        Ff7AutoPlayObservation different = changed.Observation with { Events = changed.Observation.Events.Select(item => item.Id == "bridge" ? item with { ConditionFingerprint = "changed-current" } : item).ToArray() };
        Check(!Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(changed.State, changed.Planner, different).Any(item => item.Target.Id == "bridge"), "A historical transport condition authorized a different current first action.");
        Fixture guarded = Create();
        Ff7NativeSiblingCandidate guardedCandidate = Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(guarded.State, guarded.Planner, guarded.Observation).Single(item => item.Target.Id == "local");
        var capability = Ff7AutoPlayPolicy.CreateNativeCandidateAvailabilityReader(guarded.State, guarded.Observation, guardedCandidate.Target);
        AutoPlayNavigationVisit navigation = guarded.State.Coverage.NavigationVisits.Single(item => item.Key == "left");
        Check(capability(navigation, navigation.Events.Single(item => item.Id == "local")) is { Unavailable: false }, "A proved local continuation could not expose its scoped capability.");
        Check(Adopt(guarded, guardedCandidate), "A complete guarded-current proof could not atomically adopt its genuine shared local goal.");
        Check(guarded.State.Active?.EventId == "local" && guarded.State.NativeRevalidation is null && guarded.State.NativeRoute is not null, "Guarded-current adoption borrowed a remote source task or lost proof ownership.");
        Console.WriteLine("PASS fair current guarded probes and exact source-refresh transports/tasks, retained infinite owner, fresh plan/condition/receipt fences and atomic adoption.");
    }

    private static Fixture Create()
    {
        Ff7AutoPlayState state = new();
        AutoPlayEvent slow = new("slow", AutoPlayEventKind.Transition, Order: 0, ConditionSignature: "slow-condition");
        AutoPlayEvent bridge = new("bridge", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "bridge-condition");
        AutoPlayEvent repair = new("repair", AutoPlayEventKind.Interaction, Order: 2, ConditionSignature: "repair-condition");
        AutoPlayEvent local = new("local", AutoPlayEventKind.Interaction, Order: 3, ConditionSignature: "local-condition");
        AutoPlayEventRoute route = new("visit", bridge.ConditionSignature, false, [], bridge.Id);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> bridgeCoverage = new() { Event = bridge, Available = true, Completed = true, Conditions = new(), Routes = [route] };
        bridgeCoverage.Conditions.ConsumedEntries.Add(bridge.ConditionSignature!);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> repairCoverage = new() { Event = repair, Available = true, Conditions = new() };
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> localCoverage = new() { Event = local, Available = true, Conditions = new() };
        state.Coverage.Visits.Add(new() { Visit = new("visit", "program"), Events = [new() { Event = slow, Available = true, Conditions = new() }, bridgeCoverage, repairCoverage, localCoverage] });
        state.Coverage.NavigationVisits.Add(new("left", "visit", [slow, bridge, repair, local]));
        state.Coverage.NavigationVisits.Add(new("remote", "visit", [bridge, repair]));
        state.Coverage.NavigationRoutes.Add(new("left", "remote", bridge.Id, route));
        state.VisitGuards["visit"] = new([], [], "native", Ff7AutoPlayPolicy.CurrentDiscoveryRevision);
        state.NavigationGuards["absent"] = new("visit", 2, EventId: repair.Id, NavigationKey: "left", SourceTriangle: 0, ObservedFingerprint: "absent");
        state.NavigationGuards["repair-cycle"] = new("visit", 2, EventId: repair.Id, NavigationKey: "remote", SourceTriangle: 1, MovementFailure: new(0, 10, 0, 10), ObservedFingerprint: "blocked", MovementSourceInside: true);
        state.NavigationGuards["local-cycle"] = new("visit", 3, EventId: local.Id, NavigationKey: "left", SourceTriangle: 0, MovementFailure: new(0, 10, 0, 10), ObservedFingerprint: "blocked", MovementSourceInside: true);
        repairCoverage.UnavailableAttempts.Add(new(repair.ConditionSignature!, "visit", "absent", "absent", repair.Id));
        repairCoverage.UnavailableAttempts.Add(new(repair.ConditionSignature!, "visit", "repair-cycle", "blocked", repair.Id));
        localCoverage.UnavailableAttempts.Add(new(local.ConditionSignature!, "visit", "local-cycle", "blocked", local.Id));
        var read = Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(state);
        bridgeCoverage.CompletedAvailabilityFingerprints.Add(read(state.Coverage.NavigationVisits[0], bridge)!.Fingerprint!);
        bridgeCoverage.CompletedAvailabilityFingerprints.Add(read(state.Coverage.NavigationVisits[1], bridge)!.Fingerprint!);
        state.Active = new("visit", "slow", "slow-condition", 1, -1, -1, Ff7AutoPlayTargetKind.Gateway, 10, 1, "before", NavigationKey: "left");
        state.Coverage.Visits[0].Events[0].Conditions!.ActiveEntry = "slow-condition";
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [new("slow", -1, -1, Ff7AutoPlayTargetKind.Gateway, "slow-condition", false, default, GatewayIndex: 0, DestinationField: 10), new("bridge", 1, 2, Ff7AutoPlayTargetKind.LinePush, "bridge-condition", false, default), new("repair", 2, 1, Ff7AutoPlayTargetKind.ActorTalk, "repair-condition", false, default), new("local", 3, 1, Ff7AutoPlayTargetKind.ActorTalk, "local-condition", false, default)], true, true) { NavigationKey = "left" };
        return new(state, Ff7AutoPlayPolicy.CreatePlanner(state), observation);
    }

    private static bool Adopt(Fixture fixture, Ff7NativeSiblingCandidate sibling)
    {
        AutoPlayPlanStep? fresh = Ff7AutoPlayPolicy.ReselectNativeSiblingPlan(fixture.State, fixture.Planner, fixture.Observation, sibling);
        AutoPlayNavigationVisit current = fixture.State.Coverage.NavigationVisits[0];
        var read = sibling.Guarded ? Ff7AutoPlayPolicy.CreateNativeCandidateAvailabilityReader(fixture.State, fixture.Observation, sibling.Target) : Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(fixture.State);
        Ff7AutoPlayNativeRoute proof = new(sibling.Target.Id, sibling.Target.ConditionFingerprint, 1, [new(new('A', 64), new(1, 0))], new('B', 64));
        return Ff7AutoPlayPolicy.TryAdoptSiblingNativeRoute(fixture.State, fixture.State.Active!, fixture.State.NavigationGoal, fixture.Observation, sibling, fresh, proof, new('A', 64), read(current, current.Events.Single(item => item.Id == sibling.Target.Id))!, new Dictionary<string, string> { ["local-cycle"] = "blocked" }, 1, "current-source");
    }

    private static void CheckFairWinner(Fixture fixture, Ff7NativeSiblingCandidate sibling)
    {
        int slow = 0;
        bool disposed = false;
        IEnumerable<int> Infinite()
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
        IEnumerable<int> Finite()
        {
            for (int i = 0; i < 3; i++)
            {
                Check(fixture.State.Active?.EventId == "slow", "Pending refresh abandoned the original owner/workset.");
                yield return 0;
            }
            Check(Adopt(fixture, sibling), "A finite refresh proof was starved by an infinite active target.");
        }
        string guards = JsonSerializer.Serialize(fixture.State.NavigationGuards);
        foreach (int _ in Ff7AutoplayDriver.EnumerateFairNativeCandidates([Infinite(), Finite()], () => fixture.State.NativeRoute is not null))
        {
        }
        Check(disposed && slow >= 3 && fixture.State.Active is { EventId: "bridge", Accepted: false, Returned: false } && fixture.State.NavigationGoal == sibling.Plan.Goal && fixture.State.NativeRevalidation is { NavigationKey: "remote", EventId: "repair", Condition: "repair-condition", ArrivalAcknowledged: false } task && task.DepartureReceiptKey == sibling.Revalidation!.ReceiptKey, "Winner adoption lost exact source task, shared goal, owner/proof or cancellation.");
        Check(guards == JsonSerializer.Serialize(fixture.State.NavigationGuards) && fixture.State.SourceRevalidationAttempts.Count == 0, "Travel proof cleared historical guards or fabricated a completed source probe.");
    }

    private static void CheckStaleTasks()
    {
        Fixture changed = Create();
        Ff7NativeSiblingCandidate sibling = Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(changed.State, changed.Planner, changed.Observation).Single(item => item.Target.Id == "bridge" && item.Revalidation is not null);
        changed.State.NavigationGuards["repair-cycle"] = changed.State.NavigationGuards["repair-cycle"] with { ObservedFingerprint = "new-context" };
        string before = JsonSerializer.Serialize(changed.State);
        Check(!Adopt(changed, sibling) && before == JsonSerializer.Serialize(changed.State), "A stale source-debt receipt installed a task/proof.");
        Fixture completed = Create();
        Ff7NativeSiblingCandidate old = Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(completed.State, completed.Planner, completed.Observation).Single(item => item.Target.Id == "bridge" && item.Revalidation is not null);
        completed.State.SourceRevalidationAttempts.Add(old.Revalidation!.ReceiptKey);
        Check(!Adopt(completed, old), "An already-completed exact source attempt installed a new refresh task.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
