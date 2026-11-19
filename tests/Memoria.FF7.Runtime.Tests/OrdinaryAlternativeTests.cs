using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class OrdinaryAlternativeTests
{
    internal static void Run()
    {
        CheckAdoption(false);
        CheckAdoption(true);
        CheckDeferredLeafDoesNotCreateTransportCycle();
        Console.WriteLine("PASS ordinary alternative shared selection, atomic owner/goal adoption, stale-plan refusal and unconsumed history.");
    }

    private static void CheckDeferredLeafDoesNotCreateTransportCycle()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent unreachable = new("wrong-floor", AutoPlayEventKind.Interaction, Order: 0, ConditionSignature: "unchanged");
        AutoPlayEvent walk = new("walk", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "walk");
        AutoPlayEvent down = new("down", AutoPlayEventKind.Interaction, Order: 2, ConditionSignature: "down");
        AutoPlayEvent up = new("up", AutoPlayEventKind.Interaction, Order: 3, ConditionSignature: "up");
        planner.Observe(new("visit", "program"), [unreachable, walk, down, up]);
        planner.ObserveNavigation("visit", "upper", [unreachable, walk, down, up]);
        planner.ObserveNavigation("visit", "lower", [unreachable, walk, down, up]);
        foreach (AutoPlayEvent transport in new[] { down, up })
        {
            planner.Completed("visit", transport.Id, "visit", observedStoryChanged: false, entrySignature: transport.ConditionSignature, entrySignatureAuthoritative: true, targetId: transport.Id);
            AutoPlayEventRoute route = state.Coverage.Visits[0].Events.Single(item => item.Event.Id == transport.Id).Routes.Single();
            planner.RecordNavigationRoute(transport == down ? "upper" : "lower", transport == down ? "lower" : "upper", transport.Id, route);
        }
        state.NavigationGoal = new("upper", unreachable.Id, unreachable.ConditionSignature);
        string before = JsonSerializer.Serialize(state);
        var retained = Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(state);
        AutoPlayPlanStep? plan = planner.PlanNavigation("upper", allowRemoteConditionRoutes: true, avoidFirstEvents: new HashSet<string> { unreachable.Id }, preferredGoal: state.NavigationGoal, readAvailability: (navigation, target) => Ff7AutoPlayPolicy.ReadOrdinarySchedulingAvailability(retained(navigation, target), target.Id, target.Id == unreachable.Id));
        Check(plan is { Event.Id: "walk" } && plan.Goal?.EventId != unreachable.Id, $"An ordinary failed leaf remained the shared goal through a consumed down/up transport cycle: event={plan?.Event.Id}, goal={plan?.Goal}.");
        Check(before == JsonSerializer.Serialize(state), "A scheduling deferral persisted a false impossibility or changed native coverage.");
    }

    private static void CheckAdoption(bool stale)
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent remote = new("remote", AutoPlayEventKind.Interaction, Order: 0, ConditionSignature: "remote-condition");
        AutoPlayEvent walk = new("walk", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "walk-condition");
        planner.Observe(new("visit", "program"), [remote, walk]);
        planner.ObserveNavigation("visit", "component", [remote, walk]);
        state.NavigationGoal = new("component", remote.Id, remote.ConditionSignature);
        state.Active = new("visit", remote.Id, remote.ConditionSignature!, 1, 18, 2, Ff7AutoPlayTargetKind.LinePush, null, 1, "before", NavigationKey: "component");
        planner.BeginEvent("visit", remote.Id);
        Ff7AutoPlayTarget next = new(walk.Id, 35, 1, Ff7AutoPlayTargetKind.ActorTalk, walk.ConditionSignature!, false, default);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [new(remote.Id, 18, 2, Ff7AutoPlayTargetKind.LinePush, remote.ConditionSignature!, false, default), next], true, true) { NavigationKey = "component" };
        AutoPlayPlanStep plan = planner.PlanNavigation("component", avoidFirstEvents: new HashSet<string> { remote.Id }, preferredGoal: state.NavigationGoal) ?? throw new InvalidOperationException("The deferred optional contact hid an ordinary shared alternative.");
        string before = JsonSerializer.Serialize(state);
        AutoPlayPlanStep fresh = stale ? plan with { Goal = new("changed") } : plan;
        bool adopted = Ff7AutoPlayPolicy.TryAdoptOrdinaryAlternative(state, state.Active, state.NavigationGoal, observation, next, plan, fresh, new(false, [], "entry"), new Dictionary<string, string>(), 1, "source");
        if (stale)
        {
            Check(!adopted && before == JsonSerializer.Serialize(state), "A stale ordinary plan modified its owner or coverage.");
            return;
        }
        Check(adopted && state.Active is { EventId: "walk", Accepted: false } && state.NavigationGoal == plan.Goal && state.NativeRoute is null, "An ordinary leg required a complete native route or lost its exact semantic owner.");
        Check(state.Coverage.Visits[0].Events.Single(item => item.Event.Id == remote.Id).Conditions?.ActiveEntry is null, "An ordinary alternative retained the previous unaccepted observational owner.");
        Check(state.Coverage.Visits[0].Events.All(item => !item.Completed && item.Routes.Count == 0 && item.Conditions?.HasExecutedEntry(item.Event.ConditionSignature!) != true), "Ordinary scheduling fabricated completion, a route receipt or a consumed entry.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
