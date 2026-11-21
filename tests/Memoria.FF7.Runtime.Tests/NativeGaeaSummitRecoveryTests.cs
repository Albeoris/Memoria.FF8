using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeGaeaSummitRecoveryTests
{
    private const string Program = "F7A52553710150AA23F29CF56148D003AC63921F0EEA2AC178989F329961993B";

    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh = [new(0, new(-100, -100, 0), new(200, -100, 0), new(-100, 200, 0), -1, -1, -1)];
        Ff7AutoPlayTarget[] targets = Enumerable.Range(3, 3).Select(entity => new Ff7AutoPlayTarget($"line:{entity - 3}:entity:{entity}:script:4", entity, 4, Ff7AutoPlayTargetKind.LineTouch, $"healing-{entity}", false, default)).ToArray();
        Ff7Line[] lines = [new(0, 3, 10, 10, 0, 20, 10, 0), new(1, 4, 70, 10, 0, 80, 10, 0), new(2, 5, 110, 10, 0, 120, 10, 0)];
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 9, Model = 0, PlayerControlled = true, Triangle = 0, CollisionRadius = 10 }], Lines = lines, BlockedTriangles = [0] };
        Ff7AutoPlayObservation observation = new("visit", Program, 1, targets, true, true) { NavigationKey = "summit" };
        Ff7AutoPlayState state = new() { DestinationField = 698 };
        Ff7AutoPlayTarget? Read(Ff7AutoPlayObservation? actual = null, Ff7FieldProcess? native = null) => Ff7AutoPlayPolicy.ReadNativeSummitRecovery(state, actual ?? observation, native ?? process, mesh);
        string before = JsonSerializer.Serialize(new { state, observation, process, mesh });
        Check(Read()?.Entity == 3, "The nearest actual reachable recovery LINE was not preferred.");
        state.DestinationField = 695;
        Check(Read()?.Entity == 3, "The second supported onward destination lost native recovery priority.");
        state.DestinationField = 698;
        Check(Read(observation with { Events = [] }) is null, "A missing native recovery target was invented.");
        Check(Read(observation with { ProgramIdentity = "other" }) is null, "An unrelated profile inherited recovery advice.");
        Check(Read(native: process with { Actors = [process.Actors.Single() with { Entity = 8 }] }) is null, "Another controlled actor acquired summit recovery advice.");
        Check(Read(native: process with { PlayerLocked = true }) is null && Read(native: process with { Dialogues = [new(1, 0, 1, null, null, 0)] }) is null, "Owned controls or an unrelated live display were preempted by healing advice.");
        Check(Read(observation with { Events = targets.Select(target => target with { AnalysisComplete = false }).ToArray() }) is null, "Incomplete native sources received healing priority.");
        Check(Read(native: process with { Lines = lines.Select(line => line with { Enabled = 0 }).ToArray() }) is null, "Disabled native LINEs received healing priority.");
        state.DestinationField = 694;
        Check(Read() is null, "An unrelated destination received recovery priority.");
        state.DestinationField = 698;
        Ff7AutoPlayActiveEvent active = new("visit", targets[0].Id, targets[0].ConditionFingerprint, 1, 3, 4, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, NavigationKey: "summit");
        state.Active = active;
        Check(Read() is null, "A real accepted source was replaced before its native return.");
        state.Active = null;
        Check(before == JsonSerializer.Serialize(new { state, observation, process, mesh }), "Recovery qualification mutated native facts or historical coverage.");
        Ff7AutoPlayActiveEvent optional = active with { EventId = "optional", ConditionFingerprint = "optional-condition", Entity = 6, Accepted = false };
        state.Active = optional;
        state.NavigationGoal = new("summit", optional.EventId, optional.ConditionFingerprint);
        string history = JsonSerializer.Serialize(state.Coverage);
        Check(Ff7AutoPlayPolicy.RetireUnacceptedSummitPlan(state, observation, process, mesh) && state.Active is null && state.NavigationGoal is null && state.DestinationField == 698, "An exact unaccepted transient plan prevented native recovery priority.");
        Check(history == JsonSerializer.Serialize(state.Coverage), "Recovery preference erased coverage history.");
        foreach (Ff7AutoPlayActiveEvent protectedActive in new[] { optional with { Accepted = true }, optional with { Generation = 2 }, optional with { NavigationKey = "foreign" }, optional with { Returned = true } })
        {
            state.Active = protectedActive;
            Check(!Ff7AutoPlayPolicy.RetireUnacceptedSummitPlan(state, observation, process, mesh) && state.Active == protectedActive, "An accepted, returned or foreign source was retired for recovery.");
        }
        state.Active = null;
        CheckReceiptCompletion(state, observation, process, mesh, targets[0]);
        Console.WriteLine("Native summit recovery selects real contacts and requires both actual recovery and completed source accounting.");
    }

    private static void CheckReceiptCompletion(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, IReadOnlyList<PcWalkmeshTriangle> mesh, Ff7AutoPlayTarget target)
    {
        FieldHostRequest request = new(7, 3, 1, 0x0377, new FieldPartyRecovery(AllCharacters: true));
        Ff7AutoPlayActiveEvent active = new(observation.VisitId, target.Id, target.ConditionFingerprint, observation.Generation, 3, 4, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, NavigationKey: observation.NavigationKey);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, 0x0370, 20) };
        state.Active = active;
        string native = JsonSerializer.Serialize(new { process, request, mesh });
        bool Record(FieldHostRequest actual, FieldHostReply? reply = null, long generation = 1, string program = Program) => Ff7AutoPlayPolicy.RecordNativeSummitRecovery(state, generation, program, actual, reply ?? new(true));
        foreach (FieldHostRequest invalid in new[] { request with { Entity = 6 }, request with { Priority = 7 }, request with { ProgramCounter = 0x0378 }, request with { Command = new FieldPartyRecovery() } })
        {
            Check(!Record(invalid) && state.SummitRecovery is null, "An unrelated operation created recovery evidence.");
        }
        Check(!Record(request, new(false)) && !Record(request, generation: 2) && !Record(request, program: "other"), "An incomplete or foreign operation created recovery evidence.");
        state.Active = active with { Accepted = false };
        Check(!Record(request), "An unadmitted root acquired recovery evidence.");
        state.Active = active with { ExecutionProvenance = active.ExecutionProvenance! with { Owners = [] } };
        Check(!Record(request), "Cleared observational ownership acquired a later same-slot recovery operation.");
        state.Active = active;
        Check(Record(request) && state.SummitRecovery is { Completed: false }, "A real owned native recovery was not retained before its receipt was pruned.");
        Ff7AutoPlayActiveEvent returned = active with { Returned = true, ExecutionProvenance = active.ExecutionProvenance! with { RootReturned = true, Owners = [] } };
        bool Complete(Ff7AutoPlayActiveEvent actual, Ff7FieldProcess? nativeProcess = null, Ff7AutoPlayObservation? observed = null) => Ff7AutoPlayPolicy.CompleteNativeSummitRecovery(state, observed ?? observation, actual, nativeProcess ?? process);
        Check(!Complete(active), "The recovery operation completed its still-running root.");
        Check(!Complete(returned with { ExecutionProvenance = returned.ExecutionProvenance! with { RootDispatchTick = 21 } }), "A replacement root lifetime sealed old recovery evidence.");
        Check(!Complete(returned with { ExecutionProvenance = returned.ExecutionProvenance! with { Owners = active.ExecutionProvenance!.Owners } }), "Outstanding native children were discarded by recovery accounting.");
        Check(!Complete(returned, process with { PlayerLocked = true }) && !Complete(returned, process with { Dialogues = [new(1, 0, 1, null, null, 0)] }), "Locked controls or unfinished UI sealed recovery evidence.");
        Check(!Complete(returned, observed: observation with { Generation = 2 }) && !Complete(returned, observed: observation with { ProgramIdentity = "other" }), "A foreign source sealed recovery evidence.");
        state.Active = null;
        Check(Ff7AutoPlayPolicy.ReadNativeSummitRecovery(state, observation, process, mesh) is not null, "Unsealed operation evidence prematurely suppressed recovery priority.");
        state.Active = returned;
        Check(Complete(returned) && state.SummitRecovery is { Completed: true } && !Complete(returned), "A real whole-root return did not seal recovery exactly once.");
        state.Active = null;
        Check(Ff7AutoPlayPolicy.ReadNativeSummitRecovery(state, observation, process, mesh) is null, "Pruned live receipts erased authenticated completed recovery.");
        Ff7AutoPlayState cold = JsonSerializer.Deserialize<Ff7AutoPlayState>(JsonSerializer.Serialize(state))!;
        Check(Ff7AutoPlayPolicy.HasCompletedNativeSummitRecovery(cold, observation, process), "Cold restoration lost completed native recovery evidence.");
        Check(JsonSerializer.Deserialize<Ff7AutoPlayState>("{}")!.SummitRecovery is null, "An older save invented recovery evidence.");
        Check(!Ff7AutoPlayPolicy.HasCompletedNativeSummitRecovery(cold, observation with { Generation = 2 }, process) && !Ff7AutoPlayPolicy.HasCompletedNativeSummitRecovery(cold, observation with { VisitId = "another" }, process), "A different native visit reused recovery evidence.");
        Check(native == JsonSerializer.Serialize(new { process, request, mesh }) && state.Coverage.Visits.Count == 0, "Recovery evidence accounting changed native facts or fabricated coverage completion.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
