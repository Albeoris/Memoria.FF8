using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeReusableWorldModeTests
{
    internal static void Run()
    {
        WorldScriptAnalysis inert = new(true, null, 0, 0, [], [], [], []) { IsInert = true };
        WorldScriptAnalysis rider = inert with { IsInert = false, IsPureFieldTransfer = true, Transitions = [new(1, WorldOperation.FieldTransition, 1)] };
        Ff7AutoPlayWorldModeGoal goal = new("mesh:1:2:script:7", new(1, 2), 7, 0);
        Ff7WorldTerrainReference landing = new(1, 2, 0);
        Ff7AutoPlayWorldGuideFace face = new(landing, Vector3.Zero, new(100, 0, 0), new(0, 0, 100), 7);
        Ff7AutoPlayWorldGuide path = new([face], 1, false, null);
        Ff7AutoPlayWorldModeGuide guide = new(path, 0, landing, landing, path, [landing], 2, false, null);
        bool Needed(ushort? destination = 428, int controlled = 3, int? surfaceRider = 0, Ff7AutoPlayWorldModeGuide? actual = null, WorldScriptAnalysis? vehicle = null, WorldScriptAnalysis? foot = null) => Ff7AutoPlayPolicy.IsRequestedWorldModeNeeded(destination, goal, controlled, surfaceRider, actual ?? guide, vehicle ?? inert, foot ?? rider, selector => selector == 1 ? 428 : 100);
        Check(Needed(), "A consumed landing prerequisite still needed by the requested rider field was not reusable.");
        Check(!Needed(destination: null) && !Needed(destination: 100) && !Needed(controlled: 0) && !Needed(surfaceRider: 1), "An absent/wrong goal, already returned rider or wrong attachment reused landing work.");
        Check(!Ff7AutoPlayPolicy.IsRequestedWorldModeNeeded(428, goal, 3, 0, null, inert, rider, _ => 428), "Missing native landing advice acquired reusable mode authority.");
        Check(!Needed(actual: guide with { Limited = true }) && !Needed(actual: guide with { RiderModel = 1 }) && !Needed(actual: guide with { RiderLanding = null }), "Incomplete or mismatched landing proof acquired mode reuse.");
        Check(!Needed(vehicle: inert with { Complete = false }) && !Needed(vehicle: rider) && !Needed(foot: rider with { Complete = false }) && !Needed(foot: inert), "Unknown, active vehicle or inert rider execution was treated as a proved landing prerequisite.");
        Ff7AutoPlayState state = new() { DestinationField = 428 };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent mode = new("mode:surface-rider:entry", AutoPlayEventKind.Interaction, ConditionSignature: "current-native-region");
        AutoPlayEvent oneShot = new("one-shot", AutoPlayEventKind.Interaction, ConditionSignature: "one-shot-condition");
        planner.Observe(new("world", "program"), [mode, oneShot]);
        planner.ObserveNavigation("world", "world-nav", [mode, oneShot]);
        planner.Completed("world", mode.Id, entrySignature: mode.ConditionSignature, entrySignatureAuthoritative: true, targetId: mode.Id);
        planner.Completed("world", oneShot.Id, entrySignature: oneShot.ConditionSignature, entrySignatureAuthoritative: true, targetId: oneShot.Id);
        var coverage = state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == mode.Id);
        string History() => JsonSerializer.Serialize(state.Coverage.Visits.Select(visit => visit.Events.Select(item => new { item.Event.Id, item.Completed, item.Conditions, item.Routes, item.RetiredRoutes })));
        string before = History();
        Check(!Ff7AutoPlayPolicy.IsWorldModeActionable(coverage, mode, new Dictionary<string, string>()), "A consumed ordinary mode goal changed its default one-shot behavior.");
        Check(Ff7AutoPlayPolicy.IsWorldModeActionable(coverage, mode, new Dictionary<string, string>(), reusableMode: true), "A genuinely needed native mode prerequisite was hidden by prior consumption.");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> blocked = new() { Event = mode, Completed = true, Conditions = coverage.Conditions, UnavailableAttempts = [new(mode.ConditionSignature!, "world", "physical", "blocked", mode.Id)] };
        Check(!Ff7AutoPlayPolicy.IsWorldModeActionable(blocked, mode, new Dictionary<string, string> { ["physical"] = "blocked" }, reusableMode: true), "Reusable mode eligibility bypassed an actual physical refusal.");
        Ff7AutoPlayObservation observation = new("world", "program", 1, [], true, true) { NavigationKey = "world-nav" };
        _ = Ff7AutoPlayPolicy.ResolveWorldDestinationGoal(state, observation, new HashSet<string> { mode.Id }, _ => new(false, [], null), id => id == mode.Id && Needed());
        Check(planner.PlanNavigation("world-nav", preferredGoal: state.NavigationGoal)?.Event.Id == mode.Id, "The shared planner could not select the requested consumed mode after proved rearming.");
        Check(coverage.RouteNeedsRediscovery && before == History(), "Mode rearming cleared consumption, completion or native route history.");
        Check(!Ff7AutoPlayPolicy.IsWorldModeActionable(state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == oneShot.Id), oneShot, new Dictionary<string, string>()), "Reusing one needed mode rearmed an unrelated one-shot event.");
        Check(!Ff7AutoPlayPolicy.IsWorldModeActionable(state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == oneShot.Id), oneShot, new Dictionary<string, string>(), reusableMode: true), "Mode reuse rearmed an unrelated one-shot event even when the caller requested reuse.");
        Ff7AutoPlayState cold = JsonSerializer.Deserialize<Ff7AutoPlayState>(JsonSerializer.Serialize(state))!;
        Check(JsonSerializer.Serialize(cold.Coverage) == JsonSerializer.Serialize(state.Coverage), "Cold restore changed retained mode consumption or rearm metadata.");
        Console.WriteLine("Explicit requested native rider prerequisites remain reusable without rewriting consumed event history.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
