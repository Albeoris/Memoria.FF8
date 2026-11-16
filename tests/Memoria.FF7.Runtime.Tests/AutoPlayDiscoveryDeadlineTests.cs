using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayDiscoveryDeadlineTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original world resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_DISCOVERY_SAVE") ?? throw new InvalidOperationException("Select an external interrupted world discovery.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected dialogue during world discovery."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        int before = policy.State.WorldVisits.Values.Sum(visit => visit.ObservedChunks.Count);
        HashSet<string> work = [];
        bool freeze = false;
        DiscoveryClock clock = new();
        Ff7AutoPlayController controller = new(session, driver => freeze ? default : policy.ChooseInput(driver), clock: clock, readPlanningProgress: () => policy.WorkProgress);
        controller.Start();
        for (int step = 0; step < 8 && controller.Running; step++)
        {
            clock.Seconds += 4;
            controller.Advance(1);
            work.Add(policy.WorkProgress);
        }
        int discovered = policy.State.WorldVisits.Values.Sum(visit => visit.ObservedChunks.Count) - before;
        if (controller.Error is not null || controller.Steps != 8 || work.Count < 6 || !bytes.SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException($"Bounded new world discovery was mistaken for idle work: {controller.Error ?? policy.State.Status}; steps={controller.Steps}, newChunks={discovered}.");
        }
        freeze = true;
        for (int step = 0; step < 8 && controller.Running; step++)
        {
            clock.Seconds += 4;
            controller.Advance(1);
        }
        if (controller.Failure is not TimeoutException)
        {
            throw new InvalidOperationException("Repeated catalog bookkeeping concealed actual idle world input.");
        }
        Console.WriteLine($"PASS native world catalog work through 32 seconds of virtual time, {work.Count} distinct catalog receipts ({discovered} newly explored chunks), actual idle timeout and unchanged source");
    }

    private sealed class DiscoveryClock : TimeProvider
    {
        public long Seconds { get; set; }
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Seconds;
    }
}
