using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExamTimerContinuationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void AuthenticGuardChoicePreservesProcessClockAndIndependentCurrentOwners()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string sourcePath = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        ReplayJournal journal = GameSession.LoadReplaySource(sourcePath).ReplayJournal!.GetCommittedPrefix();
        ReplayJournalEntry[] entries = journal.Entries.ToArray();
        int choiceIndex = Array.FindIndex(entries, entry => entry.Kind == ReplayEntryKind.Action && entry.FieldId == 129 && entry.Code == (int)PlayerActionKind.Choose && entry.MessageId == 9);
        Assert.True(choiceIndex > 0, "Authentic journal must contain the actual guard time decision.");
        GameState recreated = RecreateRecordedPrefix(sourcePath, directory, choiceIndex);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        world.EnsureField();
        for (int callback = 0; world.Events.CurrentDialogue is not { CanConfirm: true }; callback++)
        {
            Assert.True(callback < 3000);
            Assert.Equal(129, world.Events.FieldId);
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(9, world.Events.CurrentDialogue.MessageId);
        Assert.Equal((entries[choiceIndex].ObjectId, entries[choiceIndex].ScriptId, entries[choiceIndex].Operation), (world.Events.CurrentDialogue.ObjectId, world.Events.CurrentDialogue.ScriptId, world.Events.CurrentDialogue.Operation));
        Assert.Equal((0, 3), (world.Events.CurrentDialogue.FirstChoice, world.Events.CurrentDialogue.LastChoice));
        Assert.False(session.ExamTimer!.TimerMemoryActivated);
        long callsBeforeChoice = session.ExamTimer.ClockCalls;
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Choose, Value: 3)).Accepted);
        for (int callback = 0; session.ExamTimer is not { DisplayEnabled: true }; callback++)
        {
            Assert.True(callback < 3000);
            Assert.Null(world.Events.LastFault);
            Assert.False(world.Events.CurrentDialogue?.IsNamePrompt == true || world.Events.CurrentDialogue?.FirstChoice is not null);
            PlayerAction action = world.Events.CurrentDialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
        }
        Assert.Equal(2400, session.ExamTimer.RequireRemaining());
        Assert.True(session.ExamTimer.ClockCalls > callsBeforeChoice);
        Assert.True((session.WorldControls!.EventFlags104 & 0x40) != 0);
        Assert.True(session.ExamTimer.Event182Mask4);
        Assert.True(session.ExamTimer.SaveGlobalMask4);
        session.SetEventState(world.Events.Capture());
        GameState copied = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State, JsonOptions), JsonOptions)!;
        using var restored = new GameWorld(new GameSession(copied), directory, persistReplayJournal: false);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        world.SetRenderSize(320, 240);
        restored.SetRenderSize(320, 240);
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    internal static GameState RecreateRecordedPrefix(string sourcePath, string directory, int selectedCount)
    {
        ReplayJournal journal = GameSession.LoadReplaySource(sourcePath).ReplayJournal!.GetCommittedPrefix();
        Assert.InRange(selectedCount, 1, journal.EntryCount);
        ReplayJournalEntry[] selected = journal.Entries.Take(selectedCount).ToArray();
        // Select existing committed transactions without changing a single action or importing snapshot state.
        JsonObject capsule = JsonSerializer.SerializeToNode(journal)!.AsObject();
        using MemoryStream compressed = new();
        using (BrotliStream writer = new(compressed, CompressionLevel.Optimal, true))
        {
            writer.Write(JsonSerializer.SerializeToUtf8Bytes(selected));
        }
        capsule["EntryCount"] = selected.Length;
        capsule["Data"] = Convert.ToBase64String(compressed.ToArray());
        ReplayJournal prefix = capsule.Deserialize<ReplayJournal>()!;
        ReplayJournalEntry endpoint = selected[^1];
        return GameWorld.RecreateSave(new GameState { Scene = endpoint.ResultScene ?? endpoint.Scene, FieldId = endpoint.ResultFieldId ?? endpoint.FieldId, ReplayJournal = prefix }, directory);
    }
}
