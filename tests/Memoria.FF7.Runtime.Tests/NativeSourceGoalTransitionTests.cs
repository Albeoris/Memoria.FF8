using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeSourceGoalTransitionTests
{
    internal static void Run()
    {
        Ff7AutoPlayActiveEvent active = new("source-visit", "actor:0:script:1", "source-condition", 3, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "before", Accepted: true, NavigationKey: "source-nav");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "source-program", 20, 5) };
        FieldHostRequest map = new(7, 1, 6, 44, new FieldTransitionCommand(324, 10, 20, 2, 64));
        Ff7NativeScriptActivity dispatch = new(Dispatch: new(0, 1, 22, 1, 2, 6, 1, true, false, 10));
        Ff7NativeScriptActivity completedMap = new(HostOperation: new(map, new(true)));
        int Entry(int entity, int script) => entity == 0 && script == 1 ? 20 : entity == 1 && script == 2 ? 40 : throw new InvalidOperationException("Unexpected native source entry lookup.");
        Ff7AutoPlayActiveEvent tracked = Ff7AutoPlayExecutionOwners.Observe(active, 3, "source-program", 10, [dispatch, completedMap], Entry);
        Ff7NativeTickObservation observation = new("source-field", 3, 10, [], (FieldTransitionCommand)map.Command, "source-witness", map, new(0, 1, 1, 6), [dispatch, completedMap]) { SourceActivation = tracked };
        string before = JsonSerializer.Serialize(new { active, tracked, observation });
        Check(!active.Returned && tracked.ExecutionProvenance!.AcceptedTransition == map && tracked.ExecutionProvenance.AcceptedTransitionTick == 10, "The fixture did not record its real child MAP before root RET.");
        Ff7AutoPlayActiveEvent? adopted = Ff7AutoPlayPolicy.ReadSourceActivation(active, observation);
        Check(ReferenceEquals(adopted, tracked), "A source-captured MAP owner was lost after the consumer installed another field program.");
        Check(Ff7AutoPlayExecutionOwners.Observe(active, 4, "destination-program", 1, [dispatch, completedMap], (_, _) => 0).ExecutionProvenance!.AcceptedTransition is null, "The fixture did not distinguish the incorrect destination-epoch replay.");
        Ff7AutoPlaySourceGoal goal = new("source-visit", "source-program", 3, "source-nav", active.EventId, active.ConditionFingerprint, active.Kind, 0, 1, 20, AcceptedEntry: 20);
        Ff7AutoPlayState state = new() { SourceGoal = goal };
        string coverage = JsonSerializer.Serialize(state.Coverage);
        Check(Ff7AutoPlayPolicy.CompleteSourceGoal(state, adopted! with { TransitionOwned = true, CompletionRecorded = true }), "An actual source-owned child MAP could not complete its accepted exact source goal.");
        Check(state.SourceGoal!.Completed && coverage == JsonSerializer.Serialize(state.Coverage) && !tracked.Returned, "Source completion fabricated root RET or changed native coverage and route history.");
        foreach (Ff7AutoPlayActiveEvent wrong in new[]
        {
            active with { Generation = 4 },
            active with { ConditionFingerprint = "other" },
            active with { Entity = 2 },
            active with { EventId = "other" },
            active with { ExecutionProvenance = active.ExecutionProvenance! with { ProgramIdentity = "other" } },
            active with { ExecutionProvenance = active.ExecutionProvenance! with { RootDispatchTick = 6 } }
        })
        {
            Check(Ff7AutoPlayPolicy.ReadSourceActivation(wrong, observation) is null, "A foreign source activation borrowed the captured transition epoch.");
        }
        Check(Ff7AutoPlayPolicy.ReadSourceActivation(active, observation with { Generation = 4 }) is null && Ff7AutoPlayPolicy.ReadSourceActivation(active, observation with { SourceActivation = null }) is null, "Missing or foreign-epoch capture acquired source authority.");
        foreach (Ff7NativeScriptActivity invalid in new[]
        {
            new Ff7NativeScriptActivity(HostOperation: new(map, new(false))),
            new Ff7NativeScriptActivity(HostOperation: new(map with { Entity = 2 }, new(true))),
            new Ff7NativeScriptActivity(HostOperation: new(map with { Priority = 5 }, new(true)))
        })
        {
            Ff7AutoPlayActiveEvent unproved = Ff7AutoPlayExecutionOwners.Observe(active, 3, "source-program", 10, [dispatch, invalid], Entry);
            Ff7AutoPlayState pending = new() { SourceGoal = goal };
            Check(!Ff7AutoPlayPolicy.CompleteSourceGoal(pending, unproved with { TransitionOwned = true, CompletionRecorded = true }), "An incomplete or unowned MAP reply completed the source goal.");
        }
        Ff7NativeScriptActivity queued = dispatch with { Dispatch = dispatch.Dispatch! with { Queued = true } };
        Ff7AutoPlayActiveEvent notStarted = Ff7AutoPlayExecutionOwners.Observe(active, 3, "source-program", 10, [queued, completedMap], Entry);
        Check(notStarted.ExecutionProvenance!.AcceptedTransition is null, "A queued child without a real start acquired its later MAP receipt.");
        Ff7AutoPlayState wrongEntry = new() { SourceGoal = goal with { Entry = 21 } };
        Check(!Ff7AutoPlayPolicy.CompleteSourceGoal(wrongEntry, tracked with { TransitionOwned = true, CompletionRecorded = true }), "A different native root entry completed the requested source.");
        Check(before == JsonSerializer.Serialize(new { active, tracked, observation }), "Reading captured source authority changed native ownership or ordered receipts.");
        Console.WriteLine("Source-owned child MAP receipts survive destination epoch changes without invented return or source credit.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
