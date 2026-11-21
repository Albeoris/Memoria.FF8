using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeWhirlwindTutorialTests
{
    private const string Program = "F6C586EC658C8703F93488AA3A6F0C6A7F76D8C7C715D4E11C334EC224628DD9";

    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh = [new(0, new(-100, -100, 0), new(300, -100, 0), new(-100, 300, 0), -1, -1, -1)];
        Ff7Actor player = new() { Slot = 0, Entity = 10, Model = 0, PlayerControlled = true, Triangle = 0, CollisionRadius = 10 };
        Ff7Line[] lines = [new(0, 7, 80, 20, 0, 90, 20, 0), new(1, 8, 20, 20, 0, 30, 20, 0), new(2, 9, 120, 20, 0, 130, 20, 0)];
        Ff7AutoPlayTarget[] targets = Enumerable.Range(7, 3).Select(entity => new Ff7AutoPlayTarget($"line:{entity - 7}:entity:{entity}:script:2", entity, 2, Ff7AutoPlayTargetKind.LinePush, $"tutorial-{entity}", false, default)).ToArray();
        Ff7FieldProcess process = new() { Actors = [player], Lines = lines, BlockedTriangles = [0] };
        Ff7AutoPlayObservation observation = new("visit", Program, 1, targets, true, true) { NavigationKey = "tutorial-floor" };
        Ff7AutoPlayState state = new() { DestinationField = 704 };
        FieldVariables variables = new();
        Ff7AutoPlayTarget? Read(Ff7AutoPlayObservation? observed = null, Ff7FieldProcess? actual = null) => Ff7AutoPlayPolicy.ReadNativeWhirlwindTutorialPrerequisite(state, observed ?? observation, actual ?? process, variables, mesh);
        string native = JsonSerializer.Serialize(new { process, mesh, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() });
        Check(Read()?.Entity == 8, "The nearest reachable actual tutorial LINE was not preferred.");
        Check(Read(actual: process with { Lines = lines.Select(line => line.Entity == 8 ? line with { Enabled = 0 } : line).ToArray() })?.Entity == 7, "A disabled nearer source displaced the next actual reachable tutorial contact.");
        Check(Read(actual: process with { Lines = lines.Select(line => line.Entity == 8 ? line with { X1 = 520, X2 = 530 } : line).ToArray() })?.Entity == 7, "An out-of-floor source displaced the next actual reachable tutorial contact.");
        Check(Read(observation with { Events = [] }) is null && Read(observation with { ProgramIdentity = "other" }) is null, "Missing or foreign native content inherited tutorial advice.");
        foreach (Ff7AutoPlayTarget invalid in new[] { targets[1] with { Entity = 6 }, targets[1] with { Script = 3 }, targets[1] with { Kind = Ff7AutoPlayTargetKind.LineCross }, targets[1] with { AnalysisComplete = false }, targets[1] with { ChangesField = true }, targets[1] with { ChangesStory = true } })
        {
            Check(Read(observation with { Events = [invalid] }) is null, "An incorrect, incomplete or effect-mismatched source inherited tutorial priority.");
        }
        Check(Read(actual: process with { Lines = lines.Select(line => line with { Enabled = 0 }).ToArray() }) is null, "Disabled native tutorial LINEs received priority.");
        Check(Read(actual: process with { Actors = [player with { Entity = 9 }] }) is null, "Another controlled actor inherited the tutorial prerequisite.");
        Check(Read(actual: process with { PlayerLocked = true }) is null && Read(actual: process with { Dialogues = [new(1, 0, 1, null, null, 0)] }) is null, "Native controls or a live dialogue were preempted by tutorial advice.");
        state.DestinationField = 703;
        Check(Read() is null, "An unrelated destination inherited tutorial priority.");
        state.DestinationField = 704;
        Ff7AutoPlayActiveEvent owner = new("visit", targets[1].Id, targets[1].ConditionFingerprint, 1, 8, 2, Ff7AutoPlayTargetKind.LinePush, null, 0, "before", Accepted: true, NavigationKey: observation.NavigationKey);
        state.Active = owner;
        variables.WriteByte(FieldBank.Persistent0Byte, 0x85, 8);
        string owned = JsonSerializer.Serialize(state);
        Check(Read() is null && !Ff7AutoPlayPolicy.RetireUnacceptedWhirlwindTutorialPlan(state, observation, process, variables, mesh), "A native tutorial flag displaced its accepted owner before actual return.");
        Check(owned == JsonSerializer.Serialize(state) && state.Active is { Returned: false, CompletionRecorded: false }, "A flag publication fabricated tutorial return or completion.");
        state.Active = null;
        Check(Read() is null, "An already set native tutorial flag requested another activation.");
        variables.WriteByte(FieldBank.Persistent0Byte, 0x85, 0);
        Ff7AutoPlayActiveEvent optional = owner with { EventId = "optional", ConditionFingerprint = "optional-condition", Entity = 4, Accepted = false };
        state.Active = optional;
        state.NavigationGoal = new(observation.NavigationKey, optional.EventId, optional.ConditionFingerprint);
        string history = JsonSerializer.Serialize(state.Coverage);
        Check(Ff7AutoPlayPolicy.RetireUnacceptedWhirlwindTutorialPlan(state, observation, process, variables, mesh) && state.Active is null && state.NavigationGoal is null && state.DestinationField == 704, "An exact transient plan prevented the current native tutorial prerequisite.");
        Check(history == JsonSerializer.Serialize(state.Coverage), "Tutorial priority erased prior coverage history.");
        foreach (Ff7AutoPlayActiveEvent protectedActive in new[] { optional with { Accepted = true }, optional with { Returned = true }, optional with { Generation = 2 }, optional with { NavigationKey = "foreign" } })
        {
            state.Active = protectedActive;
            Check(!Ff7AutoPlayPolicy.RetireUnacceptedWhirlwindTutorialPlan(state, observation, process, variables, mesh) && state.Active == protectedActive, "Owned or foreign source work was retired for tutorial advice.");
        }
        Check(native == JsonSerializer.Serialize(new { process, mesh, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() }), "Tutorial advice modified native flags, actors, geometry or LINE state.");
        Console.WriteLine("Whirlwind tutorial advice selects real contacts without replacing native ownership or inventing completion.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
