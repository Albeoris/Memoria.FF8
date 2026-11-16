using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class AutoPlayNavigationRefreshTests
{
    public static void Run()
    {
        CheckComponentAvailabilityProjection();
        CheckAvailabilitySourcePredicate();
        CheckRemoteMovementRegionProjection();
        CheckOriginalControlledLadderContinuation();
        CheckOriginalForeignFrontierInvalidation();
        CheckSourceBoundaryCompatibility();
        CheckOriginalRemoteBoundaryProjection();
        CheckOriginalDestinationGoal();
        CheckOriginalExhaustedHistoricalTravel();
        CheckOriginalCurrentFieldRefresh();
        CheckOriginalForeignRefresh();
        CheckOriginalRetainedGuardObservation();
        CheckScopedPhysicalTravel();
    }

    private static void CheckScopedPhysicalTravel()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent exit = new("exit", AutoPlayEventKind.Transition, ConditionSignature: "exit-entry");
        AutoPlayEvent actor = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "actor-entry");
        planner.Observe(new("local", "local-program"), [exit]);
        planner.Observe(new("foreign", "foreign-program"), [actor]);
        planner.ObserveNavigation("local", "lower", [exit]);
        planner.ObserveNavigation("foreign", "upper", [actor]);
        planner.Completed("local", exit.Id, "foreign", observedStoryChanged: false);
        planner.RecordNavigationRoute("lower", "upper", exit.Id, state.Coverage.Visits.Single(visit => visit.Visit.Key == "local").Events.Single().Routes.Single());
        state.NavigationGuards["physical"] = new("foreign", 1, EventId: actor.Id, NavigationKey: "upper");
        planner.MarkUnavailable("foreign", actor.Id, new("actor-entry", "foreign", "physical", "blocked", actor.Id));
        Dictionary<string, string> facts = new() { ["physical"] = "blocked" };
        Check(Plan(state) is null, "An unchanged foreign physical proof created a candidate or a duplicate inspection.");
        facts["physical"] = "open";
        var next = Plan(state);
        Check(next is { IsTravel: true, Event.Id: "exit", RefreshVisitKey: null, Goal.EventId: "actor", Goal.NavigationKey: "upper" }, "Changed remote eligibility did not select its event-specific native goal.");
        Check(Plan(state.Clone())?.Goal == next!.Goal, "Cold restoration lost the exact remote execution goal.");
        var navigation = state.Coverage.NavigationVisits.Single(visit => visit.Key == "upper");
        string? availability = Ff7AutoPlayPolicy.ReadComponentAvailability(state, navigation, actor, facts).Fingerprint;
        planner.Completed("foreign", actor.Id, entrySignature: "actor-entry", entrySignatureAuthoritative: true, entryAvailabilityFingerprint: availability, availabilityFingerprintAuthoritative: true);
        Check(Plan(state) is null && Plan(state.Clone()) is null, "Consumed foreign availability reopened an arrival-only inspection frontier.");
        facts["physical"] = "changed-open";
        Check(Plan(state) is { RefreshVisitKey: null, Goal.EventId: "actor" }, "A genuinely new remote physical availability failed to retain its execution frontier.");

        AutoPlayPlanStep? Plan(Ff7AutoPlayState selected)
        {
            return new AutoPlayPlanner<Ff7AutoPlayDeferral>(selected.Coverage).PlanNavigation("lower", readAvailability: (visit, observed) => Ff7AutoPlayPolicy.ReadComponentAvailability(selected, visit, observed, facts));
        }
    }

    private static void CheckComponentAvailabilityProjection()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent ladder = new("ladder", AutoPlayEventKind.Interaction, ConditionSignature: "ladder-entry");
        AutoPlayEvent actor = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "actor-entry");
        planner.Observe(new("field", "resource"), [ladder, actor]);
        planner.ObserveNavigation("field", "current", [ladder, actor]);
        planner.ObserveNavigation("field", "remote", [actor]);
        AutoPlayNavigationVisit current = state.Coverage.NavigationVisits.Single(visit => visit.Key == "current");
        AutoPlayNavigationVisit remote = state.Coverage.NavigationVisits.Single(visit => visit.Key == "remote");
        state.NavigationGuards["current-guard"] = new("field", 1, EventId: actor.Id, NavigationKey: current.Key, SourceTriangle: 0);
        state.NavigationGuards["remote-guard"] = new("field", 1, EventId: actor.Id, NavigationKey: remote.Key, SourceTriangle: 1);
        planner.MarkUnavailable("field", actor.Id, new("actor-entry", "field", "current-guard", "current-blocked", actor.Id));
        planner.MarkUnavailable("field", actor.Id, new("actor-entry", "field", "remote-guard", "remote-blocked", actor.Id));
        Dictionary<string, string> currentFacts = new() { ["current-guard"] = "current-open" };
        Dictionary<string, string> remoteFacts = new() { ["remote-guard"] = "remote-blocked" };
        AutoPlayEventAvailability currentAvailability = Ff7AutoPlayPolicy.ReadComponentAvailability(state, current, actor, currentFacts);
        AutoPlayEventAvailability remoteAvailability = Ff7AutoPlayPolicy.ReadComponentAvailability(state, remote, actor, remoteFacts);
        Check(!currentAvailability.Unavailable && remoteAvailability.Unavailable && remoteAvailability.UnavailableVariants.SetEquals([actor.Id]), "Current physical eligibility leaked into a separately blocked remote component.");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == actor.Id);
        coverage.Unavailable = currentAvailability.Unavailable;
        coverage.UnavailableVariants = currentAvailability.UnavailableVariants;
        coverage.AvailabilityFingerprint = currentAvailability.Fingerprint;
        planner.Completed("field", actor.Id, entrySignature: "actor-entry", entrySignatureAuthoritative: true);
        planner.Completed("field", ladder.Id, "field", observedStoryChanged: false, entrySignature: "ladder-entry", entrySignatureAuthoritative: true, targetId: ladder.Id);
        planner.RecordNavigationRoute(current.Key, remote.Key, ladder.Id, state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == ladder.Id).Routes.Single());
        string original = JsonSerializer.Serialize(state);
        AutoPlayEventAvailability ReadAvailability(AutoPlayNavigationVisit visit, AutoPlayEvent observed)
        {
            return Ff7AutoPlayPolicy.ReadComponentAvailability(state, visit, observed, visit.Key == current.Key ? currentFacts : remoteFacts);
        }
        Check(planner.PlanNavigation(current.Key, readAvailability: ReadAvailability) is null && planner.ReachableNavigationFrontierCount(current.Key, readAvailability: ReadAvailability) == 0, "A refreshed current component created travel toward a blocked remote actor frontier.");
        Check(JsonSerializer.Serialize(state) == original, "Reading native component eligibility changed aggregate coverage or source guards.");
        remoteFacts["remote-guard"] = "remote-target-moved";
        Check(!Ff7AutoPlayPolicy.ReadComponentAvailability(state, remote, actor, remoteFacts).Unavailable, "A changed remote native guard failed to release its old component absence proof.");
        Check(planner.PlanNavigation(current.Key, readAvailability: ReadAvailability) is { IsTravel: true, Event.Id: "ladder", Goal.NavigationKey: "remote", Goal.EventId: "actor" } && planner.ReachableNavigationFrontierCount(current.Key, readAvailability: ReadAvailability) == 1, "A genuinely changed remote native guard did not reopen exactly one reachable actor frontier.");
        Check(JsonSerializer.Serialize(state) == original, "Projecting changed remote native facts mutated recorded aggregate evidence.");
        Ff7AutoPlayState cold = state.Clone();
        Check(Ff7AutoPlayPolicy.ReadComponentAvailability(cold, remote, actor, new Dictionary<string, string> { ["remote-guard"] = "remote-blocked" }).Unavailable, "Cold restoration lost the exact remote source absence proof.");
    }

    private static void CheckAvailabilitySourcePredicate()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent older = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "older-entry");
        planner.Observe(new("field", "resource"), [older]);
        planner.ObserveNavigation("field", "remote", [older]);
        AutoPlayNavigationVisit remote = state.Coverage.NavigationVisits.Single();
        state.NavigationGuards["older-guard"] = new("field", 1, EventId: older.Id, NavigationKey: remote.Key, SourceTriangle: 1);
        planner.MarkUnavailable("field", older.Id, new("older-entry", "field", "older-guard", "older-blocked", older.Id));
        AutoPlayEvent current = older with { ConditionSignature = "current-entry" };
        planner.Observe(new("field", "resource"), [current]);
        state.NavigationGuards["current-guard"] = new("field", 1, EventId: current.Id, NavigationKey: remote.Key, SourceTriangle: 1);
        planner.MarkUnavailable("field", current.Id, new("current-entry", "field", "current-guard", "current-blocked", current.Id));
        string original = JsonSerializer.Serialize(state);
        Check(Ff7AutoPlayPolicy.ReadComponentAvailability(state, remote, older, new Dictionary<string, string> { ["older-guard"] = "older-blocked" }).Unavailable, "The aggregate current predicate hid the remote observed predicate's original absence proof.");
        Check(!Ff7AutoPlayPolicy.ReadComponentAvailability(state, remote, current, new Dictionary<string, string> { ["current-guard"] = "current-open" }).Unavailable, "An older predicate's absence proof blocked a newly observed source predicate.");
        RejectUnknown(() => Ff7AutoPlayPolicy.ReadComponentAvailability(state, remote, current, new Dictionary<string, string>()));
        RejectUnknown(() => Ff7AutoPlayPolicy.ReadComponentAvailability(state, remote, current, new Dictionary<string, string> { ["current-guard"] = string.Empty }));
        Check(JsonSerializer.Serialize(state) == original, "Historical predicate projection or rejected unknown facts changed aggregate evidence.");
    }

    private static void CheckRemoteMovementRegionProjection()
    {
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, X4096 = 10, Y4096 = 20, Z4096 = 30 };
        Ff7Actor blocker = new() { Slot = 1, Entity = 1, Model = 1, X4096 = 15, Y4096 = 25, Z4096 = 30, Triangle = 1, CollisionRadius = 4 };
        Ff7AutoPlayMovementFailure region = new(0, 20, 0, 40, 20, 40);
        string blocked = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [player, blocker], [blocker.Entity]);
        Ff7Actor elsewhere = player with { X4096 = 1000, Y4096 = -1000, Z4096 = 2000, Triangle = 2 };
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker], [blocker.Entity], true) == blocked, "The current player's remote location released an unchanged source movement-region proof.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker], [blocker.Entity]) != blocked, "Current-component movement eligibility ignored leaving its actual recorded source region.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker with { X4096 = 16 }], [blocker.Entity], true) != blocked, "A remote source-region projection hid changed relevant blocker occupancy.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("changed-native-source", region, [elsewhere, blocker], [blocker.Entity], true) != blocked, "A remote source-region projection hid changed native target eligibility.");
    }

    private static void RejectUnknown(Func<AutoPlayEventAvailability> read)
    {
        try
        {
            read();
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("dependency is unknown", StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException("Unknown component guard facts were accepted as changed native eligibility.");
    }

    private static void CheckOriginalControlledLadderContinuation()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CONTROLLED_LADDER_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Ff7AutoPlayActiveEvent initial = source.AutoPlay?.Active ?? throw new InvalidOperationException("The fixture requires its native active ladder owner.");
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        Check(session.Host.Process.Actors.Any(actor => actor.PlayerControlled && actor.Ladder is { Completed: false }), "The fixture requires its live player-controlled native ladder.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded native ladder continuation must not create choice checkpoints."));
        bool returned = false;
        session.NativeTickCompleted += observation =>
        {
            if (policy.State.Active is { } active && active.EventId == initial.EventId && active.BeforeWitness == initial.BeforeWitness && active.Returned)
            {
                returned = true;
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        HashSet<(string EventId, string Witness)> epochs = [];
        controller.Start();
        for (int step = 0; step < 512 && controller.Running && session.FieldGeneration == source.FieldGeneration && session.Battle is null; step++)
        {
            controller.Advance(1);
            if (policy.State.Active is { } active)
            {
                epochs.Add((active.EventId, active.BeforeWitness));
                Check(!returned || active.EventId != initial.EventId || active.Returned && active.BeforeWitness == initial.BeforeWitness, "The native ladder was reselected after its control return instead of leaving its consumed source frontier.");
            }
        }
        Check(controller.Failure is null, $"The bounded original ladder continuation failed: {controller.Error}");
        Check(returned && epochs.Any(epoch => epoch.EventId != initial.EventId), "The original ladder did not return and reach another native event within the bounded continuation.");
        Check(policy.State.PendingRestore is null && bytes.SequenceEqual(File.ReadAllBytes(path)), "The bounded ladder continuation queued a choice restore or changed its original standalone source.");
        Console.WriteLine($"PASS original controlled ladder leaves its consumed event through {epochs.Count} native event epochs without repeating its source; source unchanged.");
    }

    private static void CheckOriginalForeignFrontierInvalidation()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FOREIGN_FRONTIER_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        AutoPlayTravelGoal previousGoal = source.AutoPlay?.NavigationGoal ?? throw new InvalidOperationException("The fixture requires its recorded foreign-field frontier goal.");
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded foreign-frontier check must stop before new choice exploration."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool reached = false;
        for (int step = 0; step < 128 && controller.Running && !reached; step++)
        {
            controller.Advance(1);
            reached = session.FieldGeneration != source.FieldGeneration && policy.State.Active is { } active && active.Generation == session.FieldGeneration;
        }
        Check(controller.Failure is null, $"The bounded foreign-frontier continuation failed: {controller.Error}");
        Check(reached, "The native foreign-frontier continuation did not reach the next field's own event within its bounded ordinary steps.");
        Check(policy.State.NavigationGoal != previousGoal, "A blocked foreign-field frontier retained its phantom goal through the native gateway transition.");
        Check(policy.State.PendingRestore is null && bytes.SequenceEqual(File.ReadAllBytes(path)), "Foreign-frontier invalidation queued a replay or changed its original standalone source.");
        Console.WriteLine($"PASS original foreign-field frontier eligibility replaces its stale goal after {controller.Steps} ordinary steps; source unchanged.");
    }

    private static void CheckSourceBoundaryCompatibility()
    {
        Ff7AutoPlayState state = new();
        state.NavigationGuards["legacy"] = new("visit", 1);
        state.NavigationGuards["empty"] = new("visit", 1, SpatialBoundaryTriangles: []);
        state.NavigationGuards["bounded"] = new("visit", 1, SpatialBoundaryTriangles: [4, 7]);
        state.NavigationGuards["observed"] = new("visit", 1, ObservedFingerprint: "new-native-observation");
        Ff7AutoPlayState cold = state.Clone();
        Check(cold.NavigationGuards["legacy"].SpatialBoundaryTriangles is null, "An older guard fabricated an empty source boundary domain.");
        Check(cold.NavigationGuards["empty"].SpatialBoundaryTriangles is { Length: 0 }, "A proved empty source boundary domain was lost on restore.");
        Check(cold.NavigationGuards["bounded"].SpatialBoundaryTriangles!.SequenceEqual([4, 7]), "Cold restoration changed the source boundary domain.");
        Check(cold.NavigationGuards["legacy"].ObservedFingerprint is null && cold.NavigationGuards["observed"].ObservedFingerprint == "new-native-observation", "Cold restoration fabricated or discarded an optional latest native guard observation.");
        var upgraded = Ff7AutoPlayPolicy.RestorePhysicalGuard(state.NavigationGuards["legacy"], "current", 2, [3]);
        Check(upgraded.SpatialBoundaryTriangles!.SequenceEqual([3]), "Current native revalidation did not upgrade optional source boundary evidence.");
    }

    private static void CheckOriginalRemoteBoundaryProjection()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_REMOTE_BOUNDARY_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        AutoPlayTravelGoal goal = source.AutoPlay!.NavigationGoal!;
        var guard = source.AutoPlay.NavigationGuards.First(entry => entry.Value.NavigationKey == goal.NavigationKey && entry.Value.EventId == goal.EventId);
        string retained = source.AutoPlay.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.UnavailableAttempts).Last(attempt => attempt.GuardKey == guard.Key).GuardFingerprint;
        var observation = new Ff7AutoPlayDiscovery(session).Observe();
        var target = observation.Events.Single(target => target.Id == goal.EventId);
        Ff7AutoplayDriver driver = new(session);
        driver.ConfigureSpatialBoundaries(observation.Events);
        Check(driver.CaptureSpatialBoundaryTriangles().Length == 0, "The selected fixture must begin in the lower domain without automatic triangle contacts.");
        string differentDomain = driver.ReadPhysicalAvailabilityFingerprint(target, guard.Value.BlockingEntities, guard.Value.SourceTriangle);
        string sourceDomain = driver.ReadPhysicalAvailabilityFingerprint(target, guard.Value.BlockingEntities, guard.Value.SourceTriangle, new HashSet<int> { 70, 71 });
        Check(differentDomain != retained && sourceDomain == retained, "Original source boundary projection did not exactly recover the recorded physical proof.");
        Check(driver.CaptureSpatialBoundaryTriangles().Length == 0, "Read-only remote projection changed actual navigation boundaries.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded source-boundary check must stop before new choice exploration."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 128 && controller.Running && policy.State.NavigationGoal == goal; step++)
        {
            controller.Advance(1);
        }
        Check(controller.Failure is null && policy.State.NavigationGoal != goal, "The ordinary policy retained the false remote retry frontier.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Source-boundary validation changed the original standalone.");
        Console.WriteLine($"PASS original source-boundary fingerprint recovery and remote frontier replacement after {controller.Steps} ordinary callbacks; source unchanged.");
    }

    private static void CheckOriginalDestinationGoal()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DESTINATION_GOAL_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        AutoPlayTravelGoal goal = source.AutoPlay!.NavigationGoal!;
        Check(!source.AutoPlay.NavigationGuards.Values.Any(guard => guard.NavigationKey == goal.NavigationKey && guard.EventId == goal.EventId), "The destination-goal fixture already contains the intended native attempt.");
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded destination-goal check must stop before new choice exploration."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool attempted = false;
        for (int step = 0; step < 128 && controller.Running && !attempted; step++)
        {
            controller.Advance(1);
            attempted = policy.State.NavigationGuards.Values.Any(guard => guard.NavigationKey == goal.NavigationKey && guard.EventId == goal.EventId && guard.SpatialBoundaryTriangles is not null);
        }
        Check(controller.Failure is null && attempted, "Same-named source transport still skipped the requested native contact at its destination component.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Destination-goal validation changed the original standalone.");
        Console.WriteLine($"PASS original destination goal reaches its previously skipped native contact after {controller.Steps} ordinary callbacks; source unchanged.");
    }

    private static void CheckOriginalExhaustedHistoricalTravel()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_EXHAUSTED_ROUTE_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Check(source.AutoPlay is { Active: null, NavigationGoal: null }, "The historical travel fixture requires its exhausted native frontier.");
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded historical-route check must stop before new choice exploration."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool restoredEntryMode = false;
        for (int step = 0; step < 1200 && controller.Running && session.MapId != 531; step++)
        {
            controller.Advance(1);
            restoredEntryMode |= session.MapId == 544 && session.FieldGeneration > source.FieldGeneration && session.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.MapByte, 16) == 1;
        }
        Check(controller.Failure is null && restoredEntryMode && session.MapId == 531, $"The ordinary policy did not restore native entry mode and leave the exhausted local field cycle: {controller.Error}");
        Check(policy.State.PendingRestore is null && bytes.SequenceEqual(File.ReadAllBytes(path)), "Historical travel replayed a checkpoint or changed the original standalone.");
        Console.WriteLine($"PASS original exhausted-route fallback restores native entry mode and reaches an outside field after {controller.Steps} ordinary callbacks; source unchanged.");
    }

    private static void CheckOriginalCurrentFieldRefresh()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CURRENT_REFRESH_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        AutoPlayTravelGoal inherited = source.AutoPlay!.NavigationGoal!;
        string visit = source.AutoPlay.Active!.VisitId;
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during the bounded current-field refresh check."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool inheritedFinished = false;
        bool retriedRefresh = false;
        bool actorAttempted = false;
        bool actorFinished = false;
        for (int step = 0; step < 512 && controller.Running && session.FieldGeneration == source.FieldGeneration; step++)
        {
            controller.Advance(1);
            inheritedFinished |= policy.State.NavigationGoal != inherited;
            retriedRefresh |= inheritedFinished && policy.State.NavigationGoal is { EventId: null } goal && goal.NavigationKey.StartsWith(visit + ":", StringComparison.Ordinal);
            bool actorActive = policy.State.Active is { Kind: Ff7AutoPlayTargetKind.ActorTalk, Entity: 6 };
            actorAttempted |= actorActive;
            actorFinished |= actorAttempted && !actorActive;
        }
        Check(controller.Failure is null && inheritedFinished && !retriedRefresh && actorFinished && session.FieldGeneration > source.FieldGeneration, $"Duplicate current-field refresh still starved native execution: {controller.Error}");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Current-field refresh validation changed its original standalone.");
        Console.WriteLine($"PASS original inherited arrival finishes, native actor frontier resolves and normal travel continues without duplicate same-field refresh after {controller.Steps} callbacks; source unchanged.");
    }

    private static void CheckOriginalForeignRefresh()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FOREIGN_REFRESH_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        AutoPlayTravelGoal inherited = source.AutoPlay!.NavigationGoal!;
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during the bounded foreign refresh check."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool reached = false;
        for (int step = 0; step < 800 && controller.Running && !reached; step++)
        {
            controller.Advance(1);
            reached = session.FieldGeneration > source.FieldGeneration && policy.State.NavigationGoal != inherited && policy.State.Active is { Kind: Ff7AutoPlayTargetKind.ActorTalk, Accepted: true };
        }
        Check(controller.Failure is null && reached && policy.State.NavigationGoal != inherited, $"A duplicate foreign physical inspection still starved native event admission: {controller.Error}");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Foreign refresh validation changed the original standalone.");
        Console.WriteLine($"PASS original stale foreign inspection yields to native actor admission after {controller.Steps} ordinary callbacks; source unchanged.");
    }

    private static void CheckOriginalRetainedGuardObservation()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_RETAINED_GUARD_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        var sourceGuard = source.AutoPlay!.NavigationGuards.First(entry => entry.Value.EventId == "actor:9:script:1" && entry.Value.NavigationKey!.StartsWith("ff7:525:", StringComparison.Ordinal) && entry.Value.NavigationKey.EndsWith(":16", StringComparison.Ordinal));
        string failure = source.AutoPlay.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.UnavailableAttempts).Last(attempt => attempt.GuardKey == sourceGuard.Key).GuardFingerprint;
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        int checkpoints = 0;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) =>
        {
            checkpoints++;
            throw new ExpectedChoiceBoundary();
        });
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool retainedOutside = false;
        for (int step = 0; step < 800 && controller.Running && checkpoints == 0; step++)
        {
            controller.Advance(1);
            string? observed = policy.State.NavigationGuards[sourceGuard.Key].ObservedFingerprint;
            if (!retainedOutside && session.MapId != 525 && observed is not null && observed != failure)
            {
                retainedOutside = true;
                Check(policy.State.Clone().NavigationGuards[sourceGuard.Key].ObservedFingerprint == observed, "Cold state lost the released native source proof while travelling outside its field.");
            }
        }
        Check(retainedOutside && checkpoints == 1 && controller.Failure is ExpectedChoiceBoundary, $"Latest source eligibility did not survive departure through the next native choice frontier: {controller.Error}");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Retained guard validation changed its original standalone.");
        Console.WriteLine($"PASS original latest guard observation survives foreign travel and reaches a new native choice after {controller.Steps} callbacks; source unchanged.");
    }

    private sealed class ExpectedChoiceBoundary : Exception
    {
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
