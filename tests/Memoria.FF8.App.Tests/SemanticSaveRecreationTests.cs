using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SemanticSaveRecreationTests
{
    [Fact]
    public void Cold_opening_preserves_pending_name_and_rejects_invalid_choice_and_name_drafts()
    {
        using Opening opening = new();
        opening.DriveUntil(world => world.CurrentDialogue is { CanConfirm: true, FirstChoice: not null });
        DialogueState choice = Assert.IsType<DialogueState>(opening.World.CurrentDialogue);
        GameState beforeChoice = opening.Capture();
        DialogueDraft invalidChoice = new(choice.Channel, choice.MessageId, choice.LastChoice!.Value + 1, "");

        Assert.Throws<InvalidDataException>(() => GameWorld.RecreateSave(beforeChoice with { DialogueDraft = invalidChoice }, opening.Directory, opening.Clock.ToolsDirectory));

        opening.DriveUntil(world => world.CurrentDialogue is { CanConfirm: true, IsNamePrompt: true });
        DialogueState name = Assert.IsType<DialogueState>(opening.World.CurrentDialogue);
        DialogueDraft draft = new(name.Channel, name.MessageId, name.InitialChoice ?? name.FirstChoice ?? 0, "Cec");
        opening.Session.SetDialogueDraft(draft);
        GameState source = opening.Capture();
        GameState rebuilt = GameWorld.RecreateSave(source, opening.Directory, opening.Clock.ToolsDirectory);

        Assert.Equal(draft, rebuilt.DialogueDraft);
        AssertMeaningfulStateEqual(source, rebuilt);
        Assert.Throws<InvalidDataException>(() => GameWorld.RecreateSave(source with { DialogueDraft = draft with { Channel = draft.Channel + 1 } }, opening.Directory, opening.Clock.ToolsDirectory));
        Assert.Throws<InvalidDataException>(() => GameWorld.RecreateSave(source with { DialogueDraft = draft with { EnteredName = new string('A', 13) } }, opening.Directory, opening.Clock.ToolsDirectory));

        GameState repeated = GameWorld.RecreateSave(rebuilt, opening.Directory, opening.Clock.ToolsDirectory);
        Assert.Equal(draft, repeated.DialogueDraft);
        AssertMeaningfulStateEqual(rebuilt, repeated);
    }

    [Fact]
    public void Completed_opening_visits_drop_travel_and_cadence_but_current_active_suffix_and_decisions_survive()
    {
        using Opening opening = new();
        opening.DriveUntil(world => world.Field?.Id == 229 && world.NextTrigger.Kind == PlayerTriggerKind.Playable && world.Events.CanAcceptMovementInput);
        GameState beforeMovement = opening.Capture();
        Assert.Equal("Cecilia", opening.World.Events.Scene.Names[0]);
        GameState currentBeforeMovement = GameWorld.RecreateSave(beforeMovement, opening.Directory, opening.Clock.ToolsDirectory);

        PlayerActionResult movement = opening.World.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1));
        Assert.True(movement.Accepted);
        Assert.True(opening.World.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3)).Accepted);
        GameState source = opening.Capture();
        ReplayJournal journal = Assert.IsType<ReplayJournal>(source.ReplayJournal);
        Assert.True(journal.IsComplete);
        Assert.NotEmpty(journal.CompletedVisits);
        Assert.All(journal.CompletedVisits, visit => Assert.All(visit.Intents, intent =>
        {
            Assert.DoesNotContain(intent.Kind, new[] { ReplayEntryKind.FieldLoad, ReplayEntryKind.WorldAllocation, ReplayEntryKind.Step, ReplayEntryKind.Update });
            Assert.False(intent.Kind is ReplayEntryKind.Action or ReplayEntryKind.WorldAction && intent.Code is (int)PlayerActionKind.Move or (int)PlayerActionKind.Wait);
            Assert.Null(intent.StartPose);
            Assert.Null(intent.EndPose);
            Assert.Empty(intent.Navigation);
            Assert.Empty(intent.WorldNavigation);
        }));
        Assert.Contains(journal.ActiveEntries, entry => entry.Kind == ReplayEntryKind.Action && entry.Code == (int)PlayerActionKind.Move && entry.Accepted);
        Assert.Contains(journal.ActiveEntries, entry => entry.Kind == ReplayEntryKind.Action && entry.Code == (int)PlayerActionKind.Wait && entry.Frames == 3);
        Assert.Contains(journal.CompletedVisits.SelectMany(visit => visit.Intents), entry => entry.Kind == ReplayEntryKind.Action && entry.Code == (int)PlayerActionKind.EnterName && entry.Text == "Cecilia");

        GameState rebuilt = GameWorld.RecreateSave(source, opening.Directory, opening.Clock.ToolsDirectory);
        AssertMeaningfulStateEqual(source, rebuilt);
        Assert.True(rebuilt.X != currentBeforeMovement.X || rebuilt.Y != currentBeforeMovement.Y, "The active suffix must execute actual movement under current code.");
        ReplayJournal regenerated = Assert.IsType<ReplayJournal>(rebuilt.ReplayJournal);
        Assert.True(regenerated.IsComplete);
        Assert.Contains(regenerated.ActiveEntries, entry => entry.Kind == ReplayEntryKind.Action && entry.Code == (int)PlayerActionKind.Move && entry.Accepted);

        GameState repeated = GameWorld.RecreateSave(rebuilt, opening.Directory, opening.Clock.ToolsDirectory);
        AssertMeaningfulStateEqual(rebuilt, repeated);
        Assert.Equal(rebuilt.X, repeated.X);
        Assert.Equal(rebuilt.Y, repeated.Y);
        Assert.True(repeated.ReplayJournal!.IsComplete);
    }

    [Fact]
    public void Actual_legacy_departure_reaches_next_field_before_its_final_departed_cleanup_hint()
    {
        using Opening opening = new();
        string sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_LEGACY_CLEANUP_SOURCE") ?? Path.Combine("../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(sourcePath), "The preserved authentic legacy opening journal is required.");
        GameState authentic = GameSession.LoadReplaySource(sourcePath);
        ReplayJournal journal = Assert.IsType<ReplayJournal>(authentic.ReplayJournal);
        Assert.Equal(1, journal.Version);
        ReplayJournalEntry[] entries = journal.GetCommittedPrefix().Entries.ToArray();
        int departingLoad = Array.FindIndex(entries, entry => entry.Kind == ReplayEntryKind.FieldLoad && entry.FieldId == 232);
        Assert.True(departingLoad >= 0);
        int nextLoad = Array.FindIndex(entries, departingLoad + 1, entry => entry.Kind == ReplayEntryKind.FieldLoad);
        Assert.True(nextLoad > departingLoad);
        Assert.Equal(139, entries[nextLoad].FieldId);

        using JsonDocument sourceDocument = JsonDocument.Parse(File.ReadAllBytes(sourcePath));
        JsonElement sourceCapsule = sourceDocument.RootElement.GetProperty("ReplayJournal");
        using MemoryStream compressed = new(Convert.FromBase64String(sourceCapsule.GetProperty("Data").GetString()!));
        using BrotliStream decompressor = new(compressed, CompressionMode.Decompress);
        using JsonDocument literalInputs = JsonDocument.Parse(decompressor);
        Assert.Equal(journal.EntryCount, literalInputs.RootElement.GetArrayLength());
        JsonElement[] preservedPrefix = literalInputs.RootElement.EnumerateArray().Take(nextLoad + 1).Select(entry => entry.Clone()).ToArray();
        using MemoryStream encodedPrefix = new();
        using (BrotliStream compressor = new(encodedPrefix, CompressionLevel.Optimal, true))
        {
            JsonSerializer.Serialize(compressor, preservedPrefix);
        }
        JsonObject capsule = JsonNode.Parse(sourceCapsule.GetRawText())!.AsObject();
        capsule["Data"] = Convert.ToBase64String(encodedPrefix.ToArray());
        capsule["EntryCount"] = preservedPrefix.Length;
        capsule["IsComplete"] = true;
        capsule.Remove("UnavailableReason");
        ReplayJournal prefix = capsule.Deserialize<ReplayJournal>()!;
        prefix.Validate();
        Assert.Equal(journal.SessionId, prefix.SessionId);
        Assert.Equal(JsonSerializer.Serialize(entries.Take(nextLoad + 1)), JsonSerializer.Serialize(prefix.Entries));
        ReplayCompletedVisit departed = prefix.GetSemanticProjection().CompletedVisits.Single(visit => visit.FieldId == 232);
        Assert.Equal(new ReplayVisitExit(SceneKind.Field, 139), departed.Exit);
        Assert.Equal(ReplayEntryKind.ScriptGoal, departed.Intents[^1].Kind);
        Assert.Equal((21, 197, 3720), (departed.Intents[^1].ScriptGoal!.ObjectId, departed.Intents[^1].ScriptGoal!.ScriptId, departed.Intents[^1].ScriptGoal!.Operation));

        //This shell supplies only the authenticated prefix endpoint; every input and receipt above is an unchanged actual journal entry.
        GameState source = new() { Scene = SceneKind.Field, FieldId = 139, ReplayJournal = prefix };
        GameState rebuilt = GameWorld.RecreateSave(source, opening.Directory, opening.Clock.ToolsDirectory);
        Assert.Equal(SceneKind.Field, rebuilt.Scene);
        Assert.Equal(139, rebuilt.FieldId);
        Assert.Equal(17, rebuilt.Variables.GetValueOrDefault(256));
        Assert.True(rebuilt.ReplayJournal!.IsComplete);
        GameState repeated = GameWorld.RecreateSave(rebuilt, opening.Directory, opening.Clock.ToolsDirectory);
        AssertMeaningfulStateEqual(rebuilt, repeated);
        Assert.True(repeated.ReplayJournal!.IsComplete);
    }

    private static void AssertMeaningfulStateEqual(GameState expected, GameState actual)
    {
        Assert.Equal(expected.Scene, actual.Scene);
        Assert.Equal(expected.FieldId, actual.FieldId);
        Assert.Equal(expected.Variables.Where(pair => pair.Key is >= 256 and < 1024).OrderBy(pair => pair.Key).ToArray(), actual.Variables.Where(pair => pair.Key is >= 256 and < 1024).OrderBy(pair => pair.Key).ToArray());
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.CharacterVitals), JsonSerializer.SerializeToElement(actual.CharacterVitals)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.Economy), JsonSerializer.SerializeToElement(actual.Economy)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.CardInventory), JsonSerializer.SerializeToElement(actual.CardInventory)));
        JsonElement expectedScene = expected.EventState!.Value.GetProperty("Scene");
        JsonElement actualScene = actual.EventState!.Value.GetProperty("Scene");
        Assert.True(JsonElement.DeepEquals(expectedScene.GetProperty("Names"), actualScene.GetProperty("Names")));
        Assert.True(JsonElement.DeepEquals(expectedScene.GetProperty("Party"), actualScene.GetProperty("Party")));
    }

    private sealed class Opening : IDisposable
    {
        public Opening()
        {
            Directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
            Assert.SkipUnless(System.IO.Directory.Exists(Directory), "Installed original field/movie corpus is required.");
            Session = GameSession.CreateColdProcess();
            Session.StartNewGame();
            Clock = new MovieClock(Session, Directory);
            Assert.SkipUnless(File.Exists(Path.Combine(Clock.ToolsDirectory, "ffmpeg.exe")) && File.Exists(Path.Combine(Clock.ToolsDirectory, "ffprobe.exe")), "The original movie decoder tools are required.");
            World = new GameWorld(Session, Directory, Clock, persistReplayJournal: false);
        }

        public string Directory { get; }
        public GameSession Session { get; }
        public MovieClock Clock { get; }
        public GameWorld World { get; }

        public void DriveUntil(Func<GameWorld, bool> reached)
        {
            for (int input = 0; input < 6000; input++)
            {
                World.EnsureField();
                Assert.Null(World.Events.LastFault);
                if (reached(World))
                {
                    return;
                }
                if (Session.IsMoviePlaying)
                {
                    DecodeActiveMovie();
                    continue;
                }
                DialogueState? dialogue = World.CurrentDialogue;
                PlayerAction action = dialogue is { CanConfirm: true, IsNamePrompt: true } ? new PlayerAction(PlayerActionKind.EnterName, Text: "Cecilia") : dialogue is { CanConfirm: true, FirstChoice: int first } ? new PlayerAction(PlayerActionKind.Choose, first) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
                Assert.True(World.Apply(action).Accepted);
            }
            Assert.Fail("The genuine cold opening exceeded its ordinary-input budget.");
        }

        public GameState Capture()
        {
            Assert.Equal(Session.State.FieldId, World.Events.FieldId);
            Session.SetEventState(World.Events.Capture());
            return JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(Session.State))!;
        }

        private void DecodeActiveMovie()
        {
            MovieInformation information = Clock.Prepare();
            using FfmpegMovieDecoder decoder = new(Clock.ToolsDirectory, Clock.SourcePath, information: information);
            Stopwatch timeout = Stopwatch.StartNew();
            int field = Session.State.FieldId;
            int movie = Session.State.MovieIndex;
            NativeMovieSource source = Session.State.MovieSource!;
            bool IsCurrentMovie() => Session.IsMoviePlaying && Session.State.FieldId == field && Session.State.MovieIndex == movie && Session.State.MovieSource == source;
            for (int index = 0; index < information.FrameCount && IsCurrentMovie(); index++)
            {
                MovieFrame? frame;
                double seconds = index / information.FramesPerSecond;
                while (!decoder.TryGetFrame(seconds, out frame))
                {
                    while (decoder.TryGetAudio(out _))
                    {
                    }
                    Assert.Null(decoder.Error);
                    Assert.False(decoder.VideoFinished, $"Actual movie EOF preceded decoded frame{index}.");
                    Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(45), "Authentic opening movie decoding exceeded its wall-time budget.");
                    Thread.Sleep(1);
                }
                MovieFrame decoded = Assert.IsType<MovieFrame>(frame);
                Assert.Equal(index, decoded.Index);
                Session.SetMoviePlayback(new MoviePlaybackState(index, information.FrameCount - 1, true, false, false));
                Session.SetMoviePosition(seconds);
                if (index >= 2)
                {
                    World.Events.Step();
                }
                while (decoder.TryGetAudio(out _))
                {
                }
            }
            while (IsCurrentMovie() && (!decoder.VideoFinished || !decoder.AudioFinished))
            {
                while (decoder.TryGetAudio(out _))
                {
                }
                Assert.Null(decoder.Error);
                Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(45), "Authentic opening movie EOF/audio drain exceeded its wall-time budget.");
                Thread.Sleep(1);
            }
            if (IsCurrentMovie())
            {
                Session.SetMoviePosition(information.DurationSeconds);
                Session.CompleteMovie();
            }
            Assert.Null(decoder.Error);
            Assert.False(IsCurrentMovie());
            Assert.Null(World.Events.LastFault);
        }

        public void Dispose()
        {
            World.Dispose();
        }
    }
}
