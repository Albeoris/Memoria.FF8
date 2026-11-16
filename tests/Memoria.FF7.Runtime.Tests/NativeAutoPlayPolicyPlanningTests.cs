using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayPolicyPlanningTests
{
    public static void Run()
    {
        NativeComponentProbeTests.Run();
        AutoPlayNavigationRefreshTests.Run();
        CheckDerivedConnectionProbePriority();
        CheckExactNavigationGoalCompletion();
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayVisit visit = new("current", "region");
        planner.Observe(visit, [new("repeatable", AutoPlayEventKind.Interaction, ConditionSignature: "before"), new("unseen", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "ready")]);
        planner.Completed(visit.Key, "repeatable", entrySignature: "before", entrySignatureAuthoritative: true);
        planner.Observe(visit, [new("repeatable", AutoPlayEventKind.Interaction, ConditionSignature: "changed"), new("unseen", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "ready")]);
        Check(planner.Plan(visit.Key)?.Event.Id == "unseen", "A changed repeatable event must not starve an unexecuted frontier.");
        planner.Completed(visit.Key, "unseen", entrySignature: "ready", entrySignatureAuthoritative: true);
        Check(planner.Plan(visit.Key)?.Event.Id == "repeatable", "A changed predicate must remain eligible after the unexecuted frontier is covered.");
        Ff7AutoPlayState restored = state.Clone();
        Check(Ff7AutoPlayPolicy.CreatePlanner(restored).Plan(visit.Key)?.Event.Id == "repeatable", "Cold planner restoration lost the changed predicate.");
        restored.Coverage.Visits.Single().Events.Single(entry => entry.Event.Id == "unseen").RouteNeedsRediscovery = true;
        Check(Ff7AutoPlayPolicy.CreatePlanner(restored).Plan(visit.Key)?.Event.Id == "unseen", "A consumed transport awaiting native connectivity evidence must precede a changed repeatable event.");
        Ff7AutoPlayState fair = new();
        var fairPlanner = Ff7AutoPlayPolicy.CreatePlanner(fair);
        AutoPlayEvent[] repeated = [new("first", AutoPlayEventKind.Interaction, ConditionSignature: "one"), new("second", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "one")];
        fairPlanner.Observe(visit, repeated);
        fairPlanner.Completed(visit.Key, "first", entrySignature: "one", entrySignatureAuthoritative: true);
        fairPlanner.Completed(visit.Key, "first", entrySignature: "two", entrySignatureAuthoritative: true);
        fairPlanner.Completed(visit.Key, "second", entrySignature: "one", entrySignatureAuthoritative: true);
        fairPlanner.Observe(visit, repeated.Select(item => item with { ConditionSignature = "three" }).ToArray());
        Check(fairPlanner.Plan(visit.Key)?.Event.Id == "second", "Repeated changing predicates on an earlier event must not starve a less explored native frontier.");
        CheckComponentProbeScope();
        CheckEntryAvailability();
        CheckStoryFrontierPrecedesRefresh();
        CheckCompletedStoryDoesNotStarvePartyFrontier();
        CheckMovementFailureScope();
        CheckMultipleMovementFailureRegions();
        CheckFailedApproachDoesNotStarveFreshGateway();
    }

    private static void CheckDerivedConnectionProbePriority()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent exit = new("exit", AutoPlayEventKind.Transition, ConditionSignature: "entry");
        AutoPlayEvent repeat = new("repeat", AutoPlayEventKind.Interaction, ConditionSignature: "before");
        planner.Observe(new("source", "region"), [exit, repeat]);
        planner.ObserveNavigation("source", "old-source-nav", [exit, repeat]);
        planner.Observe(new("destination", "region"), []);
        planner.ObserveNavigation("destination", "destination-nav", []);
        planner.Completed("source", exit.Id, "destination", observedStoryChanged: false, entrySignature: "entry", entrySignatureAuthoritative: true);
        AutoPlayEventRoute route = state.Coverage.Visits.Single(visit => visit.Visit.Key == "source").Events.Single(item => item.Event.Id == exit.Id).Routes.Single();
        planner.RecordNavigationRoute("old-source-nav", "destination-nav", exit.Id, route);
        planner.Completed("source", repeat.Id, entrySignature: "before", entrySignatureAuthoritative: true);
        repeat = repeat with { ConditionSignature = "after" };
        planner.Observe(new("source", "region"), [exit, repeat]);
        planner.ObserveNavigation("source", "new-source-nav", [exit, repeat]);
        AutoPlayTravelGoal preferred = new("new-source-nav", repeat.Id, repeat.ConditionSignature);
        AutoPlayPlanStep probe = planner.PlanNavigation("new-source-nav", preferredGoal: preferred)!;
        Check(probe.Event.Id == exit.Id && probe.IsTravel, "A derived native connectivity probe must interrupt a completed changed-predicate goal.");
        Check(!state.Coverage.Visits.Single(visit => visit.Visit.Key == "source").Events.Single(item => item.Event.Id == exit.Id).RouteNeedsRediscovery, "A scoped native probe must not alter aggregate progress metadata.");

        AutoPlayEvent retry = new("retry", AutoPlayEventKind.Interaction, ConditionSignature: "retry");
        planner.Observe(new("source", "region"), [exit, repeat, retry]);
        planner.ObserveNavigation("source", "new-source-nav", [exit, repeat, retry]);
        planner.MarkUnavailable("source", retry.Id, new("retry", "source", "physical", "blocked", retry.Id));
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["physical"] = "changed" });
        Check(planner.PlanNavigation("new-source-nav", preferredGoal: preferred)?.Event.Id == retry.Id, "An eligible rank-zero retry must retain its aggregate attempt history and precede a connectivity-only probe.");
        AutoPlayEvent fresh = new("fresh", AutoPlayEventKind.Interaction, ConditionSignature: "fresh");
        planner.Observe(new("source", "region"), [exit, repeat, retry, fresh]);
        planner.ObserveNavigation("source", "new-source-nav", [exit, repeat, retry, fresh]);
        Check(planner.PlanNavigation("new-source-nav", preferredGoal: preferred)?.Event.Id == fresh.Id, "A never-attempted frontier must retain its priority ahead of rank-zero retries and connectivity probes.");
    }

    private static void CheckFailedApproachDoesNotStarveFreshGateway()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent contact = new("contact", AutoPlayEventKind.Interaction, ConditionSignature: "entry");
        AutoPlayEvent gateway = new("gateway", AutoPlayEventKind.Transition, Order: 1, ConditionSignature: "gateway-entry");
        AutoPlayVisit visit = new("field", "resource");
        planner.Observe(visit, [contact, gateway]);
        planner.ObserveNavigation(visit.Key, "component", [contact, gateway]);
        planner.MarkUnavailable(visit.Key, contact.Id, new("entry", visit.Key, "guard", "blocked", contact.Id));
        planner.RefreshUnavailable(visit.Key, new Dictionary<string, string> { ["guard"] = "outside-region" });
        AutoPlayTravelGoal contactGoal = new("component", contact.Id, "entry");
        Check(planner.PlanNavigation("component", preferredGoal: contactGoal)?.Event.Id == gateway.Id, "A rearmed failed contact and its retained travel goal must yield to an untouched gateway.");
        planner.Completed(visit.Key, gateway.Id, entrySignature: "gateway-entry", entrySignatureAuthoritative: true);
        Check(planner.PlanNavigation("component", preferredGoal: contactGoal)?.Event.Id == contact.Id, "A rearmed failed contact must remain eligible after fresh frontiers are exhausted.");
        AutoPlayEvent changed = contact with { ConditionSignature = "new-entry" };
        AutoPlayEvent fresh = new("fresh", AutoPlayEventKind.Interaction, Order: 2, ConditionSignature: "fresh-entry");
        planner.Observe(visit, [changed, gateway, fresh]);
        planner.ObserveNavigation(visit.Key, "component", [changed, gateway, fresh]);
        planner.RefreshUnavailable(visit.Key, new Dictionary<string, string> { ["guard"] = "outside-region" });
        Check(planner.PlanNavigation("component")?.Event.Id == contact.Id, "A genuinely new entry condition must regain untouched-frontier priority.");
        AutoPlayEvent story = new("story", AutoPlayEventKind.Interaction, ChangesStory: true, Order: 3, ConditionSignature: "story-entry");
        planner.Observe(visit, [changed, gateway, fresh, story]);
        planner.ObserveNavigation(visit.Key, "component", [changed, gateway, fresh, story]);
        Check(planner.PlanNavigation("component", preferredGoal: new("component", contact.Id, "new-entry"))?.Event.Id == story.Id, "An untouched native story frontier must retain priority over other untouched events and retained travel goals.");
        planner.Completed(visit.Key, contact.Id, entrySignature: "new-entry", entrySignatureAuthoritative: true);
        planner.Completed(visit.Key, fresh.Id, entrySignature: "fresh-entry", entrySignatureAuthoritative: true);
        planner.Completed(visit.Key, story.Id, entrySignature: "story-entry", entrySignatureAuthoritative: true);
        state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == gateway.Id).RouteNeedsRediscovery = true;
        Check(planner.PlanNavigation("component")?.Event.Id == gateway.Id, "Exhausting untouched frontiers must preserve a known transport's pending navigation probe.");
    }

    private static void CheckMultipleMovementFailureRegions()
    {
        foreach (string kind in new[] { "physical", "movement-cycle" })
        {
            Ff7AutoPlayState state = new();
            AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
            planner.Observe(new("visit", "resource"), [new("blocked", AutoPlayEventKind.Interaction, ConditionSignature: "entry"), new("exit", AutoPlayEventKind.Transition, ConditionSignature: "exit-entry")]);
            Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, X4096 = 100, Y4096 = 100, Z4096 = 100 };
            Ff7Actor obstacle = new() { Slot = 1, Entity = 1, Model = 1, CollisionEnabled = true, X4096 = 500 };
            Ff7AutoPlayMovementFailure first = new(0, 200, 0, 200, 0, 200);
            Ff7AutoPlayMovementFailure second = first with { MinZ = 300, MaxZ = 500 };
            Record(first, player);
            Refresh(state, planner, player, obstacle);
            Check(planner.Plan("visit")?.Event.Id == "exit", "A local failed contact must yield to the unconsumed exit.");
            Ff7Actor upperPlayer = player with { Z4096 = 400 };
            Refresh(state, planner, upperPlayer, obstacle);
            Check(!state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == "blocked").Unavailable, "Leaving the first failed region must permit a new local approach.");
            Ff7AutoPlayActiveEvent active = new("visit", "blocked", "selected-before-scratch-change", 1, 2, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "before");
            Ff7AutoPlayObservation observation = new("visit", "native", 1, [new("blocked", 2, 1, Ff7AutoPlayTargetKind.ActorTalk, "entry", false, default)], true, true);
            state.Active = active;
            Check(!Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation) && state.Active == active, "An eligible outside-region approach must retain its active target.");
            Refresh(state, planner, player, obstacle);
            state.Active = active with { Accepted = true };
            Check(!Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation) && state.Active is { Accepted: true }, "A refreshed local guard must preserve accepted native script callbacks.");
            state.Active = active;
            Check(!Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation with { Generation = 2 }), "A local guard must not cancel another field generation's active target.");
            Check(!Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation with { VisitId = "other-visit" }), "A local guard must not cancel another visit's active target.");
            Check(!Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation with { Events = [observation.Events[0] with { ConditionFingerprint = "unobserved-entry" }] }), "A stale coverage signature must not cancel an active target under another current predicate.");
            Check(Ff7AutoPlayPolicy.DeferUnavailableActiveNavigation(state, observation) && state.Active is null && planner.Plan("visit")?.Event.Id == "exit", "Reentering the exact refreshed failed variant must cancel its unaccepted approach and expose the exit even if its original selection signature differs.");
            Record(second, upperPlayer);
            Refresh(state, planner, upperPlayer, obstacle);
            Check(planner.Plan("visit")?.Event.Id == "exit", "A second local failure must yield to the exit.");
            Refresh(state, planner, player, obstacle);
            Check(planner.Plan("visit")?.Event.Id == "exit", "A second region must not overwrite the original failure and reopen the futile first approach.");
            Ff7AutoPlayState cold = state.Clone();
            AutoPlayPlanner<Ff7AutoPlayDeferral> coldPlanner = Ff7AutoPlayPolicy.CreatePlanner(cold);
            Refresh(cold, coldPlanner, player, obstacle);
            Check(coldPlanner.Plan("visit")?.Event.Id == "exit", "Cold restoration must retain both local failure regions.");
            Refresh(state, planner, player with { Z4096 = 600 }, obstacle);
            Check(!state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == "blocked").Unavailable, "Outside both failed regions the contact must remain eligible for another approach.");
            Refresh(state, planner, player, obstacle with { CollisionEnabled = false });
            Check(!state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == "blocked").Unavailable, "Changed relevant collider admission must rearm a remembered region.");

            void Record(Ff7AutoPlayMovementFailure region, Ff7Actor source)
            {
                string key = Ff7AutoPlayPolicy.ReadMovementFailureGuardKey("navigation", "blocked", kind, region, [obstacle.Entity]);
                state.NavigationGuards[key] = new("visit", 2, [obstacle.Entity], "blocked", "navigation", 0, region);
                string fingerprint = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", region, [source, obstacle], [obstacle.Entity]);
                planner.MarkUnavailable("visit", "blocked", new("entry", "visit", key, fingerprint, "blocked"));
            }
        }

        static void Refresh(Ff7AutoPlayState state, AutoPlayPlanner<Ff7AutoPlayDeferral> planner, Ff7Actor player, Ff7Actor obstacle)
        {
            Dictionary<string, string> fingerprints = state.NavigationGuards.ToDictionary(entry => entry.Key, entry => Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", entry.Value.MovementFailure, [player, obstacle], entry.Value.BlockingEntities));
            planner.RefreshUnavailable("visit", fingerprints);
        }
    }

    private static void CheckMovementFailureScope()
    {
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, X4096 = 100, Y4096 = 100 };
        Ff7Actor obstacle = new() { Slot = 1, Entity = 1, Model = 1, CollisionEnabled = true, X4096 = 500 };
        Ff7AutoPlayMovementFailure bounds = new(0, 200, 0, 200);
        string blocked = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", bounds, [player, obstacle]);
        Check(blocked == Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", bounds, [player with { X4096 = 110, Direction = 128 }, obstacle]), "Small movement or facing changes inside a failed search region must not replay its cycle.");
        string departed = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", bounds, [player with { X4096 = 201 }, obstacle]);
        Check(blocked != departed, "A meaningful departure must refresh the local cycle deferral.");
        Check(blocked != Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", bounds, [player, obstacle with { X4096 = 501 }]), "Changed native occupancy must refresh the local cycle deferral.");
        Ff7AutoPlayMovementFailure layer = bounds with { MinZ = player.Z4096 - 10, MaxZ = player.Z4096 + 10 };
        string sameLayer = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", layer, [player, obstacle]);
        Check(sameLayer != Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", layer, [player with { Z4096 = player.Z4096 + 11 }, obstacle]), "A different native floor must reopen a local approach even when XY remains inside its bounds.");
        Ff7AutoPlayMovementFailure legacy = System.Text.Json.JsonSerializer.Deserialize<Ff7AutoPlayMovementFailure>("{\"MinX\":0,\"MaxX\":200,\"MinY\":0,\"MaxY\":200}")!;
        Check(legacy.MinZ is null && legacy.MaxZ is null && blocked == Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", legacy, [player, obstacle]), "A legacy XY-only approach guard must preserve its previous scope without requiring migration.");
        Ff7Actor ambient = obstacle with { Slot = 2, Entity = 2, X4096 = 1000 };
        string relevant = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", layer, [player, obstacle, ambient], [obstacle.Entity]);
        Check(relevant == Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", layer, [player, obstacle, ambient with { X4096 = 2000 }], [obstacle.Entity]), "Unrelated ambient movement must not reopen a portal blocked by a separately proved collider.");
        Check(relevant != Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("native", layer, [player, obstacle with { CollisionEnabled = false }, ambient], [obstacle.Entity]), "Disabling the relevant collider must reopen its local approach.");
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new("visit", "resource"), [new("exit", AutoPlayEventKind.Transition, ConditionSignature: "entry")]);
        planner.BeginEvent("visit", "exit");
        planner.MarkUnavailable("visit", "exit", new("entry", "visit", "cycle", blocked, "exit"));
        planner.RefreshUnavailable("visit", new Dictionary<string, string> { ["cycle"] = blocked });
        Check(planner.Plan("visit") is null, "The unchanged failed region must remain deferred.");
        planner.RefreshUnavailable("visit", new Dictionary<string, string> { ["cycle"] = departed });
        Check(planner.Plan("visit")?.Event.Id == "exit", "A local search failure must not consume execution after departure.");
        Ff7AutoPlayState older = new();
        var olderPlanner = Ff7AutoPlayPolicy.CreatePlanner(older);
        olderPlanner.Observe(new("visit", "resource"), [new("exit", AutoPlayEventKind.Transition, ConditionSignature: "entry")]);
        olderPlanner.Block("visit", "exit", "Physical navigation repeated a bounded native position cycle.", AutoPlayBlockKind.Walking);
        Ff7AutoPlayPolicy.RestoreUnscopedMovementFailures(older);
        Check(olderPlanner.Plan("visit")?.Event.Id == "exit", "Legacy optional search caches without a position witness must permit genuine reobservation.");
    }

    private static void CheckCompletedStoryDoesNotStarvePartyFrontier()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent party = new("party", AutoPlayEventKind.Interaction, ConditionSignature: "party-entry");
        AutoPlayEvent exit = new("exit", AutoPlayEventKind.Transition, ConditionSignature: "exit-entry");
        AutoPlayEvent story = new("story", AutoPlayEventKind.Interaction, ChangesStory: true, ConditionSignature: "previous-heading");
        planner.Observe(new("current", "resource-a"), [party, exit]);
        planner.Observe(new("remote", "resource-b"), [story]);
        planner.Completed("remote", "story", observedStoryChanged: true);
        story = story with { ConditionSignature = "new-heading" };
        planner.Observe(new("remote", "resource-b"), [story]);
        planner.ObserveNavigation("current", "current-component", [party, exit]);
        planner.ObserveNavigation("remote", "remote-component", [story]);
        planner.Completed("current", "exit", "remote", observedStoryChanged: false);
        planner.RecordNavigationRoute("current-component", "remote-component", "exit", state.Coverage.Visits[0].Events.Single(item => item.Event.Id == "exit").Routes.Single());
        Check(planner.PlanNavigation("current-component") is { IsTravel: false, Event.Id: "party" }, "A new raw heading on a completed potential story writer displaced a genuinely uncompleted local party frontier.");
    }

    private static void CheckStoryFrontierPrecedesRefresh()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent story = new("story", AutoPlayEventKind.Interaction, ChangesStory: true, ConditionSignature: "current-entry");
        AutoPlayEvent exit = new("exit", AutoPlayEventKind.Transition, ConditionSignature: "exit-entry");
        planner.Observe(new("current", "resource-a"), [story, exit]);
        planner.Observe(new("remote", "resource-b"), []);
        planner.ObserveNavigation("current", "current-component", [story, exit]);
        planner.ObserveNavigation("remote", "remote-component", []);
        planner.Completed("current", "exit", "remote", observedStoryChanged: false);
        planner.RecordNavigationRoute("current-component", "remote-component", "exit", state.Coverage.Visits[0].Events.Single(item => item.Event.Id == "exit").Routes.Single());
        AutoPlayPlanStep? step = planner.PlanNavigation("current-component", needsRefresh: visit => visit.Key == "remote-component", preferredGoal: new("current-component", "story", "previous-heading-entry"));
        Check(step is { IsTravel: false, Event.Id: "story" }, "A supported unconsumed native story frontier was displaced by remote refresh when its arrival heading changed the observed signature.");
    }

    private static void CheckEntryAvailability()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new("field", "resource"), [new("ladder", AutoPlayEventKind.Interaction, ConditionSignature: "entry")]);
        planner.MarkUnavailable("field", "ladder", new("entry", "field", "physical", "blocked", "ladder"));
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "source-open" });
        var coverage = state.Coverage.Visits.Single().Events.Single();
        string? source = coverage.AvailabilityFingerprint;
        Ff7AutoPlayActiveEvent active = new("field", "ladder", "entry", 1, 1, 4, Ff7AutoPlayTargetKind.LineTouch, null, 0, "stable", EntryAvailabilityFingerprint: source);
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "arrival-open" });
        string? arrival = coverage.AvailabilityFingerprint;
        Ff7AutoPlayObservation observation = new("field", "resource", 1, [], true, true);
        Ff7AutoPlayPolicy.RecordControlReturn(planner, active, observation, false, "entry", false, []);
        Check(source is not null && coverage.CompletedAvailabilityFingerprints.Contains(source), "Native completion lost the source availability under which the ladder was admitted.");
        Check(arrival is not null && !coverage.CompletedAvailabilityFingerprints.Contains(arrival), "Native completion consumed arrival availability as if it were the executed source.");
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "source-open" });
        Check(planner.Plan("field") is null, "Returning to an already consumed source availability reopened the same native ladder.");
        planner.RefreshUnavailable("field", new Dictionary<string, string> { ["physical"] = "changed-open" });
        Check(planner.Plan("field")?.Event.Id == "ladder", "An actually changed physical admission must retain its normal shared-planner retry.");
    }

    private static void CheckComponentProbeScope()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayVisit visit = new("field", "resource");
        planner.Observe(visit, [new("ladder", AutoPlayEventKind.Interaction, ConditionSignature: "current")]);
        planner.Completed(visit.Key, "ladder");
        Ff7AutoPlayTarget target = new("ladder", 1, 4, Ff7AutoPlayTargetKind.LineTouch, "current", false, default, MayChangeNavigation: true, EntryInputContract: "native-entry");
        Ff7AutoPlayObservation lower = new(visit.Key, "resource", 1, [target], true, true) { NavigationKey = "lower" };
        planner.ObserveNavigation(visit.Key, "lower", [state.Coverage.Visits.Single().Events.Single().Event]);
        planner.ObserveNavigation(visit.Key, "upper", [state.Coverage.Visits.Single().Events.Single().Event]);
        Ff7AutoPlayPolicy.ObserveNavigationProbes(state, lower);
        var coverage = state.Coverage.Visits.Single().Events.Single();
        Check(Probe("lower"), "A legacy completion must not invent component connectivity evidence.");
        state.NavigationProbes.Add(Ff7AutoPlayPolicy.NavigationProbeKey(lower.NavigationKey, target));
        Ff7AutoPlayPolicy.ObserveNavigationProbes(state, lower with { NavigationKey = "upper" });
        Check(Probe("upper"), "Inspecting a transport from another floor lost its scoped probe.");
        Ff7AutoPlayPolicy.ObserveNavigationProbes(state, lower);
        Check(!Probe("lower") && Probe("upper") && !coverage.RouteNeedsRediscovery, "An uninspected remote floor left a sticky probe on an already inspected floor or changed aggregate progress.");
        Ff7AutoPlayState cold = state.Clone();
        Ff7AutoPlayPolicy.ObserveNavigationProbes(cold, lower);
        Check(!cold.Coverage.NavigationVisits.Single(visit => visit.Key == "lower").RouteRediscovery[target.Id], "Cold restoration lost the exact component probe receipt.");
        Ff7AutoPlayPolicy.ObserveNavigationProbes(state, lower with { Events = [target with { EntryInputContract = "corrected-native-entry" }] });
        Check(Probe("lower"), "A corrected native admission contract reused an obsolete optional probe receipt.");
        Ff7AutoPlayNavigationGuard legacy = new(visit.Key, 1, EventId: target.Id);
        Ff7AutoPlayNavigationGuard restored = Ff7AutoPlayPolicy.RestorePhysicalGuard(legacy, "lower", 4);
        Check(restored.NavigationKey == "lower" && restored.SourceTriangle == 4, "A revalidated legacy guard did not recover optional component facts from the live native source.");
        Check(Ff7AutoPlayPolicy.RestorePhysicalGuard(restored, "upper", 8) == restored, "Legacy guard restoration silently reassigned a different component's physical evidence.");
        state.NavigationGoal = new("upper", "ladder", "current");
        Check(state.Clone().NavigationGoal == state.NavigationGoal, "Cold restoration lost the shared planner's multi-edge travel goal.");

        bool Probe(string key)
        {
            return state.Coverage.NavigationVisits.Single(visit => visit.Key == key).RouteRediscovery[target.Id];
        }
    }

    private static void CheckExactNavigationGoalCompletion()
    {
        Ff7AutoPlayActiveEvent transport = new("field", "ladder", "gate", 1, 2, 4, Ff7AutoPlayTargetKind.LineTouch, null, 1, "before", NavigationKey: "lower");
        AutoPlayTravelGoal goal = new("upper", "ladder", "gate");
        Check(!Ff7AutoPlayPolicy.IsNavigationGoalCompleted(goal, transport, "upper"), "Arrival using a same-named source transport discharged the unexecuted destination event.");
        Check(Ff7AutoPlayPolicy.IsNavigationGoalCompleted(goal, transport with { NavigationKey = "upper" }, "lower"), "An executed destination goal was lost when its native action changed components.");
        Check(!Ff7AutoPlayPolicy.IsNavigationGoalCompleted(goal, transport with { NavigationKey = "upper", ConditionFingerprint = "other" }, "upper"), "A different native predicate discharged the requested event goal.");
        Check(Ff7AutoPlayPolicy.IsNavigationGoalCompleted(new("upper"), transport, "upper"), "An observation-only arrival goal required an unrelated source event.");
        Check(Ff7AutoPlayPolicy.IsNavigationGoalCompleted(new("upper", "ladder"), transport with { NavigationKey = "upper" }, "lower"), "An unspecified legacy predicate prevented proved source execution from completing its goal.");
        Check(!Ff7AutoPlayPolicy.IsNavigationGoalCompleted(goal, transport with { NavigationKey = null }, "upper"), "A missing legacy source fabricated destination event execution.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
