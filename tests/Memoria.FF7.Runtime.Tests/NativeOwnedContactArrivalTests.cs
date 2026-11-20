using System.Diagnostics;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeOwnedContactArrivalTests
{
    internal static void Run()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        Ff7AutoPlayTarget target = new("actor:10:script:2", 10, 2, Ff7AutoPlayTargetKind.ActorContact, "actual", false, default);
        AutoPlayEvent entry = new(target.Id, AutoPlayEventKind.Interaction, ConditionSignature: target.ConditionFingerprint);
        planner.Observe(new("program", "region"), [entry]);
        planner.ObserveNavigation("program", "navigation", [entry]);
        state.NavigationGoal = new("navigation", target.Id, target.ConditionFingerprint);
        state.TravelCommitment = new(state.NavigationGoal, "navigation", null);
        Ff7AutoPlayObservation observation = new("program", "region", 1, [target], true, true) { NavigationKey = "navigation" };
        AutoPlayEventAvailability available = new(false, [], "physical");
        Ff7AutoPlayActiveEvent active = new("program", target.Id, target.ConditionFingerprint, 1, target.Entity, target.Script, target.Kind, null, 1, "before", NavigationKey: "navigation");
        state.Active = active;
        Check(Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation, active), "An excluded owned local goal failed its zero-candidate proof.");
        Check(!Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation, active with { Accepted = true }) && !Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation, active with { EventId = "other" }) && !Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation with { NavigationKey = "remote" }, active), "Another owner/native context bypassed ordinary alternative scanning.");
        state.NavigationGoal = new("navigation", target.Id, "changed");
        Check(!Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation, active), "A changed goal condition retained a zero-candidate shortcut.");
        state.NavigationGoal = state.TravelCommitment!.Goal;
        AutoPlayEvent forward = new("forward", AutoPlayEventKind.Transition, ConditionSignature: "forward-condition");
        AutoPlayEvent backward = new("backward", AutoPlayEventKind.Transition, ConditionSignature: "backward-condition");
        AutoPlayEvent other = new("other", AutoPlayEventKind.Interaction, ConditionSignature: "other-condition");
        planner.Observe(new("program", "region"), [entry, forward]);
        planner.ObserveNavigation("program", "navigation", [entry, forward]);
        planner.Observe(new("remote", "region"), [backward, other]);
        planner.ObserveNavigation("remote", "remote-nav", [backward, other]);
        planner.Completed("program", forward.Id, "remote", entrySignature: forward.ConditionSignature, entrySignatureAuthoritative: true, targetId: forward.Id);
        planner.RecordNavigationRoute("navigation", "remote-nav", forward.Id, state.Coverage.Visits[0].Events.Single(item => item.Event.Id == forward.Id).Routes.Single());
        planner.Completed("remote", backward.Id, "program", entrySignature: backward.ConditionSignature, entrySignatureAuthoritative: true, targetId: backward.Id);
        planner.RecordNavigationRoute("remote-nav", "navigation", backward.Id, state.Coverage.Visits[1].Events.Single(item => item.Event.Id == backward.Id).Routes.Single());
        var alternative = planner.PlanNavigation("navigation", avoidFirstEvents: new HashSet<string> { target.Id }, preferredGoal: state.NavigationGoal, strongPreferredGoal: true);
        Check(!Ff7AutoPlayPolicy.MatchesOwnedTravelPlan(state.NavigationGoal!, alternative, "navigation"), "A travel cycle recreated an excluded owned local event candidate.");
        var localGoal = state.NavigationGoal;
        var localCommitment = state.TravelCommitment;
        state.NavigationGoal = new("remote-nav", other.Id, other.ConditionSignature);
        state.TravelCommitment = new(state.NavigationGoal, "navigation", null);
        Check(Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation) && !Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation, active), "A valid remote owned goal lost its normal alternative scan.");
        state.NavigationGoal = new("remote-nav");
        state.TravelCommitment = new(state.NavigationGoal, "navigation", null);
        Check(Ff7AutoPlayPolicy.CanContinueOwnedTravel(state, observation, sourceGoalPending: true) && !Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation, active), "A source-only owned goal lost normal alternative scheduling.");
        state.NavigationGoal = localGoal;
        state.TravelCommitment = localCommitment;
        state.DestinationField = 99;
        Check(!Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation, active), "Changed user intent retained an obsolete local exclusion proof.");
        state.DestinationField = null;
        Check(!Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation with { Generation = 2 }, active), "A different native generation reused the old active exclusion proof.");
        Check(Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target, available), "A valid unchanged native contact lost authenticated travel intent.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target with { ConditionFingerprint = "changed" }, available), "Non-talk ownership rebased a changed native condition.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target, available with { Unavailable = true }), "A real unavailable approach retained owned priority.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation with { Events = [] }, target, available), "A missing native target retained owned priority.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation with { NavigationKey = "changed-source" }, target, available), "A changed native arrival context retained old ownership.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target with { AnalysisComplete = false }, available), "Unknown native execution gained owned priority.");
        var commitment = state.TravelCommitment;
        state.TravelCommitment = null;
        Check(!Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(state, observation, active), "A soft local goal suppressed normal fairness.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target, available), "A legacy soft goal invented native arrival ownership.");
        state.TravelCommitment = commitment;
        state.Coverage.Visits[0].Events[0].Conditions!.ConsumedEntries.Add(target.ConditionFingerprint);
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target, available), "An actually consumed target retained owned frontier priority.");
        Console.WriteLine("PASS unchanged owned non-talk continuation, changed/missing/consumed/physical-unavailable release");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_OWNED_CONTACT_SAVE") ?? throw new InvalidOperationException("Select a native owned-contact travel fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream source = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(source).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        var goal = policy.State.NavigationGoal!;
        string? last = null;
        bool preempted = false;
        bool preparedOwnedContact = false;
        string? actualCondition = null;
        long contactWork = 0;
        Ff7AutoPlayController controller = new(session, driver =>
        {
            int map = session.MapId;
            string before = $"{session.FieldGeneration}:{policy.State.Active?.EventId}:{policy.State.NavigationGoal?.EventId}:{policy.State.TravelCommitment is not null}";
            Ff7AutoplayInput input = policy.ChooseInput(driver);
            string after = $"{session.FieldGeneration}:{policy.State.Active?.EventId}:{policy.State.NavigationGoal?.EventId}:{policy.State.TravelCommitment is not null}";
            if (after != last || before != after)
            {
                Console.WriteLine($"OWNED map={map} tick={session.Machine.Tick} before={before} after={after} pending={driver.PlanningPending}, input={input}");
                last = after;
            }
            bool inTargetVisit = policy.State.Coverage.NavigationVisits.Any(visit => visit.Key == goal.NavigationKey && visit.VisitKey == policy.State.Active?.VisitId);
            if (inTargetVisit && policy.State.Active is { Kind: Ff7AutoPlayTargetKind.ActorContact } contact && contact.EventId == goal.EventId)
            {
                actualCondition = contact.ConditionFingerprint;
                contactWork = driver.ActorContactPlanningWork;
                preparedOwnedContact = policy.State.TravelCommitment is not null && policy.State.NavigationGoal == goal && contact.ConditionFingerprint == goal.Conditions && driver.PlanningPending && contactWork > 0;
            }
            if (policy.State.Coverage.NavigationVisits.Any(visit => visit.Key == goal.NavigationKey && visit.VisitKey == policy.State.Active?.VisitId) && policy.State.Active?.EventId != goal.EventId)
            {
                preempted = true;
            }
            return input;
        }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        while (controller.Running && !preparedOwnedContact && !preempted && elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
        }
        var targetCoverage = policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Where(item => item.Event.Id == goal.EventId && item.Event.ConditionSignature == goal.Conditions).ToArray();
        Check(targetCoverage.Length > 0 && targetCoverage.All(item => item.Conditions?.HasExecutedEntry(goal.Conditions!) != true), "Preparing an owned contact fabricated native entry credit.");
        Check(controller.Error is null && preparedOwnedContact && !preempted && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Actual owned arrival lost unchanged native contact intent before its preparation quantum.");
        Console.WriteLine($"OWNED currentCF={actualCondition}, catalogWork={contactWork}");
        Console.WriteLine($"OWNED final map={session.MapId}, generation={session.FieldGeneration}, tick={session.Machine.Tick}, callbacks={controller.Steps}, preempted={preempted}, goal={policy.State.NavigationGoal}, commitment={policy.State.TravelCommitment is not null}, error={controller.Error}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; sourceUnchanged={bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path))}");
    }
    internal static void RunCatalogOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_OWNED_CONTACT_SAVE") ?? throw new InvalidOperationException("Select a native owned-contact fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream source = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(source).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Actor target = session.Host.GetActor(save.AutoPlay!.Active!.Entity);
        Ff7AutoplayDriver driver = new(session);
        var reachable = Ff7AutoplayDriver.ReachableTriangles(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle);
        var enumerate = typeof(Ff7AutoplayDriver).GetMethod("EnumerateActorContactRegion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var candidates = (IEnumerable<(System.Numerics.Vector3 Point, int Triangle)>)enumerate.Invoke(driver, [target, player, reachable])!;
        using var catalog = new Ff7AutoplayDriver.ActorContactCatalog("current-source", candidates.GetEnumerator());
        string Native() => System.Text.Json.JsonSerializer.Serialize(new { session.Progress, Vm = session.Machine.Capture(), Process = session.Host.Capture() });
        string before = Native();
        Stopwatch elapsed = Stopwatch.StartNew();
        long work = 0;
        while (!catalog.Completed && elapsed.Elapsed < TimeSpan.FromSeconds(10))
        {
            work += catalog.Resume(new());
        }
        Check(catalog.Completed && work > 1 && before == Native() && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Actual contact catalog did not traverse all candidates without changing native source.");
        Console.WriteLine($"PASS original complete native contact catalog: work={work}, witnesses={catalog.Contacts.Count}, callbacks=0, tick={session.Machine.Tick}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; native/source unchanged");
    }
    internal static void RunAlternativeOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_OWNED_CONTACT_SAVE") ?? throw new InvalidOperationException("Select a native owned-contact fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream source = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(source).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        int skipped = 0;
        long work = 0;
        Ff7AutoPlayController controller = new(session, driver =>
        {
            Ff7AutoplayInput input = policy.ChooseInput(driver);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var observation = (Ff7AutoPlayObservation?)typeof(Ff7AutoPlayPolicy).GetField("ordinaryObservation", flags)!.GetValue(policy);
            if (driver.PlanningPending && policy.State.Active is { } active && observation is not null)
            {
                Check(Ff7AutoPlayPolicy.CanSkipOwnedLocalAlternativeScan(policy.State, observation, active), "Actual pending owner did not satisfy the excluded-local-goal proof.");
                Check(typeof(Ff7AutoPlayPolicy).GetField("ordinaryAlternativeSource", flags)!.GetValue(policy) is null, "The excluded-local-goal shortcut still entered global alternative preparation.");
                skipped++;
            }
            work = driver.ActorContactPlanningWork + driver.PhysicalPlanningWork;
            return input;
        }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        while (controller.Running && skipped < 8 && elapsed.Elapsed < TimeSpan.FromSeconds(10))
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
        }
        Check(controller.Error is null && skipped >= 8 && work > 0 && policy.State.NavigationGoal == save.AutoPlay!.NavigationGoal && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Actual pending contact did not advance without repeated global alternative scans.");
        Console.WriteLine($"PASS original excluded owned local goal: skippedScans={skipped}, realContactWork={work}, callbacks={controller.Steps}, tick={session.Machine.Tick}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; goal/source unchanged");
    }

}
