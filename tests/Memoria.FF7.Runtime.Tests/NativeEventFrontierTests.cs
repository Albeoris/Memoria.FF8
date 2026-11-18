using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEventFrontierTests
{
    public static void RunOriginal()
    {
        CheckOwnership();
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_EVENT_FRONTIER_SAVE") ?? throw new InvalidOperationException("A native event frontier fixture is required.");
        byte[] source = File.ReadAllBytes(path);
        RunBoundary(root, source, false);
        RunBoundary(root, source, true);
        Check(source.SequenceEqual(File.ReadAllBytes(path)), "The original native event frontier was changed.");
    }

    private static void CheckOwnership()
    {
        Ff7AutoPlayActiveEvent owner = new("visit", "transport", "condition", 4, 1, 2, Ff7AutoPlayTargetKind.LinePush, null, 10, "progress", NavigationKey: "source");
        Check(Ff7AutoPlayPolicy.MatchesNativeTransportOwner(owner, owner with { Accepted = true, Returned = true }), "Native acceptance/return lost an unchanged transport owner.");
        foreach (Ff7AutoPlayActiveEvent other in new[] { owner with { VisitId = "other" }, owner with { NavigationKey = "other" }, owner with { ConditionFingerprint = "other" }, owner with { Generation = 5 }, owner with { Entity = 2 }, owner with { Script = 3 }, owner with { EventId = "other" } })
        {
            Check(!Ff7AutoPlayPolicy.MatchesNativeTransportOwner(owner, other), "A replaced same-ID transport owner was accepted as its original native receipt.");
        }
        Ff7AutoPlayNativeRoute repair = new("target", "condition", 4, [new(new('A', 64), default)], new('B', 64), BattleBoundary: true);
        Check(!Ff7AutoPlayPolicy.HasCompletedNativeRepair(repair, repair, repair.After, true), "An installed but incomplete repair admitted an early native battle.");
        Check(!Ff7AutoPlayPolicy.HasCompletedNativeRepair(repair, null, new('C', 64), true) && !Ff7AutoPlayPolicy.HasCompletedNativeRepair(repair, null, repair.After, false), "A mismatching repair terminal state was accepted.");
        Check(Ff7AutoPlayPolicy.HasCompletedNativeRepair(repair, null, repair.After, true), "An exactly authenticated consumed repair lost its native handoff.");
    }

    private static void RunBoundary(string root, byte[] source, bool cold)
    {
        Ff7Installation installation = Ff7Installation.Discover(root);
        using MemoryStream input = new(source);
        Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A frontier audit requested a choice checkpoint."));
        Ff7AutoPlayObservation initial = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoPlayTarget local = initial.Events.First(target => policy.State.NavigationGuards.Values.Any(guard => guard.NavigationKey == initial.NavigationKey && guard.EventId == target.Id && guard.MovementFailure is not null));
        string before = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        Check(driver.ProveNativeRoute(local, initial.Events) is null && before == Ff7AutoplayDriver.ReadNativeRouteFingerprint(session), "A failed current-source proof changed native state or unexpectedly admitted the fixture.");
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        string? transport = null;
        string? transportSource = null;
        string? arrival = null;
        bool remoteProved = false;
        bool restored = false;
        bool acknowledged = false;
        try
        {
            controller.Start();
            for (int attempt = 0; attempt < 512 && controller.Running && session.Battle is null; attempt++)
            {
                controller.Advance(1);
                remoteProved |= policy.LastRemoteNativeProbe?.StartsWith("Proved ", StringComparison.Ordinal) == true;
                if (transport is null && remoteProved && policy.State.NativeRoute is { } prefix && policy.State.Active is { } owner)
                {
                    transport = prefix.EventId;
                    transportSource = owner.NavigationKey;
                    Console.WriteLine(policy.LastRemoteNativeProbe);
                }
                if (transport is not null && !acknowledged && policy.State.NativeRoute is null && policy.State.Active is { Accepted: true, Returned: true } completed && completed.EventId == transport)
                {
                    Ff7Actor actor = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                    arrival = Ff7AutoplayDriver.ReadNavigationKey(completed.VisitId, session.Field.Walkmesh, session.Host.Process.BlockedTriangles, actor.Triangle);
                    acknowledged = arrival != transportSource && actor.Ladder is null && actor.Jump is null && !session.Host.HasScriptedMotion(actor);
                }
                if (cold && !restored && transport is not null && policy.State.NativeRoute is { Cursor: >= 6 } activePrefix && activePrefix.EventId == transport)
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
            Console.WriteLine($"FRONTIER cold{cold}/restored{restored}, tick{session.Host.Process.Tick}, transport{transport}, acknowledged{acknowledged}, battle{session.Battle is not null}, error{controller.Error}");
            Check(remoteProved && acknowledged && session.Battle is not null && controller.Error is null && (!cold || restored), "The normal controller did not authenticate remote transport, source repair and genuine native handoff.");
            Check(policy.State.Coverage.NavigationRoutes.Where(route => route.SourceKey == transportSource && route.EventId == transport).All(route => route.DestinationKey == arrival), "Downstream repair was attributed to the first transport's native route.");
        }
        finally
        {
            policy.Dispose();
            session.Dispose();
        }
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
