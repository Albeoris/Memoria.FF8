using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SaveRecreationTests
{
    [Fact]
    public void Pending_name_draft_survives_portable_recreation_without_restoring_obsolete_VM_state()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-pending-draft-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var session = GameSession.CreateColdProcess();
            session.StartNewGame();
            using var world = new GameWorld(session, directory, persistReplayJournal: false);
            DialogueState? prompt = null;
            for (int input = 0; input < 2000; input++)
            {
                world.EnsureField();
                prompt = world.Events.CurrentDialogue;
                if (prompt?.IsNamePrompt == true)
                {
                    break;
                }
                PlayerAction action = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : prompt?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : prompt is not null ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
                Assert.True(world.Apply(action).Accepted);
            }
            Assert.NotNull(prompt);
            Assert.True(prompt.IsNamePrompt);
            var draft = new DialogueDraft(prompt.Channel, prompt.MessageId, prompt.InitialChoice ?? prompt.FirstChoice ?? 0, "Cec");
            session.SetDialogueDraft(draft);
            session.SetEventState(world.Events.Capture());
            System.Text.Json.Nodes.JsonObject portable = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(session.State))!.AsObject();
            portable["EventState"] = "obsolete derived VM shape";
            File.WriteAllText(path, portable.ToJsonString());
            GameState source = GameSession.LoadReplaySource(path);
            Assert.Null(source.EventState);
            Assert.Equal(draft, source.DialogueDraft);
            GameState recreated = GameWorld.RecreateSave(source, directory);
            Assert.Equal(draft, recreated.DialogueDraft);
            Assert.True(JsonElement.DeepEquals(session.State.EventState!.Value, recreated.EventState!.Value));
            using var restored = new GameWorld(new GameSession(recreated), directory, persistReplayJournal: false);
            restored.EnsureField();
            Assert.Equal((draft.SelectedChoice, "Cec"), DialoguePresentation.ResolveDraft(restored.Events.CurrentDialogue, recreated.DialogueDraft, "Squall"));
            GameState repeated = GameWorld.RecreateSave(recreated, directory);
            Assert.Equal(draft, repeated.DialogueDraft);
            Assert.Throws<InvalidDataException>(() => GameWorld.RecreateSave(source with { DialogueDraft = draft with { Channel = draft.Channel + 1 } }, directory));
            Assert.Throws<InvalidDataException>(() => GameWorld.RecreateSave(source with { DialogueDraft = draft with { EnteredName = new string('A', 13) } }, directory));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Fresh_game_recorded_opening_recreates_complete_VM_and_continues_from_portable_save()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        DriveOpening(world, session);
        Assert.Equal(229, world.Field!.Id);
        Assert.Null(world.Events.LastFault);
        world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1));
        world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1));
        session.SetEventState(world.Events.Capture());
        GameState source = session.State;
        Assert.True(source.ReplayJournal!.IsComplete);
        string portable = JsonSerializer.Serialize(source);
        var watch = Stopwatch.StartNew();
        GameState recreated = GameWorld.RecreateSave(JsonSerializer.Deserialize<GameState>(portable)!, directory);
        Console.WriteLine($"Opening causal recreation: {watch.Elapsed.TotalSeconds:F3}s, journal entries{source.ReplayJournal.EntryCount}, portable bytes{System.Text.Encoding.UTF8.GetByteCount(portable)}, journal bytes{System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(source.ReplayJournal))}.");
        Assert.Equal(source.X, recreated.X);
        Assert.Equal(source.Y, recreated.Y);
        Assert.True(JsonElement.DeepEquals(source.EventState!.Value, recreated.EventState!.Value));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source with { ReplayJournal = null }), JsonSerializer.SerializeToElement(recreated with { ReplayJournal = null })));
        var restoredSession = new GameSession(recreated);
        using var restored = new GameWorld(restoredSession, directory, persistReplayJournal: false);
        restored.EnsureField();
        world.SetRenderSize(640, 448);
        restored.SetRenderSize(640, 448);
        Assert.True(restored.Events.IsUserControlEnabled);
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));

        var wrongBoundary = GameSession.CreateColdProcess();
        wrongBoundary.StartNewGame();
        IReadOnlyList<ReplayJournalEntry> recorded = source.ReplayJournal.Entries;
        for (int index = 0; index < recorded.Count; index++)
        {
            wrongBoundary.AppendReplayEntry(index == recorded.Count - 1 ? recorded[index] with { ResultTrigger = (int)PlayerTriggerKind.Waiting } : recorded[index]);
        }
        Assert.Throws<InvalidDataException>(() => GameWorld.RecreateSave(source with { ReplayJournal = wrongBoundary.ReplayJournal }, directory));

        foreach (PlayerAction next in new[] { new PlayerAction(PlayerActionKind.Wait), new PlayerAction(PlayerActionKind.Move, Horizontal: -1) })
        {
            Assert.Equal(world.Apply(next), restored.Apply(next));
            session.SetEventState(world.Events.Capture());
            restoredSession.SetEventState(restored.Events.Capture());
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State with { ReplayJournal = null }), JsonSerializer.SerializeToElement(restoredSession.State with { ReplayJournal = null })));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }

        var continuedSession = new GameSession(JsonSerializer.Deserialize<GameState>(portable)!);
        using var continued = new GameWorld(continuedSession, directory, persistReplayJournal: false);
        int originalEntries = continuedSession.ReplayJournal!.EntryCount;
        continued.EnsureField();
        Assert.Equal(originalEntries, continuedSession.ReplayJournal.EntryCount);
        continued.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1));
        continuedSession.SetEventState(continued.Events.Capture());
        GameState replayedContinuation = GameWorld.RecreateSave(continuedSession.State, directory);
        Assert.True(JsonElement.DeepEquals(continuedSession.State.EventState!.Value, replayedContinuation.EventState!.Value));

        continuedSession.MarkReplayUnavailable("An unsupported command failed after the last committed transaction.");
        Assert.Throws<NotSupportedException>(() => GameWorld.RecreateSave(continuedSession.State, directory));
        GameState committed = GameWorld.RecreateCommittedPrefix(continuedSession.State, directory);
        Assert.True(committed.ReplayJournal!.IsComplete);
        Assert.False(continuedSession.ReplayJournal!.IsComplete);
        Assert.True(JsonElement.DeepEquals(continuedSession.State.EventState!.Value, committed.EventState!.Value));
    }

    [Fact]
    public void Stale_derived_actor_speed_is_recomputed_from_current_initialization()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        DriveOpening(world, session);
        session.SetEventState(world.Events.Capture());
        System.Text.Json.Nodes.JsonNode obsolete = System.Text.Json.Nodes.JsonNode.Parse(session.State.EventState!.Value.GetRawText())!;
        foreach (System.Text.Json.Nodes.JsonNode? actor in obsolete["Actors"]!.AsArray())
        {
            actor!["MovementSpeed"] = 1;
        }
        GameState outdated = session.State with { EventState = JsonSerializer.SerializeToElement(obsolete) };
        GameState recreated = GameWorld.RecreateSave(outdated, directory);
        Assert.True(JsonElement.DeepEquals(session.State.EventState!.Value, recreated.EventState!.Value));
        using var restored = new GameWorld(new GameSession(recreated), directory, persistReplayJournal: false);
        restored.EnsureField();
        ActorState player = restored.Events.Actors.Single(actor => actor.Active && actor.CharacterId == 0);
        Assert.Equal(7929, player.MovementSpeed);
    }

    [Fact]
    public void Sampled_movie_clocks_keep_asset_proof_across_repeated_recreation()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed movie corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        var clock = new MovieClock(session, directory);
        using var world = new GameWorld(session, directory, clock, persistReplayJournal: false);
        world.EnsureField();
        for (int frame = 0; frame < 500 && !session.IsMoviePlaying; frame++)
        {
            world.Events.Step();
        }
        Assert.True(session.IsMoviePlaying);
        StorySafeMovieSkipTests.DecodeThrough(session, world, clock, directory, 3);
        session.SetEventState(world.Events.Capture());
        GameState first = GameWorld.RecreateSave(session.State, directory);
        GameState second = GameWorld.RecreateSave(first, directory);
        Assert.True(JsonElement.DeepEquals(first.EventState!.Value, second.EventState!.Value));
        ReplayJournalEntry[] samples = second.ReplayJournal!.Entries.Where(entry => entry.Kind == ReplayEntryKind.MovieSample).ToArray();
        Assert.NotEmpty(samples);
        Assert.All(samples, entry => Assert.Equal(MovieClock.GetSourceHash(directory, 30), entry.MovieSourceHash));

        var forged = GameSession.CreateColdProcess();
        forged.StartNewGame();
        foreach (ReplayJournalEntry entry in session.ReplayJournal!.Entries)
        {
            forged.AppendReplayEntry(entry.Kind == ReplayEntryKind.MovieSample ? entry with { MovieSourceHash = new string('0', 64) } : entry);
        }
        Assert.Throws<InvalidDataException>(() => GameWorld.RecreateSave(session.State with { ReplayJournal = forged.ReplayJournal }, directory));

        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SkipMovie)).Accepted);
        for (int frame = 0; frame < 500 && session.State.FieldId == 74; frame++)
        {
            world.Events.Step();
        }
        Assert.Equal(180, session.State.FieldId);
        Assert.Equal(74, world.Events.FieldId);
        session.SetEventState(null);
        session.SetDepartingFieldState(world.Events.Capture());
        GameState transition = GameWorld.RecreateSave(session.State, directory);
        Assert.Null(transition.EventState);
        Assert.Equal(74, transition.DepartingFieldState!.Value.GetProperty("FieldId").GetInt32());
        using var destination = new GameWorld(new GameSession(transition), directory, persistReplayJournal: false);
        destination.EnsureField();
        world.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), destination.Events.Capture()));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), destination.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), destination.Events.Capture()));

        var duplicated = GameSession.CreateColdProcess();
        duplicated.StartNewGame();
        foreach (ReplayJournalEntry entry in transition.ReplayJournal!.Entries)
        {
            duplicated.AppendReplayEntry(entry);
            if (entry.Kind == ReplayEntryKind.FieldLoad)
            {
                duplicated.AppendReplayEntry(entry);
            }
        }
        Assert.Throws<InvalidDataException>(() => GameWorld.RecreateSave(transition with { ReplayJournal = duplicated.ReplayJournal }, directory));
    }

    private static void DriveOpening(GameWorld world, GameSession session)
    {
        for (int input = 0; input < 6000; input++)
        {
            world.EnsureField();
            if (world.Field!.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                return;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction action = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Cecilia") : dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is not null ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
        }
        Assert.Fail("Fresh opening exceeded its causal action budget.");
    }
}
