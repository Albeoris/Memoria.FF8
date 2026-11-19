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
        CheckHigherHopDetour();
        CheckFairGuideAlternatives();
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

    private static void CheckHigherHopDetour()
    {
        Ff7Actor source = Source with { MoveSpeed = 256 };
        HashSet<int> lowerHopPreviews = [];
        Ff7NativeMoveResult Preview(Ff7Actor actor, Ff7AutoplayInput input)
        {
            if (actor.Triangle == 0)
            {
                if (input == new Ff7AutoplayInput(1, 0))
                {
                    return new(actor with { X4096 = 4096, Triangle = 1 }, false, true, []);
                }
                if (input == new Ff7AutoplayInput(0, 1))
                {
                    return new(actor with { Y4096 = 4096, Triangle = 2 }, false, true, []);
                }
            }
            if (actor.Triangle == 1)
            {
                lowerHopPreviews.Add(actor.X4096);
                if (input == new Ff7AutoplayInput(1, 0) && actor.X4096 < 64 * 4096)
                {
                    return new(actor with { X4096 = actor.X4096 + 4096 }, false, true, []);
                }
            }
            if (actor.Triangle == 2 && input == new Ff7AutoplayInput(0, 1))
            {
                return new(actor with { Y4096 = 2 * 4096, Triangle = 3 }, false, true, []);
            }
            return new(actor, false, false, []);
        }
        int Remaining(int triangle) => triangle == 2 ? 4 : triangle == 3 ? 2 : 3;
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = new(source, new(0, 2, 0), Remaining, Preview, (_, _) => true);
        Check(cursor.Resume(new(1, TimeSpan.MaxValue)).Status == Ff7AutoPlayPhysicalSearchStatus.Pending, "The detour fixture did not preserve its two pending native alternatives.");
        Ff7AutoPlayPhysicalRoute route = cursor.Resume(new(1, TimeSpan.MaxValue));
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && route.Expanded == 2 && route.Inputs.SequenceEqual(new[] { new Ff7AutoplayInput(0, 1), new Ff7AutoplayInput(0, 1) }) && lowerHopPreviews.Count == 0, "A nearby higher-hop native detour waited behind a physically costly lower-hop region.");
        cursor = new(source, new(0, 2, 0), Remaining, Preview, (_, step) => step.Actor.Triangle != 3);
        do
        {
            route = cursor.Resume(new(8, TimeSpan.MaxValue));
        }
        while (route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending);
        Check(route is { Status: Ff7AutoPlayPhysicalSearchStatus.Exhausted, Expanded: 66, Inputs.Length: 0 }, "Best-first guidance bypassed native admission or truncated the finite search frontier.");
    }

    private static void CheckFairGuideAlternatives()
    {
        Ff7AutoplayDriver.PhysicalSearchCursor Unresolved()
        {
            Ff7NativeMoveResult Preview(Ff7Actor actor, Ff7AutoplayInput input)
            {
                return input == new Ff7AutoplayInput(0, 1) ? new(actor with { Y4096 = actor.Y4096 + 8 }, false, true, []) : new(actor, false, false, []);
            }
            return new(Source, Vector3.UnitY, _ => 1, Preview, (_, _) => true);
        }
        Ff7AutoplayDriver driver = new(null!);
        Ff7AutoplayDriver.PhysicalSearchCursor original = Unresolved();
        Ff7AutoplayDriver.PhysicalSearchCursor shortGuide = CreateChain(2, true);
        Ff7AutoplayDriver.PhysicalSearchCursor laterGuide = Unresolved();
        original.ConfigureGuides([new(original, Vector3.Zero, 1, []), new(shortGuide, Vector3.UnitY, 2, [(0, 1)]), new(laterGuide, Vector3.UnitX, 3, [(0, 1), (0, 2)])]);
        Ff7AutoPlayPhysicalRoute route = driver.ResumePhysicalSearch(original, new(1, TimeSpan.MaxValue));
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending && original.Expanded == 1 && shortGuide.Expanded == 0 && laterGuide.Expanded == 0, "The first guide exceeded its shared one-expansion scheduling turn.");
        Field("obstacleSearch").SetValue(driver, original);
        Field("obstacleSearchContext").SetValue(driver, "finite-guides");
        Field("navigationRunMode").SetValue(driver, false);
        object snapshot = Method("CaptureNavigationMode").Invoke(driver, null)!;
        Method("CancelObstacleSearch").Invoke(driver, null);
        Method("RestoreNavigationMode").Invoke(driver, [snapshot]);
        route = driver.ResumePhysicalSearch(original, new(1, TimeSpan.MaxValue));
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending && shortGuide.Expanded == 1 && laterGuide.Expanded == 0, "Mode restoration lost the retained next-guide turn.");
        for (int turn = 0; turn < 3; turn++)
        {
            route = driver.ResumePhysicalSearch(original, new(1, TimeSpan.MaxValue));
        }
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && route.Expanded == 5 && driver.PhysicalPlanningWork == 5 && original.WinningGuide is { NextTriangle: 2 } winner && winner.AdviceExclusions.SequenceEqual(new[] { (0, 1) }) && route.Inputs.SequenceEqual(new[] { new Ff7AutoplayInput(0, 1), new Ff7AutoplayInput(0, 1) }), "A pending original starved the short alternative, mixed its input prefix or committed unrelated tested-guide exclusions.");
        Method("CancelObstacleSearch").Invoke(driver, null);
        Check(Field("obstacleSearch").GetValue(driver) is null, "Guide cancellation retained the workset in the driver.");
        original = Unresolved();
        Ff7AutoplayDriver.PhysicalSearchCursor denied = new(Source, Vector3.UnitY, _ => 1, (actor, _) => new(actor with { Triangle = 1 }, false, true, []), (_, _) => false);
        original.ConfigureGuides([new(original, Vector3.Zero, 1, []), new(denied, Vector3.UnitY, 2, [(0, 1)])]);
        route = original.Resume(new(8, TimeSpan.MaxValue));
        Check(route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending && denied.Expanded == 1 && original.WinningGuide is null, "One denied guide became a negative receipt while another guide remained pending.");
        original = CreateChain(3, false);
        Ff7AutoplayDriver.PhysicalSearchCursor finite = CreateChain(2, false);
        original.ConfigureGuides([new(original, Vector3.Zero, 1, []), new(finite, Vector3.UnitY, 2, [(0, 1)])]);
        do
        {
            route = original.Resume(new(2, TimeSpan.MaxValue));
        }
        while (route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending);
        Check(route is { Status: Ff7AutoPlayPhysicalSearchStatus.Exhausted, Expanded: 7, Inputs.Length: 0 } && original.WinningGuide is null, "A finite guide workset did not exhaust every admitted graph exactly once.");
    }

    private static void CheckModeSnapshotAndCancellation()
    {
        Ff7AutoplayDriver driver = new(null!);
        Ff7AutoplayDriver.PhysicalSearchCursor cursor = CreateChain(1537, true);
        Check(cursor.Resume(new(3, TimeSpan.MaxValue)).Expanded == 3, "The snapshot fixture did not retain pending work.");
        Field("obstacleSearch").SetValue(driver, cursor);
        Field("obstacleSearchContext").SetValue(driver, "walk-context");
        Field("navigationRunMode").SetValue(driver, false);
        object snapshot = Method("CaptureNavigationMode").Invoke(driver, null)!;
        Method("CancelObstacleSearch").Invoke(driver, null);
        Field("navigationRunMode").SetValue(driver, true);
        Check(Field("obstacleSearch").GetValue(driver) is null, "Cancelling a physical query retained its cursor.");
        Method("RestoreNavigationMode").Invoke(driver, [snapshot]);
        Check(ReferenceEquals(Field("obstacleSearch").GetValue(driver), cursor) && Equals(Field("obstacleSearchContext").GetValue(driver), "walk-context") && Equals(Field("navigationRunMode").GetValue(driver), false), "Speculative run-mode restoration lost the walk search or its alternate guide.");
        Check(cursor.Resume(new(2, TimeSpan.MaxValue)).Expanded == 5, "Restoring a mode restarted the retained physical search.");
        Method("CancelObstacleSearch").Invoke(driver, null);
        Check(Field("obstacleSearch").GetValue(driver) is null && Field("obstacleSearchContext").GetValue(driver) is null, "Cancelling a changed source retained physical query state.");
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
        Check(driver.PlanningProgress == new Ff7AutoPlayPlanningProgress(false, 0, 0, 0), "The initial public planning counters fabricated work.");
        driver.ResumePhysicalSearch(cursor, new(1, TimeSpan.MaxValue));
        Check(driver.PhysicalPlanningWork == 1, "Physical planning did not count its actual expansion.");
        driver.DeferPlanning();
        Ff7AutoPlayPlanningProgress observed = driver.PlanningProgress;
        Check(observed == new Ff7AutoPlayPlanningProgress(true, 1, 0, 0) && driver.PlanningProgress == observed, "Reading pending telemetry changed or omitted actual expanded work.");
        Ff7NativePlanningQuantum spent = new(1, TimeSpan.MaxValue);
        Check(spent.TryBeginOperation(), "The spent allowance fixture failed.");
        driver.ResumePhysicalSearch(cursor, spent);
        Check(driver.PhysicalPlanningWork == 1, "A scheduling yield reported physical work without an expansion.");
        Check(driver.PlanningProgress == observed, "A zero-work scheduling return advanced the exposed planning counters.");
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
