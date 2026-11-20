using System.Diagnostics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class OwnedTravelContinuationTests
{
    internal static void Run()
    {
        CheckSourceOnlyContinuation();
        CheckActorRefresh();
        Ff7AutoPlayState state = new();
        AutoPlayEvent goalEvent = new("goal", AutoPlayEventKind.Interaction, ConditionSignature: "condition");
        AutoPlayEvent rival = new("rival", AutoPlayEventKind.Interaction, ConditionSignature: "fresh");
        AutoPlayEvent transport = new("edge", AutoPlayEventKind.Transition, ConditionSignature: "edge-condition");
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage, candidateProgressionOrder: (_, target, _) => target.Id == rival.Id ? -1 : 0);
        planner.Observe(new("source", "program"), [transport, rival]);
        planner.Observe(new("destination", "other"), [goalEvent]);
        planner.ObserveNavigation("source", "source-nav", [transport, rival]);
        planner.ObserveNavigation("destination", "goal-nav", [goalEvent]);
        planner.Completed("source", transport.Id, "destination", observedStoryChanged: false, entrySignature: transport.ConditionSignature, entrySignatureAuthoritative: true, targetId: transport.Id);
        AutoPlayEventRoute route = state.Coverage.Visits[0].Events.Single(item => item.Event.Id == transport.Id).Routes.Single();
        planner.RecordNavigationRoute("source-nav", "goal-nav", transport.Id, route);
        AutoPlayTravelGoal goal = new("goal-nav", goalEvent.Id, goalEvent.ConditionSignature);
        Check(planner.PlanNavigation("source-nav", preferredGoal: goal)?.Event.Id == rival.Id, "Default soft preference stopped yielding to a fresh frontier.");
        string before = JsonSerializer.Serialize(state);
        Check(planner.PlanNavigation("source-nav", preferredGoal: goal, strongPreferredGoal: true) is { Event.Id: "edge", Goal: var selected } && selected == goal, "The explicit owned continuation did not retain the eligible route goal.");
        Check(before == JsonSerializer.Serialize(state), "Route preference fabricated progress.");
        Check(planner.PlanNavigation("source-nav", preferredGoal: goal, strongPreferredGoal: true, readAvailability: (_, target) => new(target.Id == goalEvent.Id, [], null))?.Event.Id == rival.Id, "Owned preference bypassed actual goal unavailability.");
        Ff7AutoPlayObservation observation = new("source", "program", 1, [], true, true) { NavigationKey = "source-nav" };
        state.NavigationGoal = goal;
        Ff7AutoPlayActiveEvent arrival = new("source", transport.Id, transport.ConditionSignature!, 1, 7, 4, Ff7AutoPlayTargetKind.LineTouch, null, 1, "before", Accepted: true, TransitionOwned: true, NavigationKey: "source-nav", TravelRoute: route);
        Ff7AutoPlayObservation arrived = observation with { VisitId = "destination", NavigationKey = "goal-nav", Generation = 2 };
        Check(Ff7AutoPlayPolicy.IsOwnedTravelArrival(state, arrival, arrived, true), "A real matching owned travel receipt could not seed continuation.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTravelArrival(state, arrival with { Accepted = false, TransitionOwned = false }, arrived, true), "A merely planned route fabricated completed travel ownership.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTravelArrival(state, arrival, arrived with { NavigationKey = "unexpected" }, true), "An unconfirmed arrival component seeded owned continuation.");
        Check(!Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation), "A legacy soft goal invented owned priority.");
        state.TravelCommitment = new(goal, "source-nav", null);
        Check(Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation) && Ff7AutoPlayPolicy.CanContinueOwnedTravel(state.Clone(), observation), "An optional owned commitment failed to survive capture/restore.");
        state.DestinationField = 99;
        Check(!Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation), "Explicit user intent failed to release the old commitment.");
        state.DestinationField = null;
        Check(!Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation with { NavigationKey = "changed" }), "A changed topology retained the old commitment.");
        state.Coverage.Visits[1].Events[0].Conditions!.ConsumedEntries.Add(goal.Conditions!);
        Check(!Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation), "An already consumed current goal retained owned frontier priority.");
        state.Coverage.Visits[1].Events[0].Conditions!.ConsumedEntries.Clear();
        state.Coverage.NavigationVisits[1] = state.Coverage.NavigationVisits[1] with { Events = [goalEvent with { ConditionSignature = "changed" }] };
        Check(!Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation), "An obsolete goal condition retained owned priority.");
        Check(JsonSerializer.Deserialize<Ff7AutoPlayState>("{}")!.TravelCommitment is null, "An old save without optional planning metadata was rejected.");
        Ff7AutoPlayState malformed = JsonSerializer.Deserialize<Ff7AutoPlayState>("{\"TravelCommitment\":{\"Goal\":null}}")!;
        Check(!Ff7AutoPlayPolicy.CanContinueOwnedTravel(malformed, observation), "An incomplete optional marker invented an owned goal.");
        Console.WriteLine("PASS scoped owned travel preference, native availability, source/goal release and old-save metadata compatibility");
    }

    private static void CheckSourceOnlyContinuation()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent arrival = new("arrive", AutoPlayEventKind.Interaction, ConditionSignature: "arrival");
        AutoPlayEvent exit = new("exit", AutoPlayEventKind.Interaction, ConditionSignature: "exit");
        AutoPlayEvent wrong = new("wrong", AutoPlayEventKind.Interaction, ConditionSignature: "wrong");
        AutoPlayEvent fresh = new("fresh", AutoPlayEventKind.Interaction, ConditionSignature: "fresh");
        planner.Observe(new("origin", "program"), [arrival]);
        planner.Observe(new("middle", "program"), [exit, wrong]);
        planner.Observe(new("world", "program"), []);
        planner.Observe(new("competing", "program"), [fresh]);
        planner.ObserveNavigation("origin", "origin-nav", [arrival]);
        planner.ObserveNavigation("middle", "middle-nav", [exit, wrong]);
        planner.ObserveNavigation("world", "world-nav", []);
        planner.ObserveNavigation("competing", "competing-nav", [fresh]);
        AutoPlayEventRoute Link(string source, string sourceNav, string destination, string destinationNav, AutoPlayEvent target)
        {
            planner.Completed(source, target.Id, destination, observedStoryChanged: false, entrySignature: target.ConditionSignature, entrySignatureAuthoritative: true, targetId: target.Id);
            var route = state.Coverage.Visits.Single(visit => visit.Visit.Key == source).Events.Single(item => item.Event.Id == target.Id).Routes.Single();
            planner.RecordNavigationRoute(sourceNav, destinationNav, target.Id, route);
            return route;
        }
        var route = Link("origin", "origin-nav", "middle", "middle-nav", arrival);
        Link("middle", "middle-nav", "world", "world-nav", exit);
        Link("middle", "middle-nav", "competing", "competing-nav", wrong);
        state.NavigationGoal = new("world-nav");
        bool NeedsRefresh(AutoPlayVisit visit) => visit.Key == "world-nav";
        AutoPlayPlanStep? Plan(bool strong, bool blocked = false) => planner.PlanNavigation("middle-nav", needsRefresh: NeedsRefresh, preferredGoal: state.NavigationGoal, strongPreferredGoal: strong, readAvailability: (_, target) => blocked && target.Id == exit.Id ? new(true, new HashSet<string> { exit.Id }, "blocked") : null);
        Check(Plan(false)?.Goal?.EventId == fresh.Id, "An unowned source-only goal unexpectedly preempted a fresh frontier.");
        Ff7AutoPlayActiveEvent active = new("origin", arrival.Id, arrival.ConditionSignature!, 1, 7, 4, Ff7AutoPlayTargetKind.LineTouch, null, 641, "before", Accepted: true, TransitionOwned: true, NavigationKey: "origin-nav", TravelRoute: route);
        Ff7AutoPlayObservation observation = new("middle", "program", 2, [], true, true) { NavigationKey = "middle-nav" };
        Check(!Ff7AutoPlayPolicy.IsOwnedTravelArrival(state, active, observation, true) && Ff7AutoPlayPolicy.IsOwnedTravelArrival(state, active, observation, true, sourceGoalPending: true), "A source-only commitment was invented without current pending proof or lost a real native arrival.");
        Check(!Ff7AutoPlayPolicy.IsOwnedTravelArrival(state, active with { Accepted = false, TransitionOwned = false }, observation, true, sourceGoalPending: true), "An unaccepted leg acquired source-only owned priority.");
        state.TravelCommitment = new(state.NavigationGoal, observation.NavigationKey, null);
        Check(Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation, sourceGoalPending: true) && !Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation), "A completed/unknown source inherited an owned refresh continuation.");
        AutoPlayPlanStep? continuation = Plan(true);
        Check(continuation is { Event.Id: "exit", Goal.EventId: null, Goal.NavigationKey: "world-nav" } && Ff7AutoPlayPolicy.MatchesOwnedTravelPlan(state.NavigationGoal, continuation, observation.NavigationKey), "Fresh remote work preempted the authenticated pending source visit.");
        Check(!Ff7AutoPlayPolicy.MatchesOwnedTravelPlan(state.NavigationGoal, Plan(true, blocked: true), observation.NavigationKey), "A physically unavailable source route bypassed ordinary eligibility.");
        Check(!Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation with { NavigationKey = "world-nav" }, sourceGoalPending: true), "Actual source arrival retained an already completed continuation.");
        state.NavigationGoal = new("different");
        Check(!Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation, sourceGoalPending: true), "Changed user/goal intent retained an old source-only commitment.");
    }

    private static void CheckActorRefresh()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        Ff7AutoPlayTarget target = new("actor:11:script:1", 11, 1, Ff7AutoPlayTargetKind.ActorTalk, "current", false, default);
        AutoPlayEvent native = new(target.Id, AutoPlayEventKind.Interaction, ConditionSignature: target.ConditionFingerprint);
        planner.Observe(new("native-program", "program"), [native]);
        planner.ObserveNavigation("native-program", "component", [native]);
        var coverage = state.Coverage.Visits[0].Events[0];
        coverage.Completed = true;
        coverage.Conditions!.ConsumedEntries.Add("historical");
        state.NavigationGoal = new("component", target.Id, "historical");
        state.TravelCommitment = new(state.NavigationGoal, "component", null);
        Ff7AutoPlayObservation observation = new("native-program", "program", 1, [target], true, true) { NavigationKey = "component" };
        Ff7Actor actor = new() { Slot = 3, Entity = 11, Model = 3, TalkEnabled = true };
        AutoPlayEventAvailability available = new(false, [], "physical");
        string before = JsonSerializer.Serialize(state);
        Check(Ff7AutoPlayPolicy.CanRefreshOwnedActorTarget(state, observation, target, actor, available) && before == JsonSerializer.Serialize(state), "Fresh current actor facts were rejected or fabricated historical execution.");
        Check(!Ff7AutoPlayPolicy.CanRefreshOwnedActorTarget(state, observation, target, actor with { PlayerControlled = true }, available) && !Ff7AutoPlayPolicy.CanRefreshOwnedActorTarget(state, observation, target, actor with { EntityBound = false }, available), "A changed native role inherited the owned actor intent.");
        Check(!Ff7AutoPlayPolicy.CanRefreshOwnedActorTarget(state, observation with { VisitId = "different-program" }, target, actor, available) && !Ff7AutoPlayPolicy.CanRefreshOwnedActorTarget(state, observation with { NavigationKey = "different-component" }, target, actor, available), "A different native program/topology inherited target refresh.");
        Check(!Ff7AutoPlayPolicy.CanRefreshOwnedActorTarget(state, observation, target, actor, available with { Unavailable = true }) && !Ff7AutoPlayPolicy.CanRefreshOwnedActorTarget(state, observation, target with { Kind = Ff7AutoPlayTargetKind.LineTouch }, actor, available), "An unavailable or nonactor target inherited fresh interaction intent.");
        coverage.Conditions.ConsumedEntries.Add("current");
        Check(!Ff7AutoPlayPolicy.CanRefreshOwnedActorTarget(state, observation, target, actor, available), "A consumed current condition was refreshed into a new frontier.");
    }

    internal static void RunOriginal()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_OWNED_TRAVEL_SAVE") ?? throw new InvalidOperationException("Select an external completed native travel fixture.");
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream source = new(original, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(source).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A real native choice checkpoint was reached."));
        AutoPlayTravelGoal goal = policy.State.NavigationGoal ?? throw new InvalidOperationException("Missing native travel goal.");
        int targetMap = int.Parse(goal.NavigationKey.Split(':')[1]);
        int entity = int.Parse(goal.EventId!.Split(':')[1]);
        int script = int.Parse(goal.EventId.Split(':')[3]);
        bool OldConsumed() => policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == goal.NavigationKey.Split(":nav:")[0]).Events.Single(item => item.Event.Id == goal.EventId).Conditions?.HasExecutedEntry(goal.Conditions!) == true;
        bool oldConsumed = OldConsumed();
        bool admitted = false;
        bool changedCondition = false;
        AutoPlayTravelGoal? arrivalGoal = null;
        int arrivalTriangle = -1;
        string? arrivalVariables = null;
        string? arrivalHost = null;
        uint? arrivalGil = null;
        bool? arrivalMusic = null;
        int refreshes = 0;
        Ff7AutoPlayDiscovery discovery = new(session);
        session.NativeInteractionScriptRequested += actual =>
        {
            if (session.MapId == targetMap && actual.Entity == entity && actual.Script == script && actual.Accepted && actual.Started)
            {
                Check(policy.State.NavigationGoal is { } actualGoal && actualGoal.EventId == goal.EventId && actualGoal.Conditions == new Ff7AutoPlayDiscovery(session).Observe().Events.Single(item => item.Id == goal.EventId).ConditionFingerprint, "The actual goal admission lost its refreshed owned intent.");
                admitted = true;
            }
        };
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput input = policy.ChooseInput(driver);
            if (session.MapId == targetMap && discovery.Observe() is { CanAcceptInput: true } observation)
            {
                Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(item => item.Id == goal.EventId);
                var dependencies = session.Machine.AnalyzeEntryDependencies(entity, script);
                string variables = session.Machine.ReadScriptAnalysisFingerprint(entity, script, dependencies.Reads);
                string host = Ff7AutoPlayDiscovery.ReadHostConditions(dependencies.HostReads, session.Progress, session.Host.Process, session.Host.ReadNativeMusicPlaying).Fingerprint;
                var player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                if (arrivalGoal is null)
                {
                    arrivalGoal = policy.State.NavigationGoal;
                    arrivalTriangle = player.Triangle;
                    arrivalVariables = variables;
                    arrivalHost = host;
                    arrivalGil = session.Progress.Gil;
                    arrivalMusic = session.Host.ReadNativeMusicPlaying();
                    Console.WriteLine($"ARRIVAL current={target?.ConditionFingerprint}, planned={arrivalGoal}, player={player.Entity}/triangle{player.Triangle}, hostReads=[{string.Join(';', dependencies.HostReads)}], variables={variables}, host={host}");
                    Check(arrivalGoal?.EventId == goal.EventId && arrivalGoal.Conditions == target?.ConditionFingerprint, "The authenticated arrival did not refresh the planned target to current native facts.");
                }
                changedCondition = target?.ConditionFingerprint != policy.State.NavigationGoal?.Conditions;
                if (target?.ConditionFingerprint != arrivalGoal!.Conditions && refreshes++ == 0)
                {
                    Console.WriteLine($"REFRESH approach condition: arrival={arrivalGoal.Conditions}, current={target?.ConditionFingerprint}, player={player.Entity}/triangle{arrivalTriangle}->{player.Triangle}, variables={arrivalVariables}->{variables}, host={arrivalHost}->{host}, gil={arrivalGil}->{session.Progress.Gil}, music={arrivalMusic}->{session.Host.ReadNativeMusicPlaying()}, goal={policy.State.NavigationGoal}, navigation={observation.NavigationKey}");
                }
            }
            return input;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        for (int slice = 0; slice < 2048 && controller.Running && !admitted && !changedCondition && session.Battle is null && elapsed.Elapsed < TimeSpan.FromSeconds(30); slice++)
        {
            controller.Advance(1);
        }
        Check(original.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Owned native travel changed its standalone source.");
        Check(oldConsumed == OldConsumed(), "Fresh native target admission credited the historical source condition.");
        Check(!admitted || policy.State.Active is { Accepted: true, Entity: var actualEntity, Script: var actualScript } && actualEntity == entity && actualScript == script && policy.State.TravelCommitment is null, "The real target did not acquire ordinary native ownership and release its planning commitment.");
        Console.WriteLine($"Owned travel boundary: map={session.MapId}, tick={session.Host.Process.Tick}, callbacks={controller.Steps}, admitted={admitted}, dialogues={session.Host.Process.Dialogues.Count(dialogue => !dialogue.Completed)}, changedCondition={changedCondition}, battle={session.Battle is not null}, goal={policy.State.NavigationGoal}, error={controller.Error}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source unchanged");
        Check(controller.Error is null && admitted && !changedCondition, "The actual owned multi-leg travel did not reach its current-condition native goal admission.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
