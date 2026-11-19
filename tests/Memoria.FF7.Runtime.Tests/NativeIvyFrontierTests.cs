using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeIvyFrontierTests
{
    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_IVY_FRONTIER_SAVE") ?? throw new InvalidOperationException("External native frontier required.");
        byte[] bytes = File.ReadAllBytes(path);
        Run(Ff7Installation.Discover(root), bytes, false);
        Run(Ff7Installation.Discover(root), bytes, true);
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The original source changed.");
    }

    private static void Run(Ff7Installation installation, byte[] bytes, bool cold)
    {
        using MemoryStream stream = new(bytes);
        Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
        Ff7AutoPlayPolicy policy = CreatePolicy(session);
        Ff7AutoPlayController controller = CreateController(session, policy);
        var observation = new Ff7AutoPlayDiscovery(session).Observe();
        var debt = Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(policy.State, observation).First();
        var edge = policy.State.Coverage.NavigationRoutes.Single(edge => edge.SourceKey == observation.NavigationKey && edge.DestinationKey == debt.NavigationKey);
        var currentDriver = new Ff7AutoplayDriver(session);
        currentDriver.ConfigureSpatialBoundaries(observation.Events);
        Dictionary<string, string> facts = [];
        foreach (var entry in policy.State.NavigationGuards.Where(entry => entry.Value.NavigationKey == observation.NavigationKey))
        {
            var target = observation.Events.SingleOrDefault(target => target.Id == entry.Value.EventId);
            if (target is not null)
            {
                facts[entry.Key] = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint(currentDriver.ReadPhysicalAvailabilityFingerprint(target, entry.Value.BlockingEntities), entry.Value.MovementFailure, session.Host.Process.Actors, entry.Value.BlockingEntities);
            }
        }
        string nativeBefore = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        string stateBefore = System.Text.Json.JsonSerializer.Serialize(policy.State);
        var candidates = observation.Events.Where(target => target.AnalysisComplete && target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && policy.State.NavigationGuards.Values.Any(guard => guard.NavigationKey == observation.NavigationKey && guard.EventId == target.Id && guard.MovementFailure is not null)).ToArray();
        var ordered = Ff7AutoPlayPolicy.PrioritizeNativeProbeCandidates(policy.State, Ff7AutoPlayPolicy.CreatePlanner(policy.State), observation, candidates, facts, _ => false);
        Check(ordered[0].Id == edge.EventId, "Proof work did not prioritize the actual shared pending transport.");
        Check(ordered.Skip(1).Select(target => target.Id).SequenceEqual(candidates.Where(target => target.Id != edge.EventId).Select(target => target.Id)), "Proof priority changed remaining candidate order.");
        Check(nativeBefore == Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) && stateBefore == System.Text.Json.JsonSerializer.Serialize(policy.State), "Read-only proof preselection changed native/ledger/receipt/task state.");
        long start = session.Host.Process.Tick;
        int quanta = 0;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        long reportedNative = -1;
        bool restored = false;
        bool transport = false;
        bool acknowledged = false;
        try
        {
            controller.Start();
            while (elapsed.Elapsed < TimeSpan.FromMinutes(2) && quanta++ < 100000 && controller.Running && session.Battle is null && session.Host.Process.Tick - start < 2048)
            {
                controller.Advance(1);
                long native = session.Host.Process.Tick-start;
                if (quanta % 128 == 0 || native >= reportedNative+32)
                {
                    Console.WriteLine($"IVY-PROGRESS cold:{cold}, elapsed:{elapsed.Elapsed.TotalSeconds:F1}, native:{native}, quanta:{quanta}, phase:{policy.NativePlanningPhase}, active:{policy.State.Active?.EventId}, goal:{policy.State.NavigationGoal?.NavigationKey.Split(':').Last()}");
                    reportedNative=native;
                }
                if (policy.State.Active is { } active && active.EventId == edge.EventId)
                {
                    Check(active.TravelRoute is not null && policy.State.NavigationGoal is { EventId: null } goal && goal.NavigationKey == debt.NavigationKey, "Owned recovery manufactured a consumed self-goal instead of retaining shared travel intent.");
                    transport = true;
                    var current = new Ff7AutoPlayDiscovery(session).Observe();
                    var player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                    if (current.NavigationKey == debt.NavigationKey && active.Accepted && active.Returned && player.Ladder is null && !session.Host.Process.PlayerLocked)
                    {
                        acknowledged = true;
                        break;
                    }
                }
                if (cold && !restored && session.Host.Process.Tick - start >= 10)
                {
                    var saved = session.Capture();
                    policy.Dispose();
                    session.Dispose();
                    session = Ff7Session.Restore(installation, saved);
                    policy = CreatePolicy(session);
                    controller = CreateController(session, policy);
                    controller.Start();
                    restored = true;
                }
            }
            Console.WriteLine($"IVY cold:{cold}, restored:{restored}, native:{session.Host.Process.Tick-start}, quanta:{quanta}, transport:{transport}, acknowledged:{acknowledged}, battle:{session.Battle is not null}, goal:{policy.State.NavigationGoal}, task:{policy.State.NativeRevalidation}, error:{controller.Error}");
            Check(controller.Error is null && transport && (acknowledged || session.Battle is not null), "The normal graph controller did not reach its genuine transport boundary: " + controller.Error);
            Check(!cold || restored, "Cold prefix was not exercised.");
        }
        finally
        {
            policy.Dispose();
            session.Dispose();
        }
    }

    private static Ff7AutoPlayPolicy CreatePolicy(Ff7Session session) => new(session, (_, _) => throw new InvalidOperationException("Unexpected choice."));
    private static Ff7AutoPlayController CreateController(Ff7Session session, Ff7AutoPlayPolicy policy) => new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
    private static void Check(bool condition, string reason)
    {
        if (!condition)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
