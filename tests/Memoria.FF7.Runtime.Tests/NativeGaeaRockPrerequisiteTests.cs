using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeGaeaRockPrerequisiteTests
{
    private const string CaveProgram = "4155CB665FC5C08C49E5C0EC460727D2313FDAA236B8F631B71F9809968037E5";
    private const string OuterProgram = "9F55F9FE23216E5B695E5522BC8C6B7375882C5016AF3B187F927024EB1A9083";
    private sealed record Planning(Ff7AutoPlayState State, Ff7AutoPlayObservation Observation, Ff7FieldProcess Process, FieldVariables Variables, IReadOnlySet<int> Reachable);

    internal static void Run()
    {
        foreach ((int stage, string expected) in new[] { (0, "gateway:3"), (1, "gateway:2"), (2, "actor:10:script:1") })
        {
            Planning source = CreatePlanning(stage);
            string before = Capture(source);
            Check(Read(source)?.Id == expected, "The actual offered native rock prerequisite did not match the current component.");
            Check(Read(source with { Observation = source.Observation with { Events = [] } }) is null, "A missing actual target was invented by the prerequisite strategy.");
            Check(Read(source with { Observation = source.Observation with { ProgramIdentity = "other" } }) is null, "Another native program inherited a rock prerequisite.");
            Check(Read(source with { Reachable = new HashSet<int>() }) is null, "An unknown current component received a prerequisite.");
            Ff7AutoPlayTarget preferred = Read(source)!;
            Check(Read(source with { Observation = source.Observation with { Events = [preferred with { AnalysisComplete = false }] } }) is null, "An incomplete native target was promoted to a prerequisite.");
            source.Variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 2);
            Check(Read(source) is null, "An already moved native rock received a new prerequisite.");
            source.Variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 0);
            source.State.DestinationField = 690;
            Check(Read(source) is null, "An unrelated explicit destination received rock advice.");
            source.State.DestinationField = 692;
            Check(before == Capture(source), "Prerequisite qualification changed native facts or observation history.");
        }
        CheckRetirement();
        CheckOwnedChoice();
        Console.WriteLine("Reviewed cave rock prerequisites preserve current components, source ownership and actual choice completion.");
    }

    private static void CheckRetirement()
    {
        Planning Create()
        {
            Planning source = CreatePlanning(0);
            Ff7AutoPlayActiveEvent active = new(source.Observation.VisitId, "old", "old-condition", 1, 3, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "before", NavigationKey: source.Observation.NavigationKey);
            source.State.Active = active;
            source.State.NavigationGoal = new(source.Observation.NavigationKey, active.EventId, active.ConditionFingerprint);
            var planner = Ff7AutoPlayPolicy.CreatePlanner(source.State);
            planner.Observe(new(source.Observation.VisitId, CaveProgram), [new(active.EventId, AutoPlayEventKind.Interaction, ConditionSignature: active.ConditionFingerprint)]);
            return source;
        }
        Planning valid = Create();
        string history = JsonSerializer.Serialize(valid.State.Coverage);
        Check(Retire(valid) && valid.State.Active is null && valid.State.NavigationGoal is null && valid.State.DestinationField == 692, "An exact unaccepted transient plan prevented the native prerequisite.");
        Check(history == JsonSerializer.Serialize(valid.State.Coverage), "Plan retirement erased completion or route history.");
        foreach (Action<Ff7AutoPlayState> invalidate in new Action<Ff7AutoPlayState>[]
        {
            state => state.Active = state.Active! with { Accepted = true },
            state => state.Active = state.Active! with { Returned = true },
            state => state.Active = state.Active! with { TransitionOwned = true },
            state => state.Active = state.Active! with { Generation = 2 },
            state => state.Active = state.Active! with { VisitId = "other" },
            state => state.NavigationGoal = new("foreign", "old", "old-condition"),
            state => state.Choice = new("choice", null),
            state => state.PendingRestore = new("before")
        })
        {
            Planning invalid = Create();
            invalidate(invalid.State);
            string before = Capture(invalid);
            Check(!Retire(invalid) && before == Capture(invalid), "Owned work, a foreign source or a choice/restore debt was retired.");
        }
    }

    private static Planning CreatePlanning(int stage)
    {
        Ff7AutoPlayTarget gateway3 = new("gateway:3", -1, -1, Ff7AutoPlayTargetKind.Gateway, "gateway3-condition", false, default, GatewayIndex: 3, DestinationField: 690);
        Ff7AutoPlayTarget gateway2 = new("gateway:2", -1, -1, Ff7AutoPlayTargetKind.Gateway, "gateway2-condition", false, default, GatewayIndex: 2, DestinationField: 691);
        Ff7AutoPlayTarget rock = new("actor:10:script:1", 10, 1, Ff7AutoPlayTargetKind.ActorTalk, "rock-condition", false, default, MayChangeNavigation: true);
        Ff7AutoPlayObservation observation = new("visit", stage == 1 ? OuterProgram : CaveProgram, 1, [gateway3, gateway2, rock], true, true) { NavigationKey = "current" };
        int triangle = stage == 0 ? 163 : stage == 1 ? 0 : 85;
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = stage == 1 ? 5 : 8, Model = 0, PlayerControlled = true, Triangle = triangle }] };
        IReadOnlySet<int> reachable = stage == 1 ? new HashSet<int> { 0, 79 } : new HashSet<int> { triangle };
        return new(new() { DestinationField = 692 }, observation, process, new(), reachable);
    }

    private static Ff7AutoPlayTarget? Read(Planning source) => Ff7AutoPlayPolicy.ReadNativeRockPrerequisite(source.State, source.Observation, source.Process, source.Variables, source.Reachable);
    private static bool Retire(Planning source) => Ff7AutoPlayPolicy.RetireUnacceptedRockPlan(source.State, source.Observation, source.Process, source.Variables, source.Reachable);
    private static string Capture(Planning source) => JsonSerializer.Serialize(new { source.State, source.Observation, source.Process, source.Reachable, Persistent = source.Variables.CopyPersistent(), Map = source.Variables.CopyMap() });

    private static void CheckOwnedChoice()
    {
        FieldEntitySnapshot Ambient() => new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        Ff7AutoPlayActiveEvent active = new("visit", "actor:10:script:1", "rock-condition", 1, 10, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "before", Accepted: true, NavigationKey: "current");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, CaveProgram, 0x0600, 20) };
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 15).Select(_ => Ambient()).ToArray();
        entities[10] = entities[10] with { Priority = 1, ProgramCounter = 0x0628 };
        entities[10].SavedProgramCounters[7] = 0x0600;
        FieldHostRequest request = new(8, 10, 1, 0x0628, new FieldDialogueCommand(0, 1, 1, 2));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, CaveProgram, 30, true, new byte[1280], new byte[256], entities, PendingRequests: [request]);
        Ff7Actor[] rocks = Enumerable.Range(10, 5).Select((entity, slot) => new Ff7Actor { Slot = slot, Entity = entity, Model = slot, Visible = true }).ToArray();
        Ff7SessionSave source = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { PlayerLocked = true, MenuDisabled = true, Actors = rocks } };
        Ff7AutoPlayState state = new() { DestinationField = 692, Active = active };
        Ff7Dialogue dialogue = new(8, 0, 1, 1, 2, 1);
        string original = JsonSerializer.Serialize(new { source, state });
        Check(Ff7AutoPlayChoiceWorkflow.IsNativeRockPrompt(source, state, dialogue), "The actual owned native rock ASK was not recognized.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeRockPrompt(source, state, dialogue with { LastChoice = 3 }) && !Ff7AutoPlayChoiceWorkflow.IsNativeRockPrompt(source, state, dialogue with { Text = 2 }), "An unrelated prompt inherited rock input preference.");
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeRockPrompt(source with { Vm = vm with { PendingRequests = [] } }, state, dialogue), "Missing actual ASK ownership received rock preference.");
        state.Active = active with { Accepted = false };
        Check(!Ff7AutoPlayChoiceWorkflow.IsNativeRockPrompt(source, state, dialogue), "An unadmitted source received rock preference.");
        state.Active = active;
        Check(original == JsonSerializer.Serialize(new { source, state }), "Prompt qualification changed actual native state or source ownership.");

        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        Ff7AutoPlayChoiceWorkflow choosing = new(() => source, planner, state, (name, _) => name, _ => true);
        Check(choosing.ChooseSelection(dialogue) == 1, "The actual rock prompt did not prefer its native Push option.");
        Ff7AutoPlayChoiceState choice = state.Choice ?? throw new InvalidOperationException("The actual native rock choice was not recorded.");
        var coverage = state.Coverage.Choices.Single();
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 2);
        Ff7SessionSave returned = source with
        {
            Vm = vm with { PersistentVariables = variables.CopyPersistent(), Entities = Enumerable.Range(0, 15).Select(_ => Ambient()).ToArray(), PendingRequests = [] },
            Process = new() { Actors = rocks.Select(actor => actor with { Visible = false, CollisionEnabled = false }).ToArray(), Receipts = [new(request, new(true, 1))] }
        };
        Ff7AutoPlayActiveEvent completed = active with { Returned = true, ExecutionProvenance = active.ExecutionProvenance! with { RootReturned = true, Owners = [] } };
        state.Active = completed;
        bool Complete(Ff7SessionSave actual, int? selection = 1) => Ff7AutoPlayChoiceWorkflow.HasNativeRockControlReturn(actual, state, choice, selection);
        Check(Complete(returned), "The real Push answer, rock clearance and whole-root return did not complete selection 1.");
        Check(!Complete(returned, 2) && !Complete(returned, null) && !Complete(source), "A declined, unexecuted or live rock branch received completion credit.");
        Check(!Complete(returned with { Process = returned.Process with { Receipts = [] } }), "Missing actual Push receipt received completion credit.");
        Check(!Complete(returned with { Process = returned.Process with { Receipts = [new(request, new(true, 2))] } }), "The native decline receipt received Push completion credit.");
        Check(!Complete(returned with { Vm = returned.Vm with { PersistentVariables = vm.PersistentVariables } }), "Missing native rock flag received completion credit.");
        Check(!Ff7AutoPlayChoiceWorkflow.HasNativeRockControlReturn(returned, state, choice with { NativeSourceOwned = false }, 1), "An unrelated or legacy unowned choice inherited rock completion.");
        Check(!Complete(returned with { Process = returned.Process with { Actors = [.. returned.Process.Actors.Take(4), returned.Process.Actors[4] with { CollisionEnabled = true }] } }), "An uncleared native obstacle received completion credit.");
        Check(!Complete(returned with { Process = returned.Process with { BattlesDisabled = true } }), "An unfinished native control footer received completion credit.");
        state.Active = completed with { Returned = false };
        Check(!Complete(returned), "A child completion substituted for the actual root return.");
        state.Active = completed with { ExecutionProvenance = completed.ExecutionProvenance! with { RootDispatchTick = 21 } };
        Check(!Complete(returned), "A different native root lifetime completed the old rock choice.");
        state.Active = completed;
        string actualState = JsonSerializer.Serialize(returned);
        Ff7AutoPlayChoiceWorkflow finishing = new(() => returned, planner, state, (name, _) => name, _ => true);
        finishing.CompleteAtControlReturn();
        Check(state.Choice is null && state.PendingRestore is null && coverage.CompletedOptions.Contains(1) && !coverage.CompletedOptions.Contains(2) && coverage.PolicyDeferredOptions.Contains(2), "Rock completion restored an old state or fabricated completion of the declined option.");
        Check(actualState == JsonSerializer.Serialize(returned) && state.Active?.Returned == true && state.Active?.TransitionOwned == false, "Choice accounting changed actual native state or fabricated transition ownership.");

        Ff7AutoPlayChoiceState parent = new("parent", null) { NativeSourceOwned = false };
        state.Active = active;
        state.Choice = parent;
        planner.ObserveChoice(parent.Key, 2, 3);
        Check(choosing.ChooseSelection(dialogue) == 1 && ReferenceEquals(state.Choice?.Parent, parent) && parent.NativeSourceOwned == false, "The nested rock prompt replaced its finite parent or promoted its source ownership.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
