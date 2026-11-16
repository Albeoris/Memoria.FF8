using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class AutoPlayNavigationRefreshTests
{
    public static void Run()
    {
        CheckCompletedRemoteAvailability();
        CheckComponentAvailabilityProjection();
        CheckAvailabilitySourcePredicate();
        CheckRemoteMovementRegionProjection();
        CheckOriginalControlledLadderContinuation();
        CheckOriginalForeignFrontierInvalidation();
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent ladder = new("ladder", AutoPlayEventKind.Interaction, ConditionSignature: "ladder-entry");
        AutoPlayEvent actor = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "actor-entry");
        planner.Observe(new("field", "resource"), [ladder, actor]);
        planner.ObserveNavigation("field", "lower", [ladder, actor]);
        planner.BeginEvent("field", "ladder");
        planner.Completed("field", "ladder", "field", observedStoryChanged: false);
        planner.ObserveNavigation("field", "upper", [actor]);
        planner.RecordNavigationRoute("lower", "upper", "ladder", planner.State.Visits.Single().Events.Single(item => item.Event.Id == "ladder").Routes.Single());
        state.NavigationGuards["lower-guard"] = new("field", 0, EventId: "actor", NavigationKey: "lower");
        state.NavigationGuards["upper-guard"] = new("field", 0, EventId: "actor", NavigationKey: "upper");
        planner.MarkUnavailable("field", "actor", new("actor-entry", "field", "lower-guard", "lower-physical", "actor"));
        Check(planner.PlanNavigation("lower") is null, "An aggregate unavailable actor should not be counted as a currently executable remote frontier.");
        AutoPlayPlanStep? refresh = planner.PlanNavigation("lower", NeedsRefresh);
        Check(refresh is { IsTravel: true, Event.Id: "ladder", RefreshVisitKey: "upper", Goal.NavigationKey: "upper" }, "A component refresh must use the shared planner's recorded first-hop route despite aggregate event unavailability.");
        Check(planner.ReachableNavigationFrontierCount("lower", needsRefresh: NeedsRefresh) == 1, "A required remote inspection must count as one reachable refresh frontier.");
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["lower-guard"] = "upper-physical" });
        Check(planner.PlanNavigation("upper") is { IsTravel: false, Event.Id: "actor" }, "The upper component's changed physical guard must permit its normal native actor retry.");
        planner.MarkUnavailable("field", "actor", new("actor-entry", "field", "upper-guard", "upper-physical", "actor"));
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["lower-guard"] = "upper-physical", ["upper-guard"] = "upper-physical" });
        Check(planner.PlanNavigation("lower", NeedsRefresh) is null, "Physical failures recorded in both components must bound refresh travel instead of cycling.");
        Ff7AutoPlayState cold = state.Clone();
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(cold, new("upper", "resource")), "Cold restoration lost the component-specific failure inspection receipt.");
        AutoPlayEvent changedActor = actor with { ConditionSignature = "changed-scratch" };
        planner.Observe(new("field", "resource"), [ladder, changedActor]);
        planner.ObserveNavigation("field", "lower", [ladder, changedActor]);
        planner.ObserveNavigation("field", "upper", [changedActor]);
        state.NavigationGuards["lower-guard"] = state.NavigationGuards["lower-guard"] with { SourceTriangle = 0 };
        state.NavigationGuards["upper-guard"] = state.NavigationGuards["upper-guard"] with { SourceTriangle = 1 };
        planner.MarkUnavailable("field", "actor", new("changed-scratch", "field", "lower-guard", "lower-physical", "actor"));
        Dictionary<string, string> validatedPhysicalGuards = new(StringComparer.Ordinal) { ["lower-guard"] = "lower-physical", ["upper-guard"] = "upper-physical" };
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), validatedPhysicalGuards), "A changed raw script condition must not invalidate an unchanged source-component physical absence proof.");
        Check(Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource")), "Without revalidated native physical facts, a legacy receipt must retain exact-condition eligibility.");
        Check(planner.PlanNavigation("lower", visit => Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, visit, validatedPhysicalGuards)) is null, "Unchanged physical proofs must prevent repeating an already consumed ladder after scratch predicates change.");
        validatedPhysicalGuards["upper-guard"] = "moved-target";
        Check(Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), validatedPhysicalGuards), "A changed target fingerprint must invalidate its old component absence proof.");
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("lower", "resource"), validatedPhysicalGuards), "Invalidating a remote target proof must preserve the unchanged local component bound.");
        Check(planner.PlanNavigation("lower", visit => Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, visit, validatedPhysicalGuards)) is { IsTravel: true, RefreshVisitKey: "upper" }, "A genuinely changed remote physical proof must permit the existing shared refresh route.");
        planner.MarkUnavailable("field", "actor", new("changed-scratch", "field", "upper-guard", "moved-target", "actor"));
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), validatedPhysicalGuards), "A new physical failure must bound the refreshed component again.");
        validatedPhysicalGuards["upper-guard"] = "moved-again";
        Check(Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), validatedPhysicalGuards), "A supplied changed physical proof must invalidate even an exact-condition failure receipt.");
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["lower-guard"] = "changed", ["upper-guard"] = "changed" });
        planner.Completed("field", "actor", entrySignature: "changed-scratch", entrySignatureAuthoritative: true);
        planner.MarkUnavailable("field", "actor", new("changed-scratch", "field", "lower-guard", "lower-physical", "actor"));
        state.NavigationGuards.Remove("upper-guard");
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource")), "An already consumed condition must not create a perpetual remote inspection frontier.");

        bool NeedsRefresh(AutoPlayVisit visit)
        {
            return Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, visit);
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

    private static void CheckCompletedRemoteAvailability()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent actor = new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "entry");
        planner.Observe(new("field", "resource"), [actor]);
        planner.ObserveNavigation("field", "upper", [actor]);
        state.NavigationGuards["physical"] = new("field", 1, EventId: "actor", NavigationKey: "lower", SourceTriangle: 0);
        planner.MarkUnavailable("field", "actor", new("entry", "field", "physical", "lower-blocked", "actor"));
        Dictionary<string, string> upper = new() { ["physical"] = "upper-open" };
        planner.RefreshUnavailable("field", upper);
        planner.Completed("field", "actor", entrySignature: "entry", entrySignatureAuthoritative: true);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> item = state.Coverage.Visits.Single().Events.Single();
        string? completed = item.AvailabilityFingerprint;
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "lower-blocked" });
        item.RouteNeedsRediscovery = true;
        string? current = item.AvailabilityFingerprint;
        Check(AutoPlayPlanner<Ff7AutoPlayDeferral>.NeedsExecution(item), "The current blocked availability must differ from the executed upper source.");
        AutoPlayEventAvailability projected = AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(item, upper);
        Check(projected.Fingerprint == completed && !projected.Unavailable && item.AvailabilityFingerprint == current && item.Unavailable, "Pure component projection must preserve exact shared availability identity without changing live coverage.");
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), componentPhysicalGuards: upper), "Current-component availability and route probes must not reopen a consumed remote component.");
        Dictionary<string, string> moved = new() { ["physical"] = "upper-changed-open" };
        Check(Ff7AutoPlayPolicy.NeedsNavigationRefresh(state, new("upper", "resource"), componentPhysicalGuards: moved), "A genuinely unconsumed remote availability must retain its refresh frontier.");
        Ff7AutoPlayState cold = state.Clone();
        Check(!Ff7AutoPlayPolicy.NeedsNavigationRefresh(cold, new("upper", "resource"), componentPhysicalGuards: upper), "Cold restoration lost consumption of the remote component's actual availability.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
