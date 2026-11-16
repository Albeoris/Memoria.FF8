using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayDirectSpatialTransitionTests
{
    internal static void Run(bool navigationOnly = false)
    {
        CheckDirectOwnership();
        CheckChangedSpatialCatalog();
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure original field resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SPATIAL_TRANSITION_SAVE") ?? throw new InvalidOperationException("Configure an external automatic spatial transition source.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        session.AdvanceTick(advanceClock: false);
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(session.Capture());
        Ff7AutoPlayObservation observed = new Ff7AutoPlayDiscovery(session).Observe();
        int spatialCount = observed.Events.Count(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial);
        Ff7Actor initialPlayer = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Check(observed.Events.Any(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && target.Id.Contains(":direct:", StringComparison.Ordinal) && target.DestinationField == session.MapId), "A direct automatic native self-transition remains absent from discovery.");
        Check(before.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(session.Capture())), "Spatial discovery changed native state, progress or RNG.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the spatial boundary."));
        string priorPlan = $"priorGeneration={policy.State.Active?.Generation}, actualGeneration={session.FieldGeneration}, sameVisit={policy.State.Active?.VisitId == observed.VisitId}, goal={policy.State.NavigationGoal?.EventId}";
        HashSet<string> observedOwners = [];
        session.NativeTickCompleted += observation =>
        {
            if (policy.State.Active is { } active && Ff7AutoPlayPolicy.IsOwnedTransition(observation, active))
            {
                observedOwners.Add(active.EventId);
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        string? firstSelection = null;
        for (int step = 0; step < 1024 && controller.Running && session.Battle is null && session.FieldGeneration == saved.FieldGeneration && policy.State.PendingRestore is null; step++)
        {
            controller.Advance(1);
            if (firstSelection is null && policy.State.Active is { } selected)
            {
                firstSelection = $"{selected.Kind}:{selected.EventId}";
            }
        }
        if (navigationOnly)
        {
            Check(controller.Error is null && session.Battle is not null, $"The recorded navigation continuation failed before its native encounter: {controller.Error ?? policy.State.Status}");
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The native navigation source changed.");
            Console.WriteLine($"PASS original spatial navigation continuation to its native encounter after {controller.Steps} steps; source unchanged");
            return;
        }
        if (policy.State.PendingRestore is { } restore)
        {
            Check(controller.Error is null && policy.State.Choice is null, "A completed automatic choice retained its interrupted movement owner.");
            using FileStream primaryStream = File.OpenRead(restore.Path);
            Ff7SessionSave primary = CombinedSessionCodec.Read(primaryStream).Ff7!;
            Check(primary.World is not null, "The supplied choice did not restore its recorded world continuation.");
            Ff7AutoPlayState state = policy.State.Clone();
            state.PendingRestore = null;
            using Ff7Session continued = Ff7Session.Restore(Ff7Installation.Discover(root), primary with { AutoPlay = state });
            continued.AdvanceTick(advanceClock: false);
            Check(continued.World is not null && bytes.SequenceEqual(File.ReadAllBytes(path)), "Restoring the recorded choice continuation lost world ownership or changed the source.");
            Console.WriteLine($"PASS original stochastic spatial discovery ({spatialCount} demands), preserved native source and automatic choice return to its recorded world continuation after {controller.Steps} steps");
            return;
        }
        Ff7Actor finalPlayer = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        string diagnostics = $"spatial={spatialCount}, first={firstSelection}, {priorPlan}, initial=({initialPlayer.X4096},{initialPlayer.Y4096},{initialPlayer.Z4096}), final=({finalPlayer.X4096},{finalPlayer.Y4096},{finalPlayer.Z4096})";
        diagnostics += $", pending={session.HasPendingTick}, initialCycle={session.Machine.HasCompletedInitialCycle}, global={session.Host.Process.GlobalCommand}, locked={session.Host.Process.PlayerLocked}, join={session.Host.Process.Join is not null}, transition={session.Host.Process.Transition is not null}, dialogue={session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed)}, scriptedMotion={session.Host.HasScriptedMotion(finalPlayer)}, tick={session.Host.Process.Tick}, steps={controller.Steps}";
        Check(controller.Error is null && session.FieldGeneration != saved.FieldGeneration, $"Normal spatial planning did not reach its native boundary: {controller.Error ?? policy.State.Status}; {diagnostics}");
        controller.Advance(1);
        Check(policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Any(item => item.Completed && observedOwners.Contains(item.Event.Id)), $"The actual source-owned direct spatial boundary was not recorded as completed; {diagnostics}");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The original spatial source changed.");
        Console.WriteLine($"PASS original direct spatial transition discovery, native handoff and completion after {controller.Steps} steps");
    }

    internal static void CheckChangedSpatialCatalog()
    {
        CheckLateParticipants();
        Ff7AutoPlayActiveEvent active = new("source-visit", "line:0:entity:2:script:5", "condition", 4, 2, 5, Ff7AutoPlayTargetKind.LineEnter, 10, 0, "before");
        Ff7AutoPlayState state = new() { Active = active, NavigationGoal = new("navigation", active.EventId) };
        state.Coverage.Visits.Add(new() { Visit = new("source-visit", "program"), Events = [new() { Event = new("completed", AutoPlayEventKind.Interaction), Completed = true }, new() { Event = new(active.EventId, AutoPlayEventKind.Interaction) }] });
        Ff7AutoPlayTarget target = new("spatial:2:0100:direct:triangle:42", 2, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, "predicate", false, System.Numerics.Vector3.Zero);
        Ff7AutoPlayObservation observation = new("source-visit", "program", 4, [target], true, true);
        string retainedCoverage = JsonSerializer.Serialize(state.Coverage);
        Ff7AutoPlayState replanned = state.Clone();
        Check(Ff7AutoPlayPolicy.ReplanForNewNativeTargets(replanned, observation) && replanned.Active is null && replanned.NavigationGoal is null, "A newly discovered native automatic boundary did not replan an unaccepted movement target.");
        Check(JsonSerializer.Serialize(replanned.Coverage) == retainedCoverage && replanned.Status is not null, "Spatial catalog replanning altered completed coverage or omitted its status.");
        Ff7AutoPlayState stale = state.Clone();
        stale.Active = active with { Generation = 3 };
        Check(Ff7AutoPlayPolicy.ReplanForNewNativeTargets(stale, observation) && stale.Active is null && stale.NavigationGoal is null, "New automatic boundaries retained a stale unaccepted movement plan or its preferred goal.");
        foreach (Ff7AutoPlayActiveEvent preserved in new[] { active with { Accepted = true }, active with { Returned = true }, active with { TransitionOwned = true }, active with { CompletionRecorded = true }, active with { Kind = Ff7AutoPlayTargetKind.AutonomousSpatial }, active with { Generation = 3, Kind = Ff7AutoPlayTargetKind.Gateway }, active with { VisitId = "different-visit" } })
        {
            Ff7AutoPlayState retained = state.Clone();
            retained.Active = preserved;
            Check(!Ff7AutoPlayPolicy.ReplanForNewNativeTargets(retained, observation) && retained.Active == preserved && retained.NavigationGoal == state.NavigationGoal, "New spatial metadata discarded accepted, native-owned or unrelated work.");
        }
        state.Coverage.Visits[0].Events.Add(new() { Event = new(target.Id, AutoPlayEventKind.Interaction) });
        Check(!Ff7AutoPlayPolicy.ReplanForNewNativeTargets(state, observation) && state.Active == active, "An unchanged spatial catalog repeatedly discarded the current unaccepted route.");
    }

    private static void CheckLateParticipants()
    {
        Ff7AutoPlayActiveEvent exit = new("visit", "line:0:entity:2:script:5", "exit-condition", 4, 2, 5, Ff7AutoPlayTargetKind.LineEnter, 10, 0, "before");
        Ff7AutoPlayTarget participant = new("actor:6:script:1", 6, 1, Ff7AutoPlayTargetKind.ActorTalk, "new-condition", true, System.Numerics.Vector3.Zero);
        Ff7AutoPlayObservation observation = new("visit", "program", 4, [participant], true, true);
        Ff7AutoPlayState source = new() { Active = exit, NavigationGoal = new("remote", "remote-event") };
        source.Coverage.Visits.Add(new() { Visit = new("visit", "program") });

        Ff7AutoPlayState newlyExposed = source.Clone();
        string coverage = JsonSerializer.Serialize(newlyExposed.Coverage);
        Check(Ff7AutoPlayPolicy.ReplanForNewNativeTargets(newlyExposed, observation) && newlyExposed.Active is null && newlyExposed.NavigationGoal is null, "A newly exposed native participant did not interrupt an unaccepted departure.");
        Check(JsonSerializer.Serialize(newlyExposed.Coverage) == coverage, "Late participant replanning changed native event coverage.");

        source.Coverage.Visits[0].Events.Add(new()
        {
            Event = new(participant.Id, AutoPlayEventKind.Interaction, ConditionSignature: "old-condition"),
            Available = false,
            Completed = true,
            Conditions = new() { ConsumedEntries = ["old-condition"], ConsumedExits = [participant.ConditionFingerprint] }
        });
        Ff7AutoPlayState newlyAvailable = source.Clone();
        Check(Ff7AutoPlayPolicy.ReplanForNewNativeTargets(newlyAvailable, observation), "An unavailable completed participant's unconsumed current entry was hidden by its old completion or recorded exit predicate.");
        Ff7AutoPlayTarget line = participant with { Id = "line:1:entity:6:script:1", Kind = Ff7AutoPlayTargetKind.LineTalk };
        Check(Ff7AutoPlayPolicy.ReplanForNewNativeTargets(source.Clone(), observation with { Events = [line] }), "A newly exposed native LINE participant did not interrupt departure.");

        Ff7AutoPlayState consumed = source.Clone();
        consumed.Coverage.Visits[0].Events[0].Conditions!.ConsumedEntries.Add(participant.ConditionFingerprint);
        Check(!Ff7AutoPlayPolicy.ReplanForNewNativeTargets(consumed, observation) && consumed.Active == exit, "A participant already executed under its current predicate interrupted departure again.");
        Ff7AutoPlayState available = source.Clone();
        available.Coverage.Visits[0].Events[0].Available = true;
        Check(!Ff7AutoPlayPolicy.ReplanForNewNativeTargets(available, observation) && available.Active == exit && available.NavigationGoal == source.NavigationGoal, "An unchanged available participant repeatedly preempted the departure plan.");

        foreach (Ff7AutoPlayObservation excluded in new[] { observation with { CanAcceptInput = false }, observation with { Generation = 5 }, observation with { VisitId = "other" }, observation with { Events = [participant with { AnalysisComplete = false }] } })
        {
            Ff7AutoPlayState retained = source.Clone();
            Check(!Ff7AutoPlayPolicy.ReplanForNewNativeTargets(retained, excluded) && retained.Active == exit && retained.NavigationGoal == source.NavigationGoal, "Unready, unsupported or unrelated participant metadata discarded departure ownership.");
        }
        foreach (Ff7AutoPlayActiveEvent excluded in new[] { exit with { Accepted = true }, exit with { Returned = true }, exit with { TransitionOwned = true }, exit with { CompletionRecorded = true }, exit with { DestinationField = null }, exit with { Kind = Ff7AutoPlayTargetKind.AutonomousSpatial } })
        {
            Ff7AutoPlayState retained = source.Clone();
            retained.Active = excluded;
            Check(!Ff7AutoPlayPolicy.ReplanForNewNativeTargets(retained, observation) && retained.Active == excluded && retained.NavigationGoal == source.NavigationGoal, "Late participants interrupted native-owned work or an ordinary local interaction.");
        }
        Ff7AutoPlayState gateway = source.Clone();
        gateway.Active = exit with { Kind = Ff7AutoPlayTargetKind.Gateway, DestinationField = null };
        Check(Ff7AutoPlayPolicy.ReplanForNewNativeTargets(gateway, observation), "A gateway without a retained destination ignored a newly exposed participant.");
        Ff7AutoPlayState multipleDestinations = source.Clone();
        multipleDestinations.Active = exit with { DestinationField = null };
        Ff7AutoPlayTarget exitTarget = participant with { Id = exit.EventId, Entity = exit.Entity, Script = exit.Script, Kind = exit.Kind, ChangesField = true };
        Check(Ff7AutoPlayPolicy.ReplanForNewNativeTargets(multipleDestinations, observation with { Events = [exitTarget, participant] }), "A native departure with multiple possible destinations ignored a newly exposed participant.");
    }

    internal static void CheckDirectOwnership()
    {
        Ff7AutoPlayActiveEvent active = new("source-visit", "spatial:2:0100:direct:triangle:42", "authored-predicate", 4, 2, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, 10, 0, "before");
        FieldTransitionCommand transition = new(10, 100, 200, 42, 128);
        FieldHostRequest owner = new(1, 2, 7, 0x100, transition);
        Ff7NativeTickObservation observation = new("source", 4, 26, [], transition, "after", owner);
        Check(Ff7AutoPlayPolicy.IsOwnedTransition(observation, active), "A direct autonomous MAP lost its exact source entity, PC and priority ownership.");
        Check(!Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(active, 10), "A shared destination substituted for observed direct-MAP ownership.");
        Check(Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(active with { TransitionOwned = true }, 10), "A source-owned direct MAP did not complete after reload.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation with { TransitionOwner = owner with { ProgramCounter = 0x120 } }, active), "A different stochastic branch with the same destination completed the selected direct MAP.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation with { TransitionOwner = owner with { Entity = 6 } }, active), "A different autonomous entity completed the selected direct MAP.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation with { TransitionOwner = owner with { Priority = 1 } }, active), "An interaction request acquired autonomous priority-seven MAP ownership.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation with { Generation = 5 }, active), "A direct MAP observation crossed native allocations.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation with { TransitionOwner = null }, active), "A destination alone completed a direct spatial frontier.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation with { Transition = transition with { X = 1 } }, active), "A different transition command reused the source MAP receipt.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation, active with { DestinationField = 11 }), "A direct MAP without a proved matching destination completed its frontier.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation, active with { Entity = 6 }), "A direct MAP identity disagreed with its saved event entity.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTransition(observation, active with { EventId = "spatial:2:invalid:direct:triangle:42" }), "An invalid direct MAP identity acquired transition ownership.");
        Ff7AutoPlayActiveEvent restored = JsonSerializer.Deserialize<Ff7AutoPlayActiveEvent>(JsonSerializer.Serialize(active with { TransitionOwned = true }))!;
        Check(Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(restored, 10), "Cold restoration lost captured direct MAP ownership.");
        Check(Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(active with { EventId = "spatial:2:0100:triangle:42" }, 10), "An existing requested-child spatial frontier acquired a new required save marker.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
