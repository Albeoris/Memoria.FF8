using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeGaeaMoviePrerequisiteTests
{
    private const string Program = "DFC7E66D342921243A62BD7F892060E8CE934B8E48F332324C12803BCFED0B8A";

    internal static void Run()
    {
        CheckStages();
        CheckNeutralInput();
        Console.WriteLine("Native Gaea movie prerequisites preserve component contacts and actual neutral entry/return ownership.");
    }

    private static void CheckStages()
    {
        PcWalkmeshTriangle[] mesh = Enumerable.Range(0, 32).Select(index => new PcWalkmeshTriangle(index, new(10000 + index * 200, 0, 0), new(10100 + index * 200, 0, 0), new(10000 + index * 200, 100, 0), -1, -1, -1)).ToArray();
        mesh[31] = new(31, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), -1, -1, -1);
        mesh[2] = new(2, new(300, 0, 100), new(400, 0, 100), new(300, 100, 100), -1, -1, -1);
        Ff7Actor player = new() { Slot = 0, Entity = 8, Model = 0, PlayerControlled = true, Triangle = 31, X4096 = 10 << 12, Y4096 = 10 << 12, CollisionRadius = 10 };
        Ff7Line[] lines = [new(0, 3, 20, 20, 0, 30, 20, 0), new(1, 5, 320, 20, 100, 330, 20, 100)];
        Ff7FieldProcess process = new() { Actors = [player], Lines = lines, BlockedTriangles = new byte[4] };
        Ff7AutoPlayTarget climb = new("line:0:entity:3:script:4", 3, 4, Ff7AutoPlayTargetKind.LineTouch, "climb-condition", false, default, MayChangeNavigation: true) { LadderLandings = [new(8, 10, new(320, 20, 100, 2, 1, 1, 0, 1), true)] };
        Ff7AutoPlayTarget movie = new("line:1:entity:5:script:4", 5, 4, Ff7AutoPlayTargetKind.LineTouch, "movie-condition", false, default);
        Ff7AutoPlayObservation observation = new("visit", Program, 1, [climb, movie], true, true) { NavigationKey = "lower" };
        Ff7AutoPlayState state = new() { DestinationField = 700 };
        FieldVariables variables = new();
        Ff7AutoPlayTarget? Read(Ff7AutoPlayObservation? observed = null, Ff7FieldProcess? actual = null) => Ff7AutoPlayPolicy.ReadNativeGaeaMoviePrerequisite(state, observed ?? observation, actual ?? process, variables, mesh);
        string before = JsonSerializer.Serialize(new { process, mesh, state, observation, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() });
        Check(Read() == climb, "The current lower component did not select its real player ladder LINE.");
        Check(Read(observation with { Events = [movie] }) is null, "A disconnected upper movie LINE bypassed its actual ladder prerequisite.");
        Check(Read(observation with { Events = [climb with { AnalysisComplete = false }] }) is null, "Incomplete ladder advice received priority.");
        Check(Read(actual: process with { Lines = lines.Select(line => line with { Enabled = 0 }).ToArray() }) is null, "Disabled native sources received priority.");
        Ff7FieldProcess upper = process with { Actors = [player with { Triangle = 2, X4096 = 320 << 12, Y4096 = 20 << 12, Z4096 = 100 << 12 }] };
        Check(Read(actual: upper) == movie, "The actual upper contact did not select its movie source.");
        Check(Read(observation with { ProgramIdentity = "other" }) is null && Read(actual: process with { Actors = [player with { Entity = 7 }] }) is null, "Another profile or controlled actor inherited movie advice.");
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 1);
        Check(Read() is null, "A completed native movie mechanism replayed a prerequisite.");
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 0);
        state.DestinationField = 699;
        Check(Read() is null, "An unrelated destination inherited movie advice.");
        state.DestinationField = 700;
        Check(before == JsonSerializer.Serialize(new { process, mesh, state, observation, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() }), "Movie prerequisite advice mutated native state or history.");
        Ff7AutoPlayActiveEvent optional = new("visit", "optional", "optional-condition", 1, 4, 2, Ff7AutoPlayTargetKind.LinePush, null, 0, "before", NavigationKey: observation.NavigationKey);
        state.Active = optional;
        state.NavigationGoal = new(observation.NavigationKey, optional.EventId, optional.ConditionFingerprint);
        string history = JsonSerializer.Serialize(state.Coverage);
        Check(Ff7AutoPlayPolicy.RetireUnacceptedGaeaMoviePlan(state, observation, process, variables, mesh) && state.Active is null && state.NavigationGoal is null && state.DestinationField == 700, "An exact unaccepted optional plan prevented native movie progression.");
        Check(history == JsonSerializer.Serialize(state.Coverage), "Movie priority erased source history.");
        foreach (Ff7AutoPlayActiveEvent protectedActive in new[] { optional with { Accepted = true }, optional with { Returned = true }, optional with { Generation = 2 }, optional with { NavigationKey = "foreign" } })
        {
            state.Active = protectedActive;
            Check(!Ff7AutoPlayPolicy.RetireUnacceptedGaeaMoviePlan(state, observation, process, variables, mesh) && state.Active == protectedActive, "Owned or foreign work was retired for movie advice.");
        }
    }

    private static void CheckNeutralInput()
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 9).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[5] = entities[5] with { ProgramCounter = 0x032C };
        entities[8] = entities[8] with { ModelBound = true };
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 30, true, new byte[1280], new byte[256], entities, PendingRequests: []);
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 8, Model = 0, PlayerControlled = true, Triangle = 2 }], Lines = [new(1, 5, 0, 0, 0, 50, 0, 0, Touch: true)] };
        Ff7AutoPlayActiveEvent active = new("visit", "line:1:entity:5:script:4", "movie-condition", 1, 5, 4, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", NavigationKey: "upper");
        Ff7AutoPlayState state = new() { DestinationField = 700, Active = active };
        string before = JsonSerializer.Serialize(new { state, vm, process });
        Check(Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralEntry(state, 1, vm, process), "The selected unaccepted movie source did not preserve native neutral entry input.");
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralEntry(state, 1, vm, process with { Lines = [process.Lines.Single() with { Touch = false }] }), "An out-of-contact movie source inherited neutral entry.");
        state.Active = active with { Entity = 3 };
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralEntry(state, 1, vm, process), "A different selected LINE inherited the neutral entry exception.");
        state.Active = active;
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralEntry(state, 1, vm with { ProgramIdentity = "other" }, process), "Another program inherited neutral movie entry.");
        Check(before == JsonSerializer.Serialize(new { state, vm, process }), "Neutral entry advice mutated native or policy facts.");

        active = active with { Accepted = true };
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, 0x0330, 20) };
        state.Active = active;
        entities[5] = entities[5] with { Priority = 1, ProgramCounter = 0x033D };
        entities[5].SavedProgramCounters[7] = 0x032C;
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        variables.WriteByte(FieldBank.Persistent0Byte, 0x83, 1);
        vm = vm with { PersistentVariables = variables.CopyPersistent() };
        process = process with { Movie = new(20, false, 100, true, null) };
        before = JsonSerializer.Serialize(new { state, vm, process });
        Check(Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process), "The actual owned post-movie KEY did not receive its native false/RET input.");
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process with { Movie = null }) && !Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process with { Movie = process.Movie! with { Completed = false } }) && !Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process with { Movie = process.Movie! with { PreparedId = 19 } }), "Missing, unfinished or unrelated movie state inherited neutral return.");
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process with { PlayerLocked = true }) && !Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process with { Dialogues = [new(1, 0, 1, null, null, 0)] }), "Locked controls or a live prompt received the neutral return exception.");
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm with { PendingRequests = [new(7, 5, 1, 0x033D, new FieldDialogueCommand(0, 1))] }, process), "Outstanding owned host work was skipped by neutral return.");
        state.Active = active with { ExecutionProvenance = active.ExecutionProvenance! with { Owners = [] } };
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process), "Missing current native ownership inherited neutral return.");
        state.Active = active with { Generation = 2 };
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process), "A mismatched root scope inherited neutral return.");
        state.Active = active;
        entities[5] = entities[5] with { ProgramCounter = 0x0341 };
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process), "The reverse-child dispatch site inherited the neutral KEY exception.");
        entities[5] = entities[5] with { Priority = 7, ProgramCounter = 0x033D };
        Check(!Ff7AutoPlayScriptedInput.IsNativeGaeaMovieNeutralReturn(state, 1, vm, process), "An ambient slot inherited the accepted root's return exception.");
        entities[5] = entities[5] with { Priority = 1 };
        Check(before == JsonSerializer.Serialize(new { state, vm, process }), "Neutral return advice fabricated native return, flags or movement.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
