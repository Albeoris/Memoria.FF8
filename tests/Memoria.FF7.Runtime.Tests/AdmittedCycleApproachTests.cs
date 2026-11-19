using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class AdmittedCycleApproachTests
{
    internal static void Run()
    {
        Ff7AutoPlayTarget target = new("actor", 1, 1, Ff7AutoPlayTargetKind.ActorTalk, "entry", false, default);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [target], true, true);
        string source = observation.NavigationKey;
        Ff7AutoPlayMovementFailure region = new(0, 10, 0, 10);
        string key = Ff7AutoPlayPolicy.ReadMovementFailureGuardKey(source, target.Id, "movement-cycle", region);
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent item = new(target.Id, AutoPlayEventKind.Interaction, ConditionSignature: target.ConditionFingerprint);
        planner.Observe(new("visit", "program"), [item]);
        AutoPlayNavigationVisit navigation = new(source, "visit", [item]);
        state.Coverage.NavigationVisits.Add(navigation);
        state.NavigationGuards[key] = new("visit", 1, EventId: target.Id, NavigationKey: source, MovementFailure: region, MovementSourceInside: true);
        planner.MarkUnavailable("visit", target.Id, new("entry", "visit", key, "inside", target.Id));
        Dictionary<string, string> facts = new() { [key] = "inside" };
        Ff7AutoPlayActiveEvent active = new("visit", target.Id, "entry", 1, 1, 1, target.Kind, null, 0, "before", NavigationKey: source, EntryGuardFingerprints: new Dictionary<string, string> { [key] = "outside" });
        state.Active = active;
        string history = JsonSerializer.Serialize(state);
        HashSet<string> admitted = Ff7AutoPlayPolicy.ReadAdmittedCycleGuards(state, active, target, observation, _ => "outside");
        Check(admitted.SetEquals([key]), "The exact outside admission did not identify its old cycle refusal.");
        Check(Ff7AutoPlayPolicy.ReadComponentAvailability(state, navigation, item, facts).Unavailable, "An active continuation erased candidate admission evidence.");
        Check(!Ff7AutoPlayPolicy.ReadComponentAvailability(state, navigation, item, facts, includeRefusal: (guardKey, _) => !admitted.Contains(guardKey)).Unavailable, "Only the authenticated old cycle still blocked its advancing owned approach.");
        Check(history == JsonSerializer.Serialize(state), "Scoped continuation mutated candidate coverage/history.");
        Check(!Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation, (_, _) => true) && ReferenceEquals(state.Active, active), "An admitted advancing continuation lost its owner.");
        foreach (Ff7AutoPlayActiveEvent changed in new[] { active with { VisitId = "other" }, active with { Generation = 2 }, active with { NavigationKey = "other" }, active with { EventId = "other" }, active with { Entity = 2 }, active with { Script = 2 }, active with { Kind = Ff7AutoPlayTargetKind.ActorContact }, active with { ConditionFingerprint = "changed" }, active with { EntryGuardFingerprints = null }, active with { EntryGuardFingerprints = new Dictionary<string, string> { [key] = "inside" } } })
        {
            Check(Ff7AutoPlayPolicy.ReadAdmittedCycleGuards(state, changed, target, observation, _ => "outside").Count == 0, "A changed owner/context or inside entry inherited outside admission.");
        }
        Check(Ff7AutoPlayPolicy.ReadAdmittedCycleGuards(state, active, target, observation, _ => "changed-occupancy").Count == 0, "Changed native physical context inherited the old entry.");
        Ff7AutoPlayState cold = state.Clone();
        Check(Ff7AutoPlayPolicy.ReadAdmittedCycleGuards(cold, cold.Active!, target, observation, _ => "outside").SetEquals(admitted), "Cold/battle-persistent entry evidence changed its scope.");
        foreach (string kind in new[] { "physical", "movement-cycle" })
        {
            string otherKey = Ff7AutoPlayPolicy.ReadMovementFailureGuardKey(source, target.Id, kind, new(20, 30, 20, 30));
            Ff7AutoPlayState blocked = state.Clone();
            blocked.NavigationGuards[otherKey] = new("visit", 1, EventId: target.Id, NavigationKey: source, MovementFailure: kind == "physical" ? null : new(20, 30, 20, 30), MovementSourceInside: true);
            blocked.Coverage.Visits[0].Events[0].UnavailableAttempts.Add(new("entry", "visit", otherKey, "new-refusal", target.Id));
            var both = new Dictionary<string, string>(facts) { [otherKey] = "new-refusal" };
            Check(Ff7AutoPlayPolicy.ReadComponentAvailability(blocked, navigation, item, both, includeRefusal: (guardKey, _) => !admitted.Contains(guardKey)).Unavailable, "An independent structural/new-cycle refusal was bypassed.");
        }
        Check(Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation, (_, _) => false) && state.Active is null, "A nonadvancing ordinary action inherited an unconditional cycle exemption.");
        Console.WriteLine("PASS exact admitted-cycle owner/context/cold scope, unchanged candidate/history and independent refusal rejection");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
