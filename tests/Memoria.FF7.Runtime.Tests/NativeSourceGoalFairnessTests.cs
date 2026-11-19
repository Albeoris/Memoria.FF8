using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeSourceGoalFairnessTests
{
    internal static void Run()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent slow = new("slow", AutoPlayEventKind.Interaction, ConditionSignature: "slow-entry");
        AutoPlayEvent bridge = new("bridge", AutoPlayEventKind.Interaction, ConditionSignature: "bridge-entry");
        AutoPlayEvent goal = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "actor-entry");
        planner.Observe(new("visit", "program"), [slow, bridge, goal]);
        planner.ObserveNavigation("visit", "left", [slow, bridge, goal]);
        planner.ObserveNavigation("visit", "remote", [goal]);
        planner.Completed("visit", bridge.Id, "visit", observedStoryChanged: false, entrySignature: bridge.ConditionSignature, entrySignatureAuthoritative: true, targetId: bridge.Id);
        AutoPlayEventRoute route = state.Coverage.Visits[0].Events.Single(item => item.Event.Id == bridge.Id).Routes.Single();
        planner.RecordNavigationRoute("left", "remote", bridge.Id, route);
        state.VisitGuards["visit"] = new([], [], "facts", Ff7AutoPlayPolicy.CurrentDiscoveryRevision);
        state.NavigationGuards["native-absence"] = new("visit", 2, EventId: goal.Id, NavigationKey: "left", ObservedFingerprint: "absent");
        planner.MarkUnavailable("visit", goal.Id, new(goal.ConditionSignature!, "visit", "native-absence", "absent", goal.Id));
        state.NavigationGuards["arbitrary-old-region"] = new("visit", 2, EventId: goal.Id, NavigationKey: "remote", MovementFailure: new(0, 10, 0, 10), MovementSourceInside: true, ObservedFingerprint: "failed");
        planner.MarkUnavailable("visit", goal.Id, new(goal.ConditionSignature!, "visit", "arbitrary-old-region", "failed", goal.Id));
        state.Active = new("visit", slow.Id, slow.ConditionSignature!, 1, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 1, "before", NavigationKey: "left");
        state.NativeRevalidation = new("left", slow.Id, slow.ConditionSignature!, new('A', 64), true);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [new(slow.Id, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, slow.ConditionSignature!, false, default), new(bridge.Id, 1, 2, Ff7AutoPlayTargetKind.LinePush, bridge.ConditionSignature!, false, default), new(goal.Id, 2, 1, Ff7AutoPlayTargetKind.ActorTalk, goal.ConditionSignature!, false, default)], true, true) { NavigationKey = "left" };
        string before = JsonSerializer.Serialize(state);
        Ff7NativeSiblingCandidate sibling = Ff7AutoPlayPolicy.ReadNativeSiblingCandidates(state, planner, observation).Single(item => item.Target.Id == bridge.Id);
        Check(sibling.Plan.Goal is { NavigationKey: "remote", EventId: "actor" } && sibling.Plan.RefreshVisitKey is null && sibling.Revalidation is null && before == JsonSerializer.Serialize(state), "Legacy maintenance excluded a genuine ordinary remote goal or created another maintenance task.");
        AutoPlayPlanStep? fresh = Ff7AutoPlayPolicy.ReselectNativeSiblingPlan(state, planner, observation, sibling);
        Ff7AutoPlayNativeRoute proof = new(bridge.Id, bridge.ConditionSignature!, 1, [new(new('A', 64), new(1, 0))], new('B', 64));
        Check(Ff7AutoPlayPolicy.TryAdoptSiblingNativeRoute(state, state.Active!, state.NavigationGoal, observation, sibling, fresh, proof, new('A', 64), new(false, [], "actual-entry"), new Dictionary<string, string>(), 1, "same-source"), "A genuine sibling proof could not replace an optional arrived task.");
        Check(state.NativeRevalidation is null && state.SourceRevalidationAttempts.Count == 0 && state.NavigationGoal == sibling.Plan.Goal && state.Active?.EventId == bridge.Id, "Replacing maintenance fabricated completion or lost the real semantic goal.");
        NativeGoalFairnessTests.Run();
        Console.WriteLine("PASS genuine remote goal under legacy maintenance, retained observation history and task replacement without fake completion.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
