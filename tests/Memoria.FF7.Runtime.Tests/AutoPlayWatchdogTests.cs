using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class AutoPlayWatchdogTests
{
    public static void Run()
    {
        FrontierProgressSurvivesLifecycleRestores();
        ReexecutingConsumedEventsDoesNotRenewFrontierBudget();
        OriginalCompletedChoiceLeavesRepeatedFrontier();
        Ff7WorldProcess world = World();
        Ff7AutoPlayProgressWatchdog stalled = new();
        AutoPlayIdleDeadline deadline = new(TimeProvider.System, TimeSpan.FromSeconds(10));
        Check(stalled.Observe(Ff7AutoPlayController.WorldProgress(world)), "The initial native boundary was not admitted.");
        for (int tick = 1; tick < Ff7AutoPlayProgressWatchdog.IdleStepLimit; tick++)
        {
            deadline.Reset();
            world = world with { Tick = tick, Vm = world.Vm with { Tick = tick }, PresentationCounter = (uint)tick };
            Check(!stalled.Observe(Ff7AutoPlayController.WorldProgress(world)), "Raw world ticks or presentation counters were counted as native progress.");
        }
        ExpectTimeout(() => stalled.Observe(Ff7AutoPlayController.WorldProgress(world)));

        Ff7AutoPlayProgressWatchdog cycling = new();
        cycling.Observe("native state A");
        cycling.Observe("native state B");
        for (int step = 1; step < Ff7AutoPlayProgressWatchdog.IdleStepLimit; step++)
        {
            Check(!cycling.Observe(step % 2 == 0 ? "native state A" : "native state B"), "A repeating native script cycle reset its no-progress budget.");
        }
        ExpectTimeout(() => cycling.Observe("native state A"));
        cycling.Reset();
        Check(cycling.Observe("native state A"), "A fresh lifecycle retained an expired semantic boundary.");

        Ff7AutoPlayProgressWatchdog advancing = new();
        for (int step = 0; step < Ff7AutoPlayProgressWatchdog.IdleStepLimit * 3; step++)
        {
            Check(advancing.Observe($"native movie frame:{step}"), "Advancing native presentation or scripted states timed out.");
        }

        string baseline = Ff7AutoPlayController.WorldProgress(world);
        Check(baseline != Ff7AutoPlayController.WorldProgress(world with { Entities = [world.Entities[0] with { X = 1 }] }), "Native world movement was omitted from semantic progress.");
        Check(baseline != Ff7AutoPlayController.WorldProgress(world with { Vm = world.Vm with { System = world.Vm.System with { ProgramCounter = 2 } } }), "Native world script execution was omitted from semantic progress.");
        Check(baseline != Ff7AutoPlayController.WorldProgress(world with { Vm = world.Vm with { System = world.Vm.System with { MoveCounter = 1 } } }), "A native world movement wait was omitted from semantic progress.");
        Check(baseline != Ff7AutoPlayController.WorldProgress(world with { Vm = world.Vm with { System = world.Vm.System with { Calls = [new(3, 1, 1)] } } }), "A native world call continuation was omitted from semantic progress.");
        Ff7Dialogue dialogue = new(1, 0, 0, null, null, 0);
        Ff7WorldProcess speaking = world with { Dialogue = dialogue };
        Check(Ff7AutoPlayController.WorldProgress(speaking) != Ff7AutoPlayController.WorldProgress(speaking with { Dialogue = dialogue with { TokenPosition = 1 } }), "Native dialogue reveal was omitted from semantic progress.");
        Check(Ff7AutoPlayController.WorldProgress(speaking) != Ff7AutoPlayController.WorldProgress(speaking with { Dialogue = dialogue with { WaitTicks = 1 } }), "Native dialogue waiting was omitted from semantic progress.");
        Console.WriteLine("PASS autoplay semantic watchdog, repeated cycles and bounded host batches.");
    }

    private static void FrontierProgressSurvivesLifecycleRestores()
    {
        Ff7AutoPlayState state = new();
        state.Coverage.Visits.Add(new() { Visit = new("field", "program"), Events = [new() { Event = new("native", AutoPlayEventKind.Interaction) }] });
        Ff7AutoPlayPolicy.TrackFrontierProgress(state, 10);
        for (int step = 1; step < Ff7AutoPlayPolicy.FrontierIdleLimit; step++)
        {
            state.Coverage.Visits[0].Events[0].Event = state.Coverage.Visits[0].Events[0].Event with { ConditionSignature = step.ToString() };
            Ff7AutoPlayPolicy.TrackFrontierProgress(state, 10);
        }
        state = state.Clone();
        ExpectTimeout(() => Ff7AutoPlayPolicy.TrackFrontierProgress(state, 10));
        state.Coverage.Visits[0].Events[0].Completed = true;
        Ff7AutoPlayPolicy.TrackFrontierProgress(state, 10);
        Check(state.FrontierIdleSteps == 0, "A newly completed frontier did not reopen the progress budget.");
        Ff7AutoPlayPolicy.TrackFrontierProgress(state, 11);
        Check(state.FrontierIdleSteps == 0, "Native story progress did not reopen the progress budget.");
    }

    private static void ReexecutingConsumedEventsDoesNotRenewFrontierBudget()
    {
        Ff7AutoPlayState state = new();
        state.Coverage.Visits.Add(new() { Visit = new("field", "program"), Events = [new() { Event = new("native", AutoPlayEventKind.Interaction), Completed = true, Conditions = new() { ConsumedEntries = ["entry"] } }] });
        Ff7AutoPlayPolicy.TrackFrontierProgress(state, 10);
        for (int step = 1; step < Ff7AutoPlayPolicy.FrontierIdleLimit; step++)
        {
            state.Coverage.Visits[0].Events[0].Completed = step % 2 == 0;
            state = state.Clone();
            Ff7AutoPlayPolicy.TrackFrontierProgress(state, 10);
        }
        ExpectTimeout(() => Ff7AutoPlayPolicy.TrackFrontierProgress(state, 10));
        state.Coverage.Visits[0].Events[0].Conditions!.ConsumedEntries.Add("new-entry");
        Ff7AutoPlayPolicy.TrackFrontierProgress(state, 10);
        Check(state.FrontierIdleSteps == 0, "A newly authenticated condition did not renew the frontier budget.");
    }

    private static void OriginalCompletedChoiceLeavesRepeatedFrontier()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_COMPLETED_CHOICE_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Ff7AutoPlayActiveEvent previous = source.AutoPlay?.Active ?? throw new InvalidOperationException("The fixture needs its completed native event.");
        Check(previous.CompletionRecorded && previous.Returned, "The fixture must have reached native control return.");
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The completed native choice was unexpectedly reentered."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        controller.Advance(1);
        Check(controller.Failure is null || controller.Failure is NotSupportedException { Message: "No reachable unconsumed native event remains in the observed frontier." }, $"Original completed choice failed: {controller.Error}");
        Check(policy.State.Active?.EventId != previous.EventId, "Original completed choice reopened the same proved native frontier.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The original completed-choice source changed.");
        Console.WriteLine("PASS original completed choice leaves its proved frontier or stops at actual exhaustion.");
    }

    private static Ff7WorldProcess World()
    {
        WorldEntitySnapshot system = new(-1, 1, 0, 0, false, -1, []);
        WorldSnapshot vm = new(1, "fixture", 0, 1, false, [], [], system, []);
        return new(0, 0, 0, vm, [new(0, TerrainResolved: true)], 0, false, 0, 0, null, [], 0, new(), 0, 0, ControlLock: 3, MainState: 1, FadeAlpha: 0, FadeDisplayedAlpha: 0, Initialized: true);
    }

    private static void ExpectTimeout(Action action)
    {
        try
        {
            action();
        }
        catch (TimeoutException)
        {
            return;
        }
        throw new InvalidOperationException("Autoplay continued past its bounded no-progress budget.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
