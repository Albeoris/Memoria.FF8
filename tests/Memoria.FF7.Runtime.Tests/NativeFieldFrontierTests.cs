using System.Diagnostics;
using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeFieldFrontierTests
{
    public static void RunQueuedLineRequests()
    {
        Ff7AutoPlayTarget enter = new("enter", 14, 5, Ff7AutoPlayTargetKind.LineEnter, "condition", false, Vector3.Zero, MayChangeNavigation: true);
        Ff7Line line = new(2, 14, 0, 0, 0, 100, 0, 0, 1, 0, Enter: true);
        if (Ff7AutoplayDriver.ReadPendingNavigationLineRequest([enter], [line], _ => false) != enter)
        {
            throw new InvalidOperationException("An already queued effectful native LINE entry did not request an ordinary native callback.");
        }
        int speculativeSteps = 0;
        bool disposed = false;
        IEnumerable<Memoria.AutoPlay.AutoPlayPlanStep?> UnresolvedSearch()
        {
            try
            {
                while (true)
                {
                    speculativeSteps++;
                    yield return null;
                }
            }
            finally
            {
                disposed = true;
            }
        }
        using Ff7NativePlanningCursor speculation = new(UnresolvedSearch().GetEnumerator());
        speculation.Resume(new(1, TimeSpan.FromHours(1)));
        int beforeWait = speculativeSteps;
        Ff7AutoPlayState state = new();
        string intent = Ff7AutoPlayPolicy.ReadNativePlanningIntent(state);
        Ff7AutoplayInput handoff = Ff7AutoPlayPolicy.WaitForQueuedNativeLine(state, enter, speculation.Dispose);
        if (handoff != default || !disposed || speculativeSteps != beforeWait || intent != Ff7AutoPlayPolicy.ReadNativePlanningIntent(state))
        {
            throw new InvalidOperationException("A real queued LINE obligation resumed unresolved speculation, manufactured input or changed planning ownership instead of yielding a native callback.");
        }
        foreach (var candidate in new[] { enter with { MayChangeNavigation = false }, enter with { Kind = Ff7AutoPlayTargetKind.ActorTalk }, enter with { Kind = Ff7AutoPlayTargetKind.LineTouch } })
        {
            if (Ff7AutoplayDriver.ReadPendingNavigationLineRequest([candidate], [line], _ => false) is not null)
            {
                throw new InvalidOperationException("An ambient, unrelated or unqueued contact blocked ordinary navigation.");
            }
        }
        if (Ff7AutoplayDriver.ReadPendingNavigationLineRequest([enter], [line], _ => true) is not null || Ff7AutoplayDriver.ReadPendingNavigationLineRequest([enter], [line with { Enter = false, Touch = true }], _ => false) is not null)
        {
            throw new InvalidOperationException("An effectless entry or perpetually ambient Touch was treated as a queued navigation request.");
        }
    }

    public static void RunOriginal()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FIELD_FRONTIER_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            Console.WriteLine("SKIP native field frontier: configure original resources and standalone fixture.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        RunQueuedLineRequests();
        Ff7Installation installation = Ff7Installation.Discover(game);
        Ff7SessionSave latched = RunHandoff(installation, source, false);
        CheckDetachedWait(installation, source);
        RunHandoff(installation, latched, true);
        if (!bytes.SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("Queued native handoff changed its original standalone.");
        }
    }

    private static void CheckDetachedWait(Ff7Installation installation, Ff7SessionSave source)
    {
        using Ff7Session session = Ff7Session.Restore(installation, source);
        string before = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget target = observation.Events.Single(target => target.Id == source.AutoPlay!.Active!.EventId);
        Ff7AutoplayDriver driver = new(session);
        bool waited = false;
        bool returned = false;
        Ff7NativeRouteProbe probe = new()
        {
            QueuedLineWaitObserved = (step, tick) =>
            {
                waited |= step.Input == default;
                returned |= tick.ScriptReturns.Any(item => item.Entity == 14 && item.Priority == 1);
            }
        };
        using IEnumerator<int> cursor = driver.EnumerateNativeRoute(target, observation.Events, probe).GetEnumerator();
        Stopwatch elapsed = Stopwatch.StartNew();
        int slices = 0;
        while (!returned && slices < 64 && elapsed.Elapsed < TimeSpan.FromSeconds(10) && cursor.MoveNext())
        {
            slices++;
        }
        cursor.Dispose();
        if (!waited || !returned || driver.ActiveNativeRouteCandidates != 0 || before != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session))
        {
            throw new InvalidOperationException("The bounded detached proof failed to record a neutral queued LINE callback with its actual native return, or changed its source.");
        }
        Console.WriteLine($"DETACHED-QUEUED-WAIT neutral:{waited}, returned:{returned}, schedulerSlices:{slices}, candidatesDisposed:{driver.ActiveNativeRouteCandidates == 0}; no full-route claim");
    }

    private static Ff7SessionSave RunHandoff(Ff7Installation installation, Ff7SessionSave source, bool cold)
    {
        using Ff7Session session = Ff7Session.Restore(installation, source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during queued LINE handoff."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayActiveEvent owner = policy.State.Active!;
        int guardCount = policy.State.NavigationGuards.Count;
        Ff7Actor start = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7SessionSave? latched = cold ? source : null;
        bool returned = false;
        bool preserved = false;
        int native = 0;
        int slices = 0;
        session.NativeTickCompleted += tick =>
        {
            native++;
            Ff7Line line = session.Host.Process.Lines.Single(line => line.Entity == 14);
            if (line.Enter && latched is null)
            {
                latched = session.Capture();
            }
            returned |= tick.ScriptReturns.Any(item => item.Entity == 14 && item.Priority == 1);
            if (returned && !preserved)
            {
                preserved = policy.State.Active?.EventId == owner.EventId && policy.State.Active.ConditionFingerprint == owner.ConditionFingerprint && policy.State.NavigationGuards.Count == guardCount;
                if (!preserved)
                {
                    throw new InvalidOperationException("The queued native LINE handoff discarded its selected owner or manufactured a physical refusal.");
                }
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        Stopwatch elapsed = Stopwatch.StartNew();
        controller.Start();
        while (controller.Running && session.Battle is null && native < (cold ? 2 : 3) && elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            controller.Advance(1);
            slices++;
        }
        Ff7Actor final = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"QUEUED-HANDOFF cold:{cold}, native:{native}, slices:{slices}, returned:{returned}, preserved:{preserved}, guards:{guardCount}->{policy.State.NavigationGuards.Count}, owner:{policy.State.Active?.EventId}, idle:{policy.State.FrontierIdleSteps}, position:({start.X4096},{start.Y4096})->({final.X4096},{final.Y4096}), error:{controller.Error}, seconds:{elapsed.Elapsed.TotalSeconds:F3}");
        if (controller.Error is not null || !returned || !preserved || policy.State.NavigationGuards.Count != guardCount || policy.State.Active?.EventId != owner.EventId || start.X4096 == final.X4096 && start.Y4096 == final.Y4096)
        {
            throw new InvalidOperationException("The ordinary controller failed to consume the queued LINE request and resume its selected native approach.");
        }
        return latched ?? throw new InvalidOperationException("No actual queued LINE boundary was captured.");
    }

}
