using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeReturnedLineLifecycleTests
{
    internal static void Run()
    {
        foreach (Ff7AutoPlayTargetKind kind in new[] { Ff7AutoPlayTargetKind.LineTouch, Ff7AutoPlayTargetKind.LinePush })
        {
            Ff7AutoPlayActiveEvent active = new("visit", "line", "original", 1, 0, 4, kind, null, 0, "before", NavigationKey: "navigation", Accepted: true, Returned: true);
            active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 2, 10) with { RootReturned = true, Owners = [] } };
            Ff7AutoPlayState state = new() { Active = active };
            Ff7AutoPlayTarget queued = new("line", 0, 4, kind, "after-own-writes", false, default);
            Ff7AutoPlayObservation observation = new("visit", "program", 1, [queued], true, true) { NavigationKey = "navigation" };
            FieldEntitySnapshot entity = new(5, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
            FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 20, true, new byte[1280], new byte[256], [entity], PendingRequests: []);
            string before = JsonSerializer.Serialize(new { state, observation, queued, vm });
            bool Read(Ff7AutoPlayActiveEvent candidate, Ff7AutoPlayObservation current, Ff7AutoPlayTarget line, FieldVmSnapshot native) => Ff7AutoPlayPolicy.IsReturnedQueuedLineActivation(new() { Active = candidate }, current, line, native);
            Check(Read(active, observation, queued, vm), "A real returned LINE was reclassified as queued work after its own condition writes.");
            foreach (Ff7AutoPlayActiveEvent invalid in new[]
            {
                active with { Accepted = false }, active with { Returned = false }, active with { CompletionRecorded = true },
                active with { Generation = 2 }, active with { VisitId = "other" }, active with { NavigationKey = "other" },
                active with { EventId = "other" }, active with { Script = 3 }, active with { Entity = 1 },
                active with { ExecutionProvenance = null },
                active with { ExecutionProvenance = active.ExecutionProvenance! with { RootReturned = false } },
                active with { ExecutionProvenance = active.ExecutionProvenance! with { RootDispatchTick = 21 } }
            })
            {
                Check(!Read(invalid, observation, queued, vm), "An unreturned, foreign or stale activation bypassed native queued LINE handling.");
            }
            Check(!Read(active, observation, queued with { Kind = Ff7AutoPlayTargetKind.ActorTalk }, vm), "A non-LINE event acquired returned LINE authority.");
            Check(!Read(active, observation, queued with { AnalysisComplete = false }, vm), "Unknown queued source analysis was treated as a completed activation.");
            Check(!Read(active, observation, queued, vm with { ProgramIdentity = "other" }), "Another native program borrowed returned LINE provenance.");
            Check(!Read(active, observation, queued, vm with { Entities = [entity with { Priority = 1 }] }), "A current P1 activation was silently treated as already returned.");
            Check(!Read(active, observation, queued, vm with { PendingRequests = [new(9, 0, 1, 5, new FieldDialogueCommand(0, 1))] }), "Outstanding native P1 work was discarded by the returned LINE fast path.");
            Check(before == JsonSerializer.Serialize(new { state, observation, queued, vm }) && state.Active!.ConditionFingerprint == "original", "Lifecycle recognition rebased original activation credit or changed native facts.");
        }
        Console.WriteLine("Returned queued LINE recognition preserves original activation credit across own condition changes.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
