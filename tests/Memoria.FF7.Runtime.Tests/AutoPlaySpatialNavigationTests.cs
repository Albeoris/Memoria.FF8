using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlaySpatialNavigationTests
{
    public static void Run()
    {
        CheckExactNativeOwnership();
        CheckActivityOrderAndColdState();
        CheckScriptedInputRoot();
        CheckOwnedSpatialContinuationInput();
        CheckOriginalNavigation();
    }

    private static Ff7AutoPlayActiveEvent Active()
    {
        return new("visit", "spatial", "predicate", 4, 0, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, null, 469, "before", SpatialOwner: new(104, 2, 3, 5, 3));
    }

    private static FieldScriptDispatch Dispatch()
    {
        return new(0, 7, 104, 2, 3, 5, 3, true, false, 10);
    }

    private static FieldScriptReturn Return()
    {
        return new(2, 5, 200, 10);
    }

    private static void CheckExactNativeOwnership()
    {
        Ff7AutoPlayActiveEvent active = Active();
        FieldScriptDispatch exact = Dispatch();
        FieldScriptDispatch[] rejected =
        [
            exact with { Accepted = false },
            exact with { SourceEntity = 1 },
            exact with { SourcePriority = 6 },
            exact with { SourceProgramCounter = 105 },
            exact with { TargetEntity = 1 },
            exact with { TargetScript = 4 },
            exact with { TargetPriority = 4 },
            exact with { RequestMode = 2 }
        ];
        foreach (FieldScriptDispatch dispatch in rejected)
        {
            Ff7AutoPlayActiveEvent observed = Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(active, [new(Dispatch: dispatch), new(Return: Return())]);
            Check(observed == active, "A busy or mismatched native dispatch admitted an unrelated same-field spatial child.");
        }
        Ff7AutoPlayActiveEvent accepted = Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(active, [new(Dispatch: exact)]);
        Check(accepted.Accepted && !accepted.Returned && !active.Accepted, "Exact native child dispatch failed admission or changed its immutable source observation.");
        Ff7AutoPlayActiveEvent queued = Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(active, [new(Dispatch: exact with { Queued = true })]);
        Check(queued.Accepted && !queued.Returned, "A natively accepted queued child lost its exact dispatch ownership.");
        Check(Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(accepted, [new(Return: Return() with { Entity = 1 })]) == accepted, "An unrelated entity return completed the spatial child.");
        Check(Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(accepted, [new(Return: Return() with { Priority = 4 })]) == accepted, "An unrelated priority return completed the spatial child.");
        Check(Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(accepted, [new(Return: Return())]).Returned, "The admitted native child did not complete on its exact priority return.");
        Ff7AutoPlayActiveEvent ordinary = active with { SpatialOwner = null };
        Check(Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(ordinary, [new(Dispatch: exact), new(Return: Return())]) == ordinary, "Spatial activity changed an ordinary interaction's ownership contract.");
    }

    private static void CheckActivityOrderAndColdState()
    {
        Ff7AutoPlayActiveEvent active = Active();
        Ff7AutoPlayActiveEvent premature = Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(active, [new(Return: Return()), new(Dispatch: Dispatch())]);
        Check(premature.Accepted && !premature.Returned, "A return preceding native dispatch was incorrectly attributed to the newly accepted child.");
        Ff7AutoPlayActiveEvent completed = Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(active, [new(Dispatch: Dispatch()), new(Return: Return())]);
        Check(completed.Accepted && completed.Returned, "An exact dispatch and child return in one callback lost their native execution order.");
        Ff7AutoPlayState state = new() { Active = premature };
        Ff7AutoPlayState cold = state.Clone();
        Check(cold.Active == premature && cold.Active!.SpatialOwner == active.SpatialOwner, "Cold state restoration lost the exact expected native spatial owner or pending child admission.");
        cold.Active = Ff7AutoPlayPolicy.ObserveSpatialScriptActivity(cold.Active!, [new(Return: Return())]);
        Check(cold.Active == completed && state.Active == premature, "Cold restoration replayed dispatch or changed the original pending native child observation.");
        Ff7AutoPlayState finished = cold.Clone();
        Check(finished.Active!.Returned && finished.Active.Accepted && finished.Active.SpatialOwner == active.SpatialOwner, "Cold restoration lost a completed native spatial child observation.");
        Ff7TickContinuation continuation = new(new(), ScriptActivity: [new(Return: Return()), new(Dispatch: Dispatch())]);
        Ff7TickContinuation restored = JsonSerializer.Deserialize<Ff7TickContinuation>(JsonSerializer.Serialize(continuation))!;
        Check(restored.ScriptActivity!.SequenceEqual(continuation.ScriptActivity!), "Cold native tick continuation reordered dispatch and return activity.");
    }

    private static void CheckScriptedInputRoot()
    {
        FieldProgram program = new(new byte[8], Enumerable.Range(0, 3).Select(_ => new ushort[32]).ToArray());
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldVmSnapshot snapshot = machine.Capture();
        snapshot.Entities[2] = snapshot.Entities[2] with { Priority = 5 };
        snapshot.Entities[1] = snapshot.Entities[1] with { Priority = 4 };
        snapshot.Entities[1].SyncOwners[4] = 2;
        snapshot.Entities[1].SyncStates[4] = FieldSyncState.Waiting;
        string unchanged = JsonSerializer.Serialize(snapshot);
        Ff7AutoPlayActiveEvent active = Active() with { Accepted = true };
        IReadOnlyDictionary<int, int> owners = Ff7AutoPlayScriptedInput.ReadInteractionOwners(snapshot, active, active.Generation);
        Check(owners.Count == 2 && owners[2] == 5 && owners[1] == 4 && !owners.ContainsKey(active.Entity), "Spatial scripted input did not use its admitted child priority and synchronous descendant ownership.");
        IReadOnlyDictionary<int, int> continuation = Ff7AutoPlayScriptedInput.ReadInteractionOwners(snapshot, active with { Returned = true }, active.Generation);
        Check(continuation.Count == 1 && continuation[active.Entity] == active.SpatialOwner!.SourcePriority, "A returned spatial child failed to restore its exact source-parent input ownership or retained the completed child as root.");
        Check(JsonSerializer.Serialize(snapshot) == unchanged, "Reading spatial child input ownership changed native VM state.");
    }

    private static void CheckOwnedSpatialContinuationInput()
    {
        FieldProgram program = new(new byte[8], Enumerable.Range(0, 3).Select(_ => new ushort[32]).ToArray());
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldVmSnapshot snapshot = machine.Capture();
        snapshot.Entities[1] = snapshot.Entities[1] with { Priority = 4 };
        snapshot.Entities[1].SyncOwners[4] = 0;
        snapshot.Entities[1].SyncStates[4] = FieldSyncState.Waiting;
        Ff7AutoPlayActiveEvent active = Active() with { Accepted = true, Returned = true };
        FieldScriptInputDemand source = new("source-gate", snapshot.ProgramIdentity, active.Entity, 7, 1, FieldScriptInputKind.Held, 0x40, false, false) { DelayTicks = 12 };
        FieldScriptInputDemand child = source with { Identity = "child-gate", Entity = 1, Priority = 4 };
        string unchanged = JsonSerializer.Serialize(new { snapshot, source, child });
        Check(Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source, snapshot, active, active.Generation, true), "The locked exact spatial source parent lost its native post-child continuation gate.");
        Check(Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(child, snapshot, active, active.Generation, true), "An exact synchronous descendant of the native continuation parent lost input ownership.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source with { Entity = 2 }, snapshot, active, active.Generation, true), "An unrelated entity or the completed old child inherited spatial continuation input.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source with { Priority = 6 }, snapshot, active, active.Generation, true), "An unrelated source priority inherited spatial continuation input.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source, snapshot, active, active.Generation + 1, true), "Another field generation inherited spatial continuation input.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source with { ProgramIdentity = "different-program" }, snapshot, active, active.Generation, true), "A gate from another native program inherited spatial continuation input.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source, snapshot, active, active.Generation, false), "An unlocked native field admitted an unproved continuation branch input.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source, snapshot, active with { Accepted = false }, active.Generation, true), "An unaccepted spatial child authorized its parent continuation gate.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source, snapshot, active with { Returned = false }, active.Generation, true), "A still-running spatial child authorized an unrelated parent continuation gate.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source, snapshot, active with { SpatialOwner = null }, active.Generation, true), "An event without exact spatial ownership admitted a non-wait continuation gate.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedSpatialContinuationDemand(source, snapshot, active with { Kind = Ff7AutoPlayTargetKind.ActorTalk }, active.Generation, true), "An ordinary actor event admitted a non-wait spatial continuation gate.");
        Ff7AutoplayInput selected = Ff7AutoPlayScriptedInput.SelectOwnedSpatialContinuationInput(source, snapshot, active, active.Generation, true, new());
        Check(selected.Held == 0x40 && !selected.Run, "An authenticated native continuation gate lost its literal physical button projection.");
        Check(Ff7AutoPlayScriptedInput.SelectOwnedSpatialContinuationInput(child, snapshot, active, active.Generation, true, new()).Held == 0x40, "A proved synchronous continuation child lost its literal physical button projection.");
        Check(Ff7AutoPlayScriptedInput.SelectOwnedSpatialContinuationInput(source with { Kind = FieldScriptInputKind.Pressed }, snapshot, active, active.Generation, true, new()).Held == 0, "A delayed continuation gate emitted its pressed edge before the native poll.");
        Check(Ff7AutoPlayScriptedInput.SelectOwnedSpatialContinuationInput(source with { Kind = FieldScriptInputKind.Released }, snapshot, active, active.Generation, true, new()).Held == 0x40, "A delayed continuation gate released its button before the native poll.");
        bool rejected = false;
        try
        {
            Ff7AutoPlayScriptedInput.SelectInput(source, new());
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Check(rejected && !source.AwaitingInput && !child.AwaitingInput, "Owned continuation admission weakened the public pure-wait selector or fabricated an AwaitingInput proof.");
        Check(JsonSerializer.Serialize(new { snapshot, source, child }) == unchanged, "Reading or projecting native continuation ownership changed the source VM or its observed input demands.");
    }

    private static void CheckOriginalNavigation()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SPATIAL_NAVIGATION_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), source);
        Ff7Actor initialPlayer = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        string native = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget[] spatial = observation.Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && target.MayChangeNavigation && !target.ChangesField && target.DestinationField is null && target.SpatialDestinations is { Length: 0 } && target.SpatialOwner is not null).ToArray();
        Check(spatial.Length != 0, "The original native source exposed no same-field spatial navigation child.");
        Check(JsonSerializer.Serialize(session.Capture()) == native, "Spatial navigation discovery changed original native progress, scripts, variables or RNG.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded same-field spatial continuation must stop before new choice exploration."));
        string? completedEvent = null;
        session.NativeTickCompleted += tick =>
        {
            if (policy.State.Active is { SpatialOwner: not null, Accepted: true, Returned: true } active && spatial.Any(target => target.Id == active.EventId))
            {
                completedEvent = active.EventId;
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool reached = false;
        for (int step = 0; step < 1000 && controller.Running && session.FieldGeneration == source.FieldGeneration && !reached; step++)
        {
            controller.Advance(1);
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            reached = completedEvent is not null && player.Z4096 < initialPlayer.Z4096 && policy.State.Coverage.NavigationRoutes.Any(route => route.EventId == completedEvent && route.SourceKey == observation.NavigationKey && route.DestinationKey != route.SourceKey);
        }
        Check(controller.Failure is null, $"The ordinary same-field spatial navigation continuation failed: {controller.Error}");
        Check(reached && completedEvent is not null, "The original spatial child did not reach its lower navigation component and owned native completion within the bounded continuation.");
        Check(policy.State.PendingRestore is null && bytes.SequenceEqual(File.ReadAllBytes(path)), "Same-field spatial navigation queued a replay or changed its original standalone source.");
        Console.WriteLine($"PASS original same-field spatial navigation and exact child completion after {controller.Steps} ordinary steps; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
