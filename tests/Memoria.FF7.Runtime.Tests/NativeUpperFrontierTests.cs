using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeUpperFrontierTests
{
    public static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_UPPER_FRONTIER_SAVE") ?? throw new InvalidOperationException("An upper native frontier fixture is required.");
        byte[] bytes = File.ReadAllBytes(path);
        Run(Ff7Installation.Discover(root), bytes, false);
        Run(Ff7Installation.Discover(root), bytes, true);
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The original source changed.");
    }

    private static void Run(Ff7Installation installation, byte[] bytes, bool cold)
    {
        using MemoryStream input = new(bytes);
        Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        Ff7AutoPlayPolicy policy = CreatePolicy(session);
        Ff7AutoPlayController controller = CreateController(session, policy);
        Ff7NativeSourceRevalidation debt = Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(policy.State, new Ff7AutoPlayDiscovery(session).Observe()).First();
        string source = debt.NavigationKey;
        Ff7AutoPlayNavigationGuard[] originalRefusals = policy.State.NavigationGuards.Values.Where(guard => guard.NavigationKey == source && guard.EventId == debt.EventId && guard.MovementFailure is not null).ToArray();
        long start = session.Host.Process.Tick;
        bool restored = false;
        int quanta = 0;
        try
        {
            controller.Start();
            while (quanta++ < 20000 && controller.Running && session.Battle is null && session.Host.Process.Tick - start < 2048)
            {
                controller.Advance(1);
                if (cold && !restored && session.Host.Process.Tick - start >= 10)
                {
                    Ff7SessionSave save = session.Capture();
                    policy.Dispose();
                    session.Dispose();
                    session = Ff7Session.Restore(installation, save);
                    policy = CreatePolicy(session);
                    controller = CreateController(session, policy);
                    controller.Start();
                    restored = true;
                }
                Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
                if (observation.NavigationKey == source && policy.State.Active is { Accepted: true, Returned: true })
                {
                    break;
                }
            }
            Ff7AutoPlayObservation final = new Ff7AutoPlayDiscovery(session).Observe();
            Console.WriteLine($"SOURCE-REFRESH cold:{cold}, restored:{restored}, native:{session.Host.Process.Tick - start}, quanta:{quanta}, component:{final.NavigationKey.Split(':').Last()}, battle:{session.Battle is not null}, goal:{policy.State.NavigationGoal}, active:{policy.State.Active?.EventId}, error:{controller.Error}");
            Check(controller.Error is null, "Normal source refresh stopped: " + controller.Error);
            Check(session.Battle is not null || final.NavigationKey == source, "No native battle/source arrival was reached.");
            Check(!cold || restored, "Cold prefix was not restored.");
            Check(!policy.State.SourceRevalidationAttempts.Contains(debt.ReceiptKey), "Travel prematurely recorded a remote local-proof receipt.");
            Check(originalRefusals.All(guard => policy.State.NavigationGuards.Values.Contains(guard)), "Travel cleared or rewrote the remote native refusal.");
            var remote = policy.State.Coverage.NavigationVisits.Single(visit => visit.Key == source);
            var deferred = remote.Events.Single(item => item.Id == debt.EventId && item.ConditionSignature == debt.Condition);
            Check(Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(policy.State)(remote, deferred)?.Unavailable == true, "Source refresh made the obstructed remote event available.");
            Check(policy.State.NativeRoute is null, "Travel installed a predicted remote native prefix.");
            if (session.Battle is not null)
            {
                Check(policy.State.NavigationGoal is { EventId: null } goal && goal.NavigationKey == source, "Battle lost the genuine source-only goal.");
                Check(policy.State.NativeRevalidation is { ArrivalAcknowledged: false } task && task.NavigationKey == source && task.EventId == debt.EventId && task.Condition == debt.Condition && task.DepartureReceiptKey == debt.ReceiptKey, "Natural battle/cold restore lost or prematurely completed its local source task.");
            }
        }
        finally
        {
            policy.Dispose();
            session.Dispose();
        }
    }

    private static Ff7AutoPlayPolicy CreatePolicy(Ff7Session session) => new(session, (_, _) => throw new InvalidOperationException("Unexpected choice checkpoint."));
    private static Ff7AutoPlayController CreateController(Ff7Session session, Ff7AutoPlayPolicy policy) => new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
