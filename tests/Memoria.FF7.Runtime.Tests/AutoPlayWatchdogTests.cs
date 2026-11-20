using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class AutoPlayWatchdogTests
{
    public static void Run()
    {
        PlanningPollsDoNotSpendSemanticCallbacks();
        FieldAllocationsDoNotRenewSemanticProgress();
        FrontierProgressSurvivesLifecycleRestores();
        ReexecutingConsumedEventsDoesNotRenewFrontierBudget();
        OriginalCompletedChoiceLeavesRepeatedFrontier();
        OriginalControlledLadderCannotBypassFrontierBudget();
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
        Check(Ff7AutoPlayController.WorldProgress(speaking) == Ff7AutoPlayController.WorldProgress(speaking with { Dialogue = dialogue with { RequestId = 1000 } }), "Reallocating the same native world dialogue invented progress.");
        Check(Ff7AutoPlayController.WorldProgress(speaking) != Ff7AutoPlayController.WorldProgress(speaking with { Dialogue = dialogue with { TokenPosition = 1 } }), "Native dialogue reveal was omitted from semantic progress.");
        Check(Ff7AutoPlayController.WorldProgress(speaking) != Ff7AutoPlayController.WorldProgress(speaking with { Dialogue = dialogue with { WaitTicks = 1 } }), "Native dialogue waiting was omitted from semantic progress.");
        Console.WriteLine("PASS autoplay semantic watchdog, repeated cycles and bounded host batches.");
    }

    private static void PlanningPollsDoNotSpendSemanticCallbacks()
    {
        Ff7AutoPlaySemanticWatchdog semantic = new();
        Check(semantic.Observe("native A", "frontier0:catalog0"), "The initial semantic boundary must be seeded.");
        for (int poll = 0; poll < Ff7AutoPlayProgressWatchdog.IdleStepLimit * 2; poll++)
        {
            Check(!semantic.Observe("native A", $"uncommitted-poll:{poll}"), "A speculative poll consumed a native callback or published frontier progress.");
        }
        semantic.NativeAdvanced();
        Check(semantic.Observe("native B", "frontier0:catalog0"), "The first real native successor was not observed.");
        Ff7AutoPlayProgressWatchdog computational = new();
        for (int callback = 1; callback < Ff7AutoPlayProgressWatchdog.IdleStepLimit; callback++)
        {
            computational.Observe($"physical-work:{callback}:native-work:{callback * 8}");
            semantic.NativeAdvanced();
            Check(!semantic.Observe(callback % 2 == 0 ? "native A" : "native B", "frontier0:catalog0"), "Growing computation renewed an actual two-state cycle.");
        }
        semantic.NativeAdvanced();
        ExpectTimeout(() => semantic.Observe("native A", "frontier0:catalog0"));
        semantic.Reset();
        for (int callback = 0; callback < Ff7AutoPlayProgressWatchdog.IdleStepLimit * 2; callback++)
        {
            semantic.NativeAdvanced();
            Check(semantic.Observe("native A", $"frontier:{callback / 2}:catalog:{callback % 2}"), "Genuine frontier/catalog progress was lost.");
        }
        foreach (string kind in new[] { "field-script", "dialogue", "battle", "shop", "chocobo" })
        {
            semantic.Reset();
            for (int phase = 0; phase < Ff7AutoPlayProgressWatchdog.IdleStepLimit * 2; phase++)
            {
                semantic.NativeAdvanced();
                Check(semantic.Observe($"{kind}:phase:{phase}", null), "An advancing native owner phase timed out.");
                Check(!semantic.Observe($"{kind}:phase:{phase}", null), "One callback was observed twice.");
            }
        }
    }

    private static void FieldAllocationsDoNotRenewSemanticProgress()
    {
        FieldVmSnapshot vm = new(1, "profile", "program", 0, true, [], [], [new(4, 0, 0, [], [], [])]);
        Ff7SessionSave field = new()
        {
            ResourceProfile = "profile",
            Language = "en",
            FieldName = "field-a",
            FieldGeneration = 1,
            Vm = vm,
            Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] },
            Process = new() { Dialogues = [new(1, 0, 5, null, null, 0)] }
        };
        string original = Ff7AutoPlayController.FieldProgress(field);
        Check(original == Ff7AutoPlayController.FieldProgress(field with { FieldGeneration = 900, Vm = vm with { Tick = 500, NextRequestId = 700 }, Process = field.Process with { Tick = 600, Dialogues = [field.Process.Dialogues[0] with { RequestId = 800 }] } }), "Reallocating an identical field or dialogue invented semantic progress.");
        Ff7AutoPlayProgressWatchdog cycle = new();
        cycle.Observe(original);
        string other = Ff7AutoPlayController.FieldProgress(field with { FieldName = "field-b" });
        cycle.Observe(other);
        for (int step = 1; step < Ff7AutoPlayProgressWatchdog.IdleStepLimit; step++)
        {
            field = field with { FieldGeneration = step + 1000, FieldName = step % 2 == 0 ? "field-a" : "field-b" };
            Check(!cycle.Observe(Ff7AutoPlayController.FieldProgress(field)), "A repeated native field return renewed the semantic no-progress budget.");
        }
        ExpectTimeout(() => cycle.Observe(Ff7AutoPlayController.FieldProgress(field)));
        Check(original != Ff7AutoPlayController.FieldProgress(field with { FieldName = "field-a", Vm = vm with { Entities = [vm.Entities[0] with { WaitCounter = 1 }] } }), "An advancing finite native wait lost its semantic progress.");
        Check(original != Ff7AutoPlayController.FieldProgress(field with { FieldName = "field-a", Progress = field.Progress with { CompletedBattles = [new(1, 1, 1, new(0, 0, 0, []))] } }), "A completed native battle lost its semantic progress.");
        Ff7AutoPlayProgressWatchdog movie = new();
        for (int frame = 0; frame < Ff7AutoPlayProgressWatchdog.IdleStepLimit * 2; frame++)
        {
            Check(movie.Observe(Ff7AutoPlayController.FieldProgress(field with { Process = field.Process with { Movie = new(1, true, frame, false, 1) } })), "An advancing native movie timed out under stable field identity.");
        }
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
        for (int step = 0; step < 4 && controller.Running && policy.State.Active?.TravelRoute is null; step++)
        {
            controller.Advance(1);
        }
        Check(controller.Failure is null, $"Original completed choice failed to reach its recorded navigation path: {controller.Error}");
        Check(policy.State.Active?.TravelRoute is not null, "Original completed choice did not retain its recorded route through another native topology.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The original completed-choice source changed.");
        Console.WriteLine("PASS original completed choice reaches its recorded navigation path through another topology.");
    }

    private static void OriginalControlledLadderCannotBypassFrontierBudget()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CONTROLLED_LADDER_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Memoria.FF7.Resources.Ff7Installation.Discover(game), source);
        Check(session.Host.Process.Actors.Any(actor => actor.PlayerControlled && actor.Ladder is { Completed: false }), "The supplied fixture needs its active player-controlled ladder.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The ladder-budget check must not create choice checkpoints."));
        Ff7AutoPlayPolicy.TrackFrontierProgress(policy.State, session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0));
        policy.State.FrontierIdleSteps = Ff7AutoPlayPolicy.FrontierIdleLimit - 1;
        string native = System.Text.Json.JsonSerializer.Serialize(session.Capture() with { AutoPlay = null });
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        controller.Advance(1);
        Check(controller.Failure is TimeoutException && controller.Error!.Contains("frontier work", StringComparison.Ordinal), "A controlled native ladder bypassed the exhausted semantic frontier budget.");
        Check(session.AutoPlayState!.FrontierIdleSteps == Ff7AutoPlayPolicy.FrontierIdleLimit, "The host checkpoint lost the ladder's exhausted observational budget.");
        Check(System.Text.Json.JsonSerializer.Serialize(session.Capture() with { AutoPlay = null }) == native, "Budget exhaustion advanced or rewrote native execution.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The original ladder fixture changed.");
        Console.WriteLine("PASS original controlled ladder obeys the durable frontier budget without advancing native state.");
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
