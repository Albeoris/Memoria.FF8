using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class AutoPlayWatchdogTests
{
    public static void Run()
    {
        AuthenticatedReplayedMoviesRenewProgress();
        PlanningPollsDoNotSpendSemanticCallbacks();
        OwnedBlockingAnimationFramesRenewProgress();
        OwnedFadeWaitCountersRenewProgress();
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

    private static void OwnedFadeWaitCountersRenewProgress()
    {
        FieldHostRequest request = new(9, 0, 1, 4, new FieldFadeCommand(0, Wait: true));
        Ff7SessionSave field = new()
        {
            ResourceProfile = "profile",
            Language = "en",
            FieldName = "field",
            Vm = new(1, "profile", "program", 0, true, [], [], [new(4, 1, 0, [], [], [])], PendingRequests: [request]),
            Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] },
            Process = new() { FadeState = new(1, 40, 8, 0, 0, 0, false) }
        };
        Ff7SessionSave AtPosition(Ff7SessionSave state, int position) => state with { Process = state.Process with { FadeState = state.Process.FadeState! with { Position = position } } };
        string initial = Ff7AutoPlayController.FieldProgress(field);
        Check(initial != Ff7AutoPlayController.FieldProgress(AtPosition(field, 32)), "An owned finite fade counter lost semantic progress.");
        foreach (Ff7SessionSave invalid in new[]
        {
            field with { Vm = field.Vm with { PendingRequests = [] } },
            field with { Vm = field.Vm with { PendingRequests = [request with { Entity = 1 }] } },
            field with { Vm = field.Vm with { PendingRequests = [request with { Priority = 2 }] } },
            field with { Vm = field.Vm with { PendingRequests = [request with { ProgramCounter = 5 }] } },
            field with { Vm = field.Vm with { PendingRequests = [request with { Command = new FieldFadeCommand(1) }] } },
            field with { Process = field.Process with { FadeState = field.Process.FadeState! with { Speed = 0 } } }
        })
        {
            Check(Ff7AutoPlayController.FieldProgress(invalid) == Ff7AutoPlayController.FieldProgress(AtPosition(invalid, 32)), "An unowned, stale or frozen fade invented semantic progress.");
        }
        Check(initial == Ff7AutoPlayController.FieldProgress(field with { Vm = field.Vm with { PendingRequests = [request with { Id = 99 }] } }), "Reallocating an unchanged fade wait invented semantic progress.");
        Ff7AutoPlayProgressWatchdog stationary = new();
        stationary.Observe(initial);
        for (int step = 1; step < Ff7AutoPlayProgressWatchdog.IdleStepLimit; step++)
        {
            Check(!stationary.Observe(initial), "An unchanged finite fade renewed the watchdog.");
        }
        ExpectTimeout(() => stationary.Observe(initial));
        Ff7AutoPlayProgressWatchdog repeating = new();
        repeating.Observe(initial);
        string other = Ff7AutoPlayController.FieldProgress(AtPosition(field, 32));
        repeating.Observe(other);
        for (int step = 1; step < Ff7AutoPlayProgressWatchdog.IdleStepLimit; step++)
        {
            Check(!repeating.Observe(step % 2 == 0 ? initial : other), "A repeated fade counter cycle renewed the watchdog.");
        }
        ExpectTimeout(() => repeating.Observe(initial));
    }

    private static void OwnedBlockingAnimationFramesRenewProgress()
    {
        FieldHostRequest request = new(9, 0, 1, 4, new FieldAnimation(5, 1, true, true));
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, Animation = 5, AnimationStatus = 2 };
        Ff7SessionSave field = new()
        {
            ResourceProfile = "profile",
            Language = "en",
            FieldName = "field",
            Vm = new(1, "profile", "program", 0, true, [], [], [new(4, 1, 0, [], [], [])], PendingRequests: [request]),
            Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] },
            Process = new() { Actors = [actor], AnimationRequests = [new(9, 0)] }
        };
        string initial = Ff7AutoPlayController.FieldProgress(field);
        Ff7AutoPlayProgressWatchdog watchdog = new();
        for (int frame = 0; frame < Ff7AutoPlayProgressWatchdog.IdleStepLimit * 2; frame++)
        {
            Check(watchdog.Observe(Ff7AutoPlayController.FieldProgress(field with { Process = field.Process with { Actors = [actor with { AnimationFrame16 = frame }] } })), "A genuine owned blocking animation stalled at a fixed pose and script PC.");
        }
        foreach (Ff7SessionSave unowned in new[]
        {
            field with { Vm = field.Vm with { PendingRequests = [] } },
            field with { Vm = field.Vm with { PendingRequests = [request with { Priority = 2 }] } },
            field with { Vm = field.Vm with { PendingRequests = [request with { ProgramCounter = 5 }] } },
            field with { Vm = field.Vm with { PendingRequests = [request with { Command = new FieldAnimation(5, 1, false, true) }] } },
            field with { Process = field.Process with { AnimationRequests = [new(99, 0)] } },
            field with { Process = field.Process with { AnimationRequests = [new(9, 1)] } },
            field with { Process = field.Process with { Actors = [actor with { EntityBound = false }] } }
        })
        {
            string baseline = Ff7AutoPlayController.FieldProgress(unowned);
            Check(baseline == Ff7AutoPlayController.FieldProgress(unowned with { Process = unowned.Process with { Actors = [unowned.Process.Actors[0] with { AnimationFrame16 = 16 }] } }), "Ambient, stale or foreign animation frames invented semantic progress.");
            Check(initial != baseline, "Unowned animation retained the genuine blocking phase witness.");
        }
        Check(initial == Ff7AutoPlayController.FieldProgress(field with { Vm = field.Vm with { PendingRequests = [request with { Id = 99 }] }, Process = field.Process with { AnimationRequests = [new(99, 0)] } }), "Reallocating an otherwise identical blocking request invented progress.");
    }

    private static Ff7SessionSave MovieState()
    {
        FieldHostRequest request = new(1, 0, 6, 10, new FieldMovieCommand(FieldMovieOperation.Play));
        return new()
        {
            ResourceProfile = "profile",
            Language = "en",
            FieldName = "transport",
            FieldGeneration = 1,
            Vm = new(1, FieldMachine.SemanticProfile, "movie-program", 1, true, new byte[1280], new byte[256], [new(10, 6, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())], PendingRequests: [request]),
            Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] },
            Process = new() { Movie = new(15, true, 0, false, 1), GlobalCommand = Ff7GlobalCommand.Movie, GlobalRequestId = 1 }
        };
    }

    private static void AuthenticatedReplayedMoviesRenewProgress()
    {
        CheckReplayedMovie(MovieState(), 49);
        Ff7SessionSave state = MovieState();
        object machine = new();
        Ff7AutoPlayMovieProgress movie = new();
        Check(!movie.Observe(state, machine), "An occurrence allocation alone invented movie progress.");
        Check(movie.Observe(state with { Process = state.Process with { Movie = state.Process.Movie! with { Frame = 1 } } }, machine), "Actual increasing frame did not renew its authenticated movie owner.");
        Check(!movie.Observe(state, machine) && !movie.Observe(state with { Process = state.Process with { Movie = state.Process.Movie! with { Frame = 1 } } }, machine), "Backward/repeated frames renewed progress.");
        Check(!movie.Observe(state with { FieldGeneration = 2 }, machine), "A field occurrence alone renewed movie progress.");
        Check(!movie.Observe(state with { FieldGeneration = 2 }, new object()), "A new machine alone renewed movie progress.");
        Ff7SessionSave newRequest = state with { Vm = state.Vm with { PendingRequests = [state.Vm.PendingRequests![0] with { Id = 2 }] }, Process = state.Process with { Movie = state.Process.Movie! with { RequestId = 2 }, GlobalRequestId = 2 } };
        Check(!movie.Observe(newRequest, machine), "A new request alone renewed movie progress.");
        foreach (Ff7SessionSave wrong in new[]
        {
            state with { Process = state.Process with { GlobalCommand = Ff7GlobalCommand.None } },
            state with { Process = state.Process with { GlobalRequestId = 2 } },
            state with { Process = state.Process with { Movie = state.Process.Movie! with { Playing = false } } },
            state with { Process = state.Process with { Movie = state.Process.Movie! with { Completed = true } } },
            state with { Vm = state.Vm with { PendingRequests = [] } },
            state with { Vm = state.Vm with { Entities = [state.Vm.Entities[0] with { Priority = 7 }] } },
            state with { Vm = state.Vm with { Entities = [state.Vm.Entities[0] with { ProgramCounter = 11 }] } },
            state with { Vm = state.Vm with { PendingRequests = [state.Vm.PendingRequests![0] with { Command = new FieldMovieCommand(FieldMovieOperation.Prepare, 15) }] } }
        })
        {
            movie.Reset();
            Check(!movie.Observe(wrong, machine) && !movie.Observe(wrong with { Process = wrong.Process with { Movie = wrong.Process.Movie! with { Frame = 1 } } }, machine), "An unowned/wrong native movie request renewed progress.");
        }
        Ff7AutoPlayProgressWatchdog frozen = new();
        movie.Reset();
        frozen.Observe(Ff7AutoPlayController.FieldProgress(state), movie.Observe(state, machine));
        for (int step = 1; step < Ff7AutoPlayProgressWatchdog.IdleStepLimit; step++)
        {
            Ff7SessionSave reallocated = state with { FieldGeneration = step + 1 };
            Check(!frozen.Observe(Ff7AutoPlayController.FieldProgress(reallocated), movie.Observe(reallocated, machine)), "Frozen movie owner allocations bypassed the unchanged watchdog.");
        }
        ExpectTimeout(() => frozen.Observe(Ff7AutoPlayController.FieldProgress(state), movie.Observe(state, machine)));
        Ff7AutoPlayProgressWatchdog unowned = new();
        for (int frame = 0; frame < 49; frame++)
        {
            unowned.Observe(Ff7AutoPlayController.FieldProgress(state with { Process = state.Process with { Movie = state.Process.Movie! with { Frame = frame } } }));
        }
        movie.Reset();
        ExpectTimeout(() =>
        {
            for (int frame = 0; frame < 49; frame++)
            {
                Ff7SessionSave wrong = state with { Process = state.Process with { GlobalRequestId = 2, Movie = state.Process.Movie! with { Frame = frame } } };
                for (int poll = 0; poll < 40; poll++)
                {
                    unowned.Observe(Ff7AutoPlayController.FieldProgress(wrong), movie.Observe(wrong, machine));
                }
            }
        });
    }

    private static void CheckReplayedMovie(Ff7SessionSave source, int frameCount)
    {
        Ff7AutoPlayProgressWatchdog old = new();
        Ff7AutoPlayProgressWatchdog current = new();
        Ff7AutoPlaySemanticWatchdog semantic = new();
        Ff7AutoPlayMovieProgress movie = new();
        object first = new();
        object second = new();
        int initial = source.Process.Movie!.Frame;
        for (int frame = initial; frame < initial + frameCount; frame++)
        {
            Ff7SessionSave phase = source with { Process = source.Process with { Movie = source.Process.Movie with { Frame = frame } } };
            string progress = Ff7AutoPlayController.FieldProgress(phase);
            old.Observe(progress);
            current.Observe(progress, movie.Observe(phase, first));
            semantic.NativeAdvanced();
            semantic.Observe(progress, null);
        }
        bool oldFailed = false;
        for (int frame = initial; frame < initial + frameCount; frame++)
        {
            Ff7SessionSave phase = source with { FieldGeneration = source.FieldGeneration + 1, Process = source.Process with { Movie = source.Process.Movie with { Frame = frame } } };
            string progress = Ff7AutoPlayController.FieldProgress(phase);
            for (int poll = 0; poll < 40; poll++)
            {
                if (!oldFailed)
                {
                    try
                    {
                        old.Observe(progress);
                    }
                    catch (TimeoutException)
                    {
                        oldFailed = true;
                    }
                }
                bool advanced = movie.Observe(phase, second);
                current.Observe(progress, advanced);
                semantic.NativeAdvanced();
                semantic.Observe(progress, null, advanced);
            }
        }
        Check(oldFailed, "The warm repeated-frame case did not reproduce the old guard's false positive.");
        Check(!current.Observe(Ff7AutoPlayController.FieldProgress(source)), "Movie progress erased the general seen-state history.");
    }

    internal static void RunMovieOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MOVIE_PROGRESS_SAVE") ?? throw new InvalidOperationException("Select an authenticated native movie-phase fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        CheckReplayedMovie(source, 49);
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The native movie phase fixture changed.");
        Console.WriteLine("PASS original-backed repeated owned movie phase observations: old guard fails, advancing authenticated replay survives; native source never restored/mutated and file unchanged.");
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
