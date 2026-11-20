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
        Check(Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target, available), "A valid unchanged native contact lost authenticated travel intent.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target with { ConditionFingerprint = "changed" }, available), "Non-talk ownership rebased a changed native condition.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target, available with { Unavailable = true }), "A real unavailable approach retained owned priority.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation with { Events = [] }, target, available), "A missing native target retained owned priority.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation with { NavigationKey = "changed-source" }, target, available), "A changed native arrival context retained old ownership.");
        Check(!Ff7AutoPlayPolicy.CanPreserveUnchangedOwnedTarget(state, observation, target with { AnalysisComplete = false }, available), "Unknown native execution gained owned priority.");
        var commitment = state.TravelCommitment;
        state.TravelCommitment = null;
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
}
