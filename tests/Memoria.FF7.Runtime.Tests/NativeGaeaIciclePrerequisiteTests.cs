using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeGaeaIciclePrerequisiteTests
{
    private const string Program = "1B72A6B868085A718BB72B39B6C756AD76FA71966D6C9B6DF1865A7A50851091";

    internal static void Run()
    {
        CheckPlanning();
        CheckLowerBridge();
        CheckChoice();
        Console.WriteLine("Reviewed icicle prerequisites select actual missing sources and preserve native prompt ownership.");
    }

    private static void CheckPlanning()
    {
        Ff7AutoPlayTarget[] targets = Enumerable.Range(3, 3).Select(entity => new Ff7AutoPlayTarget($"line:{entity - 3}:entity:{entity}:script:5", entity, 5, Ff7AutoPlayTargetKind.LineEnter, $"condition-{entity}", false, default, DestinationField: 693, MayChangeNavigation: true)).ToArray();
        Ff7AutoPlayObservation observation = new("visit", Program, 1, targets, true, true) { NavigationKey = "upper" };
        Ff7AutoPlayState state = new() { DestinationField = 694 };
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 10, Model = 0, PlayerControlled = true, Triangle = 39 }] };
        FieldVariables variables = new();
        IReadOnlySet<int> reachable = new HashSet<int> { 39, 36, 37, 33, 35, 28, 29 };
        Ff7AutoPlayTarget? Read(Ff7AutoPlayObservation? observed = null, IReadOnlySet<int>? component = null) => Ff7AutoPlayPolicy.ReadNativeIciclePrerequisite(state, observed ?? observation, process, variables, component ?? reachable);
        string before = JsonSerializer.Serialize(new { state, observation, process });
        foreach ((byte flags, int entity) in new[] { ((byte)194, 3), ((byte)198, 4), ((byte)206, 5) })
        {
            variables.WriteByte(FieldBank.Persistent0Byte, 0x83, flags);
            Check(Read()?.Entity == entity, "The first missing native icicle was not selected in source order.");
        }
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 28);
        Check(Read() is null, "Completed required icicles replayed an optional fight.");
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 0);
        Check(Read(observation with { Events = targets.Skip(1).ToArray() }) is null, "A missing actual first source was replaced by an invented prerequisite.");
        Check(Read(observation with { ProgramIdentity = "other" }) is null, "Another program inherited icicle advice.");
        Check(Read(component: new HashSet<int> { 39, 3 }) is null && Read(component: new HashSet<int> { 3 }) is null, "The lower or mixed component received upper-icicle advice.");
        Check(Read(observation with { Events = [targets[0] with { AnalysisComplete = false }] }) is null, "Incomplete source analysis received priority.");
        state.DestinationField = 693;
        Check(Read() is null, "Another destination received icicle prerequisites.");
        state.DestinationField = 694;
        state.Active = new("visit", targets[0].Id, targets[0].ConditionFingerprint, 1, 3, 5, Ff7AutoPlayTargetKind.LineEnter, null, 0, "before", Accepted: true, NavigationKey: "upper");
        Check(Read() is null, "An accepted native owner was preempted by prerequisite discovery.");
        state.Active = null;
        Check(before == JsonSerializer.Serialize(new { state, observation, process }), "Prerequisite inspection mutated native state or retained history.");
        Ff7AutoPlayActiveEvent old = new("visit", "old", "old-condition", 1, 8, 2, Ff7AutoPlayTargetKind.LinePush, null, 0, "before", NavigationKey: "upper");
        state.Active = old;
        state.NavigationGoal = new("upper", old.EventId, old.ConditionFingerprint);
        string history = JsonSerializer.Serialize(state.Coverage);
        Check(Ff7AutoPlayPolicy.RetireUnacceptedIciclePlan(state, observation, process, variables, reachable) && state.Active is null && state.NavigationGoal is null && state.DestinationField == 694, "An exact unaccepted conflicting plan prevented actual icicle selection.");
        Check(history == JsonSerializer.Serialize(state.Coverage), "Retiring transient advice erased historical accounting.");
        foreach (Ff7AutoPlayActiveEvent protectedActive in new[] { old with { Accepted = true }, old with { Returned = true }, old with { Generation = 2 }, old with { NavigationKey = "other" } })
        {
            state.Active = protectedActive;
            Check(!Ff7AutoPlayPolicy.RetireUnacceptedIciclePlan(state, observation, process, variables, reachable) && state.Active == protectedActive, "Owned or foreign source state was retired by an icicle preference.");
        }
    }

    private static void CheckChoice()
    {
        FieldEntitySnapshot Ambient() => new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        Ff7AutoPlayActiveEvent active = new("visit", "line:0:entity:3:script:5", "ice-condition", 1, 3, 5, Ff7AutoPlayTargetKind.LineEnter, null, 0, "before", Accepted: true, NavigationKey: "upper");
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, 0x054F, 20) };
        active = active with { ExecutionProvenance = active.ExecutionProvenance! with { Owners = [.. active.ExecutionProvenance.Owners, new(10, 6, 3, 0x06AF, 3, 1, 0x057C, 21, false)] } };
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 15).Select(_ => Ambient()).ToArray();
        entities[3] = entities[3] with { Priority = 1, ProgramCounter = 0x057C };
        entities[3].SavedProgramCounters[7] = 0x054F;
        entities[10] = entities[10] with { Priority = 6, ProgramCounter = 0x06BD };
        entities[10].SavedProgramCounters[7] = 0x0600;
        entities[10].SyncStates[6] = FieldSyncState.Waiting;
        entities[10].SyncOwners[6] = 3;
        FieldHostRequest request = new(8, 10, 6, 0x06BD, new FieldDialogueCommand(0, 3, 1, 2));
        FieldVariables variables = new();
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 4);
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 30, true, variables.CopyPersistent(), variables.CopyMap(), entities, PendingRequests: [request]);
        Ff7SessionSave source = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] }, Vm = vm, Process = new() { PlayerLocked = true, MenuDisabled = true, Actors = [new() { Slot = 0, Entity = 10, Model = 0, PlayerControlled = true, Triangle = 39 }] } };
        Ff7AutoPlayState state = new() { DestinationField = 694, Active = active };
        Ff7Dialogue dialogue = new(8, 0, 3, 1, 2, 1);
        int? Read(Ff7SessionSave? actual = null, Ff7Dialogue? prompt = null) => Ff7AutoPlayChoiceWorkflow.ReadNativeIciclePreference(actual ?? source, state, prompt ?? dialogue);
        string before = JsonSerializer.Serialize(new { source, state });
        Check(Read() == 1, "The first successful icicle did not preserve the upper route with No.");
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 28);
        Check(Read(source with { Vm = vm with { PersistentVariables = variables.CopyPersistent() } }) == 2, "Three actual icicle flags did not prefer the native downward return.");
        Check(Read(source with { Vm = vm with { ProgramIdentity = "other" } }) is null, "Another program inherited the prompt strategy.");
        Check(Read(source with { Vm = vm with { PendingRequests = [] } }) is null, "A missing actual ASK received a choice preference.");
        Check(Read(prompt: dialogue with { Window = 1 }) is null && Read(prompt: dialogue with { Text = 4 }) is null && Read(prompt: dialogue with { LastChoice = 3 }) is null, "Another prompt header inherited icicle preference.");
        Check(Read(source with { Vm = vm with { PendingRequests = [request with { Priority = 1 }] } }) is null, "A wrong-priority pending request borrowed the live child owner.");
        state.Active = active with { Accepted = false };
        Check(Read() is null, "An unadmitted root acquired the icicle choice.");
        state.Active = active with { ExecutionProvenance = active.ExecutionProvenance! with { Owners = [] } };
        Check(Read() is null, "Cleared observational ownership resurrected an unrelated pending child.");
        state.Active = active;
        Check(before == JsonSerializer.Serialize(new { source, state }), "Choice qualification changed native flags, script state or coverage.");
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        Ff7AutoPlayChoiceWorkflow choosing = new(() => source, planner, state, (name, _) => name, _ => true);
        Check(choosing.ChooseSelection(dialogue) == 1, "An actual owned first-icicle ASK did not select No.");
        Ff7AutoPlayChoiceState choice = state.Choice ?? throw new InvalidOperationException("The actual choice was not registered.");
        Ff7AutoPlayActiveEvent returnedActive = active with { Returned = true, ExecutionProvenance = active.ExecutionProvenance! with { RootReturned = true, Owners = [] } };
        Ff7SessionSave returned = source with
        {
            Vm = vm with { Entities = Enumerable.Range(0, 15).Select(_ => Ambient()).ToArray(), PendingRequests = [] },
            Process = new() { Actors = source.Process.Actors, Receipts = [new(request, new(true, 1))] }
        };
        state.Active = returnedActive;
        bool Complete(Ff7SessionSave actual, int? selection) => Ff7AutoPlayChoiceWorkflow.HasNativeIcicleControlReturn(actual, state, choice, selection);
        Check(Complete(returned, 1), "A real No receipt and complete source-root return did not finish the icicle choice.");
        Check(!Complete(source, 1) && !Complete(returned, 2) && !Complete(returned, null), "Live work or an unexecuted downward answer received completion credit.");
        Check(!Complete(returned with { Process = returned.Process with { Receipts = [] } }, 1), "A missing actual No receipt received completion credit.");
        Check(!Complete(returned with { Vm = returned.Vm with { PersistentVariables = variables.CopyPersistent() } }, 1), "The third icicle's No answer completed instead of preserving the required downward return.");
        state.Active = returnedActive with { Returned = false };
        Check(!Complete(returned, 1), "A child return substituted for the whole root return.");
        state.Active = returnedActive with { ExecutionProvenance = returnedActive.ExecutionProvenance! with { RootDispatchTick = 22 } };
        Check(!Complete(returned, 1), "Another accepted root lifetime completed an old choice.");
        state.Active = active with { TransitionOwned = true, DestinationField = 693 };
        Ff7SessionSave lower = returned with
        {
            FieldGeneration = 2,
            Vm = returned.Vm with { ProgramIdentity = Ff7AutoPlayChoiceWorkflow.GaeaIcicleLowerProgram, PersistentVariables = variables.CopyPersistent() },
            Process = new() { PreviousMap = 697, Actors = [new() { Slot = 0, Entity = 20, Model = 0, PlayerControlled = true, Triangle = 106 }] }
        };
        Check(Complete(lower, 2) && state.Active.Returned == false, "A genuine owned lower-room transition required a fabricated source RET.");
        Check(!Complete(lower with { FieldGeneration = 1 }, 2) && !Complete(lower with { Process = lower.Process with { PreviousMap = 696 } }, 2), "A same-generation or unrelated field arrival completed the downward answer.");
        state.Active = active;
        Check(!Complete(lower, 2), "An unowned ambient arrival substituted for the actual source MAP.");
    }

    private static void CheckLowerBridge()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), -1, -1, -1),
            new(1, new(300, 0, 0), new(400, 0, 0), new(300, 100, 0), -1, -1, -1)
        ];
        Ff7Actor player = new() { Slot = 0, Entity = 20, Model = 0, PlayerControlled = true, Triangle = 0, X4096 = 20 << 12, Y4096 = 20 << 12, CollisionRadius = 10 };
        Ff7Line line = new(0, 5, 20, 20, 0, 30, 20, 0);
        Ff7AutoPlayTarget target = new("bridge", 5, 2, Ff7AutoPlayTargetKind.LinePush, "bridge-condition", false, default, MayChangeNavigation: true) { PossibleJumpLandings = [new(20, 10, new(320, 20, 1, 17), true)] };
        Ff7AutoPlayState state = new() { DestinationField = 694 };
        Ff7AutoPlayObservation observation = new("lower", Ff7AutoPlayChoiceWorkflow.GaeaIcicleLowerProgram, 2, [target], true, true) { NavigationKey = "lower-component" };
        Ff7FieldProcess process = new() { Actors = [player], Lines = [line], BlockedTriangles = [0] };
        FieldVariables variables = new();
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 28);
        IReadOnlySet<int> reachable = new HashSet<int> { 0 };
        Ff7AutoPlayTarget? Read(Ff7AutoPlayTarget? candidate = null, Ff7FieldProcess? actual = null, Ff7AutoPlayObservation? observed = null) => Ff7AutoPlayPolicy.ReadNativeIciclePrerequisite(state, observed ?? observation with { Events = [candidate ?? target] }, actual ?? process, variables, reachable, mesh);
        string before = JsonSerializer.Serialize(new { state, observation, process, mesh, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() });
        Check(Read() == target, "The actual current lower-component native jump LINE was not selected.");
        Check(Read(actual: process with { Lines = [line with { Enabled = 0 }] }) is null, "A disabled native bridge LINE received priority.");
        PcWalkmeshTriangle[] connected = [mesh[0] with { AdjacentAB = 1 }, mesh[1] with { AdjacentAB = 0 }];
        Ff7FieldProcess remoteContact = process with { Lines = [line with { X1 = 320, X2 = 330 }] };
        Check(Ff7AutoPlayPolicy.ReadNativeIciclePrerequisite(state, observation, remoteContact, variables, reachable, connected) == target, "A reachable adjacent contact was incorrectly omitted.");
        Check(Ff7AutoPlayPolicy.ReadNativeIciclePrerequisite(state, observation, remoteContact with { BlockedTriangles = [2] }, variables, reachable, connected) is null, "A blocked adjacent contact manufactured a bridge prerequisite.");
        Check(Read(actual: process with { Lines = [line with { X1 = 320, X2 = 330 }] }) is null, "A disconnected LINE contact received current-component priority.");
        foreach (Ff7AutoPlayTarget invalid in new[] { target with { AnalysisComplete = false }, target with { PossibleJumpLandings = [target.PossibleJumpLandings[0] with { Literal = false }] }, target with { PossibleJumpLandings = [target.PossibleJumpLandings[0] with { Entity = 19 }] } })
        {
            Check(Read(invalid) is null, "An incomplete, dynamic or nonplayer bridge received priority.");
        }
        Check(Read(observed: observation with { ProgramIdentity = Program }) is null, "The upper program borrowed lower-bridge advice.");
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 12);
        Check(Read() is null, "An unfinished ice mechanism received lower-bridge advice.");
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 28);
        Check(before == JsonSerializer.Serialize(new { state, observation, process, mesh, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() }), "Lower-bridge advice mutated native flags, geometry, actors or history.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
