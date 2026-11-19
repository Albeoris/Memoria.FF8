using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeSourceRevalidationTests
{
    internal static void Run()
    {
        Ff7AutoPlayState state = new();
        AutoPlayEvent bridge = new("bridge", AutoPlayEventKind.Interaction, ConditionSignature: "executed");
        AutoPlayEvent repair = new("repair", AutoPlayEventKind.Interaction, ConditionSignature: "pending");
        AutoPlayEventRoute route = new("visit", "executed", false, [], bridge.Id);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> transport = new() { Event = bridge, Available = true, Completed = true, Conditions = new(), Routes = [route] };
        transport.Conditions.ConsumedEntries.Add("executed");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> pending = new() { Event = repair, Available = true, Conditions = new() };
        state.Coverage.Visits.Add(new() { Visit = new("visit", "region"), Events = [transport, pending] });
        state.Coverage.NavigationVisits.Add(new("left", "visit", [bridge, repair]));
        state.Coverage.NavigationVisits.Add(new("remote", "visit", [bridge, repair]));
        state.Coverage.NavigationRoutes.Add(new("left", "remote", bridge.Id, route));
        state.VisitGuards["visit"] = new([], [], "native", Ff7AutoPlayPolicy.CurrentDiscoveryRevision);
        state.NavigationGuards["left-absence"] = new("visit", 2, EventId: repair.Id, NavigationKey: "left", SourceTriangle: 0, ObservedFingerprint: "absent");
        state.NavigationGuards["remote-cycle"] = new("visit", 2, EventId: repair.Id, NavigationKey: "remote", SourceTriangle: 1, MovementFailure: new(0, 10, 0, 10), ObservedFingerprint: "blocked", MovementSourceInside: true);
        pending.UnavailableAttempts.Add(new("pending", "visit", "left-absence", "absent", repair.Id));
        pending.UnavailableAttempts.Add(new("pending", "visit", "remote-cycle", "blocked", repair.Id));
        Ff7AutoPlayObservation observation = new("visit", "program", 4, [new(repair.Id, 2, 2, Ff7AutoPlayTargetKind.LinePush, "pending", false, default)], true, true) { NavigationKey = "left" };
        var read = Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(state);
        transport.CompletedAvailabilityFingerprints.Add(read(state.Coverage.NavigationVisits[1], bridge)!.Fingerprint!);
        string guards = JsonSerializer.Serialize(state.NavigationGuards);
        string coverage = JsonSerializer.Serialize(state.Coverage);
        Ff7NativeSourceRevalidation[] debt = Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(state, observation);
        Check(debt.Length == 1 && debt[0].NavigationKey == "remote", "The actual deferred remote source did not expose honest revalidation debt.");
        Check(read(state.Coverage.NavigationVisits[1], repair) is { Unavailable: true }, "Source refresh made the failed target available.");
        AutoPlayPlanStep? plan = Ff7AutoPlayPolicy.CreatePlanner(state).PlanNavigation("left", needsRefresh: visit => visit.Key == "remote", refreshNavigationKey: "remote", preferredGoal: new("remote"), readAvailability: read);
        Check(plan is { IsTravel: true, RefreshVisitKey: "remote", Goal.EventId: null } && plan.Event.Id == bridge.Id && plan.Goal.NavigationKey == "remote", "Shared refresh did not choose the recorded admitted transport/source-only goal.");
        var provedAvailability = new AutoPlayEventAvailability(false, [], "new-owned-capability");
        AutoPlayEventAvailability? Proved(AutoPlayNavigationVisit navigation, AutoPlayEvent native) => navigation.Key == "left" && native.Id == bridge.Id ? provedAvailability : read(navigation, native);
        AutoPlayPlanStep? incidentalSelf = Ff7AutoPlayPolicy.CreatePlanner(state).PlanNavigation("left", readAvailability: Proved);
        Check(incidentalSelf is not null && incidentalSelf.Event.Id == bridge.Id && !Ff7AutoPlayPolicy.IsNativeContinuationGoalPending(state, incidentalSelf.Goal, "left", bridge.Id), "A consumed owned-intermediate bridge became a genuine exploration goal: " + incidentalSelf);
        AutoPlayPlanStep? ownedRefresh = Ff7AutoPlayPolicy.CreatePlanner(state).PlanNavigation("left", needsRefresh: visit => visit.Key == "remote", refreshNavigationKey: "remote", preferredGoal: new("remote"), readAvailability: Proved);
        Check(ownedRefresh is { IsTravel: true, RefreshVisitKey: "remote", Goal.EventId: null } && ownedRefresh.Event.Id == bridge.Id, "Rejected consumed self-admission hid its real shared refresh transport.");
        Check(Ff7AutoPlayPolicy.IsNavigationGoalCompleted(plan!.Goal!, new("visit", bridge.Id, "executed", 4, 1, 2, Ff7AutoPlayTargetKind.LinePush, null, 1, "before", NavigationKey: "left"), "remote"), "Acknowledged arrival did not complete the source-only goal.");
        Check(JsonSerializer.Serialize(state.NavigationGuards) == guards && JsonSerializer.Serialize(state.Coverage) == coverage, "Planning a source refresh changed guards or event consumption.");
        Check(state.SourceRevalidationAttempts.Count == 0, "Travel planning prematurely recorded a completed local proof.");
        Ff7AutoPlayNavigationGuard priorGuard = state.NavigationGuards["remote-cycle"];
        var legacyFacts = new[] { new { Guard = "remote-cycle", priorGuard.Entity, priorGuard.SourceTriangle, priorGuard.MovementFailure, priorGuard.BlockingEntities, priorGuard.SpatialBoundaryTriangles, priorGuard.MovementSourceInside, Fact = "blocked" } };
        string legacyReceipt = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Source = "remote", Event = repair.Id, Condition = "pending", Revision = 1, Facts = legacyFacts })));
        Ff7AutoPlayState legacyAttempt = state.Clone();
        legacyAttempt.SourceRevalidationAttempts.Add(legacyReceipt);
        string rejectedOwnerReceipt = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Source = "remote", Event = repair.Id, Condition = "pending", Revision = 2, Facts = legacyFacts })));
        legacyAttempt.SourceRevalidationAttempts.Add(rejectedOwnerReceipt);
        string guideOnlyReceipt = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Source = "remote", Event = repair.Id, Condition = "pending", Revision = 3, Facts = legacyFacts })));
        legacyAttempt.SourceRevalidationAttempts.Add(guideOnlyReceipt);
        string preTalkTerminalReceipt = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Source = "remote", Event = repair.Id, Condition = "pending", Revision = 4, Facts = legacyFacts })));
        legacyAttempt.SourceRevalidationAttempts.Add(preTalkTerminalReceipt);
        Check(Ff7AutoPlayPolicy.LocalSourceProverRevision == 5 && preTalkTerminalReceipt != debt[0].ReceiptKey, "Expanded native talk terminal contract reused its older negative-attempt namespace.");
        Check(legacyReceipt != debt[0].ReceiptKey && Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(legacyAttempt, observation).Single().NavigationKey == "remote", "A retired prover's receipt suppressed a genuine unfinished remote source.");
        Ff7AutoPlayPolicy.RecordSourceRevalidationAttempt(legacyAttempt, "remote", repair.Id, "pending");
        Check(legacyAttempt.SourceRevalidationAttempts.Contains(legacyReceipt) && legacyAttempt.SourceRevalidationAttempts.Contains(rejectedOwnerReceipt) && rejectedOwnerReceipt != debt[0].ReceiptKey && guideOnlyReceipt != debt[0].ReceiptKey && legacyAttempt.SourceRevalidationAttempts.Contains(guideOnlyReceipt) && legacyAttempt.SourceRevalidationAttempts.Contains(preTalkTerminalReceipt) && Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(legacyAttempt, observation).Length == 0, "Current prover completion failed to suppress the same unchanged source attempt or erased its history.");
        string positiveCapability = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("physical|native-continuation:2")));
        Check(Ff7AutoPlayPolicy.ReadNativeContinuationAvailability("physical") == positiveCapability && JsonSerializer.Serialize(legacyAttempt.Coverage) == coverage && JsonSerializer.Serialize(legacyAttempt.NavigationGuards) == guards, "Negative-attempt versioning changed positive capability, consumed completion or refusal history.");
        pending.Conditions!.ConsumedEntries.Add("pending");
        Check(Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(state, observation).Length == 0, "All-consumed field invented maintenance debt.");
        AutoPlayEvent frontier = new("frontier", AutoPlayEventKind.Interaction, ConditionSignature: "fresh");
        state.Coverage.Visits[0].Events.Add(new() { Event = frontier, Available = true, Conditions = new() });
        Ff7AutoPlayObservation bridgeObservation = observation with { Events = [.. observation.Events, new(frontier.Id, 3, 2, Ff7AutoPlayTargetKind.ActorTalk, "fresh", false, default)] };
        Check(Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(state, bridgeObservation).Single().EventId == repair.Id, "Consumed blocked bridge lost source debt toward a genuine frontier.");
        state.Coverage.Visits[0].Events.RemoveAt(2);
        pending.Conditions.ConsumedEntries.Remove("pending");
        state.NativeRevalidation = new("remote", repair.Id, "pending", debt[0].ReceiptKey);
        Ff7AutoPlayObservation arrival = observation with { NavigationKey = "remote" };
        Check(!Ff7AutoPlayPolicy.HasArrivedSourceRevalidation(state, arrival), "A transient source triangle satisfied unacknowledged arrival.");
        Ff7AutoPlayState taskCold = state.Clone();
        Check(taskCold.NativeRevalidation == state.NativeRevalidation && taskCold.SourceRevalidationAttempts.Count == 0, "Cold travel changed or completed its local task.");
        taskCold.NativeRevalidation = null;
        Check(taskCold.SourceRevalidationAttempts.Count == 0, "Cancellation fabricated a completed local probe.");
        taskCold.NativeRevalidation = state.NativeRevalidation with { ArrivalAcknowledged = true };
        Check(Ff7AutoPlayPolicy.HasArrivedSourceRevalidation(taskCold, arrival), "Acknowledged arrival did not request its local probe.");
        Check(!Ff7AutoPlayPolicy.HasArrivedSourceRevalidation(taskCold, arrival with { Events = [] }), "A vanished source condition still authorized the local probe.");
        Ff7AutoPlayState released = taskCold.Clone();
        Check(!Ff7AutoPlayPolicy.CancelReleasedSourceRevalidation(released, arrival, new Dictionary<string, string> { ["remote-cycle"] = "blocked" }), "Unchanged actual refusal cancelled its required local probe.");
        Check(Ff7AutoPlayPolicy.CancelReleasedSourceRevalidation(released, arrival, new Dictionary<string, string> { ["remote-cycle"] = "released" }) && released.NativeRevalidation is null && released.SourceRevalidationAttempts.Count == 0, "Actual released refusal did not cancel without a fabricated probe receipt.");
        taskCold.NavigationGuards["remote-cycle"] = taskCold.NavigationGuards["remote-cycle"] with { MovementFailure = new(0, 12, 0, 10) };
        string actualReceipt = Ff7AutoPlayPolicy.ReadSourceRevalidationKey(taskCold, "remote", repair.Id, "pending");
        Ff7AutoPlayPolicy.CompleteSourceRevalidation(taskCold, "remote", repair.Id, "pending");
        Check(taskCold.NativeRevalidation is null && taskCold.SourceRevalidationAttempts.Contains(actualReceipt) && !taskCold.SourceRevalidationAttempts.Contains(debt[0].ReceiptKey), "Completion used departure geometry instead of actual source facts or retained its task.");
        Ff7AutoPlayState cold = state.Clone();
        Check(Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(cold, observation).Single().ReceiptKey == debt[0].ReceiptKey, "Cold restore changed stable source debt identity.");
        Ff7AutoPlayPolicy.RecordSourceRevalidationAttempt(cold, "remote", repair.Id, "pending");
        Check(Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(cold, observation).Length == 0, "An unchanged completed source attempt caused another refresh bounce.");
        Check(JsonSerializer.Serialize(cold.NavigationGuards) == guards && JsonSerializer.Serialize(cold.Coverage) == coverage, "A local-proof receipt changed native refusal or consumption facts.");
        Ff7AutoPlayState changed = cold.Clone();
        changed.NavigationGuards["remote-cycle"] = changed.NavigationGuards["remote-cycle"] with { MovementFailure = new(0, 12, 0, 10) };
        Check(Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(changed, observation).Length == 1, "Changed retained failure geometry did not create a fresh local obligation.");
        cold.NavigationGuards["remote-cycle"] = cold.NavigationGuards["remote-cycle"] with { ObservedFingerprint = "changed-physical" };
        Check(Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(cold, observation).Length == 0, "A released native refusal was treated as a new unavailable source.");
        Ff7AutoPlayState older = JsonSerializer.Deserialize<Ff7AutoPlayState>("{\"SourceRevalidationAttempts\":null}")!;
        older.Validate();
        Check(older.SourceRevalidationAttempts.Count == 0 && new Ff7AutoPlayState().SourceRevalidationAttempts.Count == 0, "Missing/null older nonprogress receipts did not restore empty.");
        Console.WriteLine("PASS honest source refresh, unchanged guards/consumption, stable completed-attempt receipts and cold/default compatibility");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
