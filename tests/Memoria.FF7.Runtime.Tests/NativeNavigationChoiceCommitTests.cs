using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeNavigationChoiceCommitTests
{
    private sealed record Fixture(Ff7SessionSave Current, Ff7AutoPlayState State);

    internal static void Run()
    {
        Fixture source = Create();
        string native = JsonSerializer.Serialize(source.Current);
        string history = JsonSerializer.Serialize(source.State.Coverage);
        Check(Observe(source), "An actually owned requested field transfer did not commit its selected choice chain.");
        Check(source.State.Choice is { NativeExecutionCommitted: true, Parent.NativeExecutionCommitted: true }, "A nested same-root navigation choice was left replayable.");
        Check(native == JsonSerializer.Serialize(source.Current) && history == JsonSerializer.Serialize(source.State.Coverage), "Navigation commitment changed native wallet, execution or completed-option history.");
        foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
        {
            value => value with { Current = value.Current with { FieldGeneration = 1 } },
            value => value with { Current = value.Current with { FieldGeneration = 3 } }
        })
        {
            Check(!Observe(invalidate(Create())), "An unrelated field allocation borrowed the source MAP choice.");
        }
        Check(!Ff7AutoPlayChoiceWorkflow.ObserveOwnedFieldTransitionChoice(Create().Current, Create().State, 325), "Another destination received the recorded MAP commitment.");
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            value => value.State.Active = value.State.Active! with { Accepted = false },
            value => value.State.Active = value.State.Active! with { TransitionOwned = false },
            value => value.State.Active = value.State.Active! with { ConditionFingerprint = "foreign" },
            value => value.State.Active = value.State.Active! with { ExecutionProvenance = null },
            value => value.State.Active = value.State.Active! with { ExecutionProvenance = value.State.Active!.ExecutionProvenance! with { AcceptedTransition = null } },
            value => value.State.Active = value.State.Active! with { ExecutionProvenance = value.State.Active!.ExecutionProvenance! with { AcceptedTransitionTick = 19 } },
            value => value.State.Active = value.State.Active! with { ExecutionProvenance = value.State.Active!.ExecutionProvenance! with { Owners = [value.State.Active!.ExecutionProvenance!.Owners[0] with { Queued = true }] } },
            value => value.State.Choice = value.State.Choice! with { NativeNavigationRequested = false },
            value => value.State.Choice = value.State.Choice! with { NativeNavigationRequested = null },
            value => value.State.Choice = value.State.Choice! with { NativeSourceOwned = false },
            value => value.State.Choice = value.State.Choice! with { NativeOwner = value.State.Choice!.NativeOwner! with { ProgramIdentity = "foreign" } },
            value => value.State.Choice = value.State.Choice! with { Parent = value.State.Choice!.Parent! with { NativeNavigationRequested = false } },
            value => value.State.Choice = value.State.Choice! with { Parent = value.State.Choice!.Parent! with { BeforeActive = value.State.Active! with { EventId = "foreign" } } },
            value => value.State.Coverage.Choices.Single(item => item.Key == "choice").ActiveOption = null,
            value => value.State.PendingRestore = new("before")
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Observe(invalid) && invalid.State.Choice?.NativeExecutionCommitted != true, "An unowned, exploratory, incomplete or mismatched choice acquired navigation commitment.");
        }
        Check(Observe(Create(marker: null, paid: true)), "An old admitted paid-transfer choice lost its exact current debit and retained goal evidence.");
        Check(!Observe(Create(marker: false, paid: true)), "Explicit exploration was overridden by a historical paid transport marker.");
        Fixture noGoal = Create(marker: null, paid: true);
        noGoal.State.NavigationGoal = null;
        Check(!Observe(noGoal), "A paid transfer without retained navigation intent became irreversible.");
        Fixture wrongCash = Create(marker: null, paid: true);
        Check(!Observe(wrongCash with { Current = wrongCash.Current with { Progress = wrongCash.Current.Progress with { Gil = 89 } } }), "A different native debit acquired legacy navigation commitment.");
        Fixture unpaid = Create(marker: null, paid: true);
        unpaid.State.Active = unpaid.State.Active! with { TransportEntry = unpaid.State.Active!.TransportEntry! with { Cost = 0 } };
        Check(!Observe(unpaid), "A zero-cost or mismatched transport marker acquired legacy paid-route authority.");
        CheckControlReturn(source);
        CheckMenuDisabledNavigationReturn();
        Console.WriteLine("Requested owned field-transition choices retain the current native continuation without replaying paid travel.");
    }

    private static void CheckMenuDisabledNavigationReturn()
    {
        Fixture source = Create();
        Check(Observe(source), "The menu-disabled return fixture did not authenticate its actual MAP.");
        source.State.Active = source.State.Active! with { CompletionRecorded = true };
        Ff7SessionSave current = source.Current with { Process = source.Current.Process with { MenuDisabled = true } };
        Check(Ff7AutoPlayChoiceWorkflow.CanSettleOwnedNavigationChoice(current, source.State, 324), "An accounted actual field transfer confused disabled menu opening with a pending menu.");
        Check(!source.State.Active!.Returned && source.State.Active.ExecutionProvenance!.AcceptedTransition is not null, "The fixture lost its real MAP boundary before source RET.");
        foreach (var choice in source.State.Coverage.Choices)
        {
            choice.BeforeCheckpoint = "before";
            choice.ContinuationCheckpoint = "historical-primary";
            choice.CompletedOptions.Add(1);
        }
        string native = JsonSerializer.Serialize(current);
        int writes = 0;
        Ff7AutoPlayChoiceWorkflow workflow = new(() => current, Ff7AutoPlayPolicy.CreatePlanner(source.State), source.State, (_, save) =>
        {
            Check(save.Process.MenuDisabled && save.Progress.Gil == current.Progress.Gil && save.FieldGeneration == current.FieldGeneration, "Settling navigation changed native menu state, wallet or field allocation.");
            return "live-" + ++writes;
        }, _ => true, _ => 1299);
        workflow.CompleteAtControlReturn();
        Check(source.State.Choice is null && source.State.PendingRestore is null && writes == 2 && source.State.Coverage.Choices.All(choice => choice.CompletedOptions.ToHashSet().SetEquals([1, 2]) && choice.PolicyDeferredOptions.Contains(3)), "Disabled menu opening prevented real navigation-choice settlement or credited another option.");
        Check(native == JsonSerializer.Serialize(current), "Navigation settlement mutated its live native continuation.");
        foreach (Func<Ff7SessionSave, Ff7SessionSave> busy in new Func<Ff7SessionSave, Ff7SessionSave>[]
        {
            save => save with { Process = save.Process with { PlayerLocked = true } },
            save => save with { Process = save.Process with { GlobalCommand = Ff7GlobalCommand.Minigame } },
            save => save with { Process = save.Process with { Menu = new(99, 6, 0) } },
            save => save with { Process = save.Process with { Dialogues = [new(99, 0, 1, null, null, 0)] } }
        })
        {
            Fixture pending = Create();
            Check(Observe(pending), "A pending-return fixture lost its actual MAP evidence.");
            pending.State.Active = pending.State.Active! with { CompletionRecorded = true };
            Ff7SessionSave blocked = busy(pending.Current with { Process = pending.Current.Process with { MenuDisabled = true } });
            string coverage = JsonSerializer.Serialize(pending.State.Coverage);
            Ff7AutoPlayChoiceWorkflow waiting = new(() => blocked, Ff7AutoPlayPolicy.CreatePlanner(pending.State), pending.State, (_, _) => throw new InvalidOperationException("An actual pending UI/control boundary published a navigation continuation."), _ => true, _ => 1299);
            Check(!waiting.CompleteAtControlReturn() && pending.State.Choice is not null && pending.State.PendingRestore is null && coverage == JsonSerializer.Serialize(pending.State.Coverage), "Navigation settlement bypassed actual pending UI, global work or player ownership.");
        }
        Fixture incomplete = Create();
        Check(Observe(incomplete) && !Ff7AutoPlayChoiceWorkflow.CanSettleOwnedNavigationChoice(current, incomplete.State, 324), "An unaccounted source completion bypassed the disabled-menu guard.");
    }

    private static void CheckControlReturn(Fixture source)
    {
        foreach (var choice in source.State.Coverage.Choices)
        {
            choice.BeforeCheckpoint = "before";
            choice.ContinuationCheckpoint = "historical-primary";
            choice.CompletedOptions.Add(1);
        }
        source.State.Active = source.State.Active! with { Returned = true, ExecutionProvenance = source.State.Active!.ExecutionProvenance! with { RootReturned = true, Owners = [] } };
        string native = JsonSerializer.Serialize(source.Current);
        List<Ff7SessionSave> writes = [];
        Ff7AutoPlayChoiceWorkflow workflow = new(() => source.Current, Ff7AutoPlayPolicy.CreatePlanner(source.State), source.State, (name, save) =>
        {
            writes.Add(save);
            return name;
        }, _ => true, _ => 1299);
        workflow.CompleteAtControlReturn();
        Check(source.State.Choice is null && source.State.PendingRestore is null && writes.Count == 2, "A real navigation return queued a historical branch restore.");
        Check(source.State.Coverage.Choices.All(choice => choice.CompletedOptions.ToHashSet().SetEquals([1, 2]) && choice.PolicyDeferredOptions.Contains(3)), "Navigation completion credited an unexecuted option or erased previous coverage.");
        Check(writes.All(save => save.FieldGeneration == 2 && save.Progress.Gil == source.Current.Progress.Gil) && native == JsonSerializer.Serialize(source.Current), "Navigation continuation publication rewrote current native progress or wallet.");
    }

    private static Fixture Create(bool? marker = true, bool paid = false)
    {
        Ff7AutoPlayTransportEntry? transport = paid ? new("paid-region", 10, 100, 324) : null;
        Ff7AutoPlayActiveEvent active = new("visit", "talk", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 1299, "before", Accepted: true, NavigationKey: "nav", TransportEntry: transport);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "source-program", 10, 20) };
        Ff7AutoPlayActiveEvent before = active;
        FieldHostRequest map = new(8, 0, 1, 20, new FieldTransitionCommand(324, 10, 20, 2, 64));
        active = Ff7AutoPlayExecutionOwners.Observe(active, 1, "source-program", 30, [new(HostOperation: new(map, new(true)))], (_, _) => 10) with { TransitionOwned = true };
        FieldEntitySnapshot entity = new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "destination-program", 1, true, new byte[1280], new byte[256], [entity], PendingRequests: []);
        Ff7SessionSave current = new() { ResourceProfile = "test", Language = "test", FieldName = "destination", FieldGeneration = 2, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [], Gil = paid ? 90u : 100u }, Vm = vm, Process = new() };
        Ff7AutoPlayState state = new() { Active = active, NavigationGoal = new("nav", active.EventId, active.ConditionFingerprint) };
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.ObserveChoice("parent", 3, 1);
        planner.ObserveChoice("choice", 3, 1);
        foreach (var choice in state.Coverage.Choices)
        {
            choice.ActiveOption = 2;
        }
        Ff7AutoPlayChoiceNativeOwner owner = new(1, "source-program", 6, 0, 1, 15, new int[8]);
        Ff7AutoPlayChoiceState parent = new("parent", before) { NativeSourceOwned = true, NativeOwner = owner, NativeNavigationRequested = marker };
        state.Choice = new("choice", before, parent) { NativeSourceOwned = true, NativeOwner = owner with { RequestId = 7 }, NativeNavigationRequested = marker };
        return new(current, state);
    }

    private static bool Observe(Fixture source) => Ff7AutoPlayChoiceWorkflow.ObserveOwnedFieldTransitionChoice(source.Current, source.State, 324);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
