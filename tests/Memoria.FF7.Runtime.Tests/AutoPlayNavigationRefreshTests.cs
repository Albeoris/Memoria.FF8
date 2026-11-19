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
        CheckOriginalScopedFailureContinuation();
        CheckOriginalControlledLadderContinuation();
        CheckOriginalForeignFrontierInvalidation();
        CheckSourceBoundaryCompatibility();
        CheckOriginalRemoteBoundaryProjection();
        CheckOriginalDestinationGoal();
        CheckOriginalExhaustedHistoricalTravel();
        CheckOriginalCurrentFieldRefresh();
        CheckOriginalForeignRefresh();
        CheckOriginalRetainedGuardObservation();
        CheckCachedDeferralKeepsSourceEvidence();
        CheckOriginalFailedAdmission();
        CheckScopedPhysicalTravel();
        CheckRetainedWorldFrontier();
        CheckWorldComponentDeferralPersistence();
        CheckOriginalWorldFrontier();
        CheckOriginalWorldAvailabilityReceipt();
    }

    private static void CheckOriginalWorldAvailabilityReceipt()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_AVAILABILITY_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Check(source.World is not null, "The availability receipt fixture requires its native world continuation.");
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded world availability receipt must not create choice checkpoints."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        Ff7AutoPlayActiveEvent? selected = null;
        bool accepted = false;
        bool completed = false;
        controller.Start();
        for (int step = 0; step < 4096 && controller.Running && !completed; step++)
        {
            controller.Advance(1);
            if (selected is null && policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh, EntryAvailabilityFingerprint: not null } candidate && candidate != source.AutoPlay?.Active)
            {
                AutoPlayEventCoverage<Ff7AutoPlayDeferral> entry = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == candidate.VisitId).Events.Single(item => item.Event.Id == candidate.EventId);
                if (!entry.CompletedAvailabilityFingerprints.Contains(candidate.EntryAvailabilityFingerprint))
                {
                    selected = candidate;
                }
            }
            if (selected is not null)
            {
                if (policy.State.Active is { } current && current.VisitId == selected.VisitId && current.EventId == selected.EventId && current.BeforeWitness == selected.BeforeWitness)
                {
                    accepted |= current.Accepted;
                }
                accepted |= policy.State.WorldDispatch is { } dispatch && dispatch.VisitId == selected.VisitId && dispatch.EventId == selected.EventId;
                AutoPlayEventCoverage<Ff7AutoPlayDeferral> receipt = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == selected.VisitId).Events.Single(item => item.Event.Id == selected.EventId);
                completed = receipt.CompletedAvailabilityFingerprints.Contains(selected.EntryAvailabilityFingerprint!) && receipt.Conditions?.HasExecutedEntry(selected.ConditionFingerprint) == true;
            }
        }
        Check(controller.Failure is null, $"The bounded native world availability receipt failed: {controller.Error}");
        Check(selected is not null && accepted && completed, $"A newly admitted native world availability did not return its exact entry fingerprint and consumed predicate within the bounded continuation; selected={selected?.EventId}, availability={selected?.EntryAvailabilityFingerprint}, accepted={accepted}, steps={controller.Steps}, status={policy.State.Status}");
        Check(policy.State.PendingRestore is null && bytes.SequenceEqual(File.ReadAllBytes(path)), "World availability completion queued a replay or changed its original standalone source.");
        Ff7AutoPlayState cold = policy.State.Clone();
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> retained = cold.Coverage.Visits.Single(visit => visit.Visit.Key == selected!.VisitId).Events.Single(item => item.Event.Id == selected!.EventId);
        Check(retained.CompletedAvailabilityFingerprints.Contains(selected!.EntryAvailabilityFingerprint!) && retained.Conditions?.HasExecutedEntry(selected.ConditionFingerprint) == true, "Cold restoration lost the actual native world availability completion receipt.");
        Console.WriteLine($"PASS original native world handoff/control return records its exact admitted availability and entry predicate after {controller.Steps} ordinary steps; source unchanged.");
    }

    private static void CheckWorldComponentDeferralPersistence()
    {
        Ff7WorldTerrainReference source = new(2, 3, 4);
        Ff7WorldTerrainReference connected = new(2, 3, 5);
        Ff7WorldTerrainReference outside = new(2, 3, 6);
        Ff7AutoPlayState state = new();
        state.WorldVisits["world"] = new()
        {
            PhysicalGuards = ["blocked-leg", "other-leg"],
            ComponentDeferrals = new()
            {
                ["blocked-leg"] = new("native-context", [source, connected], "proved-blocked"),
                ["other-leg"] = new("other-context", [outside], "other-proof")
            }
        };
        Ff7AutoPlayState cold = state.Clone();
        Ff7AutoPlayWorldVisitState visited = cold.WorldVisits["world"];
        string original = JsonSerializer.Serialize(visited);
        Check(Ff7AutoPlayPolicy.ReadWorldGuardFingerprint(visited, "blocked-leg", "native-context", source, "new-position") == "proved-blocked", "Cold restoration discarded a proved closed world component when the player returned at another position.");
        Check(Ff7AutoPlayPolicy.ReadWorldGuardFingerprint(visited, "blocked-leg", "native-context", connected, "another-position") == "proved-blocked", "Moving within the proved world component reopened its failed remote leg.");
        Check(JsonSerializer.Serialize(visited) == original, "Retaining a world component proof changed its native dependency metadata.");
        Ff7AutoPlayWorldVisitState reallocated = cold.Clone().WorldVisits["world"];
        Check(Ff7AutoPlayPolicy.ReadWorldGuardFingerprint(reallocated, "blocked-leg", "native-context", connected, "reentry-position") == "proved-blocked", "A reallocated policy visit required an ephemeral world component cache to retain its proof.");

        foreach ((string context, Ff7WorldTerrainReference? ground) in new (string, Ff7WorldTerrainReference?)[] { ("changed-context", source), ("native-context", outside), ("native-context", null) })
        {
            Ff7AutoPlayWorldVisitState changed = state.Clone().WorldVisits["world"];
            Check(Ff7AutoPlayPolicy.ReadWorldGuardFingerprint(changed, "blocked-leg", context, ground, "current-native") == "current-native", "A changed or unknown world component retained an inapplicable physical proof.");
            Check(!changed.ComponentDeferrals.ContainsKey("blocked-leg") && changed.ComponentDeferrals.ContainsKey("other-leg") && changed.PhysicalGuards.SetEquals(["blocked-leg", "other-leg"]), "Releasing one world component proof removed another source or its recorded physical guard.");
        }
        Check(Ff7AutoPlayPolicy.ReadWorldGuardFingerprint(visited, "unknown-leg", "native-context", source, "unscoped-native") == "unscoped-native", "An unscoped world guard borrowed another leg's component proof.");
        Ff7AutoPlayWorldVisitState legacy = JsonSerializer.Deserialize<Ff7AutoPlayWorldVisitState>("{\"PhysicalGuards\":[\"legacy-leg\"]}")!;
        Check(legacy.ComponentDeferrals.Count == 0 && Ff7AutoPlayPolicy.ReadWorldGuardFingerprint(legacy, "legacy-leg", "native-context", source, "legacy-native") == "legacy-native", "An older world visit fabricated a required component proof or lost normal guard revalidation.");
        Check(state.WorldVisits["world"].ComponentDeferrals.Count == 2, "Cold component proof updates mutated their source state.");
    }

    private static void CheckRetainedWorldFrontier()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent entry = new("world-entry", AutoPlayEventKind.Transition, ConditionSignature: "entry-condition");
        AutoPlayEvent blocked = new("blocked", AutoPlayEventKind.Interaction, ConditionSignature: "blocked-condition");
        AutoPlayEvent alternative = new("alternative", AutoPlayEventKind.Interaction, ConditionSignature: "alternative-condition");
        planner.Observe(new("world", "world-program"), [entry]);
        planner.Observe(new("field", "field-program"), [blocked, alternative]);
        planner.ObserveNavigation("world", "world-node", [entry]);
        planner.ObserveNavigation("field", "field-node", [blocked, alternative]);
        planner.Completed("world", entry.Id, "field", observedStoryChanged: false, entrySignature: entry.ConditionSignature, entrySignatureAuthoritative: true);
        planner.RecordNavigationRoute("world-node", "field-node", entry.Id, state.Coverage.Visits.Single(visit => visit.Visit.Key == "world").Events.Single().Routes.Single());
        state.VisitGuards["field"] = new([], [], "retained-field");
        state.NavigationGuards["source-guard"] = new("field", 1, EventId: blocked.Id, NavigationKey: "field-node");
        planner.MarkUnavailable("field", blocked.Id, new(blocked.ConditionSignature!, "field", "source-guard", "older-blocked", blocked.Id));
        planner.MarkUnavailable("field", blocked.Id, new(blocked.ConditionSignature!, "field", "source-guard", "latest-blocked", blocked.Id));
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> aggregate = state.Coverage.Visits.Single(visit => visit.Visit.Key == "field").Events.Single(item => item.Event.Id == blocked.Id);
        aggregate.Unavailable = false;
        aggregate.UnavailableVariants.Clear();
        aggregate.AvailabilityFingerprint = null;
        AutoPlayTravelGoal preferred = new("field-node", blocked.Id, blocked.ConditionSignature);
        Check(planner.PlanNavigation("world-node", preferredGoal: preferred)?.Goal == preferred, "The authored world source did not reproduce aggregate eligibility admitting the blocked preferred goal.");
        string before = JsonSerializer.Serialize(state);
        Check(Plan(state)?.Goal is { EventId: "alternative", NavigationKey: "field-node" }, "World planning resurrected a preferred field event whose exact source retained an unchanged failure.");
        Check(Plan(state.Clone())?.Goal is { EventId: "alternative" }, "Cold restoration lost retained source-scoped field eligibility during world planning.");
        Check(JsonSerializer.Serialize(state) == before, "Retained world frontier projection mutated aggregate coverage or source evidence.");
        AutoPlayNavigationVisit world = state.Coverage.NavigationVisits.Single(visit => visit.Key == "world-node");
        Check(Ff7AutoPlayPolicy.ReadRetainedNavigationAvailability(state, world, entry) is null, "An unknown unguarded visit fabricated retained native availability.");
        state.NavigationGuards["source-guard"] = state.NavigationGuards["source-guard"] with { ObservedFingerprint = "native-released" };
        Check(Plan(state)?.Goal == preferred, "A retained genuine source change did not release the preferred field frontier from world planning.");
        state.WorldVisits["world"] = new();
        planner.MarkUnavailable("world", entry.Id, new(entry.ConditionSignature!, "world", "world-guard", "older-world-failure", entry.Id));
        planner.MarkUnavailable("world", entry.Id, new(entry.ConditionSignature!, "world", "world-guard", "latest-world-failure", entry.Id));
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> worldAggregate = state.Coverage.Visits.Single(visit => visit.Visit.Key == "world").Events.Single();
        worldAggregate.Unavailable = false;
        worldAggregate.UnavailableVariants.Clear();
        worldAggregate.AvailabilityFingerprint = null;
        Check(Ff7AutoPlayPolicy.ReadRetainedNavigationAvailability(state, world, entry) is { Unavailable: true }, "Missing optional world observations discarded the latest recorded source failure.");
        Check(Plan(state) is null, "A field/world route reused aggregate-open world travel despite its retained native source failure.");
        state.WorldVisits["world"].ObservedPhysicalGuards["world-guard"] = "native-world-released";
        Check(Ff7AutoPlayPolicy.ReadRetainedNavigationAvailability(state, world, entry) is { Unavailable: false } && Plan(state)?.Goal == preferred, "A genuinely released world source retained its obsolete failure during remote planning.");
        state.WorldVisits["world"].ObservedPhysicalGuards["world-guard"] = "latest-world-failure";
        string retained = JsonSerializer.Serialize(state);
        Check(Plan(state) is null && Plan(state.Clone()) is null, "A freshly revalidated world failure reopened after departure or cold restoration.");
        Check(Ff7AutoPlayPolicy.ReadRetainedNavigationAvailability(state, world, entry with { ConditionSignature = "new-entry-condition" }) is { Unavailable: false }, "A world source failure leaked across native entry predicates.");
        Check(JsonSerializer.Serialize(state) == retained, "Reading retained world source facts changed aggregate coverage or observations.");

        AutoPlayPlanStep? Plan(Ff7AutoPlayState selected)
        {
            return new AutoPlayPlanner<Ff7AutoPlayDeferral>(selected.Coverage).PlanNavigation("world-node", preferredGoal: preferred, readAvailability: Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(selected));
        }
    }

    private static void CheckOriginalWorldFrontier()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_FRONTIER_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        AutoPlayTravelGoal staleGoal = source.AutoPlay?.NavigationGoal ?? throw new InvalidOperationException("The fixture requires its retained field frontier goal.");
        Check(source.World is not null, "The fixture requires an ordinary native world continuation.");
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded world-frontier continuation must stop before new choice checkpoints."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        HashSet<(string Visit, string Event, string Entry)> consumed = Entries(policy.State);
        bool advanced = false;
        int worldReturns = 0;
        bool wasWorld = true;
        controller.Start();
        // Allow the observed native travel continuation and its long ladder animation to return.
        for (int step = 0; step < 2048 && controller.Running && !advanced; step++)
        {
            controller.Advance(1);
            bool isWorld = session.World is not null;
            if (isWorld && !wasWorld)
            {
                worldReturns++;
            }
            wasWorld = isWorld;
            Check(worldReturns < 3, "The original world frontier repeated field-entry/return travel instead of leaving its retained failed preferred goal.");
            bool newNativeAdmission = session.World is null && policy.State.Active is { Accepted: true, Kind: not Ff7AutoPlayTargetKind.Gateway and not Ff7AutoPlayTargetKind.WorldMesh } admitted && admitted.Generation == session.FieldGeneration && !consumed.Contains((admitted.VisitId, admitted.EventId, admitted.ConditionFingerprint));
            advanced = newNativeAdmission || session.Battle is not null || Entries(policy.State).Any(entry => !consumed.Contains(entry) && policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == entry.Visit).Events.Single(item => item.Event.Id == entry.Event).Event.Kind == AutoPlayEventKind.Interaction);
        }
        Check(controller.Failure is null, $"The bounded original world-frontier continuation failed: {controller.Error}");
        Ff7AutoPlayActiveEvent? active = policy.State.Active;
        string diagnostics = $"field={session.Field.Name}, map={session.MapId}, generation={session.FieldGeneration}, tick={session.Host.Process.Tick}, worldTick={session.World?.Process.Tick}, steps={controller.Steps}, worldReturns={worldReturns}, active={active?.Kind}/{active?.EventId}, accepted={active?.Accepted}, returned={active?.Returned}, status={policy.State.Status}, goal={JsonSerializer.Serialize(policy.State.NavigationGoal)}";
        Check(advanced && policy.State.NavigationGoal != staleGoal, $"The original world source did not reach a new native field interaction admission, completed interaction or encounter within its bounded ordinary continuation; {diagnostics}");
        Check(policy.State.PendingRestore is null && bytes.SequenceEqual(File.ReadAllBytes(path)), "World frontier projection queued a replay or changed its original standalone source.");
        Console.WriteLine($"PASS original world frontier leaves its retained failed goal and reaches a new native interaction admission or completion/encounter after {controller.Steps} ordinary steps; source unchanged.");

        static HashSet<(string Visit, string Event, string Entry)> Entries(Ff7AutoPlayState state)
        {
            return state.Coverage.Visits.SelectMany(visit => visit.Events.SelectMany(item => (item.Conditions?.ConsumedEntries ?? []).Select(entry => (visit.Visit.Key, item.Event.Id, entry)))).ToHashSet();
        }
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
        foreach (Ff7Actor inside in new[] { player with { X4096 = 0, Y4096 = 0, Z4096 = 20 }, player with { X4096 = 20, Y4096 = 40, Z4096 = 40 }, player with { X4096 = 11, Direction = 128 } })
        {
            Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [inside, blocker], [blocker.Entity]) == blocked, "Movement within inclusive fixed-point failure bounds changed its local scope.");
        }
        foreach (Ff7Actor outside in new[] { player with { X4096 = -1 }, player with { X4096 = 21 }, player with { Y4096 = -1 }, player with { Y4096 = 41 }, player with { Z4096 = 19 }, player with { Z4096 = 41 } })
        {
            Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [outside, blocker], [blocker.Entity]) != blocked, "Crossing a recorded failure bound did not reopen its local approach.");
        }
        Ff7AutoPlayMovementFailure xy = region with { MinZ = null, MaxZ = null };
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", xy, [player, blocker]) == Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", xy, [player with { Z4096 = int.MaxValue }, blocker]), "An older XY-only failure acquired an unrecorded height boundary.");
        string legacyWitness = $"native-source|movement-entry-admission:2|occupancy:{blocker.Entity}:{blocker.Model}:{blocker.X4096}:{blocker.Y4096}:{blocker.Z4096}:{blocker.Triangle}:{blocker.CollisionRadius}";
        string legacy = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacyWitness)));
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker], [blocker.Entity], currentSource: false, retainedFingerprints: [legacy]) == legacy, "A legacy retained failure was revalidated before observing its actual native source.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [player, blocker], [blocker.Entity], retainedFingerprints: [legacy]) != legacy, "An actual source observation did not revalidate the obsolete optional scope contract.");
        string moved = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker with { X4096 = 16 }], [blocker.Entity], currentSource: false, retainedSourceInside: true, retainedFingerprints: [legacy]);
        Check(moved != legacy, "A changed retained legacy collider did not reopen its original source proof.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker], [blocker.Entity], currentSource: false, retainedFingerprints: [legacy]) == legacy, "A returned retained collider failed to restore the original legacy refusal after an intervening observation.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker], [blocker.Entity], currentSource: false, retainedSourceInside: true, retainedFingerprints: [blocked]) == blocked, "A newly recorded current-source refusal reopened when projected into its retained component.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker], [blocker.Entity], currentSource: false, retainedFingerprints: [legacy, moved]) == legacy, "A later recorded collider failure hid an earlier valid legacy refusal when its collider returned.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker], [blocker.Entity]) != blocked, "The actual current source inherited unrelated retained membership.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker], [blocker.Entity], currentSource: false, retainedSourceInside: true) == blocked, "The current player's remote location released an unchanged source movement-region proof.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native-source", region, [elsewhere, blocker with { X4096 = 16 }], [blocker.Entity], currentSource: false, retainedSourceInside: true) != blocked, "A remote source-region projection hid changed relevant blocker occupancy.");
        Check(Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("changed-native-source", region, [elsewhere, blocker], [blocker.Entity], currentSource: false, retainedSourceInside: true) != blocked, "A remote source-region projection hid changed native target eligibility.");
        foreach (string component in new[] { "left", "right" })
        {
            string physical = "native-source:" + component;
            string completed = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint(physical, region, [elsewhere, blocker], [blocker.Entity]);
            string remote = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint(physical, region, [player, blocker], [blocker.Entity], currentSource: false, retainedSourceInside: false);
            AutoPlayEventCoverage<Ff7AutoPlayDeferral> consumed = new() { Event = new(component, AutoPlayEventKind.Interaction, ConditionSignature: "stable"), Completed = true, Conditions = new(), AvailabilityFingerprint = remote };
            consumed.Conditions.ConsumedEntries.Add("stable");
            consumed.CompletedAvailabilityFingerprints.Add(completed);
            Check(remote == completed && !AutoPlayPlanner<Ff7AutoPlayDeferral>.NeedsExecution(consumed), "A completed outside-region component became a new frontier when the player entered the opposing component.");
            consumed.AvailabilityFingerprint = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint(physical, region, [player, blocker with { X4096 = 16 }], [blocker.Entity], currentSource: false, retainedSourceInside: false);
            Check(AutoPlayPlanner<Ff7AutoPlayDeferral>.NeedsExecution(consumed), "A real retained-source occupancy change failed to revise its completed availability.");
        }
        Ff7AutoPlayState typed = new();
        typed.NavigationGuards["outside"] = new("visit", 1, MovementFailure: region, ObservedFingerprint: "observed", MovementSourceInside: false);
        typed.NavigationGuards["older"] = new("visit", 1, MovementFailure: region, ObservedFingerprint: "recorded");
        Ff7AutoPlayState cold = typed.Clone();
        Check(cold.NavigationGuards["outside"].MovementSourceInside == false && cold.NavigationGuards["older"].MovementSourceInside is null && cold.NavigationGuards["older"].ObservedFingerprint == "recorded", "Cold restoration invented or lost retained source membership.");
    }

    private static void CheckOriginalScopedFailureContinuation()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SCOPED_FAILURE_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(game))
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        Check(session.MapId == 553, "The scoped failure fixture requires its recorded source field.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The scoped failure continuation reached a new choice boundary."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        bool selected = false;
        bool accepted = false;
        session.InteractionScriptRequested += (entity, script, admitted) => accepted |= entity == 7 && script == 4 && admitted;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        HashSet<string> originalGuards = policy.State.NavigationGuards.Keys.ToHashSet(StringComparer.Ordinal);
        for (int callback = 0; callback < 4096 && controller.Running && session.MapId == 553; callback++)
        {
            controller.Advance(1);
            selected |= policy.State.Active?.EventId == "line:1:entity:7:script:4";

        }
        foreach (var entry in policy.State.NavigationGuards.Where(entry => !originalGuards.Contains(entry.Key)))
        {
            Console.WriteLine($"scoped continuation new guard event={entry.Value.EventId}, entity={entry.Value.Entity}, triangle={entry.Value.SourceTriangle}, blockers={string.Join(',', entry.Value.BlockingEntities ?? [])}, region={entry.Value.MovementFailure}");
        }
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The scoped failure continuation changed its standalone source.");
        Check(selected, $"The normal policy did not reopen its scoped LINE approach: {policy.Status}");
        Check(session.MapId == 557 && controller.Error is null && accepted, $"The normal scoped continuation did not reach its selected native destination: map={session.MapId}, callbacks={controller.Steps}, accepted={accepted}, error={controller.Error}, status={policy.Status}");
        Check(policy.State.PendingRestore is null && bytes.SequenceEqual(File.ReadAllBytes(path)), "The scoped failure continuation queued a replay or changed its standalone source.");
        Console.WriteLine($"PASS original scoped failure reopens LINE and reaches its native destination after {controller.Steps} callbacks; source unchanged.");
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
        int afterActor = 0;
        for (int step = 0; step < 512 && controller.Running && afterActor < 16; step++)
        {
            controller.Advance(1);
            inheritedFinished |= policy.State.NavigationGoal != inherited;
            retriedRefresh |= inheritedFinished && policy.State.NavigationGoal is { EventId: null } goal && goal.NavigationKey.StartsWith(visit + ":", StringComparison.Ordinal);
            bool actorActive = policy.State.Active is { Kind: Ff7AutoPlayTargetKind.ActorTalk, Entity: 6 };
            actorAttempted |= actorActive;
            actorFinished |= actorAttempted && !actorActive;
            if (actorFinished)
            {
                afterActor++;
            }
        }
        Check(controller.Failure is null && inheritedFinished && !retriedRefresh && actorFinished && afterActor == 16, $"Duplicate current-field refresh still starved native execution: {controller.Error}");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Current-field refresh validation changed its original standalone.");
        Console.WriteLine($"PASS original inherited arrival and native actor attempt finish without restarting same-field refresh after {controller.Steps} callbacks; source unchanged.");
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

    private static void CheckCachedDeferralKeepsSourceEvidence()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        planner.Observe(new("visit", "program"), [new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "entry")]);
        state.NavigationGuards["guard"] = new("visit", 1, EventId: "actor", NavigationKey: "source");
        planner.MarkUnavailable("visit", "actor", new("entry", "visit", "guard", "blocked", "actor"));
        planner.RefreshUnavailable("visit", new Dictionary<string, string> { ["guard"] = "admitted" });
        Ff7AutoPlayActiveEvent active = new("visit", "actor", "entry", 1, 1, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "before", NavigationKey: "source", EntryGuardFingerprints: new Dictionary<string, string> { ["guard"] = "admitted" });
        state.Active = active;
        Check(state.Clone().Active!.EntryGuardFingerprints!["guard"] == "admitted", "Cold state lost its admitted source evidence.");
        planner.RefreshUnavailable("visit", new Dictionary<string, string> { ["guard"] = "blocked" });
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [new("actor", 1, 1, Ff7AutoPlayTargetKind.ActorTalk, "entry", false, default)], true, true);
        Check(Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation), "The known native failure did not end the unaccepted approach.");
        var item = state.Coverage.Visits.Single().Events.Single();
        Check(item.UnavailableAttempts.Count == 1 && !item.Completed && item.Conditions?.HasExecutedEntry("entry") != true, "A failed admission was lost or credited as native execution.");
        planner.RefreshUnavailable("visit", new Dictionary<string, string> { ["guard"] = "admitted" });
        Check(planner.Plan("visit")?.Event.Id == "actor", "A cached refusal fabricated a new failure at the previously admitted source.");
        planner.RefreshUnavailable("visit", new Dictionary<string, string> { ["guard"] = "genuinely-changed" });
        Check(planner.Plan("visit")?.Event.Id == "actor", "A genuine new source contract lost its next attempt.");
    }

    private static void CheckOriginalFailedAdmission()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FAILED_ADMISSION_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the bounded source-event admission."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool reached = false;
        for (int step = 0; step < 1200 && controller.Running && !reached; step++)
        {
            controller.Advance(1);
            reached = session.MapId == 536 && policy.State.Active is { Kind: Ff7AutoPlayTargetKind.ActorTalk, Entity: 5, Accepted: true };
        }
        Check(controller.Failure is null && reached, $"Unchanged failed approaches still starved the real changed source event: {controller.Error}");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Failed admission validation changed its original standalone.");
        Console.WriteLine($"PASS original repeated-approach source reaches its changed NPC frontier after {controller.Steps} ordinary callbacks; source unchanged.");
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
