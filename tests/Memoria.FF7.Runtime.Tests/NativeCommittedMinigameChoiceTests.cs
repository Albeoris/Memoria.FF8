using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeCommittedMinigameChoiceTests
{
    private sealed record Fixture(Ff7SessionSave Source, Ff7AutoPlayState State);

    internal static void Run()
    {
        Fixture source = Create();
        string native = JsonSerializer.Serialize(source.Source);
        string coverage = JsonSerializer.Serialize(source.State.Coverage);
        Check(Observe(source), "An actually owned unfinished native minigame did not commit its source choice.");
        Check(source.State.Choice?.NativeExecutionCommitted == true && source.State.Choice.Parent?.NativeExecutionCommitted == true, "The actual same-root selected choice chain was not retained.");
        Check(native == JsonSerializer.Serialize(source.Source) && coverage == JsonSerializer.Serialize(source.State.Coverage), "Committing observational choice metadata changed native facts or completed-option history.");
        foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
        {
            value => value with { Source = value.Source with { FieldGeneration = 2 } },
            value => value with { Source = value.Source with { Vm = value.Source.Vm with { ProgramIdentity = "other" } } },
            value => value with { Source = value.Source with { Vm = value.Source.Vm with { PendingRequests = [] } } },
            value => value with { Source = value.Source with { Process = value.Source.Process with { GlobalCommand = Ff7GlobalCommand.None } } },
            value => value with { Source = value.Source with { Process = value.Source.Process with { GlobalRequestId = 9 } } },
            value => value with { Source = value.Source with { Process = value.Source.Process with { Receipts = [] } } },
            value => value with { Source = value.Source with { Process = value.Source.Process with { Receipts = [value.Source.Process.Receipts.Single() with { Reply = new(true) }] } } }
        })
        {
            Fixture invalid = invalidate(Create());
            Check(!Observe(invalid) && invalid.State.Choice?.NativeExecutionCommitted != true, "An unowned, missing, completed or foreign native module request committed a choice.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            value => value.State.Active = value.State.Active! with { Accepted = false },
            value => value.State.Active = value.State.Active! with { Returned = true },
            value => value.State.Active = value.State.Active! with { ExecutionProvenance = null },
            value => value.State.Active = value.State.Active! with { ExecutionProvenance = value.State.Active!.ExecutionProvenance! with { RootReturned = true } },
            value => value.State.Active = value.State.Active! with { ExecutionProvenance = value.State.Active!.ExecutionProvenance! with { Owners = [value.State.Active!.ExecutionProvenance!.Owners[0] with { Queued = true }] } },
            value => value.State.PendingRestore = new("before"),
            value => value.State.Choice = value.State.Choice! with { NativeSourceOwned = false },
            value => value.State.Choice = value.State.Choice! with { Parent = value.State.Choice!.Parent! with { NativeSourceOwned = false } },
            value => value.State.Choice = value.State.Choice! with { Parent = value.State.Choice!.Parent! with { BeforeActive = value.State.Active! with { EventId = "foreign" } } },
            value => value.State.Choice = value.State.Choice! with { BeforeActive = value.State.Active! with { ExecutionProvenance = value.State.Active!.ExecutionProvenance! with { RootDispatchTick = 19 } } },
            value => value.Source.Vm.Entities[0] = value.Source.Vm.Entities[0] with { Priority = 6 },
            value => value.Source.Vm.Entities[0] = value.Source.Vm.Entities[0] with { ProgramCounter = 21 },
            value => value.State.Coverage.Choices.Single(item => item.Key == "choice").ActiveOption = null,
            value => value.State.Coverage.Choices.Single(item => item.Key == "choice").ActiveOption = 4
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Observe(invalid) && invalid.State.Choice?.NativeExecutionCommitted != true, "An unsupported native owner, root lifetime, selection or restore debt committed execution.");
        }
        Check(source.State.Coverage.Choices.All(item => item.CompletedOptions.Count == 0), "Native module admission fabricated a completed choice.");
        CheckControlReturn();
        CheckCompletedSourceReturn();
        Console.WriteLine("Owned native minigame choice commitment retains only authenticated live source execution without credit or native mutation.");
    }

    private static void CheckControlReturn()
    {
        Fixture source = Create();
        Check(Observe(source), "The control-return fixture did not observe actual native commitment.");
        PrepareHistory(source);
        Ff7SessionSave current = Returned(source);
        string beforeNative = JsonSerializer.Serialize(current);
        List<Ff7SessionSave> written = [];
        Ff7AutoPlayChoiceWorkflow workflow = new(() => current, Ff7AutoPlayPolicy.CreatePlanner(source.State), source.State, (name, save) =>
        {
            written.Add(save);
            return name;
        }, _ => true, _ => 1110);
        workflow.CompleteAtControlReturn();
        Check(source.State.Choice is null && source.State.PendingRestore is null && written.Count == 2, "A committed native return restored an old optional branch instead of preserving both current continuations.");
        foreach (var coverage in source.State.Coverage.Choices)
        {
            Check(coverage.CompletedOptions.ToHashSet().SetEquals([1, 2]) && coverage.PolicyDeferredOptions.Contains(3) && coverage.ContinuationCheckpoint != "old-wait", "Committed completion erased previous coverage, completed an unexecuted alternative or retained the old wait anchor.");
        }
        Check(written.All(save => save.FieldGeneration == 2 && save.Vm.ProgramIdentity == "returned-program") && beforeNative == JsonSerializer.Serialize(current), "Committed continuation publication wrote historical native data or changed the live returned snapshot.");
        foreach (Func<Ff7SessionSave, Ff7SessionSave> invalidate in new Func<Ff7SessionSave, Ff7SessionSave>[]
        {
            save => save with { Process = save.Process with { GlobalCommand = Ff7GlobalCommand.Battle } },
            save => save with { Process = save.Process with { PlayerLocked = true } },
            save => save with { Process = save.Process with { MenuDisabled = true } },
            save => save with { Process = save.Process with { Dialogues = [new(9, 0, 1, null, null, 0)] } }
        })
        {
            Fixture blocked = Create();
            Observe(blocked);
            PrepareHistory(blocked);
            Ff7SessionSave busy = invalidate(Returned(blocked));
            string history = JsonSerializer.Serialize(blocked.State.Coverage);
            Ff7AutoPlayChoiceWorkflow pending = new(() => busy, Ff7AutoPlayPolicy.CreatePlanner(blocked.State), blocked.State, (_, _) => throw new InvalidOperationException("A pending native boundary wrote a continuation."), _ => true, _ => 1110);
            Check(!pending.CompleteAtControlReturn() && blocked.State.Choice is not null && blocked.State.PendingRestore is null && history == JsonSerializer.Serialize(blocked.State.Coverage), "Pending battle, UI or control ownership completed committed choices.");
        }
        Fixture ordinary = Create();
        PrepareHistory(ordinary);
        Ff7SessionSave ordinaryReturn = Returned(ordinary);
        Ff7AutoPlayChoiceWorkflow normal = new(() => ordinaryReturn, Ff7AutoPlayPolicy.CreatePlanner(ordinary.State), ordinary.State, (name, _) => name, _ => true, _ => 1110);
        normal.CompleteAtControlReturn();
        Check(ordinary.State.PendingRestore?.Path == "before-choice" && ordinary.State.Choice is not null, "An uncommitted optional choice stopped using its normal registered before-choice replay.");
        Fixture orphan = Create();
        FieldHostRequest request = orphan.Source.Vm.PendingRequests!.Single();
        Ff7CondorSnapshot cache = new(request, 6, 0, 1, 0, Ff7CondorPhase.Introduction, [], 0, 0, 0, 0, 0, 0, 0, 96, 0, 96, 16, 0, 0);
        Check(!Observe(orphan with { Source = orphan.Source with { Condor = cache, Vm = orphan.Source.Vm with { PendingRequests = [] } } }), "An orphan minigame cache substituted for actual current native admission.");
    }

    private static void CheckCompletedSourceReturn()
    {
        Fixture source = Create();
        Observe(source);
        PrepareHistory(source);
        var active = source.State.Active!;
        source.State.SourceGoal = new(active.VisitId, "program", active.Generation, active.NavigationKey!, active.EventId, active.ConditionFingerprint, active.Kind, active.Entity, active.Script, 10, Completed: true, AcceptedEntry: 10);
        source.State.Active = null;
        Check(Ff7AutoPlayChoiceWorkflow.HasCompletedCommittedSource(source.State), "The exact completed source did not authenticate its nested committed choices.");
        foreach (Action<Ff7AutoPlayState> invalidate in new Action<Ff7AutoPlayState>[]
        {
            state => state.SourceGoal = state.SourceGoal! with { Completed = false },
            state => state.SourceGoal = state.SourceGoal! with { AcceptedEntry = null },
            state => state.SourceGoal = state.SourceGoal! with { ConditionFingerprint = "foreign" },
            state => state.SourceGoal = state.SourceGoal! with { Entry = 11, AcceptedEntry = 11 },
            state => state.SourceGoal = state.SourceGoal! with { Generation = 2 },
            state => state.SourceGoal = state.SourceGoal! with { ProgramIdentity = "foreign" },
            state => state.SourceGoal = state.SourceGoal! with { NavigationKey = "foreign" },
            state => state.Active = active,
            state => state.Active = active with { CompletionRecorded = true, EventId = "foreign" },
            state => state.Choice = state.Choice! with { Parent = state.Choice!.Parent! with { NativeExecutionCommitted = false } },
            state => state.Choice = state.Choice! with { Parent = state.Choice!.Parent! with { BeforeActive = active with { ExecutionProvenance = active.ExecutionProvenance! with { RootDispatchTick = 99 } } } },
            state => state.PendingRestore = new("old")
        })
        {
            var copy = JsonSerializer.Deserialize<Ff7AutoPlayState>(JsonSerializer.Serialize(source.State))!;
            invalidate(copy);
            Check(!Ff7AutoPlayChoiceWorkflow.HasCompletedCommittedSource(copy), "A foreign, unfinished or uncommitted source retired a choice chain.");
        }
        Ff7SessionSave current = source.Source with { Process = source.Source.Process with { PlayerLocked = true, MenuDisabled = true } };
        string native = JsonSerializer.Serialize(current);
        int writes = 0;
        Ff7AutoPlayChoiceWorkflow workflow = new(() => current, Ff7AutoPlayPolicy.CreatePlanner(source.State), source.State, (_, save) =>
        {
            Check(JsonSerializer.Serialize(save with { AutoPlay = current.AutoPlay }) == native, "Completed-source settlement rewrote native gameplay facts.");
            return "live-" + ++writes;
        }, _ => true, _ => 1110);
        workflow.CompleteAtControlReturn();
        Check(source.State.Choice is null && source.State.PendingRestore is null && writes == 2, "The authenticated completed source left a nested committed choice or queued a replay.");
        Check(source.State.Coverage.Choices.All(choice => choice.CompletedOptions.ToHashSet().SetEquals([1, 2]) && choice.PolicyDeferredOptions.Contains(3)), "Completed-source settlement lost history or credited an unexecuted alternative.");
    }

    private static void PrepareHistory(Fixture source)
    {
        foreach (var coverage in source.State.Coverage.Choices)
        {
            coverage.BeforeCheckpoint = "before-choice";
            coverage.ContinuationCheckpoint = "old-wait";
            coverage.CompletedOptions.Add(1);
        }
    }

    private static Ff7SessionSave Returned(Fixture source)
    {
        Ff7AutoPlayActiveEvent actual = source.State.Active!;
        source.State.Active = actual with { Returned = true, ExecutionProvenance = actual.ExecutionProvenance! with { RootReturned = true, Owners = [] } };
        FieldEntitySnapshot ambient = new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        return source.Source with { FieldGeneration = 2, Vm = source.Source.Vm with { ProgramIdentity = "returned-program", Entities = [ambient], PendingRequests = [] }, Process = new() };
    }

    private static Fixture Create()
    {
        Ff7AutoPlayActiveEvent active = new("visit", "talk", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 1110, "before", Accepted: true, NavigationKey: "nav");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 10, 20) };
        FieldEntitySnapshot entity = new(20, 1, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        entity.SavedProgramCounters[7] = 1;
        FieldHostRequest request = new(8, 0, 1, 20, new FieldMinigameCommand(new(356, 9, 114, 17, 128), 3));
        FieldVariables variables = new();
        variables.WriteWord(FieldBank.Persistent0Word, 0, 1110);
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 30, true, variables.CopyPersistent(), variables.CopyMap(), [entity], PendingRequests: [request]);
        Ff7SessionSave save = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { GlobalCommand = Ff7GlobalCommand.Minigame, GlobalRequestId = 8, Receipts = [new(request, new(false))] } };
        Ff7AutoPlayState state = new() { Active = active };
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.ObserveChoice("parent", 3, 1);
        planner.ObserveChoice("choice", 3, 1);
        foreach (var choice in state.Coverage.Choices)
        {
            choice.ActiveOption = 2;
        }
        Ff7AutoPlayChoiceNativeOwner owner = new(1, "program", 6, 0, 1, 15, entity.SavedProgramCounters.ToArray());
        Ff7AutoPlayChoiceState parent = new("parent", active) { NativeSourceOwned = true, NativeOwner = owner };
        state.Choice = new("choice", active, Parent: parent) { NativeSourceOwned = true, NativeOwner = owner with { RequestId = 7 } };
        return new(save, state);
    }

    private static bool Observe(Fixture source) => Ff7AutoPlayChoiceWorkflow.ObserveOwnedMinigameChoice(source.Source, source.State);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
