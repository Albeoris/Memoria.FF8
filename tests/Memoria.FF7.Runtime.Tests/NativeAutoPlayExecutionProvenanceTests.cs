using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayExecutionProvenanceTests
{
    private const long Generation = 7;
    private const string Program = "execution-program";

    public static void Run()
    {
        AsyncDescendantsSurviveReturns();
        PreemptionAndReplacementHaveDifferentLifetimes();
        QueuedAdmissionsNeedTheirActualStart();
        ActivityPrefixesAreFoldedOnce();
        ExternalAdmissionsAuthenticateRootLifetimes();
        ClearedProvenanceCannotBorrowLegacySyncOwnership();
        CompletedTransitionUsesItsActualExecutionBoundary();
        MissingAndChangedScopesRemainUnknown();
        Console.WriteLine("PASS native asynchronous execution provenance and priority lifetimes");
    }

    private static Ff7AutoPlayActiveEvent Root(long tick = 10)
    {
        Ff7AutoPlayActiveEvent active = new("source", "actor:0:script:1", "entry-condition", Generation, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 641, "before", Accepted: true, NavigationKey: "component");
        return active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, Entry(0, 1), tick) };
    }

    private static int Entry(int entity, int script)
    {
        return 20 + entity * 100 + script * 4;
    }

    private static Ff7NativeScriptActivity Dispatch(int source, int sourcePriority, int target, int priority = 6, bool accepted = true, bool queued = false, long tick = 10)
    {
        return new(Dispatch: new(source, sourcePriority, Entry(source, 1) + 3, target, 2, priority, 2, accepted, queued, tick));
    }

    private static Ff7NativeScriptActivity Returned(int entity, int priority, long tick = 10)
    {
        return new(Return: new(entity, priority, Entry(entity, 2) + 12, tick));
    }

    private static Ff7NativeScriptActivity External(int entity, int script, bool accepted, bool started, long tick)
    {
        return new(ExternalAdmission: new(new(entity, script, 1, accepted, started), tick));
    }

    private static Ff7AutoPlayActiveEvent Fold(Ff7AutoPlayActiveEvent active, long tick, params Ff7NativeScriptActivity[] activity)
    {
        return Ff7AutoPlayExecutionOwners.Observe(active, Generation, Program, tick, activity, Entry);
    }

    private static FieldVmSnapshot Vm(params (int Entity, int Priority)[] executing)
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 4).Select(entity => new FieldEntitySnapshot(Entry(entity, 2), 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        foreach (var owner in executing)
        {
            entities[owner.Entity] = entities[owner.Entity] with { Priority = owner.Priority };
        }
        return new(1, FieldMachine.SemanticProfile, Program, 10, true, new byte[1280], new byte[256], entities);
    }

    private static bool Owns(Ff7AutoPlayActiveEvent active, FieldVmSnapshot vm, int entity, int priority = 6)
    {
        return Ff7AutoPlayExecutionOwners.ReadCurrentOwners(vm, active, Generation).TryGetValue(entity, out int actual) && actual == priority;
    }

    private static void AsyncDescendantsSurviveReturns()
    {
        Ff7AutoPlayActiveEvent initial = Root();
        string before = JsonSerializer.Serialize(initial);
        Ff7AutoPlayActiveEvent active = Fold(initial, 10, Dispatch(0, 1, 1), Dispatch(1, 6, 2), Returned(1, 6), Returned(0, 1)) with { Returned = true };
        FieldVmSnapshot vm = Vm((2, 6));
        string native = JsonSerializer.Serialize(vm);
        Check(Owns(active, vm, 2), "A real asynchronous grandchild lost its root when its callers returned.");
        Check(active.ExecutionProvenance?.RootReturned == true, "The current root's actual ordered return was not recorded.");
        Check(!Owns(active, vm, 1) && !Owns(active, vm, 0, 1), "Returned priority lifetimes remained live owners.");
        Check(Ff7AutoPlayExecutionOwners.HasOutstandingChildren(vm, active, Generation), "A live asynchronous descendant did not retain its execution obligation.");
        active = Fold(active, 11, Returned(2, 6, 11));
        Check(!Owns(active, vm, 2) && !Ff7AutoPlayExecutionOwners.HasOutstandingChildren(vm, active, Generation), "A returned descendant retained stale authority.");
        Check(before == JsonSerializer.Serialize(initial) && native == JsonSerializer.Serialize(vm), "Provenance observation changed its source event or native snapshot.");
    }

    private static void PreemptionAndReplacementHaveDifferentLifetimes()
    {
        Ff7AutoPlayActiveEvent active = Fold(Root(), 10, Dispatch(0, 1, 1));
        active = Fold(active, 11, Dispatch(3, 1, 1, priority: 1, tick: 11));
        FieldVmSnapshot suspended = Vm((0, 1), (1, 1));
        suspended.Entities[1].SavedProgramCounters[6] = Entry(1, 2) + 4;
        Check(!Owns(active, suspended, 1) && Ff7AutoPlayExecutionOwners.HasOutstandingChildren(suspended, active, Generation), "Higher-priority preemption discarded or activated a suspended descendant.");
        active = Fold(active, 12, Returned(1, 1, 12));
        Check(Owns(active, Vm((0, 1), (1, 6)), 1), "A descendant did not regain authority after its preemptor returned.");
        Ff7AutoPlayActiveEvent rejected = Fold(active, 13, Dispatch(3, 1, 1, accepted: false, tick: 13));
        Check(Owns(rejected, Vm((1, 6)), 1), "A rejected request replaced a genuine execution lifetime.");
        Ff7AutoPlayActiveEvent replaced = Fold(rejected, 14, Dispatch(3, 1, 1, tick: 14));
        Check(!Owns(replaced, Vm((1, 6)), 1), "An unowned same-priority replacement inherited the old descendant's authority.");
    }

    private static void QueuedAdmissionsNeedTheirActualStart()
    {
        Ff7AutoPlayActiveEvent active = Fold(Root(), 10, Dispatch(0, 1, 1, queued: true));
        FieldVmSnapshot waiting = Vm((0, 1), (1, 1));
        waiting.Entities[1].SavedProgramCounters[6] = Entry(1, 2);
        Check(Ff7AutoPlayExecutionOwners.HasOutstandingChildren(waiting, active, Generation), "A genuinely queued descendant lost its pending obligation.");
        Check(!Owns(active, Vm((1, 6)), 1), "Priority equality alone promoted an unstarted queue entry.");
        Ff7AutoPlayActiveEvent falseSource = Fold(active, 11, Dispatch(1, 6, 2, tick: 11));
        Check(!Owns(falseSource, Vm((2, 6)), 2), "An unstarted queued owner authenticated a descendant dispatch.");
        Ff7NativeScriptActivity wrongStart = new(Boundary: new(1, 6, Entry(1, 2) + 1, 0x24));
        active = Fold(active, 11, wrongStart);
        Check(!Owns(active, Vm((1, 6)), 1), "A different instruction boundary promoted a queued activation.");
        active = Fold(active, 12, new Ff7NativeScriptActivity(Boundary: new(1, 6, Entry(1, 2), 0x24)), Dispatch(1, 6, 2, tick: 12));
        Check(Owns(active, Vm((1, 6), (2, 6)), 1) && Owns(active, Vm((1, 6), (2, 6)), 2), "An actual queued-entry start failed to authenticate its later dispatch.");
    }

    private static void ActivityPrefixesAreFoldedOnce()
    {
        Ff7NativeScriptActivity[] all = [Dispatch(0, 1, 1), Dispatch(1, 6, 2), Returned(1, 6)];
        Ff7AutoPlayActiveEvent active = Fold(Root(), 10, all[..1]);
        active = Fold(active, 10, all);
        string completed = JsonSerializer.Serialize(active);
        Check(Owns(active, Vm((2, 6)), 2), "Resuming an activity prefix lost its asynchronous descendant.");
        active = Fold(active, 10, all);
        Check(completed == JsonSerializer.Serialize(active), "The same ordered activity list was folded twice.");
        active = Fold(active, 9, Dispatch(3, 1, 2, tick: 9));
        Check(completed == JsonSerializer.Serialize(active), "An older activity observation rewound provenance.");
    }

    private static void ExternalAdmissionsAuthenticateRootLifetimes()
    {
        Ff7AutoPlayActiveEvent active = Fold(Root(), 10, Dispatch(0, 1, 2), Returned(0, 1), External(0, 1, true, true, 10), Dispatch(0, 1, 1));
        Check(Owns(active, Vm((1, 6)), 1) && !Owns(active, Vm((2, 6)), 2), "The actual root-start marker retained activity from the preceding priority lifetime.");
        Check(active.ExecutionProvenance?.RootReturned == false, "An earlier root lifetime's RET completed the newly started root.");
        active = Fold(active, 11, External(0, 2, true, false, 11), External(1, 3, false, false, 11));
        Check(Owns(active, Vm((1, 6)), 1), "Accepted-but-not-started or rejected external admission changed real provenance.");
        active = Fold(active, 12, External(0, 2, true, true, 12));
        Check(Ff7AutoPlayExecutionOwners.ReadCurrentOwners(Vm((0, 1), (1, 6)), active, Generation).Count == 0, "A new external root lifetime inherited its predecessor's causal owners.");
    }

    private static void MissingAndChangedScopesRemainUnknown()
    {
        Ff7AutoPlayActiveEvent active = Fold(Root(), 10, Dispatch(0, 1, 1));
        FieldVmSnapshot vm = Vm((0, 1), (1, 6));
        foreach (Ff7AutoPlayActiveEvent changed in new[]
        {
            active with { ExecutionProvenance = null },
            active with { Accepted = false },
            active with { Generation = Generation + 1 },
            active with { VisitId = "other-source" },
            active with { EventId = "another-entry-variant" },
            active with { Kind = Ff7AutoPlayTargetKind.ActorContact },
            active with { Entity = 2 },
            active with { Script = 2 },
            active with { ConditionFingerprint = "different-condition" }
        })
        {
            Check(Ff7AutoPlayExecutionOwners.ReadCurrentOwners(vm, changed, Generation).Count == 0 && !Ff7AutoPlayExecutionOwners.HasOutstandingChildren(vm, changed, Generation), "Missing or changed observational scope authenticated old asynchronous ownership.");
            if (changed.ExecutionProvenance is not null)
            {
                Check(Ff7AutoPlayScriptedInput.ReadInteractionOwners(vm, changed, Generation).Count == 0, "A changed provenance scope regained authority through legacy inference.");
            }
        }
        Check(Ff7AutoPlayExecutionOwners.ReadCurrentOwners(vm with { ProgramIdentity = "other-program" }, active, Generation).Count == 0, "Another native program inherited execution ownership.");
        string legacy = JsonSerializer.Serialize(active with { ExecutionProvenance = null });
        Check(!legacy.Contains("ExecutionProvenance", StringComparison.Ordinal), "An absent optional provenance value changed the legacy save shape.");
        Ff7AutoPlayActiveEvent restored = JsonSerializer.Deserialize<Ff7AutoPlayActiveEvent>(legacy)!;
        Check(restored.ExecutionProvenance is null && Ff7AutoPlayExecutionOwners.ReadCurrentOwners(vm, restored, Generation).Count == 0, "An old save invented missing asynchronous provenance.");
    }

    private static void ClearedProvenanceCannotBorrowLegacySyncOwnership()
    {
        Ff7AutoPlayActiveEvent active = Fold(Root(), 10, Dispatch(0, 1, 1));
        active = Fold(active, 11, External(0, 2, true, true, 11));
        FieldVmSnapshot vm = Vm((0, 1), (1, 6));
        vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting;
        vm.Entities[1].SyncOwners[6] = 0;
        FieldHostRequest transition = new(7, 1, 6, vm.Entities[1].ProgramCounter, new FieldTransitionCommand(2, 0, 0, 0, 0));
        vm = vm with { PendingRequests = [transition] };
        Check(Ff7AutoPlayScriptedInput.ReadInteractionOwners(vm, active, Generation).Count == 0, "Legacy synchronous inference resurrected an invalidated root execution.");
        Check(Ff7Session.ReadTransitionOwnership(vm, transition, active, Generation, []) is null, "A replacement root's synchronous child credited the preceding root's transition.");
        Ff7AutoPlayActiveEvent legacy = active with { ExecutionProvenance = null };
        Check(Ff7AutoPlayScriptedInput.ReadInteractionOwners(vm, legacy, Generation).TryGetValue(1, out int priority) && priority == 6, "An old save lost its existing synchronous owner facts.");
        Check(Ff7Session.ReadTransitionOwnership(vm, transition, legacy, Generation, []) is { RootEntity: 0, RootScript: 1, OwnerEntity: 1, OwnerPriority: 6 }, "Missing optional provenance disabled the original synchronous transition proof.");
    }

    private static void CompletedTransitionUsesItsActualExecutionBoundary()
    {
        Ff7AutoPlayActiveEvent active = Root() with { Returned = true };
        FieldHostRequest transition = new(23, 2, 6, Entry(2, 2) + 8, new FieldTransitionCommand(2, 0, 0, 0, 0));
        Ff7NativeScriptActivity completed = new(HostOperation: new(transition, new(true)));
        Ff7NativeScriptActivity[] activities = [Dispatch(0, 1, 1), Dispatch(1, 6, 2), Returned(1, 6), Returned(0, 1), completed, Returned(2, 6)];
        FieldScriptReturn[] returns = activities.Select(activity => activity.Return).OfType<FieldScriptReturn>().ToArray();
        FieldVmSnapshot ended = Vm();
        string before = JsonSerializer.Serialize(new { active, ended, activities });
        Check(Ff7Session.ReadTransitionOwnership(ended, transition, active, Generation, returns, activities, Entry) is { RootEntity: 0, RootScript: 1, OwnerEntity: 2, OwnerPriority: 6 }, "A completed native transition lost its real asynchronous owner after the owner returned and its request retired.");
        Ff7AutoPlayActiveEvent observed = Fold(active, 10, activities);
        Ff7AutoPlayActiveEvent restored = JsonSerializer.Deserialize<Ff7AutoPlayActiveEvent>(JsonSerializer.Serialize(observed))!;
        Check(restored.ExecutionProvenance?.AcceptedTransition is { Command: FieldTransitionCommand } savedTransition && savedTransition == transition, "A saved completed transition lost its exact typed request.");
        Check(Ff7Session.ReadTransitionOwnership(ended, transition, restored, Generation, returns) is { RootEntity: 0, RootScript: 1, OwnerEntity: 2, OwnerPriority: 6 }, "A restored actual transition receipt lost its ownership after native return.");
        Check(Ff7Session.ReadTransitionOwnership(ended, transition with { Id = 24 }, active, Generation, returns, activities, Entry) is null, "Another request borrowed the recorded transition execution.");
        Check(Ff7Session.ReadTransitionOwnership(ended, transition with { Command = new FieldTransitionCommand(3, 0, 0, 0, 0) }, active, Generation, returns, activities, Entry) is null, "Another typed transition borrowed the recorded execution boundary.");
        Ff7NativeScriptActivity[] pending = (Ff7NativeScriptActivity[])activities.Clone();
        pending[4] = new(HostOperation: new(transition, new(false)));
        Check(Ff7Session.ReadTransitionOwnership(ended, transition, active, Generation, returns, pending, Entry) is null, "An incomplete reply authenticated a retired transition request.");
        Check(Ff7Session.ReadTransitionOwnership(ended, transition, active, Generation, [], [completed, Returned(2, 6)], Entry) is null, "An unowned host operation created asynchronous ancestry.");
        Ff7NativeScriptActivity[] replaced = [.. activities.Take(4), External(0, 2, true, true, 10), completed, Returned(2, 6)];
        Check(Ff7Session.ReadTransitionOwnership(ended, transition, active, Generation, returns, replaced, Entry) is null, "A replaced root retained authority over a later transition.");
        Check(before == JsonSerializer.Serialize(new { active, ended, activities }), "Transition attribution changed native state or its ordered execution evidence.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
