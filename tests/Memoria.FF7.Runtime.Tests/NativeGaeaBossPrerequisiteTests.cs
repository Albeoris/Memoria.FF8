using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeGaeaBossPrerequisiteTests
{
    private const string Program = "279765F81E2101F56D4334DD43D09E2A8D1687D658C4DF90C4AAD939F79DC67C";

    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), -1, -1, -1),
            new(1, new(300, 0, 0), new(400, 0, 0), new(300, 100, 0), -1, -1, -1)
        ];
        Ff7Line line = new(0, 2, 20, 20, 0, 30, 20, 0);
        Ff7Actor player = new() { Slot = 0, Entity = 3, Model = 0, PlayerControlled = true, Triangle = 0, X4096 = 10 << 12, Y4096 = 10 << 12, CollisionRadius = 10 };
        Ff7FieldProcess process = new() { Actors = [player], Lines = [line], BlockedTriangles = [0] };
        Ff7AutoPlayTarget target = new("line:0:entity:2:script:2", 2, 2, Ff7AutoPlayTargetKind.LinePush, "boss-condition", false, default);
        Ff7AutoPlayObservation observation = new("visit", Program, 1, [target], true, true) { NavigationKey = "boss-floor" };
        Ff7AutoPlayState state = new() { DestinationField = 695 };
        FieldVariables variables = new();
        Ff7AutoPlayTarget? Read(Ff7AutoPlayObservation? observed = null, Ff7FieldProcess? actual = null) => Ff7AutoPlayPolicy.ReadNativeGaeaBossPrerequisite(state, observed ?? observation, actual ?? process, variables, mesh);
        string native = JsonSerializer.Serialize(new { process, mesh, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() });
        Check(Read() == target, "The actual current boss LINE was not preferred before the onward destination.");
        Check(Read(observation with { Events = [] }) is null && Read(observation with { ProgramIdentity = "other" }) is null, "Missing or unrelated native content inherited boss priority.");
        foreach (Ff7AutoPlayTarget invalid in new[] { target with { Entity = 4 }, target with { Script = 3 }, target with { Kind = Ff7AutoPlayTargetKind.LineCross }, target with { AnalysisComplete = false } })
        {
            Check(Read(observation with { Events = [invalid] }) is null, "An incorrect or incomplete native source received boss priority.");
        }
        Check(Read(actual: process with { Lines = [line with { Enabled = 0 }] }) is null, "A disabled boss LINE received priority.");
        Check(Read(actual: process with { Lines = [line with { X1 = 320, X2 = 330 }] }) is null, "A disconnected boss contact received priority.");
        PcWalkmeshTriangle[] connected = [mesh[0] with { AdjacentAB = 1 }, mesh[1] with { AdjacentAB = 0 }];
        Ff7FieldProcess adjacent = process with { Lines = [line with { X1 = 320, X2 = 330 }] };
        Check(Ff7AutoPlayPolicy.ReadNativeGaeaBossPrerequisite(state, observation, adjacent, variables, connected) == target, "A directed reachable boss contact was omitted.");
        Check(Ff7AutoPlayPolicy.ReadNativeGaeaBossPrerequisite(state, observation, adjacent with { BlockedTriangles = [2] }, variables, connected) is null, "A blocked adjacent boss contact received priority.");
        Check(Read(actual: process with { Actors = [player with { Entity = 4 }] }) is null, "Another controlled actor inherited the boss prerequisite.");
        Check(Read(actual: process with { PlayerLocked = true }) is null && Read(actual: process with { Dialogues = [new(1, 0, 0, null, null, 0)] }) is null, "Owned controls or UI were preempted by boss advice.");
        state.DestinationField = 694;
        Check(Read() is null, "Another destination inherited the boss prerequisite.");
        state.DestinationField = 695;
        Ff7AutoPlayActiveEvent accepted = new("visit", target.Id, target.ConditionFingerprint, 1, 2, 2, Ff7AutoPlayTargetKind.LinePush, null, 0, "before", Accepted: true, NavigationKey: observation.NavigationKey);
        state.Active = accepted;
        variables.WriteByte(FieldBank.Persistent0Byte, 0x86, 4);
        string acceptedBefore = JsonSerializer.Serialize(state);
        Check(Read() is null && !Ff7AutoPlayPolicy.RetireUnacceptedGaeaBossPlan(state, observation, process, variables, mesh), "The pre-battle flag displaced its accepted native owner.");
        Check(acceptedBefore == JsonSerializer.Serialize(state) && state.Active is { Returned: false, CompletionRecorded: false }, "A pre-battle flag fabricated victory or source return.");
        state.Active = null;
        Check(Read() is null, "An already set prerequisite flag scheduled a second boss activation.");
        variables.WriteByte(FieldBank.Persistent0Byte, 0x86, 0);
        Ff7AutoPlayActiveEvent optional = accepted with { EventId = "optional", ConditionFingerprint = "optional-condition", Entity = 4, Accepted = false };
        state.Active = optional;
        state.NavigationGoal = new(observation.NavigationKey, optional.EventId, optional.ConditionFingerprint);
        string history = JsonSerializer.Serialize(state.Coverage);
        Check(Ff7AutoPlayPolicy.RetireUnacceptedGaeaBossPlan(state, observation, process, variables, mesh) && state.Active is null && state.NavigationGoal is null && state.DestinationField == 695, "An exact transient optional plan prevented the current native prerequisite.");
        Check(history == JsonSerializer.Serialize(state.Coverage), "Native boss priority erased historical coverage.");
        foreach (Ff7AutoPlayActiveEvent protectedActive in new[] { optional with { Accepted = true }, optional with { Returned = true }, optional with { Generation = 2 }, optional with { NavigationKey = "foreign" } })
        {
            state.Active = protectedActive;
            Check(!Ff7AutoPlayPolicy.RetireUnacceptedGaeaBossPlan(state, observation, process, variables, mesh) && state.Active == protectedActive, "Owned or foreign source work was retired for a boss preference.");
        }
        Check(native == JsonSerializer.Serialize(new { process, mesh, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() }), "Boss qualification changed native variables, geometry, actors or LINE state.");
        Console.WriteLine("Native Gaea boss priority preserves real admission and never treats its pre-battle flag as victory.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
