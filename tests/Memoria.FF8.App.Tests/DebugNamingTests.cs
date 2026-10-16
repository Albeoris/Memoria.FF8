using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DebugNamingTests
{
    [Fact]
    public void Name_validation_counts_graphemes_and_rejects_invalid_or_missing_text()
    {
        Assert.Equal("Twelve", GameNames.Normalize(" Twelve "));
        Assert.Equal("\uFFFD", GameNames.Normalize("\uFFFD"));
        Assert.Equal("{Squall}", GameNames.Normalize("{Squall}"));
        foreach (string grapheme in new[] { "A", "😀", "e\u0301", "👩🏽‍🚀", "🇺🇦" })
        {
            string maximum = string.Concat(Enumerable.Repeat(grapheme, GameNames.MaximumLength));
            Assert.Equal(maximum, GameNames.Normalize(maximum));
            Assert.True(GameNames.IsValidDraft(maximum));
            Assert.False(GameNames.IsValidDraft(maximum + grapheme));
            Assert.Throws<ArgumentException>(() => GameNames.Normalize(maximum + grapheme));
        }
        foreach (string text in new[] { "", "   ", "Bad\nName", "\tName", "Broken\uD800", "Broken\uDC00" })
        {
            Assert.Throws<ArgumentException>(() => GameNames.Normalize(text));
        }
        Assert.Equal(0, GameNames.CharacterSubject(0));
        Assert.Equal(1, GameNames.CharacterSubject(4));
        Assert.Equal(33, GameNames.CharacterSubject(1));
        Assert.False(GameNames.IsValidSubject(36));
        Assert.False(GameNames.IsValidSubject(32));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameNames.GuardianForceSubject(16));
        Assert.True(GameNames.IsValidDraft(""));
        Assert.False(GameNames.IsValidDraft("\uD800"));
        Assert.True(GameNames.IsValidDraft("{Squall}"));
        Assert.False(GameNames.IsValidDraft(null));
    }

    [Theory]
    [InlineData("{Page}")]
    [InlineData("{Color Red}")]
    [InlineData("{Char Rinoa}")]
    [InlineData("{Var 0}")]
    [InlineData("👩🏽‍🚀 e\u0301 🇺🇦")]
    public void Saved_names_are_literal_text_when_native_name_tags_are_expanded(string name)
    {
        Dictionary<int, string> names = new() { [0] = GameNames.Normalize(name), [16] = name };
        DialogueDocument document = DialogueTextFormatter.Parse("{Char Squall}: {Name Quetzacotl}", names);
        Assert.Equal($"{name}: {name}", document.Text);
        Assert.Single(document.Pages);
        Assert.All(document.Pages.SelectMany(page => page.Lines).SelectMany(line => line.Runs), run => Assert.Equal(Microsoft.Xna.Framework.Color.White, run.Color));
    }

    [Fact]
    public void Battle_name_rejection_preserves_snapshot_and_unallocated_field()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, GameDirectory());
        // Isolated admission fixture: this battle snapshot makes no claim of an authentic native route.
        BattleSession fixture = new(new BattleDefinition
        {
            Combatants = [new BattleCombatantDefinition { Id = "player", Name = "Squall", IsPlayer = true }, new BattleCombatantDefinition { Id = "enemy", Name = "Fixture enemy" }]
        }, BattleAtbMode.Normal);
        session.SetBattle(fixture.Capture());
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<NotSupportedException>(() => world.Rename(0, "Changed"));
        Assert.Null(world.Field);
        Assert.Empty(world.Events.Scene.Names);
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void Invalid_subject_or_text_preserves_snapshot_without_allocating_field()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, GameDirectory());
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Rename(32, "Changed"));
        Assert.Throws<ArgumentException>(() => world.Rename(0, "Broken\uD800"));
        Assert.Null(world.Field);
        Assert.Empty(world.Events.Scene.Names);
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void Native_name_prompt_accepts_supplementary_debug_name_and_restores_pending_draft()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, GameDirectory());
        string name = string.Concat(Enumerable.Repeat("😀", 12));
        world.Rename(0, name);
        for (int input = 0; input < 6000 && world.Events.CurrentDialogue?.IsNamePrompt != true; input++)
        {
            world.EnsureField();
            Assert.True(world.Apply(OpeningAction(session, world)).Accepted);
        }
        DialogueState prompt = Assert.IsType<DialogueState>(world.Events.CurrentDialogue);
        Assert.True(prompt.IsNamePrompt);
        Assert.Equal("Squall", world.Events.PendingNameDefault);
        session.SetEventState(world.Events.Capture());
        session.SetDialogueDraft(new DialogueDraft(prompt.Channel, prompt.MessageId, 0, name));
        GameSession restored = new(SaveAndLoad(session));
        using GameWorld loaded = new(restored, GameDirectory());
        loaded.EnsureField();
        Assert.Equal(name, restored.State.DialogueDraft!.EnteredName);
        Assert.Equal("Squall", loaded.Events.PendingNameDefault);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: name)).Accepted);
        Assert.True(loaded.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: name)).Accepted);
        Assert.Equal(name, world.Events.Scene.Names[0]);
        Assert.Equal(name, loaded.Events.Scene.Names[0]);
        Assert.Null(loaded.Events.PendingNameDefault);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), loaded.Events.Capture()));
    }

    [Fact]
    public void Names_survive_native_field_departure_snapshot_load_and_next_wait()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, GameDirectory());
        world.Rename(GameNames.CharacterSubject(1), "Zell custom");
        world.Rename(GameNames.GuardianForceSubject(15), "Eden custom");
        for (int input = 0; input < 6000 && session.State.FieldId == 74; input++)
        {
            world.EnsureField();
            Assert.True(world.Apply(OpeningAction(session, world)).Accepted);
        }
        world.EnsureField();
        Assert.NotEqual(74, session.State.FieldId);
        session.SetEventState(world.Events.Capture());
        GameSession restored = new(SaveAndLoad(session));
        using GameWorld loaded = new(restored, GameDirectory());
        loaded.EnsureField();
        Assert.Equal("Zell custom", loaded.Events.Scene.Names[GameNames.CharacterSubject(1)]);
        Assert.Equal("Eden custom", loaded.Events.Scene.Names[GameNames.GuardianForceSubject(15)]);
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), loaded.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), loaded.Events.Capture()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void All_names_persist_without_frames_and_allow_identical_next_input(bool currentCheckpoint)
    {
        GameSession session;
        if (currentCheckpoint)
        {
            session = new(GameSession.Load(Path.Combine(RepositoryRoot(), "../fixtures/external-fixture.json")));
        }
        else
        {
            session = GameSession.CreateColdProcess();
            session.StartNewGame();
        }
        using GameWorld world = new(session, GameDirectory());
        world.EnsureField();
        long frame = world.Events.Frame;
        string? evidence = currentCheckpoint ? Environment.GetEnvironmentVariable("MEMORIA_FF8_NAMING_EVIDENCE") : null;
        if (evidence is not null)
        {
            session.SetEventState(world.Events.Capture());
            session.Save(Path.Combine(evidence, "snapshot-source-bc769ae6.json"));
        }
        foreach (int subject in Enumerable.Range(0, GameNames.BokoSubject + 1).Where(GameNames.IsValidSubject))
        {
            world.Rename(subject, $"Name {subject}");
        }
        world.Rename(0, string.Concat(Enumerable.Repeat("👩🏽‍🚀", 14)) + "{Page}");
        Assert.Equal(frame, world.Events.Frame);
        Assert.Equal(30, world.Events.Scene.Names.Count);
        if (evidence is not null)
        {
            session.Save(Path.Combine(evidence, "snapshot-named-bc769ae6.json"));
        }
        GameSession restored = new(SaveAndLoad(session));
        using GameWorld loaded = new(restored, GameDirectory());
        loaded.EnsureField();
        Assert.Equal(frame, loaded.Events.Frame);
        foreach ((int subject, string name) in world.Events.Scene.Names)
        {
            Assert.Equal(name, loaded.Events.Scene.Names[subject]);
        }
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), loaded.Events.Capture()));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), loaded.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), loaded.Events.Capture()));
        if (evidence is not null)
        {
            restored.SetEventState(loaded.Events.Capture());
            restored.Save(Path.Combine(evidence, "snapshot-loaded-bc769ae6.json"));
            File.WriteAllText(Path.Combine(evidence, "snapshot-proof-bc769ae6.json"), JsonSerializer.Serialize(new
            {
                Baseline = "main bc769ae6 standalone snapshot contract",
                Implementations = new[] { "Memoria naming on normally loaded current field220 snapshot", "Memoria independent ordinary snapshot load and next Wait" },
                OriginalFF8Parity = "Naming is an explicit research command. Save/load consistency does not establish original FF8 agreement.",
                Source = "snapshot-source-bc769ae6.json",
                Named = "snapshot-named-bc769ae6.json",
                LoadedAndWaited = "snapshot-loaded-bc769ae6.json",
                FieldId = restored.State.FieldId,
                RenameFrame = frame,
                NextWaitFrame = loaded.Events.Frame,
                All28NamesPersist = true,
                RenameDoesNotAdvanceFrame = true,
                LoadedVmAndNextWaitAgree = true
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static PlayerAction OpeningAction(GameSession session, GameWorld world)
    {
        DialogueState? dialogue = world.Events.CurrentDialogue;
        return session.IsMoviePlaying ? new(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new(PlayerActionKind.EnterName, Text: "Squall") : dialogue?.FirstChoice is int choice ? new(PlayerActionKind.Choose, choice) : world.Events.CanConfirmDialogue ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait);
    }

    private static GameState SaveAndLoad(GameSession session)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ff8-names-{Guid.NewGuid():N}.json");
        try
        {
            session.Save(path);
            return GameSession.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string GameDirectory() => Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory?.FullName ?? throw new DirectoryNotFoundException("FF8 repository root is required.");
    }
}
