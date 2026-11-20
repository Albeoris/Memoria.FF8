using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NavigationModeClockTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MODE_CLOCK_SAVE") ?? throw new InvalidOperationException("Select a native mode-clock fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The mode-clock continuation reached an unsupported choice boundary."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        var driver = (Ff7AutoplayDriver)typeof(Ff7AutoPlayController).GetField("driver", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
        Dictionary<bool, Ff7AutoplayDriver.PhysicalSearchCursor> Cursors()
        {
            IDictionary parked = (IDictionary)typeof(Ff7AutoplayDriver).GetField("parkedNavigationModes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(driver)!;
            Dictionary<bool, Ff7AutoplayDriver.PhysicalSearchCursor> result = [];
            foreach (bool run in parked.Keys)
            {
                object state = parked[run]!;
                if (state.GetType().GetProperty("ObstacleSearch")!.GetValue(state) is Ff7AutoplayDriver.PhysicalSearchCursor cursor)
                {
                    result[run] = cursor;
                }
            }
            return result;
        }
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        long beforeTick = -1;
        Dictionary<bool, Ff7AutoplayDriver.PhysicalSearchCursor> retained = [];
        Dictionary<bool, long> expanded = [];
        bool survived = false;
        for (int poll = 0; poll < 128 && controller.Running && !survived && elapsed.Elapsed < TimeSpan.FromSeconds(10); poll++)
        {
            var beforeClock = session.Progress.Clock;
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
            var current = Cursors();
            if (retained.Count == 2 && current.Count == 2 && session.Host.Process.Tick == beforeTick && session.Progress.Clock != beforeClock && current.All(pair => ReferenceEquals(pair.Value, retained[pair.Key])))
            {
                survived = current.All(pair => pair.Value.Expanded > expanded[pair.Key]);
            }
            if (!survived && current.Count == 2 && (retained.Count != 2 || session.Host.Process.Tick != beforeTick || current.Any(pair => !retained.TryGetValue(pair.Key, out var old) || !ReferenceEquals(old, pair.Value))))
            {
                retained = current;
                expanded = current.ToDictionary(pair => pair.Key, pair => (long)pair.Value.Expanded);
                beforeTick = session.Host.Process.Tick;
            }
        }
        if (controller.Error is not null || !survived || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException($"Native mode ownership failed across App-clock polls: retained={survived}, callbacks={controller.Steps}, work={driver.PhysicalPlanningWork}, pending={driver.PlanningPending}, status={policy.Status}, error={controller.Error}.");
        }
        Console.WriteLine($"PASS original App-clock mode ownership: BOTH cursor identities retained at nativeTick={session.Host.Process.Tick}, run={retained[true].Expanded}, walk={retained[false].Expanded}, physical={driver.PhysicalPlanningWork}, callbacks={controller.Steps}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; actual source unchanged");
    }
}
