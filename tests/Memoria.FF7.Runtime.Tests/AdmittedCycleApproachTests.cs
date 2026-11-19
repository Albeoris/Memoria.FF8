using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class AdmittedCycleApproachTests
{
    internal static void Run()
    {
        AutoPlayEvent item = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "entry");
        Ff7AutoPlayTarget target = new(item.Id, 1, 1, Ff7AutoPlayTargetKind.ActorTalk, "entry", false, default);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [target], true, true);
        AutoPlayNavigationVisit navigation = new(observation.NavigationKey, "visit", [item]);
        foreach (string kind in new[] { "physical", "movement-cycle", "legacy-arbitrary" })
        {
            foreach (Ff7AutoPlayMovementFailure? region in new Ff7AutoPlayMovementFailure?[] { null, new(0, 10, 0, 10) })
            {
                if (kind == "legacy-arbitrary" && region is null)
                {
                    continue;
                }
                string key = Ff7AutoPlayPolicy.ReadMovementFailureGuardKey(navigation.Key, target.Id, kind, region);
                Ff7AutoPlayState state = new();
                AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = new() { Event = item, Available = true };
                coverage.UnavailableAttempts.Add(new("entry", "visit", key, "observed", item.Id));
                state.Coverage.Visits.Add(new() { Visit = new("visit", "program"), Events = [coverage] });
                state.Coverage.NavigationVisits.Add(navigation);
                state.NavigationGuards[key] = new("visit", 1, EventId: item.Id, NavigationKey: navigation.Key, MovementFailure: region, MovementSourceInside: true, ObservedFingerprint: "observed");
                Ff7AutoPlayActiveEvent active = new("visit", item.Id, "entry", 1, 1, 1, target.Kind, null, 1, "before", NavigationKey: navigation.Key);
                state.Active = active;
                string history = JsonSerializer.Serialize(state);
                AutoPlayEventAvailability actual = Ff7AutoPlayPolicy.ReadComponentAvailability(state, navigation, item, new Dictionary<string, string> { [key] = "observed" });
                Check(!actual.Unavailable && history == JsonSerializer.Serialize(state), "A sampled physical refusal/cycle AABB remained a region-wide event certificate or history was erased.");
                coverage.Unavailable = actual.Unavailable;
                coverage.UnavailableVariants = actual.UnavailableVariants;
                Check(!Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation) && state.Active == active, "An ordinary owner needed a special outside-entry waiver to cross its old heuristic region.");
                Check(!Ff7AutoPlayPolicy.HasSourceRevalidationRefusal(state, navigation.Key, item.Id, "entry"), "A heuristic movement observation created certifying source maintenance debt.");
                Ff7AutoPlayState cold = state.Clone();
                Check(!Ff7AutoPlayPolicy.ReadComponentAvailability(cold, navigation, item, new Dictionary<string, string> { [key] = "observed" }).Unavailable, "Cold restore promoted retained movement advice to native unavailability.");
                cold.NavigationGuards["native-refusal"] = new("visit", 1, EventId: item.Id, NavigationKey: navigation.Key, ObservedFingerprint: "actual-native");
                cold.Coverage.Visits[0].Events[0].UnavailableAttempts.Add(new("entry", "visit", "native-refusal", "actual-native", item.Id));
                Check(Ff7AutoPlayPolicy.ReadComponentAvailability(cold, navigation, item, new Dictionary<string, string> { [key] = "observed", ["native-refusal"] = "actual-native" }).Unavailable, "Removing heuristic certificates erased an independent native refusal.");
            }
        }
        Console.WriteLine("PASS retained physical/cycle advice, ordinary active continuity without waiver, cold history and independent native refusals.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
