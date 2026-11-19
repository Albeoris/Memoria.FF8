using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayDestinationPreferenceTests
{
    public static void Run()
    {
        Ff7AutoPlayState state = new();
        AutoPlayVisit source = new("ff7:100:source", "region");
        AutoPlayEvent back = new("back", AutoPlayEventKind.Transition, Order: 10000, ConditionSignature: "back");
        AutoPlayEvent forward = new("forward", AutoPlayEventKind.Transition, Order: 10002, ConditionSignature: "forward");
        Dictionary<string, int> destinations = new() { [back.Id] = 200, [forward.Id] = 300 };
        AutoPlayPlanner<Ff7AutoPlayDeferral> Create(Ff7AutoPlayState current)
        {
            return Ff7AutoPlayPolicy.CreatePlanner(current, (_, item) => destinations.TryGetValue(item.Id, out int destination) ? Ff7AutoPlayPolicy.NativeDestinationPhase(current, destination) : 0, (_, item) => destinations.TryGetValue(item.Id, out int destination) && destination > 64 && destination != short.MaxValue && Ff7AutoPlayPolicy.NativeDestinationPhase(current, destination) == 0);
        }
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Create(state);
        planner.Observe(new("ff7:200:visited", "region"), []);
        planner.Observe(new("ff7:64:world", "region"), []);
        planner.Observe(new($"ff7:{short.MaxValue}:sentinel", "region"), []);
        planner.Observe(source, [back, forward]);
        Check(planner.Plan(source.Key)?.Event.Id == forward.Id, "An unseen native destination must precede an unexecuted return gateway.");
        Check(planner.Plan(source.Key, preferredGoal: new(source.Key, back.Id, back.ConditionSignature))?.Event.Id == back.Id, "Destination novelty must preserve a valid preferred goal.");
        planner.Observe(source, [back]);
        Check(planner.Plan(source.Key)?.Event.Id == back.Id, "A visited destination must remain selectable when it is the only frontier.");
        AutoPlayEvent talk = new("talk", AutoPlayEventKind.Interaction, Order: 80, ConditionSignature: "talk");
        planner.Observe(source, [back, forward, talk]);
        Check(planner.Plan(source.Key)?.Event.Id == talk.Id, "Ordinary interactions must retain their existing precedence over novel gateways.");
        var local = state.Coverage.Visits.Single(visit => visit.Visit.Key == source.Key);
        local.Events.Single(item => item.Event.Id == talk.Id).UnavailableAttempts.Add(new(talk.ConditionSignature!, source.Key, "physical-advice", "same-geometry", talk.Id));
        local.Events.Single(item => item.Event.Id == forward.Id).UnavailableAttempts.Add(new(forward.ConditionSignature!, source.Key, "physical-advice", "same-geometry", forward.Id));
        string unchanged = JsonSerializer.Serialize(state);
        Check(planner.Plan(source.Key)?.Event.Id == forward.Id, "Repeated optional approaches outranked an equally attempted exit to an unseen native field.");
        Check(JsonSerializer.Serialize(state) == unchanged && !local.Events.Single(item => item.Event.Id == talk.Id).Completed, "Exploration scheduling consumed or rewrote the deferred local objective.");
        planner.Observe(source, [back, forward]);
        string before = JsonSerializer.Serialize(state);
        Ff7AutoPlayState restored = JsonSerializer.Deserialize<Ff7AutoPlayState>(before)!;
        AutoPlayPlanner<Ff7AutoPlayDeferral> cold = Create(restored);
        Check(cold.Plan(source.Key)?.Event.Id == forward.Id, "Cold coverage must retain native destination novelty after transient lookup is supplied.");
        Check(JsonSerializer.Serialize(restored) == before, "Destination preference must not change serialized coverage or introduce saved hints.");
        cold.Observe(new("ff7:300:now-visited", "region"), []);
        Check(cold.Plan(source.Key)?.Event.Id == back.Id, "Once both destinations are visited, native event order must resume.");
        Check(Ff7AutoPlayPolicy.NativeDestinationPhase(state, 200) == 1, "An exact visited field must receive the return phase.");
        foreach (int? destination in new int?[] { null, -1, 0, 64, short.MaxValue, 20, 300 })
        {
            Check(Ff7AutoPlayPolicy.NativeDestinationPhase(state, destination) == 0, "Unknown, world, sentinel and nonmatching field destinations must remain neutral.");
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
