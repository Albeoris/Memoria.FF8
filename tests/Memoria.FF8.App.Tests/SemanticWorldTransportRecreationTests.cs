using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SemanticWorldTransportRecreationTests
{
    [Fact]
    public void Actual_mode66_transport_reaches_260_and_replays_current_world_effects_once()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        string sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_LEGACY_TRANSPORT_SOURCE") ?? Path.Combine("../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(sourcePath), "The installed original corpus and preserved authentic legacy journal are required.");
        GameState authentic = GameSession.LoadReplaySource(sourcePath);
        ReplayJournal original = Assert.IsType<ReplayJournal>(authentic.ReplayJournal);
        Assert.Equal(1, original.Version);
        ReplayJournalEntry[] inputs = original.GetCommittedPrefix().Entries.ToArray();
        int destination = Array.FindIndex(inputs, input => input.Kind == ReplayEntryKind.FieldLoad && input.FieldId == 260);
        Assert.True(destination > 0);
        Assert.Contains(inputs.Take(destination), input => input.Scene == SceneKind.WorldMap && input.FieldId == 123 && input.Kind == ReplayEntryKind.WorldAction && input.Accepted && input.Advanced > 0);
        ReplayJournal prefix = LiteralPrefix(sourcePath, original, inputs, destination);
        ReplayCompletedVisit originalTransport = Assert.Single(prefix.GetSemanticProjection().CompletedVisits, visit => visit.Scene == SceneKind.WorldMap && visit.FieldId == 123);
        Assert.Equal(new ReplayVisitExit(SceneKind.Field, 260), originalTransport.Exit);

        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        MovieClock clock = new(cold, directory);
        Assert.SkipUnless(File.Exists(Path.Combine(clock.ToolsDirectory, "ffmpeg.exe")) && File.Exists(Path.Combine(clock.ToolsDirectory, "ffprobe.exe")), "The authentic movie decoder tools are required.");
        //Only the unchanged recorded input prefix supplies history; the shell authenticates its final allocation without source state or poses.
        GameState rebuilt = GameWorld.RecreateSave(new GameState { Scene = SceneKind.Field, FieldId = 260, ReplayJournal = prefix }, directory, clock.ToolsDirectory);
        Assert.Equal(SceneKind.Field, rebuilt.Scene);
        Assert.Equal(260, rebuilt.FieldId);
        Assert.True(rebuilt.ReplayJournal!.IsComplete);
        Assert.Equal(2, rebuilt.ReplayJournal.Version);
        Assert.NotEqual(prefix.SessionId, rebuilt.ReplayJournal.SessionId);
        AssertTransportEndpoint(rebuilt);

        GameState repeated = GameWorld.RecreateSave(rebuilt, directory, clock.ToolsDirectory);
        Assert.Equal(rebuilt.Scene, repeated.Scene);
        Assert.Equal(rebuilt.FieldId, repeated.FieldId);
        Assert.True(repeated.ReplayJournal!.IsComplete);
        Assert.NotEqual(rebuilt.ReplayJournal.SessionId, repeated.ReplayJournal.SessionId);
        AssertTransportEndpoint(repeated);
        Assert.Equal(rebuilt.Variables.Where(pair => pair.Key is >= 256 and < 1024).OrderBy(pair => pair.Key).ToArray(), repeated.Variables.Where(pair => pair.Key is >= 256 and < 1024).OrderBy(pair => pair.Key).ToArray());
        EqualOwner(rebuilt.CharacterVitals, repeated.CharacterVitals);
        EqualOwner(rebuilt.CharacterAppearance, repeated.CharacterAppearance);
        EqualOwner(rebuilt.Economy, repeated.Economy);
        EqualOwner(rebuilt.CardInventory, repeated.CardInventory);
        EqualOwner(rebuilt.SeeD, repeated.SeeD);
        EqualOwner(rebuilt.ExamTimer, repeated.ExamTimer);
        EqualOwner(rebuilt.WorldProcess, repeated.WorldProcess);
        EqualOwner(rebuilt.SalaryPresentation, repeated.SalaryPresentation);
        EqualOwner(rebuilt.VibrationProcess, repeated.VibrationProcess);
        EqualOwner(rebuilt.GlobalSoundProcess, repeated.GlobalSoundProcess);
        EqualOwner(rebuilt.WorldControls, repeated.WorldControls);
        EqualOwner(rebuilt.WorldConfiguration, repeated.WorldConfiguration);
        EqualOwner(rebuilt.WorldEntryMemory, repeated.WorldEntryMemory);
        EqualOwner(rebuilt.SavedWorld, repeated.SavedWorld);
        EqualOwner(rebuilt.DepartingWorldState, repeated.DepartingWorldState);
        JsonElement currentScene = rebuilt.EventState!.Value.GetProperty("Scene");
        JsonElement repeatedScene = repeated.EventState!.Value.GetProperty("Scene");
        Assert.True(JsonElement.DeepEquals(currentScene.GetProperty("Party"), repeatedScene.GetProperty("Party")));
        Assert.True(JsonElement.DeepEquals(currentScene.GetProperty("Names"), repeatedScene.GetProperty("Names")));
    }

    private static void AssertTransportEndpoint(GameState state)
    {
        WorldState world = Assert.IsType<WorldState>(state.DepartingWorldState);
        NativeScriptedWorldRuntimeState scripted = Assert.IsType<NativeScriptedWorldRuntimeState>(world.NativeScriptedRuntime);
        scripted.Validate();
        Assert.Equal((byte)66, scripted.Path.EntityType);
        Assert.Equal((short)66, scripted.Path.Packet.Mode);
        Assert.True(scripted.Path.FieldTransitionReady);
        Assert.True(world.Frame > 0);
        Assert.Equal(world.Frame, scripted.Path.Callback);
        Assert.Contains(world.Actions, action => action.AdvancedFrames > 0 && action.Kind == "Wait");
        NativeSavedWorldScriptedReturn endpoint = Assert.IsType<NativeSavedWorldScriptedReturn>(scripted.Return);
        Assert.True(endpoint.SourceWorldFrame > 0);
        Assert.Equal(endpoint.SourceWorldFrame + 16, world.Frame);
        Assert.Equal((ushort)123, endpoint.LastFieldId);
        Assert.Equal(260, endpoint.FieldEntry.FieldId);
        EqualOwner(state.FieldArrival, endpoint.FieldEntry);
        NativeSavedWorldState saved = Assert.IsType<NativeSavedWorldState>(state.SavedWorld);
        EqualOwner(saved.ScriptedReturn, endpoint);
        Assert.Null(saved.FootReturn);
        Assert.Null(saved.CarReturn);
        NativeWorldConfiguration configuration = Assert.IsType<NativeWorldConfiguration>(state.WorldConfiguration);
        Assert.Equal(NativeWorldConfiguration.ReturnRule, configuration.Rule);
        Assert.Null(configuration.SemanticTravel);
        Assert.Equal(endpoint.SourceWorldFrame, configuration.SourceWorldFrame);
        Assert.True(Assert.IsType<NativeWorldProcess>(state.WorldProcess).MainLoopCallbacks > 0);
        ReplayCompletedVisit visit = Assert.Single(state.ReplayJournal!.CompletedVisits, visit => visit.Scene == SceneKind.WorldMap && visit.FieldId == 123);
        Assert.Equal(260, visit.Exit.FieldId);
        Assert.NotNull(visit.EntryFieldLoad!.WorldAllocation);
        Assert.Contains(visit.Intents, intent => intent.Kind == ReplayEntryKind.PersistentEffect && intent.Code == (int)ReplayPersistentEffectKind.WorldCallbackEffects);
        Assert.NotNull(state.EventState);
        Assert.Equal(JsonValueKind.Null, state.EventState.Value.GetProperty("LastFault").ValueKind);
    }

    private static ReplayJournal LiteralPrefix(string sourcePath, ReplayJournal original, ReplayJournalEntry[] inputs, int destination)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(sourcePath));
        JsonElement capsule = document.RootElement.GetProperty("ReplayJournal");
        using MemoryStream bytes = new(Convert.FromBase64String(capsule.GetProperty("Data").GetString()!));
        using BrotliStream reader = new(bytes, CompressionMode.Decompress);
        using JsonDocument recorded = JsonDocument.Parse(reader);
        Assert.Equal(original.EntryCount, recorded.RootElement.GetArrayLength());
        JsonElement[] literalPrefix = recorded.RootElement.EnumerateArray().Take(destination + 1).Select(input => input.Clone()).ToArray();
        using MemoryStream compressed = new();
        using (BrotliStream writer = new(compressed, CompressionLevel.Optimal, true))
        {
            JsonSerializer.Serialize(writer, literalPrefix);
        }
        JsonObject prefixCapsule = JsonNode.Parse(capsule.GetRawText())!.AsObject();
        prefixCapsule["EntryCount"] = literalPrefix.Length;
        prefixCapsule["Data"] = Convert.ToBase64String(compressed.ToArray());
        prefixCapsule["IsComplete"] = true;
        prefixCapsule.Remove("UnavailableReason");
        ReplayJournal prefix = prefixCapsule.Deserialize<ReplayJournal>()!;
        prefix.Validate();
        Assert.Equal(original.SessionId, prefix.SessionId);
        Assert.Equal(JsonSerializer.Serialize(inputs.Take(destination + 1)), JsonSerializer.Serialize(prefix.Entries));
        return prefix;
    }

    private static void EqualOwner<T>(T expected, T actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(actual)), $"The current {typeof(T).Name} owner differs after V2 recreation.");
    }
}
