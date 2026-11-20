using System.Collections;
using System.Reflection;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NavigationModeSchedulingTests
{
    private static Ff7Actor Source => new() { Slot = 0, Entity = 0, Model = 0, Triangle = 0, MoveSpeed = 1 };
    private static FieldInfo Field(string name) => typeof(Ff7AutoplayDriver).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static MethodInfo Method(string name) => typeof(Ff7AutoplayDriver).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static int Parked(Ff7AutoplayDriver driver) => ((IDictionary)Field("parkedNavigationModes").GetValue(driver)!).Count;
    private static int PrefixCount(Ff7AutoplayDriver driver) => (int)Field("obstacleEscape").GetValue(driver)!.GetType().GetProperty("Count")!.GetValue(Field("obstacleEscape").GetValue(driver))!;

    private static Ff7AutoplayDriver.PhysicalSearchCursor Cursor()
    {
        Ff7NativeMoveResult Preview(Ff7Actor actor, Ff7AutoplayInput input)
        {
            return input == new Ff7AutoplayInput(0, 1) && actor.Y4096 < 80000 ? new(actor with { Y4096 = actor.Y4096 + 8 }, false, true, []) : new(actor, false, false, []);
        }
        return new(Source, System.Numerics.Vector3.UnitX, _ => 1, Preview, (_, _) => true);
    }

    internal static void Run()
    {
        CheckElapsedClockOwnership();
        CheckContactSearchPhase();
        CheckCheapReadyWalk();
        CheckFairPendingAndCancellation();
        CheckSelectedWalkPrefix();
        Console.WriteLine("PASS retained run/walk fair shared-quantum scheduling, cancellation, required mode and selected walk prefix");
    }

    private static void CheckElapsedClockOwnership()
    {
        Ff7AutoplayDriver driver = new(null!);
        FieldVmSnapshot vm = new(1, "authored", "program", 7, true, new byte[1280], new byte[256], []);
        Ff7Progress progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [], Clock = new(100, ElapsedPhase: 500, CountdownPhase: 500, CallbackRemainderTicks: 100) };
        Dictionary<bool, Ff7AutoplayDriver.PhysicalSearchCursor> cursors = [];
        Ff7AutoplayInput Choose(bool run)
        {
            if (Field("obstacleSearch").GetValue(driver) is not Ff7AutoplayDriver.PhysicalSearchCursor cursor)
            {
                cursor = Cursor();
                Field("obstacleSearch").SetValue(driver, cursor);
            }
            cursors[run] = cursor;
            driver.ResumePhysicalSearch(cursor);
            driver.DeferPlanning();
            return default;
        }
        Ff7AutoPlayTarget goal = new("goal", 1, 4, Ff7AutoPlayTargetKind.LineTouch, "goal-condition", false, default);
        Ff7AutoPlayTarget optional = new("optional", 16, 1, Ff7AutoPlayTargetKind.ActorTalk, "random-condition", false, default, AnalysisComplete: false);
        FieldScriptAnalysis known = new(true, null, "program", false, [], [], []);
        bool Independent(Ff7AutoPlayTarget selected, params Ff7AutoPlayTarget[] offered) => Ff7AutoplayDriver.ReadNavigationModeClockTargets(selected, offered).All(candidate => Ff7AutoplayDriver.HasClockIndependentModeAnalysis(candidate.Id == optional.Id ? known with { Complete = false } : known));
        bool independent = Independent(goal, goal, optional);
        Check(independent && !Independent(optional, goal, optional), "An unrelated random talk entry invalidated movement ownership, or the selected unknown talk goal escaped its clock contract.");
        foreach (var protectedKind in new[] { Ff7AutoPlayTargetKind.LineTouch, Ff7AutoPlayTargetKind.ActorContact, Ff7AutoPlayTargetKind.AutonomousSpatial })
        {
            Check(!Independent(goal, goal, optional with { Kind = protectedKind }), "An unknown movement-triggered contract was removed from clock ownership.");
        }
        string source = Ff7AutoplayDriver.ReadNavigationModeExecutionFence(progress, vm, independent);
        driver.ChooseNavigationModes(source, "goal", new(), Source, Choose, _ => true);
        var runCursor = cursors[true];
        var walkCursor = cursors[false];
        Ff7Progress advanced = progress with { Clock = Ff7NativeClock.Advance(progress.Clock!, TimeSpan.FromMilliseconds(427123), countUp: false) };
        string before = System.Text.Json.JsonSerializer.Serialize(advanced);
        string later = Ff7AutoplayDriver.ReadNavigationModeExecutionFence(advanced, vm, independent);
        Check(source == later && before == System.Text.Json.JsonSerializer.Serialize(advanced), "Elapsed clock observation changed the source fence or rewrote actual native progress.");
        driver.ChooseNavigationModes(later, "goal", new(), Source, Choose, _ => true);
        Check(ReferenceEquals(runCursor, cursors[true]) && ReferenceEquals(walkCursor, cursors[false]) && walkCursor.Expanded > 0, "Wall-clock advancement at a frozen VM/pose restarted one of the pending mode cursors.");
        Ff7Progress countdown = advanced with { Clock = advanced.Clock! with { CountdownSeconds = 1 } };
        string timer = Ff7AutoplayDriver.ReadNavigationModeExecutionFence(countdown, vm, true);
        Check(timer != later, "Starting an active native countdown preserved inactive-clock mode ownership.");
        driver.ChooseNavigationModes(timer, "goal", new(), Source, Choose, _ => true);
        Check(!ReferenceEquals(runCursor, cursors[true]) && !ReferenceEquals(walkCursor, cursors[false]), "Countdown change resumed stale run/walk graphs.");
        runCursor = cursors[true];
        walkCursor = cursors[false];
        byte[] published = vm.PersistentVariables.ToArray();
        published[18] = 1;
        string publication = Ff7AutoplayDriver.ReadNavigationModeExecutionFence(countdown, vm with { PersistentVariables = published }, true);
        driver.ChooseNavigationModes(publication, "goal", new(), Source, Choose, _ => true);
        Check(publication != timer && !ReferenceEquals(runCursor, cursors[true]) && !ReferenceEquals(walkCursor, cursors[false]), "Actual VM clock publication was masked from mode ownership.");
        Check(Ff7AutoplayDriver.ReadNavigationModeExecutionFence(countdown with { Clock = countdown.Clock! with { CountdownPhase = 1 } }, vm, true) != timer, "Active countdown phase was normalized.");
        Check(Ff7AutoplayDriver.ReadNavigationModeExecutionFence(progress, vm, false) != Ff7AutoplayDriver.ReadNavigationModeExecutionFence(advanced, vm, false), "Unknown time-dependent work ignored the real elapsed clock.");
        byte[] countUp = vm.PersistentVariables.ToArray();
        countUp[95] = 2;
        Check(Ff7AutoplayDriver.ReadNavigationModeExecutionFence(progress, vm with { PersistentVariables = countUp }, true) != Ff7AutoplayDriver.ReadNavigationModeExecutionFence(advanced, vm with { PersistentVariables = countUp }, true), "A native count-up timer was treated as inactive elapsed bookkeeping.");
        FieldScriptAnalysis analysis = new(true, null, "program", false, [], [], []);
        Check(Ff7AutoplayDriver.HasClockIndependentModeAnalysis(analysis) && !Ff7AutoplayDriver.HasClockIndependentModeAnalysis(analysis with { Complete = false }) && !Ff7AutoplayDriver.HasClockIndependentModeAnalysis(analysis with { Reads = [new(FieldBank.Persistent0Word, 15, true)] }) && !Ff7AutoplayDriver.HasClockIndependentModeAnalysis(analysis with { HostReads = [new(FieldHostReadKind.MovieFrame)] }), "An unknown, aliased-clock or time-dependent host query acquired elapsed-independent mode ownership.");
    }

    private static void CheckContactSearchPhase()
    {
        Ff7AutoplayDriver driver = new(null!);
        Field("navigationTalkContactGoal").SetValue(driver, (Func<Ff7Actor, bool>)(_ => false));
        Dictionary<bool, Ff7AutoplayDriver.PhysicalSearchCursor> cursors = [];
        Ff7AutoplayInput Choose(bool run)
        {
            if (!run && !(bool)Field("navigationTalkContactSearchPhase").GetValue(driver)!)
            {
                return new(1, 0, Run: false);
            }
            if (Field("obstacleSearch").GetValue(driver) is not Ff7AutoplayDriver.PhysicalSearchCursor cursor)
            {
                cursor = Cursor();
                Field("obstacleSearch").SetValue(driver, cursor);
            }
            cursors[run] = cursor;
            driver.ResumePhysicalSearch(cursor);
            driver.DeferPlanning();
            return default;
        }
        Ff7AutoplayInput input = driver.ChooseNavigationModes("source", "contact", new(), Source, Choose, _ => true);
        Check(driver.PlanningPending && input == default && Parked(driver) == 2 && cursors.Count == 2, "A nonterminal greedy walk displaced the owned actual-contact search phase.");
        Check(driver.HasRetainedPhysicalSearch, "A real pending mode cursor lost its current-step physical ownership certificate.");
        driver.BeginPlanningStep();
        driver.DeferPlanning();
        Check(!driver.HasRetainedPhysicalSearch, "A generic or stale planning flag inherited physical ownership from the preceding poll.");
        driver.ChooseNavigationModes("source", "contact", new(), Source, Choose, _ => true);
        Check(cursors[true].Expanded > 0 && cursors[false].Expanded > 0 && (bool)Field("navigationTalkContactSearchPhase").GetValue(driver)!, "The actual-contact phase was lost when its sibling mode resumed.");
        input = driver.ChooseNavigationModes("source", "contact", new(), Source, run => !run ? new(Talk: true, Run: false) : Choose(run), _ => true);
        Check(input.Talk && !input.Run && !driver.PlanningPending && Parked(driver) == 0, "A cheap real terminal walk contact was starved by pending run work.");
    }

    private static void CheckCheapReadyWalk()
    {
        Ff7AutoplayDriver driver = new(null!);
        List<bool> calls = [];
        Ff7AutoplayInput Choose(bool run)
        {
            calls.Add(run);
            Check(((((FieldInputState)Field("navigationLineInput").GetValue(driver)!).Held & 0x40) != 0) == run, "The other mode's protected native input leaked into this mode.");
            if (!run)
            {
                return new(1, 0, Run: false);
            }
            Ff7AutoplayDriver.PhysicalSearchCursor cursor = Cursor();
            Field("obstacleSearch").SetValue(driver, cursor);
            Check(driver.ResumePhysicalSearch(cursor).Status == Ff7AutoPlayPhysicalSearchStatus.Pending, "The fake long run search did not remain pending.");
            driver.DeferPlanning();
            return default;
        }
        Ff7AutoplayInput action = driver.ChooseNavigationModes("source", "goal", new(), Source, Choose, _ => true);
        Check(action == new Ff7AutoplayInput(1, 0, Run: false) && !driver.PlanningPending && calls.SequenceEqual(new[] { true, false }), "An unresolved run graph starved a ready native walk input.");
        Check(driver.PhysicalPlanningWork is > 0 and <= 8 && Parked(driver) == 0 && Field("obstacleSearch").GetValue(driver) is null, "Adopting a ready walk retained stale run work or spent more than the shared quantum.");
        calls.Clear();
        action = driver.ChooseNavigationModes("same-source", "required", new(), Source, Choose, _ => true, requiredRun: false);
        Check(!action.Run && calls.SequenceEqual(new[] { false }), "RequiredRun explored an unrequested mode.");
    }

    private static void CheckFairPendingAndCancellation()
    {
        Ff7AutoplayDriver driver = new(null!);
        Dictionary<bool, Ff7AutoplayDriver.PhysicalSearchCursor> cursors = [];
        string point = "point-a";
        Ff7AutoplayInput Choose(bool run)
        {
            Method("UpdateNavigationModeGoal").Invoke(driver, [point]);
            if (Field("obstacleSearch").GetValue(driver) is not Ff7AutoplayDriver.PhysicalSearchCursor cursor)
            {
                cursor = Cursor();
                Field("obstacleSearch").SetValue(driver, cursor);
            }
            cursors[run] = cursor;
            Check(driver.ResumePhysicalSearch(cursor).Status == Ff7AutoPlayPhysicalSearchStatus.Pending, "The finite long scheduling graph completed before its authored boundary.");
            driver.DeferPlanning();
            return default;
        }
        driver.ChooseNavigationModes("source", "goal", new(), Source, Choose, _ => true);
        Ff7AutoplayDriver.PhysicalSearchCursor runCursor = cursors[true];
        Ff7AutoplayDriver.PhysicalSearchCursor walkCursor = cursors[false];
        int runBefore = runCursor.Expanded;
        long before = driver.PhysicalPlanningWork;
        Check(driver.PlanningPending && Parked(driver) == 2 && runBefore > 0 && walkCursor.Expanded == 0, "Both pending mode identities were not retained under one shared quantum.");
        driver.ChooseNavigationModes("source", "goal", new(), Source, Choose, _ => true);
        Check(ReferenceEquals(runCursor, cursors[true]) && ReferenceEquals(walkCursor, cursors[false]) && runCursor.Expanded == runBefore && walkCursor.Expanded > 0 && driver.PhysicalPlanningWork - before <= 8, "Pending resumption restarted a mode or failed to give walk the next quantum.");
        before = driver.PhysicalPlanningWork;
        driver.ChooseNavigationModes("source", "goal", new(), Source, Choose, _ => true);
        Check(runCursor.Expanded > runBefore && driver.PhysicalPlanningWork - before <= 8, "The fair scheduler failed to return real work to run.");
        point = "point-b";
        driver.ChooseNavigationModes("source", "goal", new(), Source, Choose, _ => true);
        Check(!ReferenceEquals(runCursor, cursors[true]) && !ReferenceEquals(walkCursor, cursors[false]), "A changed actual contact goal retained the old mode graphs.");
        runCursor = cursors[true];
        walkCursor = cursors[false];
        driver.ChooseNavigationModes("changed-source", "goal", new(), Source, Choose, _ => true);
        Check(!ReferenceEquals(runCursor, cursors[true]) && !ReferenceEquals(walkCursor, cursors[false]), "A changed native source resumed stale parked physical work.");
        Method("CancelObstacleSearch").Invoke(driver, null);
        Check(Parked(driver) == 0 && Field("obstacleSearch").GetValue(driver) is null, "Explicit cancellation retained a sibling mode cursor.");
    }

    private static void CheckSelectedWalkPrefix()
    {
        Ff7AutoplayDriver driver = new(null!);
        Ff7Actor next = Source with { Y4096 = 8 };
        Type stepType = typeof(Ff7AutoplayDriver).GetNestedType("EscapeStep", BindingFlags.NonPublic)!;
        object queue = Field("obstacleEscape").GetValue(driver)!;
        void Enqueue(Ff7Actor actor)
        {
            object step = Activator.CreateInstance(stepType, new object[] { new Ff7AutoplayInput(0, 1, Run: false), actor.X4096, actor.Y4096, actor.Z4096, actor.Triangle, actor.Direction })!;
            queue.GetType().GetMethod("Enqueue")!.Invoke(queue, [step]);
        }
        driver.ChooseNavigationModes("source-a", "goal", new(), Source, run =>
        {
            if (run)
            {
                Field("obstacleSearch").SetValue(driver, Cursor());
                driver.DeferPlanning();
                return default;
            }
            Enqueue(next);
            Enqueue(next with { Y4096 = 16 });
            return new(0, 1, Run: false);
        }, _ => true);
        Check(PrefixCount(driver) == 2 && Parked(driver) == 0, "The winning walk prefix was discarded with its losing run sibling.");
        bool resumedWalk = false;
        driver.ChooseNavigationModes("source-b", "goal", new(), next, run =>
        {
            resumedWalk = !run;
            Check(!run && PrefixCount(driver) == 2, "Default run displaced a selected live walk prefix after native source advancement.");
            queue.GetType().GetMethod("Dequeue")!.Invoke(queue, null);
            return new(0, 1, Run: false);
        }, _ => true);
        Check(resumedWalk && PrefixCount(driver) == 1, "The continued native walk input lost its remaining prefix.");
        driver.ChooseNavigationModes("source-c", "different-intent", new(), Source, run =>
        {
            Check(PrefixCount(driver) == 0 && Parked(driver) == 0, "A new target intent retained an old selected prefix.");
            return new(0, 1, Run: run);
        }, _ => true);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
