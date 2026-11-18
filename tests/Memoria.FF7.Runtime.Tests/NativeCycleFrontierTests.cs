using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

internal static class NativeCycleFrontierTests
{
    public static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CYCLE_FRONTIER_SAVE") ?? throw new InvalidOperationException("A native cycle frontier fixture is required.");
        byte[] source = File.ReadAllBytes(path);
        CheckStableGoal();
        RunBoundary(root, source, false);
        RunBoundary(root, source, true);
        Check(source.SequenceEqual(File.ReadAllBytes(path)), "The original native cycle frontier was changed.");
    }

    private static void RunBoundary(string root, byte[] source, bool cold)
    {
        Ff7Installation installation = Ff7Installation.Discover(root);
        using MemoryStream input = new(source);
        Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A frontier audit requested a choice checkpoint."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        bool prefixSeen = false;
        bool admitted = false;
        bool restored = false;
        bool completed = false;
        string sourceNavigation = new Ff7AutoPlayDiscovery(session).Observe().NavigationKey;
        string? ownerId = null;
        try
        {
            controller.Start();
            for (int slice = 0; slice < 512 && controller.Running && session.Battle is null && !completed; slice++)
            {
                controller.Advance(1);
                if (policy.State.NativeRoute is { } prefix)
                {
                    prefixSeen = true;
                    ownerId ??= prefix.EventId;
                    Check(Ff7AutoPlayPolicy.IsNativeContinuationGoalPending(policy.State, policy.State.NavigationGoal, policy.State.Active!.NavigationKey!, prefix.EventId), "Ordinary departure selected a consumed synthetic self-goal.");
                }
                admitted |= ownerId is not null && policy.State.NativeRoute is null && policy.State.Active is { Accepted: true } active && active.EventId == ownerId;
                if (admitted && policy.State.Active is { Accepted: true, Returned: true } owner && owner.EventId == ownerId)
                {
                    Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                    string currentNavigation = Ff7AutoplayDriver.ReadNavigationKey(owner.VisitId, session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle);
                    completed = currentNavigation != sourceNavigation && player.Ladder is null && player.Jump is null && !session.Host.HasScriptedMotion(player) && !(session.Machine.Capture().PendingRequests ?? []).Any(request => request.Entity == player.Entity && request.Command is FieldActorLadder or FieldActorMove or FieldActorJump);
                }
                if (cold && !restored && policy.State.NativeRoute is { Cursor: >= 2 } live)
                {
                    Ff7SessionSave save = session.Capture();
                    policy.Dispose();
                    session.Dispose();
                    session = Ff7Session.Restore(installation, save);
                    policy = new(session, (_, _) => throw new InvalidOperationException("A frontier audit requested a choice checkpoint."));
                    controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
                    controller.Start();
                    restored = true;
                }
            }
            Console.WriteLine($"CYCLE cold{cold}/restored{restored}, tick{session.Host.Process.Tick}, prefix{prefixSeen}, owner{ownerId}, admitted{admitted}, completed{completed}, triangle{session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle}, error{controller.Error}");
            Check(prefixSeen && admitted && completed && controller.Error is null && (!cold || restored), "The normal controller did not reach its selected LINE owner's acknowledged native arrival.");
        }
        finally
        {
            policy.Dispose();
            session.Dispose();
        }
    }

    private static void CheckStableGoal()
    {
        Ff7AutoPlayState state = new();
        AutoPlayEvent transport = new("transport", AutoPlayEventKind.Interaction, ConditionSignature: "same");
        AutoPlayEvent frontier = new("frontier", AutoPlayEventKind.Interaction, ConditionSignature: "pending");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> consumed = new() { Event = transport, Completed = true, Conditions = new() };
        consumed.Conditions.ConsumedEntries.Add("same");
        consumed.AvailabilityFingerprint = Ff7AutoPlayPolicy.ReadNativeContinuationAvailability("same-physical-source");
        consumed.CompletedAvailabilityFingerprints.Add(consumed.AvailabilityFingerprint);
        state.Coverage.Visits.Add(new() { Visit = new("visit", "region"), Events = [consumed, new() { Event = frontier, Conditions = new() }] });
        AutoPlayNavigationVisit navigation = new("component", "visit", [transport, frontier]);
        state.Coverage.NavigationVisits.Add(navigation);
        Check(!AutoPlayPlanner<Ff7AutoPlayDeferral>.NeedsExecution(consumed), "A consumed stable capability became a new native frontier.");
        Check(!Ff7AutoPlayPolicy.IsNativeContinuationGoalPending(state, new("component", transport.Id, "same"), "component", transport.Id), "A consumed self-admission became an exploration goal.");
        Check(!Ff7AutoPlayPolicy.IsNativeContinuationGoalPending(state, null, "component", transport.Id), "A consumed implicit local self-goal was rearmed.");
        Check(Ff7AutoPlayPolicy.IsNativeContinuationGoalPending(state, new("component", frontier.Id, "pending"), "component", transport.Id), "A genuine unconsumed shared frontier was excluded.");
        Check(Ff7AutoPlayPolicy.IsNativeContinuationGoalPending(state, null, "component", frontier.Id), "A genuine unconsumed local planner action with no explicit Goal was excluded.");
        navigation.RouteRediscovery[transport.Id] = true;
        Check(Ff7AutoPlayPolicy.IsNativeContinuationGoalPending(state, new("component", transport.Id, "same"), "component", transport.Id), "An explicit pending topology observation was excluded.");
        Check(Ff7AutoPlayPolicy.IsNativeContinuationGoalPending(state, null, "component", transport.Id), "A real local topology probe with no explicit Goal was excluded.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
