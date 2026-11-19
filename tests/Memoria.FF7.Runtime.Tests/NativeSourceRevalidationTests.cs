using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeSourceRevalidationTests
{
    internal static void Run()
    {
        AutoPlayEvent actor = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "entry");
        Ff7AutoPlayTarget target = new(actor.Id, 1, 1, Ff7AutoPlayTargetKind.ActorTalk, "entry", false, default);
        Ff7AutoPlayState state = new();
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = new() { Event = actor, Available = true, Conditions = new() };
        state.Coverage.Visits.Add(new() { Visit = new("visit", "program"), Events = [coverage] });
        state.Coverage.NavigationVisits.Add(new("left", "visit", [actor]));
        state.Coverage.NavigationVisits.Add(new("remote", "visit", [actor]));
        state.VisitGuards["visit"] = new([], [], "facts", Ff7AutoPlayPolicy.CurrentDiscoveryRevision);
        foreach (string key in new[] { "legacy-arbitrary-region", Ff7AutoPlayPolicy.ReadMovementFailureGuardKey("remote", actor.Id, "physical", null) })
        {
            state.NavigationGuards[key] = new("visit", 1, EventId: actor.Id, NavigationKey: "remote", MovementFailure: key.StartsWith("legacy", StringComparison.Ordinal) ? new(0, 10, 0, 10) : null, MovementSourceInside: true, ObservedFingerprint: "failed");
            coverage.UnavailableAttempts.Add(new("entry", "visit", key, "failed", actor.Id));
        }
        Ff7AutoPlayObservation current = new("visit", "program", 1, [target], true, true) { NavigationKey = "left" };
        string history = JsonSerializer.Serialize(state);
        Check(Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(state, current).Length == 0 && !Ff7AutoPlayPolicy.HasSourceRevalidationRefusal(state, "remote", actor.Id, "entry"), "Uncertified movement observations invented source maintenance debt.");
        Check(!Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(state)(state.Coverage.NavigationVisits[1], actor)!.Unavailable && history == JsonSerializer.Serialize(state), "Legacy region/sample history became global unavailability or was erased.");
        string positive = Ff7AutoPlayPolicy.ReadNativeContinuationAvailability("native-facts");
        string originalGuards = JsonSerializer.Serialize(state.NavigationGuards);
        string originalCoverage = JsonSerializer.Serialize(state.Coverage);
        for (int revision = 1; revision <= 6; revision++)
        {
            state.SourceRevalidationAttempts.Add(new string((char)('A' + revision), 64));
        }
        state.NativeRevalidation = new("remote", actor.Id, "entry", new('A', 64), true);
        Ff7AutoPlayState cold = state.Clone();
        Ff7AutoPlayObservation arrived = current with { NavigationKey = "remote" };
        Check(Ff7AutoPlayPolicy.HasArrivedSourceRevalidation(cold, arrived), "An older optional arrival record was refused on restore.");
        Dictionary<string, string> facts = cold.NavigationGuards.Keys.ToDictionary(key => key, _ => "failed");
        Check(Ff7AutoPlayPolicy.CancelReleasedSourceRevalidation(cold, arrived, facts) && cold.NativeRevalidation is null && cold.SourceRevalidationAttempts.SetEquals(state.SourceRevalidationAttempts), "Retiring unproved maintenance fabricated probe completion or removed old attempts.");
        Check(JsonSerializer.Serialize(cold.NavigationGuards) == originalGuards && JsonSerializer.Serialize(cold.Coverage) == originalCoverage && Ff7AutoPlayPolicy.ReadNativeContinuationAvailability("native-facts") == positive, "Cache compatibility changed refusal history, consumption or positive identity.");
        Check(Ff7AutoPlayPolicy.LocalSourceProverRevision == 7, "Actual-ready talk successor contract lost its observational namespace.");
        Ff7AutoPlayState absent = JsonSerializer.Deserialize<Ff7AutoPlayState>("{}")!;
        absent.Validate();
        Check(absent.SourceRevalidationAttempts.Count == 0 && absent.NativeRevalidation is null, "Absent old optional caches did not restore empty.");
        Console.WriteLine("PASS retired unproved source debt, legacy arbitrary-region/sample advice, arrived-cache compatibility and unchanged history/positive receipts.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
