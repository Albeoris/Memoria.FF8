using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SemanticSpatialRecreationTests
{
    [Fact]
    public void Actual_129_prefix_completes_spatial_commands_and_join_before_departing_to_131()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        string sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_LEGACY_SPATIAL_SOURCE") ?? Path.Combine("../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(sourcePath), "The installed original corpus and preserved authentic legacy journal are required.");
        GameState authentic = GameSession.LoadReplaySource(sourcePath);
        ReplayJournal original = Assert.IsType<ReplayJournal>(authentic.ReplayJournal);
        Assert.Equal(1, original.Version);
        ReplayJournalEntry[] inputs = original.GetCommittedPrefix().Entries.ToArray();
        int arrival = Array.FindIndex(inputs, input => input.Kind == ReplayEntryKind.FieldLoad && input.FieldId == 129);
        Assert.True(arrival >= 0);
        int departure = Array.FindIndex(inputs, arrival + 1, input => input.Kind == ReplayEntryKind.FieldLoad);
        Assert.True(departure > arrival);
        Assert.Equal(131, inputs[departure].FieldId);
        Assert.Contains(inputs.Skip(arrival).Take(departure - arrival), input => input.Kind == ReplayEntryKind.Action && input.Code == (int)PlayerActionKind.Interact && input.Value == 9 && input.Accepted);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(sourcePath));
        JsonElement capsule = document.RootElement.GetProperty("ReplayJournal");
        using MemoryStream bytes = new(Convert.FromBase64String(capsule.GetProperty("Data").GetString()!));
        using BrotliStream reader = new(bytes, CompressionMode.Decompress);
        using JsonDocument recorded = JsonDocument.Parse(reader);
        Assert.Equal(original.EntryCount, recorded.RootElement.GetArrayLength());
        JsonElement[] literalPrefix = recorded.RootElement.EnumerateArray().Take(departure + 1).Select(input => input.Clone()).ToArray();
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
        Assert.Equal(JsonSerializer.Serialize(inputs.Take(departure + 1)), JsonSerializer.Serialize(prefix.Entries));
        ReplayCompletedVisit legacyVisit = prefix.GetSemanticProjection().CompletedVisits.Single(visit => visit.FieldId == 129);
        Assert.Equal(new ReplayVisitExit(SceneKind.Field, 131), legacyVisit.Exit);

        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        MovieClock clock = new(cold, directory);
        Assert.SkipUnless(File.Exists(Path.Combine(clock.ToolsDirectory, "ffmpeg.exe")) && File.Exists(Path.Combine(clock.ToolsDirectory, "ffprobe.exe")), "The authentic movie decoder tools are required.");
        //Only an unchanged authentic input prefix is supplied; no source snapshot pose, follower trace or story values seed reconstruction.
        GameState rebuilt = GameWorld.RecreateSave(new GameState { Scene = SceneKind.Field, FieldId = 131, ReplayJournal = prefix }, directory, clock.ToolsDirectory);
        Assert.Equal(SceneKind.Field, rebuilt.Scene);
        Assert.Equal(131, rebuilt.FieldId);
        Assert.True(rebuilt.ReplayJournal!.IsComplete);
        Assert.NotEqual(prefix.SessionId, rebuilt.ReplayJournal.SessionId);
        ReplayCompletedVisit current = rebuilt.ReplayJournal.CompletedVisits.Single(visit => visit.FieldId == 129);
        Assert.Equal(SceneKind.Field, current.Exit.Scene);
        Assert.Equal(131, current.Exit.FieldId);
        ReplaySemanticTrigger currentExit = Assert.IsType<ReplaySemanticTrigger>(current.Exit.Trigger);
        Assert.Equal(ReplaySemanticTriggerKind.FieldGateway, currentExit.Kind);
        Assert.Equal(129, currentExit.FieldId);
        Assert.Equal(0, currentExit.InfIndex);
        Assert.Equal(current.EntryFieldLoad!.NativeSourceHash, currentExit.ProgramHash);
        Assert.Equal(current.EntryFieldLoad.Text, currentExit.ExecutableHash);
        Assert.Single(current.Intents, intent => intent.SemanticTrigger is { Kind: ReplaySemanticTriggerKind.ActorInteraction, ObjectId: 9 });
        Assert.DoesNotContain(current.Intents, intent => intent.Kind == ReplayEntryKind.Action && intent.Code == (int)PlayerActionKind.Move);
        Assert.NotNull(rebuilt.EventState);
        Assert.Equal(JsonValueKind.Null, rebuilt.EventState.Value.GetProperty("PartyJoin").ValueKind);
        Assert.Equal(JsonValueKind.Null, rebuilt.EventState.Value.GetProperty("LastFault").ValueKind);

        GameState repeated = GameWorld.RecreateSave(rebuilt, directory, clock.ToolsDirectory);
        Assert.Equal(rebuilt.Scene, repeated.Scene);
        Assert.Equal(rebuilt.FieldId, repeated.FieldId);
        Assert.True(repeated.ReplayJournal!.IsComplete);
        Assert.Equal(rebuilt.Variables.Where(pair => pair.Key is >= 256 and < 1024).OrderBy(pair => pair.Key).ToArray(), repeated.Variables.Where(pair => pair.Key is >= 256 and < 1024).OrderBy(pair => pair.Key).ToArray());
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(rebuilt.CharacterVitals), JsonSerializer.SerializeToElement(repeated.CharacterVitals)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(rebuilt.Economy), JsonSerializer.SerializeToElement(repeated.Economy)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(rebuilt.CardInventory), JsonSerializer.SerializeToElement(repeated.CardInventory)));
        JsonElement currentScene = rebuilt.EventState.Value.GetProperty("Scene");
        JsonElement repeatedScene = repeated.EventState!.Value.GetProperty("Scene");
        Assert.True(JsonElement.DeepEquals(currentScene.GetProperty("Party"), repeatedScene.GetProperty("Party")));
        Assert.True(JsonElement.DeepEquals(currentScene.GetProperty("Names"), repeatedScene.GetProperty("Names")));
        Assert.Equal(JsonValueKind.Null, repeated.EventState.Value.GetProperty("PartyJoin").ValueKind);
        Assert.Equal(JsonValueKind.Null, repeated.EventState.Value.GetProperty("LastFault").ValueKind);
        Assert.Equal(ObserveOrdinaryActiveSuffix(rebuilt), ObserveOrdinaryActiveSuffix(repeated));

        (bool Before, bool After) ObserveOrdinaryActiveSuffix(GameState checkpoint)
        {
            GameState independent = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(checkpoint))!;
            GameSession currentSession = new(independent);
            using GameWorld currentWorld = new(currentSession, directory, persistReplayJournal: false);
            currentWorld.EnsureField();
            Assert.False(currentWorld.Events.IsSemanticSpatialReplay);
            bool before = currentWorld.Events.CanAcceptMovementInput;
            long frame = currentWorld.Events.Frame;
            PlayerActionResult waited = currentWorld.Apply(new PlayerAction(PlayerActionKind.Wait));
            Assert.True(waited.Accepted);
            Assert.Equal(1, waited.FramesAdvanced);
            Assert.Equal(frame + 1, currentWorld.Events.Frame);
            Assert.False(currentWorld.Events.IsSemanticSpatialReplay);
            Assert.Null(currentWorld.Events.LastFault);
            return (before, currentWorld.Events.CanAcceptMovementInput);
        }
    }
}
