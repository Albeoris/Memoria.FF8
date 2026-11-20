using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldPhysicalSearchContinuationTests
{
    private static Ff7AutoPlayWorldMovementState Source => new(0, 1, new(0, 0, 0, 0), new(), new(), 0, 0, null, 0);

    private static Ff7AutoplayDriver.WorldPhysicalSearchCursor Chain(int length, bool reachable)
    {
        Ff7AutoPlayWorldMovementPreview Preview(Ff7AutoplayInput input, Ff7AutoPlayWorldMovementState source)
        {
            if (input.Horizontal != 1 || input.Vertical != 0 || source.Player.X == length)
            {
                return new(true, null, source);
            }
            var next = source with { Player = source.Player with { X = source.Player.X + 1 } };
            return new(true, null, next, true, ModelBoundaries: reachable && next.Player.X == length ? [new(5, 4)] : []);
        }
        return new(Source, Preview, _ => 100, _ => true, step => step.ModelBoundaries?.Any(boundary => boundary.Model == 5 && boundary.Event == 4) == true);
    }

    internal static void Run()
    {
        CheckPolicyPending();
        Ff7AutoplayDriver driver = new(null!);
        var cursor = Chain(1537, true);
        int created = 0;
        Ff7AutoplayDriver.WorldPhysicalSearchCursor Create()
        {
            created++;
            return cursor;
        }
        string before = JsonSerializer.Serialize(Source);
        for (int quantum = 0; quantum < 3; quantum++)
        {
            var pending = driver.ResumeWorldPhysicalSearch("source:goal:final-guide-null", Create, 512);
            Check(pending.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && pending.Steps.Length == 0 && driver.PlanningPending && cursor.FrontierCount > 0 && cursor.Expanded == (quantum + 1) * 512 && created == 1, "A no-input final-guide quantum discarded its frontier, returned failure or restarted its source.");
        }
        var reached = driver.ResumeWorldPhysicalSearch("source:goal:final-guide-null", Create, 512);
        Check(reached.Status == Ff7AutoPlayWorldNavigationStatus.Step && reached.Steps.Length == 1537 && !driver.PlanningPending && driver.PhysicalPlanningWork == 1537 && before == JsonSerializer.Serialize(Source), "A route beyond three scheduling quanta did not preserve its parent trace or source.");
        Check(reached.Steps.Zip(reached.Steps.Skip(1)).All(pair => pair.First.DestinationKey == pair.Second.SourceKey), "Retained world search emitted a discontinuous native input prefix.");
        cursor = Chain(1300, false);
        driver.ResumeWorldPhysicalSearch("finite", Create, 512);
        driver.ResumeWorldPhysicalSearch("finite", Create, 512);
        var exhausted = driver.ResumeWorldPhysicalSearch("finite", Create, 512);
        Check(exhausted.Status == Ff7AutoPlayWorldNavigationStatus.Blocked && cursor.FrontierCount == 0 && cursor.Expanded == 1301 && !driver.PlanningPending, "A finite represented graph failed before genuine queue exhaustion.");
        long work = driver.PhysicalPlanningWork;
        Check(cursor.Resume(512).Expanded == 0 && driver.PhysicalPlanningWork == work, "An already exhausted cursor manufactured new physical work.");
        int generations = 0;
        Ff7AutoplayDriver.WorldPhysicalSearchCursor New()
        {
            generations++;
            return Chain(1537, true);
        }
        object owner = new();
        driver.ResumeWorldPhysicalSearch("source-a:goal-a:guide-a:input-a", New, 8, owner);
        driver.ResumeWorldPhysicalSearch("source-a:goal-a:guide-a:input-a", New, 8, owner);
        Check(generations == 1, "Unchanged source/goal restarted cooperative world work.");
        driver.ResumeWorldPhysicalSearch("source-b:goal-a:guide-a:input-a", New, 8, owner);
        driver.ResumeWorldPhysicalSearch("source-b:goal-b:guide-a:input-a", New, 8, owner);
        driver.ResumeWorldPhysicalSearch("source-b:goal-b:guide-b:input-a", New, 8, owner);
        driver.ResumeWorldPhysicalSearch("source-b:goal-b:guide-b:input-b", New, 8, owner);
        driver.ResumeWorldPhysicalSearch("source-b:goal-b:guide-b:input-b", New, 8, new object());
        Check(generations == 6, "Changed native source, goal, guide, input or world instance retained an obsolete cursor.");
        long renewalWork = driver.PhysicalPlanningWork;
        var renewed = Chain(1537, true);
        var prepared = driver.ResumeWorldPhysicalSearch("renewed-preferences", () => renewed, 0);
        Check(driver.HasPendingWorldPhysicalSearch && driver.PlanningPending && prepared.Expanded == 0 && renewed.Expanded == 0 && driver.PhysicalPlanningWork == renewalWork, "Preference renewal invented work or yielded without a retained new queue.");
        driver.ResumeWorldPhysicalSearch("renewed-preferences", () => throw new InvalidOperationException("Renewal restarted its prepared queue."), 512);
        Check(renewed.Expanded == 512 && driver.PhysicalPlanningWork == renewalWork + 512, "The prepared renewal queue did not resume real physical work next poll.");
        Console.WriteLine("PASS retained world physical quanta, final-guide unknown, true finite exhaustion, parent trace and scoped cancellation");
    }

    private static void CheckPolicyPending()
    {
        Ff7AutoplayDriver unowned = new(null!);
        Ff7AutoPlayState unknown = new();
        Check(!Ff7AutoPlayPolicy.TryWaitForWorldPhysicalSearch(unknown, unowned, new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, default, 512), "unknown", () => throw new InvalidOperationException("An unowned quantum was published as retained work.")) && !unowned.PlanningPending && unowned.PhysicalPlanningWork == 0, "A static limited result manufactured a retained policy Pending cursor.");
        foreach (Ff7AutoPlayTargetKind kind in new[] { Ff7AutoPlayTargetKind.WorldMesh, Ff7AutoPlayTargetKind.WorldModel, Ff7AutoPlayTargetKind.WorldControl })
        {
            Ff7AutoplayDriver driver = new(null!);
            Ff7AutoPlayState state = new()
            {
                Active = new("world", "owned", "condition", 1, 5, 4, kind, null, 641, "before"),
                NavigationGoal = new("world", "downstream", "native-condition"),
                FrontierIdleSteps = 123,
                FrontierProgressKey = "unchanged",
                Status = "planning"
            };
            state.WorldVisits["world"] = new() { PhysicalGuards = new(StringComparer.Ordinal) { "existing" } };
            var cursor = Chain(1537, false);
            int published = 0;
            string before = JsonSerializer.Serialize(state);
            for (int quantum = 0; quantum < 2; quantum++)
            {
                var result = driver.ResumeWorldPhysicalSearch("final-guide-null", () => cursor, 512);
                long work = driver.PhysicalPlanningWork;
                Check(Ff7AutoPlayPolicy.TryWaitForWorldPhysicalSearch(state, driver, new(result.Status, default, result.Expanded, result.Reason), "planning", () => published++), "A real pending terrain/model/vehicle quantum fell through to input failure or guide rejection.");
                Ff7AutoPlayPolicy.CommitFrontierInput(state, 641, true, driver.PlanningPending, false);
                Check(driver.PhysicalPlanningWork == work && before == JsonSerializer.Serialize(state), "A policy Pending return changed its owner/goal/guards, charged native frontier input or manufactured work.");
            }
            Check(published == 2 && cursor.Expanded == 1024 && cursor.FrontierCount > 0, "A policy yield discarded or restarted its retained world cursor.");
            Check(!Ff7AutoPlayPolicy.TryWaitForWorldPhysicalSearch(state, driver, new(Ff7AutoPlayWorldNavigationStatus.Blocked, default, 0), "blocked", () => published++), "True physical exhaustion was swallowed as pending scheduling.");
            Check(!Ff7AutoPlayPolicy.TryWaitForWorldPhysicalSearch(state, driver, new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, new(1, 0), 1, HasInput: true), "ready", () => published++), "An admitted partial input was prevented from native execution.");
        }
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_PHYSICAL_SAVE") ?? throw new InvalidOperationException("Select a native world physical-search fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        var world = session.World ?? throw new InvalidOperationException("The fixture has no current native world.");
        int controlled = world.ControlledModel;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        var driver = (Ff7AutoplayDriver)typeof(Ff7AutoPlayController).GetField("driver", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        long? pendingTick = null;
        int? pendingIdle = null;
        for (int poll = 0; poll < 64 && controller.Running && session.World == world && world.ControlledModel == controlled && controller.Steps < 128 && elapsed.Elapsed < TimeSpan.FromSeconds(15); poll++)
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
            if (driver.PlanningPending)
            {
                if (pendingTick == world.Process.Tick && pendingIdle != policy.State.FrontierIdleSteps)
                {
                    throw new InvalidOperationException("A world physical planning poll charged the real-input frontier counter.");
                }
                pendingTick = world.Process.Tick;
                pendingIdle = policy.State.FrontierIdleSteps;
            }
            else
            {
                pendingTick = null;
                pendingIdle = null;
            }
        }
        var retained = (Ff7AutoplayDriver.WorldPhysicalSearchCursor?)typeof(Ff7AutoplayDriver).GetField("worldPhysicalSearch", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(driver);
        bool boarded = session.World == world && world.ControlledModel != controlled;
        if (controller.Error is not null || !boarded && (retained is null || retained.Expanded <= 512 || !driver.PlanningPending) || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException($"Native world continuation failed: callbacks={controller.Steps}, work={driver.PhysicalPlanningWork}, retained={retained?.Expanded}, pending={driver.PlanningPending}, status={policy.Status}, error={controller.Error}");
        }
        Console.WriteLine($"PASS original world physical continuation: boarded={boarded}, callbacks={controller.Steps}, worldTick={world.Process.Tick}, physical={driver.PhysicalPlanningWork}, retained={retained?.Expanded}, frontier={retained?.FrontierCount}, pending={driver.PlanningPending}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
