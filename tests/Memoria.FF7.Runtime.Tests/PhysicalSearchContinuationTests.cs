using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class PhysicalSearchContinuationTests
{
    internal static void Run()
    {
        CheckLongReachableGraph();
        CheckExhaustedGraph();
        CheckModeSnapshotAndCancellation();
        CheckWorkAccounting();
        CheckControllerPending();
        Console.WriteLine("Physical search continuation checks passed.");
    }

    private static Ff7Actor Source => new() { Slot = 0, Entity = 0, Model = 0, Triangle = 0, MoveSpeed = 1 };

    private static Ff7AutoplayDriver.PhysicalSearchCursor CreateChain(int length, bool reachable, Action? visit = null)
    {
        Ff7NativeMoveResult Preview(Ff7Actor actor, Ff7AutoplayInput input)
        {
            visit?.Invoke();
            int index = actor.Y4096 / 8;
            int next = input.Horizontal == 0 ? index + input.Vertical : index;
            if (next < 0 || next > length || next == index)
            {
                return new(actor, false, false, []);
            }
            return new(actor with { Y4096 = next * 8, Triangle = reachable && next == length ? 1 : 0 }, false, true, []);
        }
        return new(Source, Vector3.UnitX, triangle => triangle == 0 ? 1 : 0, Preview, (_, _) => true);
    }

    private static void CheckLongReachableGraph()
    {
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = CreateChain(1537, true);
        Ff7AutoPlayPhysicalRoute route = cursor.Resume(new(1024, TimeSpan.MaxValue));
        Check(route is { Status: Ff7AutoPlayPhysicalSearchStatus.Pending, Expanded: 1024, Inputs.Length: 0 }, "A scheduling quantum became a refusal or an unproved physical route.");
        int previous = route.Expanded;
        while (route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending)
        {
            route = cursor.Resume(new(17, TimeSpan.MaxValue));
            Check(route.Expanded > previous && route.Expanded - previous <= 17, "A physical search restarted or exceeded its next cooperative allowance.");
            previous = route.Expanded;
        }
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && route.Expanded == 1537 && route.Inputs.Length == 1537 && route.Inputs.All(input => input == new Ff7AutoplayInput(0, 1)), "A reachable represented graph beyond 1024 nodes lost its physical input prefix.");
        Check(ReferenceEquals(route, cursor.Resume(new(1))), "A completed physical search executed again.");
        Ff7AutoPlayPhysicalRoute uninterrupted = CreateChain(1537, true).Resume(new(2000, TimeSpan.MaxValue));
        Check(route.Inputs.SequenceEqual(uninterrupted.Inputs) && route.Expanded == uninterrupted.Expanded, "Cooperative scheduling changed physical route ranking.");
    }

    private static void CheckExhaustedGraph()
    {
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = CreateChain(1536, false);
        Ff7AutoPlayPhysicalRoute route;
        do
        {
            route = cursor.Resume(new(31, TimeSpan.MaxValue));
        }
        while (route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending);
        Check(route is { Status: Ff7AutoPlayPhysicalSearchStatus.Exhausted, Expanded: 1537, Inputs.Length: 0 }, "A finite cyclic represented graph was truncated or falsely admitted.");
    }

    private static void CheckModeSnapshotAndCancellation()
    {
        Ff7AutoplayDriver driver = new(null!);
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = CreateChain(1537, true);
        Check(cursor.Resume(new(3, TimeSpan.MaxValue)).Expanded == 3, "The snapshot fixture did not retain pending work.");
        Field("obstacleSearch").SetValue(driver, cursor);
        Field("obstacleSearchContext").SetValue(driver, "walk-context");
        Field("obstacleSearchAlternative").SetValue(driver, Vector3.UnitY);
        Field("navigationRunMode").SetValue(driver, false);
        object snapshot = Method("CaptureNavigationMode").Invoke(driver, null)!;
        Method("CancelObstacleSearch").Invoke(driver, null);
        Field("navigationRunMode").SetValue(driver, true);
        Check(Field("obstacleSearch").GetValue(driver) is null, "Cancelling a physical query retained its cursor.");
        Method("RestoreNavigationMode").Invoke(driver, [snapshot]);
        Check(ReferenceEquals(Field("obstacleSearch").GetValue(driver), cursor) && Equals(Field("obstacleSearchContext").GetValue(driver), "walk-context") && Equals(Field("obstacleSearchAlternative").GetValue(driver), Vector3.UnitY) && Equals(Field("navigationRunMode").GetValue(driver), false), "Speculative run-mode restoration lost the walk search or its alternate guide.");
        Check(cursor.Resume(new(2, TimeSpan.MaxValue)).Expanded == 5, "Restoring a mode restarted the retained physical search.");
        Method("CancelObstacleSearch").Invoke(driver, null);
        Check(Field("obstacleSearch").GetValue(driver) is null && Field("obstacleSearchContext").GetValue(driver) is null && Field("obstacleSearchAlternative").GetValue(driver) is null, "Cancelling a changed source retained physical query state.");
        Check(CreateChain(1537, true).Resume(new(1, TimeSpan.MaxValue)).Expanded == 1, "A replacement query inherited expansion state from a cancelled source.");
    }

    private static void CheckControllerPending()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DISCOVERY_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP physical pending controller: configure original resources and a standalone field fixture.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream stream = new(original, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(stream).Ff7!);
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = CreateChain(4096, true);
        int selected = 0;
        int nativeAdvances = 0;
        Ff7AutoPlayController controller = new(session, driver =>
        {
            selected++;
            Ff7AutoPlayPhysicalRoute result = driver.ResumePhysicalSearch(cursor, new(1, TimeSpan.MaxValue));
            Check(result.Status == Ff7AutoPlayPhysicalSearchStatus.Pending, "The pending controller fixture unexpectedly completed.");
            driver.DeferPlanning();
            return default;
        }, advanceTick: () => nativeAdvances++);
        controller.Start();
        string before = JsonSerializer.Serialize(session.Capture());
        for (int scheduling = 0; scheduling < Ff7AutoPlayProgressWatchdog.IdleStepLimit + 5 && controller.Running; scheduling++)
        {
            controller.Advance(1);
        }
        Check(selected == Ff7AutoPlayProgressWatchdog.IdleStepLimit + 5 && nativeAdvances == 0 && controller.Steps == 0 && controller.Error is null && controller.Running && controller.RecentActions.Count == 0, "Pending physical work advanced native time, tripped the idle watchdog, produced a refusal or recorded gameplay input.");
        Check(before == JsonSerializer.Serialize(session.Capture()) && original.SequenceEqual(File.ReadAllBytes(path)), "Cooperative physical planning changed its live session or standalone fixture.");
    }

    private static void CheckWorkAccounting()
    {
        Ff7AutoplayDriver driver = new(null!);
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = CreateChain(2, false);
        driver.ResumePhysicalSearch(cursor, new(1, TimeSpan.MaxValue));
        Check(driver.PhysicalPlanningWork == 1, "Physical planning did not count its actual expansion.");
        Ff7NativePlanningQuantum spent = new(1, TimeSpan.MaxValue);
        Check(spent.TryBeginOperation(), "The spent allowance fixture failed.");
        driver.ResumePhysicalSearch(cursor, spent);
        Check(driver.PhysicalPlanningWork == 1, "A scheduling yield reported physical work without an expansion.");
        driver.ResumePhysicalSearch(cursor, new(8, TimeSpan.MaxValue));
        Check(driver.PhysicalPlanningWork == 3, "Queue exhaustion did not account for its actual expansions.");
        driver.ResumePhysicalSearch(cursor, new(8, TimeSpan.MaxValue));
        Method("CancelObstacleSearch").Invoke(driver, null);
        driver.ResumePhysicalSearch(CreateChain(2, false), new(1, TimeSpan.MaxValue));
        Check(driver.PhysicalPlanningWork == 4, "Completion or cancellation reset or fabricated physical planning work.");
    }

    private static FieldInfo Field(string name)
    {
        return typeof(Ff7AutoplayDriver).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private static MethodInfo Method(string name)
    {
        return typeof(Ff7AutoplayDriver).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
