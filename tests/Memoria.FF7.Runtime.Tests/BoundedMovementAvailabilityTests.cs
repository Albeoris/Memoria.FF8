using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class BoundedMovementAvailabilityTests
{
    internal static void Run()
    {
        AutoPlayEvent target = new("gateway", AutoPlayEventKind.Transition, ConditionSignature: "entry");
        Ff7AutoPlayState state = new();
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = new() { Event = target, Available = true };
        coverage.UnavailableAttempts.Add(new("entry", "visit", "region", "inside", target.Id));
        coverage.UnavailableAttempts.Add(new("entry", "visit", "region", "outside", target.Id));
        state.Coverage.Visits.Add(new() { Visit = new("visit", "program"), Events = [coverage] });
        AutoPlayNavigationVisit source = new("source", "visit", [target]);
        state.Coverage.NavigationVisits.Add(source);
        state.NavigationGuards["region"] = new("visit", -1, EventId: target.Id, NavigationKey: source.Key, MovementFailure: new(0, 10, 0, 10, 0, 10), MovementSourceInside: false);
        Dictionary<string, string> outside = new() { ["region"] = "outside" };
        Dictionary<string, string> inside = new() { ["region"] = "inside" };
        string history = JsonSerializer.Serialize(state);
        Check(!Ff7AutoPlayPolicy.ReadComponentAvailability(state, source, target, outside).Unavailable, "A historical outside complement denied an entire bounded movement region.");
        Check(Ff7AutoPlayPolicy.ReadComponentAvailability(state, source, target, inside, (_, _) => true).Unavailable, "Actual branch inside membership was hidden by stale saved outside state.");
        Check(history == JsonSerializer.Serialize(state), "Pure applicability projection mutated recorded guards/history.");
        Ff7AutoPlayState current = state.Clone();
        current.NavigationGuards["region"] = current.NavigationGuards["region"] with { MovementSourceInside = true };
        Check(Ff7AutoPlayPolicy.ReadComponentAvailability(current, source, target, inside).Unavailable, "A genuine inside native refusal was erased.");
        Check(!Ff7AutoPlayPolicy.ReadComponentAvailability(current, source, target, outside, (_, _) => false).Unavailable, "Actual branch exit failed to override stale saved inside state.");
        current.NavigationGuards["region"] = current.NavigationGuards["region"] with { MovementSourceInside = null };
        Check(Ff7AutoPlayPolicy.ReadComponentAvailability(current, source, target, outside).Unavailable, "An older unknown source membership was speculatively rearmed.");
        Check(!Ff7AutoPlayPolicy.ReadComponentAvailability(current.Clone(), source, target, outside, (_, _) => false).Unavailable, "Authenticated local arrival could not release older unknown membership.");
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, X4096 = 0, Y4096 = 0, Z4096 = 0 };
        var region = state.NavigationGuards["region"].MovementFailure!;
        Check(Ff7AutoPlayPolicy.ReadMovementSourceInside(region, actor) && Ff7AutoPlayPolicy.ReadMovementSourceInside(region, actor with { X4096 = 10, Y4096 = 10, Z4096 = 10 }), "An inclusive native region endpoint escaped its genuine refusal.");
        Check(!Ff7AutoPlayPolicy.ReadMovementSourceInside(region, actor with { X4096 = -1 }) && !Ff7AutoPlayPolicy.ReadMovementSourceInside(region, actor with { Z4096 = 11 }), "An outside/vertical complement remained part of the bounded region.");
        foreach (bool structural in new[] { false, true })
        {
            Ff7AutoPlayState independent = state.Clone();
            independent.NavigationGuards["other"] = new("visit", -1, EventId: target.Id, NavigationKey: source.Key, MovementFailure: structural ? null : region, MovementSourceInside: structural ? false : true);
            independent.Coverage.Visits[0].Events[0].UnavailableAttempts.Add(new("entry", "visit", "other", "blocked", target.Id));
            Check(Ff7AutoPlayPolicy.ReadComponentAvailability(independent, source, target, new Dictionary<string, string>(outside) { ["other"] = "blocked" }).Unavailable, "Exiting one region bypassed another applicable/structural native refusal.");
        }
        Ff7AutoPlayState topology = state.Clone();
        Ff7AutoPlayPolicy.RecordGatewayTopology(topology, source, target, 0, -1, false);
        Check(Ff7AutoPlayPolicy.ReadComponentAvailability(topology, source, target, outside).Unavailable, "Bounded movement applicability erased an independent no-contact gateway topology proof.");
        Console.WriteLine("PASS bounded inside/outside/endpoint applicability, pure actual/remote/legacy projection, unchanged history and independent structural/topology refusals");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
