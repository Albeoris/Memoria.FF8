using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class OrdinaryAlternativeTests
{
    internal static void Run()
    {
        CheckStaleNavigationRetirement();
        CheckUnknownContactTravelSibling();
        CheckAdoption(false);
        CheckAdoption(true);
        CheckAdoption(false, explicitSource: true);
        CheckDeferredLeafDoesNotCreateTransportCycle();
        Console.WriteLine("PASS ordinary alternative shared selection, atomic owner/goal adoption, stale-plan refusal and unconsumed history.");
    }

    private static void CheckStaleNavigationRetirement()
    {
        Ff7AutoPlayActiveEvent planned = new("visit", "line", "current", 2, 1, 5, Ff7AutoPlayTargetKind.LineEnter, null, 641, "witness", NavigationKey: "lower");
        Ff7AutoPlayObservation observed = new("visit", "program", 2, [], true, true) { NavigationKey = "upper" };
        Ff7AutoPlayState state = new() { Active = planned, DestinationField = 99, NavigationGoal = new("lower", "line", "current") };
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = new() { Event = new("line", AutoPlayEventKind.Interaction, ConditionSignature: "current"), Available = true, Conditions = new() { ActiveEntry = "current", ConsumedEntries = new(StringComparer.Ordinal) { "older" } }, Routes = [new("destination", "older", false, [], "line")] };
        state.Coverage.Visits.Add(new() { Visit = new("visit", "program"), Events = [coverage] });
        string history = JsonSerializer.Serialize(withCopy());
        Check(Ff7AutoPlayPolicy.RetireStaleUnacceptedNavigation(state, observed) && state.Active is null && coverage.Conditions.ActiveEntry is null, "A stale unadmitted source plan survived a real component change.");
        Check(state.DestinationField == 99 && state.NavigationGoal == new AutoPlayTravelGoal("lower", "line", "current") && history == JsonSerializer.Serialize(withCopy()), "Stale plan retirement changed intent or actual event history.");
        foreach (Ff7AutoPlayActiveEvent owner in new[] { planned with { Accepted = true }, planned with { Returned = true }, planned with { TransitionOwned = true }, planned with { CompletionRecorded = true }, planned with { Generation = 1 }, planned with { VisitId = "other" }, planned with { NavigationKey = "upper" }, planned with { NavigationKey = null } })
        {
            state.Active = owner;
            Check(!Ff7AutoPlayPolicy.RetireStaleUnacceptedNavigation(state, observed) && state.Active == owner, "Accepted/returned/transition ownership or a different source was retired.");
        }
        state.Active = planned;
        Check(!Ff7AutoPlayPolicy.RetireStaleUnacceptedNavigation(state, observed with { CanAcceptInput = false }), "An unready native owner was preempted.");

        object withCopy() => new { coverage.Event, coverage.Available, coverage.Completed, coverage.Conditions.ConsumedEntries, coverage.Routes, coverage.UnavailableAttempts, coverage.Unavailable };
    }

    private static void CheckUnknownContactTravelSibling()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent contact = new("unknown-contact", AutoPlayEventKind.Interaction, Order: 0, ConditionSignature: "source-pose");
        AutoPlayEvent departure = new("departure", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "departure-condition");
        AutoPlayEvent remote = new("fresh-remote", AutoPlayEventKind.Interaction, Order: 2, ConditionSignature: "remote-condition");
        planner.Observe(new("source", "program"), [contact, departure]);
        planner.Observe(new("destination", "other-program"), [remote]);
        planner.ObserveNavigation("source", "here", [contact, departure]);
        planner.ObserveNavigation("destination", "there", [remote]);
        planner.Completed("source", departure.Id, "destination", observedStoryChanged: false, entrySignature: departure.ConditionSignature, entrySignatureAuthoritative: true, targetId: departure.Id);
        AutoPlayEventRoute route = state.Coverage.Visits.Single(visit => visit.Visit.Key == "source").Events.Single(item => item.Event.Id == departure.Id).Routes.Single();
        planner.RecordNavigationRoute("here", "there", departure.Id, route);
        state.NavigationGoal = new("here", contact.Id, contact.ConditionSignature);
        state.Active = new("source", contact.Id, contact.ConditionSignature!, 1, 15, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 641, "before", NavigationKey: "here");
        var owner = state.Active;
        string history = JsonSerializer.Serialize(state.Coverage);
        var availability = Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(state);
        AutoPlayPlanStep? Choose(bool local) => planner.PlanNavigation("here", allowRemoteConditionRoutes: true, avoidFirstEvents: new HashSet<string> { contact.Id }, preferredGoal: state.NavigationGoal, readAvailability: (navigation, target) => Ff7AutoPlayPolicy.ReadOrdinarySchedulingAvailability(availability(navigation, target), target.Id, local && navigation.Key != "here"));
        AutoPlayPlanStep? ordinary = Ff7AutoPlayPolicy.ReadOrdinaryAlternativePasses(true, owner, null).Select(Choose).FirstOrDefault(plan => plan is not null);
        Check(ordinary is null, "A known fresh local repair unexpectedly enabled remote travel siblings.");
        AutoPlayPlanStep? sibling = Ff7AutoPlayPolicy.ReadOrdinaryAlternativePasses(true, owner, owner).Select(Choose).FirstOrDefault(plan => plan is not null);
        Check(sibling is { IsTravel: true, Event.Id: "departure", Goal.EventId: "fresh-remote" }, "An owned unknown contact search monopolized a reachable fresh remote sibling.");
        Check(history == JsonSerializer.Serialize(state.Coverage), "Alternative eligibility fabricated entry credit or a route.");
        Ff7AutoPlayTarget target = new(departure.Id, 2, 0, Ff7AutoPlayTargetKind.Gateway, departure.ConditionSignature!, false, default, DestinationField: 2);
        Ff7AutoPlayObservation observation = new("source", "program", 1, [target], true, true) { NavigationKey = "here" };
        Check(!Ff7AutoPlayPolicy.TryAdoptOrdinaryAlternative(state, owner, state.NavigationGoal, observation, target, sibling!, sibling, new(true, new HashSet<string> { departure.Id }, "blocked"), new Dictionary<string, string>(), 641, "before") && state.Active == owner, "Unknown-contact fairness bypassed native physical availability.");
        Check(Ff7AutoPlayPolicy.TryAdoptOrdinaryAlternative(state, owner, state.NavigationGoal, observation, target, sibling!, sibling, new(false, [], "available"), new Dictionary<string, string>(), 641, "before") && state.Active is { EventId: "departure", Accepted: false } && state.NavigationGoal?.EventId == remote.Id, "The sibling failed normal own-identity/goal adoption.");
        Check(state.Coverage.Visits.Single(visit => visit.Visit.Key == "source").Events.Single(item => item.Event.Id == contact.Id).Conditions?.HasExecutedEntry(contact.ConditionSignature!) != true, "Travel sibling adoption credited the unknown source-pose condition.");
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

    private static void CheckAdoption(bool stale, bool explicitSource = false)
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
        if (explicitSource)
        {
            state.SourceGoal = Ff7AutoPlayPolicy.CreateSourceGoal(observation, observation.Events[0], 100);
            state.NavigationGoal = null;
            Ff7AutoPlayPolicy.RetainDestinationGoal(state, new("visit", remote, false), "component", remote.ConditionSignature!);
            Check(state.NavigationGoal == new AutoPlayTravelGoal("component", remote.Id, remote.ConditionSignature), "Selecting the exact local source cleared its pending navigation goal.");
            state.NavigationGoal = null;
        }
        string before = JsonSerializer.Serialize(state);
        AutoPlayPlanStep fresh = stale ? plan with { Goal = new("changed") } : plan;
        bool adopted = Ff7AutoPlayPolicy.TryAdoptOrdinaryAlternative(state, state.Active, state.NavigationGoal, observation, next, plan, fresh, new(false, [], "entry"), new Dictionary<string, string>(), 1, "source");
        if (stale || explicitSource)
        {
            Check(!adopted && before == JsonSerializer.Serialize(state), "A stale or unrelated ordinary plan modified its owner or coverage.");
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
