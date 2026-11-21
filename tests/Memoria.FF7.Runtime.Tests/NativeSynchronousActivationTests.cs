using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeSynchronousActivationTests
{
    private const long Generation = 3;
    private const string Program = "synchronous-source";
    private sealed record Fixture(Ff7AutoPlayState State, FieldVmSnapshot Vm, Ff7FieldProcess Process, byte[] Script, Ff7NativeScriptActivity[] Activities);

    internal static void Run()
    {
        foreach (bool party in new[] { false, true })
        {
            Fixture fixture = Create(party);
            string before = JsonSerializer.Serialize(fixture);
            Ff7AutoPlayActiveEvent? completed = Read(fixture);
            Check(completed is { Accepted: true, Returned: false, TransitionOwned: false, CompletionRecorded: false }, "A completed synchronous activation lost its actual native root state.");
            Check(completed!.ExecutionProvenance is { RootReturned: false, Owners.Length: 1 }, "Completed-child accounting invented a root return or retained a finished child.");
            Check(before == JsonSerializer.Serialize(fixture), "Synchronous activation inspection changed native state, coverage or source metadata.");
            Check(!fixture.State.Coverage.Visits.Single().Events.Single().Conditions!.HasExecutedEntry("condition"), "Inspection consumed the activation before accounting.");
            Check(fixture.State.Coverage.Visits.Single().Events.Single().Routes.Count == 0, "Inspection attributed an unrelated ambient MAP route.");
        }

        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            fixture => fixture.State.Active = fixture.State.Active! with { Accepted = false },
            fixture => fixture.State.Active = fixture.State.Active! with { Returned = true },
            fixture => fixture.State.Active = fixture.State.Active! with { ExecutionProvenance = null },
            fixture => fixture.State.Active = fixture.State.Active! with { EventId = "different" },
            fixture => fixture.State.Active = fixture.State.Active! with { Generation = Generation + 1 },
            fixture => fixture.State.Active = fixture.State.Active! with { ConditionFingerprint = "different" },
            fixture => fixture.State.Active = fixture.State.Active! with { TravelSelections = [new("choice", 1)] },
            fixture => fixture.State.Choice = new("choice", fixture.State.Active),
            fixture => fixture.State.PendingRestore = new("unused-authored-path"),
            fixture => fixture.State.Coverage.Visits.Single().Events.Single().Conditions!.ActiveEntry = "different",
            fixture => fixture.State.Coverage.Visits.Single().Events.Single().Conditions!.ConsumedEntries.Add("condition"),
            fixture => fixture.Script[13] = 0x24,
            fixture => fixture.Script[10] = 0x02,
            fixture => fixture.Script[12] = 0xC4,
            fixture => fixture.Vm.Entities[0] = fixture.Vm.Entities[0] with { Priority = 7 },
            fixture => fixture.Vm.Entities[1].SyncOwners[6] = 2,
            fixture => fixture.Vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting,
            fixture => fixture.Vm.Entities[1].SavedProgramCounters[6] = 20,
            fixture => fixture.Vm.Entities[1] = fixture.Vm.Entities[1] with { Priority = 6 },
            fixture =>
            {
                fixture.Vm.Entities[2].SyncStates[5] = FieldSyncState.Waiting;
                fixture.Vm.Entities[2].SyncOwners[5] = 0;
            }
        })
        {
            Fixture fixture = Create();
            invalidate(fixture);
            Check(Read(fixture) is null, "An unauthenticated, outstanding or effectful synchronous source was completed.");
        }

        Fixture valid = Create();
        Check(Read(valid with { Activities = [] }) is null, "A saved Done state without an actual current return certified completion.");
        Check(Read(valid with { Activities = [new(Return: new(1, 6, 20, 19))] }) is null, "A historical child's return certified the current departure.");
        Check(Read(valid with { Activities = [new(Return: new(2, 6, 20, 20))] }) is null, "An unrelated child's return certified completion.");
        Check(Read(valid with { Activities = [new(Return: new(1, 5, 20, 20))] }) is null, "Another priority's return certified completion.");
        Check(Read(valid with { Activities = [new(Return: new(1, 6, 21, 20))] }) is null, "A non-RET instruction certified completion.");
        Check(Read(valid with { Process = valid.Process with { Transition = null } }) is null, "A source without real departure abandoned its native footer.");
        Check(Read(valid with { Process = valid.Process with { PlayerLocked = true } }) is null, "Locked controls certified a finished activation.");
        FieldHostRequest work = new(7, 0, 1, 10, new FieldCameraCommand(FieldCameraOperation.Wait));
        Check(Read(valid with { Vm = valid.Vm with { PendingRequests = [work] } }) is null, "Outstanding owned host work was ignored.");
        FieldHostRequest lookalike = work with { Command = valid.Process.Transition! };
        Check(Read(valid with { Vm = valid.Vm with { PendingRequests = [lookalike] } }) is null, "An outstanding owned request borrowed an ambient transition's identical operands.");

        Fixture unknown = Create(accepted: false);
        Check(Read(unknown) is null, "A rejected dispatch became a completed child.");
        Check(Read(Create(queued: true)) is null, "An unstarted queued child became a completed activation.");
        Ff7NativeScriptActivity replacement = new(Dispatch: new(2, 1, 30, 1, 3, 6, 3, true, false, 20));
        Check(Read(valid with { Activities = [replacement, .. valid.Activities] }) is null, "An unrelated same-slot replacement borrowed the old child lifetime.");
        Ff7NativeScriptActivity sibling = new(Dispatch: new(0, 1, 10, 2, 3, 6, 1, true, false, 20));
        Check(Read(valid with { Activities = [sibling, .. valid.Activities] }) is null, "Another live descendant was silently abandoned.");
        Fixture partyWrong = Create(party: true);
        FieldVariables changedParty = new(partyWrong.Vm.PersistentVariables, partyWrong.Vm.MapVariables);
        changedParty.WriteByte(FieldBank.Persistent1Byte, 9, 255);
        partyWrong = partyWrong with { Vm = partyWrong.Vm with { PersistentVariables = changedParty.CopyPersistent() } };
        Check(Read(partyWrong) is null, "A changed party binding reused the previous child completion.");
        CheckAccounting();
        Console.WriteLine("Synchronous source activation proof, native ownership, effect-free footer and source purity checks passed.");
    }

    private static void CheckAccounting()
    {
        Fixture fixture = Create();
        Ff7AutoPlayActiveEvent completed = Read(fixture) ?? throw new InvalidOperationException("The accounting fixture has no actual completion certificate.");
        Ff7NativeTickObservation observation = new("source", Generation, fixture.Vm.Tick, fixture.Activities.Select(activity => activity.Return).OfType<FieldScriptReturn>().ToArray(), fixture.Process.Transition, "unchanged-witness") { CompletedSynchronousActivation = completed };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(fixture.State);
        string native = JsonSerializer.Serialize(new { fixture.Vm, fixture.Process, fixture.Script });
        Ff7AutoPlayActiveEvent original = fixture.State.Active!;
        Check(!Ff7AutoPlayPolicy.RecordCompletedSynchronousActivation(fixture.State, planner, observation with { CompletedSynchronousActivation = null }), "An absent completion certificate consumed an activation.");
        Check(Ff7AutoPlayPolicy.RecordCompletedSynchronousActivation(fixture.State, planner, observation), "A proved source activation was not recorded.");
        var coverage = fixture.State.Coverage.Visits.Single().Events.Single();
        Check(coverage.Conditions is { ActiveEntry: null } conditions && conditions.HasExecutedEntry("condition") && conditions.ConsumedEntries.Count == 1, "Activation accounting lost its exact once-only source condition.");
        Check(coverage.Routes.Count == 0 && fixture.State.Coverage.NavigationRoutes.Count == 0, "Activation accounting fabricated an ambient destination route.");
        Check(fixture.State.Active is null && !original.Returned && !original.TransitionOwned && !completed.Returned && !completed.TransitionOwned, "Activation accounting fabricated native return or transition ownership.");
        string once = JsonSerializer.Serialize(fixture.State);
        Check(!Ff7AutoPlayPolicy.RecordCompletedSynchronousActivation(fixture.State, planner, observation) && once == JsonSerializer.Serialize(fixture.State), "A duplicate completion certificate changed accounting twice.");
        Check(native == JsonSerializer.Serialize(new { fixture.Vm, fixture.Process, fixture.Script }), "Accounting altered actual native state or pending execution.");

        foreach (Action<Fixture> race in new Action<Fixture>[]
        {
            source => source.State.Active = source.State.Active! with { EventId = "replacement" },
            source => source.State.Active = source.State.Active! with { ConditionFingerprint = "new-condition" },
            source => source.State.Coverage.Visits.Single().Events.Single().Conditions!.ActiveEntry = "new-entry",
            source =>
            {
                Ff7AutoPlayActiveEvent active = source.State.Active!;
                var provenance = active.ExecutionProvenance!;
                source.State.Active = active with { ExecutionProvenance = provenance with { Owners = provenance.Owners.Select(owner => owner.ParentEntity == -1 ? owner with { DispatchTick = owner.DispatchTick + 1 } : owner).ToArray() } };
            }
        })
        {
            Fixture changed = Create();
            Ff7AutoPlayActiveEvent receipt = Read(changed)!;
            race(changed);
            string before = JsonSerializer.Serialize(changed);
            Check(!Ff7AutoPlayPolicy.RecordCompletedSynchronousActivation(changed.State, Ff7AutoPlayPolicy.CreatePlanner(changed.State), observation with { CompletedSynchronousActivation = receipt }), "A stale source certificate consumed another entry or root lifetime.");
            Check(before == JsonSerializer.Serialize(changed), "Rejected stale accounting changed the source.");
        }
    }

    private static Fixture Create(bool party = false, bool accepted = true, bool queued = false)
    {
        byte[] script = Enumerable.Repeat((byte)0xFF, 64).ToArray();
        script[10] = party ? (byte)0x06 : (byte)0x03;
        script[11] = party ? (byte)0 : (byte)1;
        script[12] = 0xC3;
        script[13] = 0;
        script[20] = 0;
        script[30] = 0;
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 3).Select(_ => new FieldEntitySnapshot(30, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[0] = entities[0] with { ProgramCounter = 10, Priority = 1 };
        entities[0].SavedProgramCounters[7] = 30;
        entities[1] = entities[1] with { PartyCharacter = 2 };
        entities[1].SyncStates[6] = FieldSyncState.Done;
        entities[1].SyncOwners[6] = 0;
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 21, true, new byte[1280], new byte[256], entities);
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        variables.WriteByte(FieldBank.Persistent1Byte, 9, 2);
        vm = vm with { PersistentVariables = variables.CopyPersistent() };
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new("source", Program), [new("line", AutoPlayEventKind.Interaction, ConditionSignature: "condition")]);
        planner.BeginEvent("source", "line");
        Ff7AutoPlayActiveEvent active = new("source", "line", "condition", Generation, 0, 1, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, NavigationKey: "component");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, 10, 19) };
        Ff7NativeScriptActivity dispatch = new(Dispatch: new(0, 1, 10, 1, 3, 6, 3, accepted, queued, 19));
        state.Active = Ff7AutoPlayExecutionOwners.Observe(active, Generation, Program, 19, [dispatch], Entry);
        Ff7FieldProcess process = new() { Transition = new(9, 0, 0, 0, 0) };
        return new(state, vm, process, script, [new(Return: new(1, 6, 20, 20))]);
    }

    private static int Entry(int entity, int script) => entity == 0 && script == 1 ? 10 : 20;

    private static Ff7AutoPlayActiveEvent? Read(Fixture fixture)
    {
        return Ff7AutoPlayPolicy.ReadCompletedSynchronousActivation(fixture.State, fixture.Vm, fixture.Process, fixture.Script, fixture.Activities, Entry, Generation);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
