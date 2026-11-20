using System.Diagnostics;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWorldRouteCycleTests
{
    internal static void Run()
    {
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent transport = new("entry", AutoPlayEventKind.Transition, ConditionSignature: "entry-cf");
        AutoPlayEvent deferred = new("failed", AutoPlayEventKind.Interaction, ConditionSignature: "same-cf");
        AutoPlayEvent fresh = new("fresh", AutoPlayEventKind.Interaction, ConditionSignature: "fresh-cf");
        planner.Observe(new("world", "world"), [transport, fresh]);
        planner.ObserveNavigation("world", "world-nav", [transport, fresh]);
        planner.Observe(new("field", "field"), [deferred]);
        planner.ObserveNavigation("field", "field-nav", [deferred]);
        planner.Completed("world", transport.Id, "field", entrySignature: transport.ConditionSignature, entrySignatureAuthoritative: true, targetId: transport.Id);
        planner.RecordNavigationRoute("world-nav", "field-nav", transport.Id, state.Coverage.Visits[0].Events[0].Routes.Single());
        AutoPlayTravelGoal failedGoal = new("field-nav", deferred.Id, deferred.ConditionSignature);
        bool Deferred(string condition, string progress, bool refresh = false) => Ff7AutoPlayPolicy.MatchesOrdinaryGoalDeferral("same-cf", condition, "progress", progress, refresh);
        AutoPlayPlanStep? Choose(bool held) => planner.PlanNavigation("world-nav", preferredGoal: failedGoal, readAvailability: (_, target) => Ff7AutoPlayPolicy.ReadOrdinarySchedulingAvailability(null, target.Id, held && target.Id == deferred.Id));
        Check(Choose(false)?.Goal == failedGoal, "The remote pending target was not eligible before scheduling deferral.");
        Check(Choose(Deferred("same-cf", "progress")) is { Event.Id: "fresh" }, "World planning ignored a still-valid local scheduling deferral.");
        Check(Choose(Deferred("changed-cf", "progress"))?.Goal == failedGoal && Choose(Deferred("same-cf", "new-progress"))?.Goal == failedGoal && Choose(Deferred("same-cf", "progress", true))?.Goal == failedGoal, "Changed condition/progress/source facts failed to reopen retryable planning.");
        Check(state.Coverage.Visits[1].Events[0].Conditions!.ConsumedEntries.Count == 0 && state.Coverage.Visits[1].Events[0].Available, "Temporary scheduling created native unavailability or execution credit.");
        Console.WriteLine("PASS local-to-world scheduling deferral, changed facts/progress release and native-history preservation");
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
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_ROUTE_SAVE") ?? throw new InvalidOperationException("Select a native world-route fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        int worldEntries = 0;
        bool selectedNext = false;
        bool deferralValid = false;
        bool conditionMatches = false;
        bool progressMatches = false;
        bool sourceNeedsRefresh = true;
        AutoPlayTravelGoal? failedGoal = null;
        Ff7WorldRuntime? observed = null;
        Ff7AutoPlayController controller = new(session, driver =>
        {
            if (session.World is { } nextWorld && !ReferenceEquals(observed, nextWorld))
            {
                observed = nextWorld;
                worldEntries++;
            }
            Ff7AutoplayInput chosen = policy.ChooseInput(driver);
            if (worldEntries == 1 && session.World is { } first && policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh, Accepted: false } && policy.State.NavigationGoal is { EventId: not null } goal && !goal.NavigationKey.StartsWith("ff7-world:", StringComparison.Ordinal))
            {
                failedGoal ??= goal;
            }
            selectedNext = worldEntries >= 2 && session.World is { Process: { Initialized: true, MovementEnabled: true, MainState: 1 } } && policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel, Accepted: false };
            if (selectedNext && failedGoal is not null)
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var deferred = (Dictionary<(string Navigation, string Event), (string Condition, string Physical, string? Progress)>)typeof(Ff7AutoPlayPolicy).GetField("ordinaryDeferredGoals", flags)!.GetValue(policy)!;
                Check(deferred.TryGetValue((failedGoal.NavigationKey, failedGoal.EventId!), out var refusal), "The real local failure did not retain its scheduling deferral.");
                var navigation = policy.State.Coverage.NavigationVisits.Single(visit => visit.Key == failedGoal.NavigationKey);
                var aggregate = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == navigation.VisitKey);
                var current = navigation.Events.Single(item => item.Id == failedGoal.EventId);
                conditionMatches = refusal.Condition == current.ConditionSignature;
                progressMatches = refusal.Progress == policy.State.FrontierProgressKey;
                sourceNeedsRefresh = (bool)typeof(Ff7AutoPlayPolicy).GetMethod("NeedsVisitRefresh", flags)!.Invoke(policy, [new AutoPlayVisit(navigation.Key, aggregate.Visit.Region)])!;
                deferralValid = Ff7AutoPlayPolicy.MatchesOrdinaryGoalDeferral(refusal.Condition, current.ConditionSignature, refusal.Progress, policy.State.FrontierProgressKey, sourceNeedsRefresh);
                var coverage = aggregate.Events.Single(item => item.Event.Id == failedGoal.EventId);
                Check(coverage.Available && !coverage.Unavailable && !coverage.Completed && coverage.Conditions?.HasExecutedEntry(failedGoal.Conditions!) != true, "Temporary scheduling fabricated native refusal or consumed the failed gateway.");
                Check(policy.State.NavigationGoal != failedGoal, "World planning immediately routed back to the locally deferred frontier.");
            }
            return chosen;
        }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        while (controller.Running && !selectedNext && elapsed.Elapsed < TimeSpan.FromSeconds(30))
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
        }
        Check(controller.Error is null && selectedNext && failedGoal is not null && deferralValid && conditionMatches && progressMatches && !sourceNeedsRefresh && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Actual local-to-world deferral did not preserve exact valid scope and change route selection.");
        Console.WriteLine($"PASS original local-to-world scheduling: callbacks={controller.Steps}, conditionMatch={conditionMatches}, progressMatch={progressMatches}, sourceNeedsRefresh={sourceNeedsRefresh}, owner={policy.State.Active?.EventId}, goal={policy.State.NavigationGoal}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; no failed-gateway credit/native unavailability, source unchanged");
    }
}
