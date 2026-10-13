using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ReplayJournalArchiveTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-replay-archive-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Pinned_checkpoint_prefixes_survive_retention_and_later_session_branches()
    {
        ReplayJournalArchive archive = CreateArchive();
        GameSession session = Start();
        ReplayJournal prefix = session.ReplayJournal!;
        string checkpoint = Path.Combine(directory, "outside-player-namespace.json");
        session.Save(checkpoint, false);
        archive.Pin(checkpoint, prefix);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 74, 0) { Advanced = 1 });
        archive.Pin(checkpoint, session.ReplayJournal!);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(directory, "archive", prefix.SessionId), "*.journal.json").Length);
        File.SetLastWriteTimeUtc(archive.GetPath(prefix.SessionId), DateTime.UtcNow.AddDays(-60));
        Assert.Equal(0, archive.Prune(DateTimeOffset.UtcNow, TimeSpan.Zero, 0));
        Assert.True(File.Exists(archive.GetPath(prefix.SessionId)));
        ReplayJournal[] snapshots = Directory.GetFiles(Path.Combine(directory, "archive", prefix.SessionId), "*.journal.json").Select(path => JsonSerializer.Deserialize<ReplayJournal>(File.ReadAllText(path))!).ToArray();
        Assert.Contains(snapshots, journal => journal.EntryCount == 0);
        Assert.Contains(snapshots, journal => journal.EntryCount == 1);
    }

    [Fact]
    public void Unpinned_old_sessions_are_pruned_but_recent_sessions_are_kept()
    {
        ReplayJournalArchive archive = CreateArchive();
        ReplayJournal old = Start().ReplayJournal!;
        ReplayJournal recent = Start().ReplayJournal!;
        archive.Persist(old);
        archive.Persist(recent);
        File.SetLastWriteTimeUtc(archive.GetPath(old.SessionId), DateTime.UtcNow.AddDays(-60));
        Assert.Equal(1, archive.Prune(DateTimeOffset.UtcNow, keepUnreferenced: 0));
        Assert.False(File.Exists(archive.GetPath(old.SessionId)));
        Assert.True(File.Exists(archive.GetPath(recent.SessionId)));
    }

    [Fact]
    public void Registered_player_directory_protects_transferred_saves_without_existing_pin()
    {
        ReplayJournalArchive archive = CreateArchive();
        string saves = Path.Combine(directory, "player-saves");
        archive.RegisterSaveDirectory(saves);
        GameSession session = Start();
        session.Save(Path.Combine(saves, "slot01.json"), false);
        archive.Persist(session.ReplayJournal!);
        File.SetLastWriteTimeUtc(archive.GetPath(session.ReplayJournal!.SessionId), DateTime.UtcNow.AddDays(-60));
        Assert.Equal(0, archive.Prune(DateTimeOffset.UtcNow, TimeSpan.Zero, 0));
    }

    [Fact]
    public void Corrupt_reference_stops_cleanup_conservatively()
    {
        ReplayJournalArchive archive = CreateArchive();
        string saves = Path.Combine(directory, "player-saves");
        Directory.CreateDirectory(saves);
        File.WriteAllText(Path.Combine(saves, "corrupt.json"), "{invalid");
        archive.RegisterSaveDirectory(saves);
        ReplayJournal journal = Start().ReplayJournal!;
        archive.Persist(journal);
        File.SetLastWriteTimeUtc(archive.GetPath(journal.SessionId), DateTime.UtcNow.AddDays(-60));
        Assert.Equal(0, archive.Prune(DateTimeOffset.UtcNow, TimeSpan.Zero, 0));
        Assert.True(File.Exists(archive.GetPath(journal.SessionId)));
    }

    [Fact]
    public void Session_identity_cannot_escape_archive_directory()
    {
        Assert.Throws<InvalidDataException>(() => CreateArchive().GetPath("../outside"));
    }

    [Fact]
    public void Reused_slot_releases_the_previous_session_reference_after_successful_replacement()
    {
        ReplayJournalArchive archive = CreateArchive();
        string slot = Path.Combine(directory, "player-slot.json");
        GameSession old = Start();
        old.Save(slot, false);
        archive.Pin(slot, old.ReplayJournal!);
        File.SetLastWriteTimeUtc(archive.GetPath(old.ReplayJournal!.SessionId), DateTime.UtcNow.AddDays(-60));
        GameSession replacement = Start();
        archive.Pin(slot, replacement.ReplayJournal!);
        replacement.Save(slot, false);
        Assert.Equal(1, archive.Prune(DateTimeOffset.UtcNow, TimeSpan.Zero, 0));
        Assert.False(File.Exists(archive.GetPath(old.ReplayJournal.SessionId)));
        Assert.True(File.Exists(archive.GetPath(replacement.ReplayJournal!.SessionId)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Malformed_pin_or_saved_journal_header_keeps_uncertain_history(bool malformedPin)
    {
        ReplayJournalArchive archive = CreateArchive();
        GameSession session = Start();
        string save = Path.Combine(directory, "checkpoint.json");
        session.Save(save, false);
        archive.Pin(save, session.ReplayJournal!);
        File.SetLastWriteTimeUtc(archive.GetPath(session.ReplayJournal!.SessionId), DateTime.UtcNow.AddDays(-60));
        if (malformedPin)
        {
            File.WriteAllText(Directory.GetFiles(Path.Combine(directory, "archive", "pins")).Single(), "{}");
        }
        else
        {
            File.WriteAllText(save, "{\"ReplayJournal\":{}}");
        }
        Assert.Equal(0, archive.Prune(DateTimeOffset.UtcNow, TimeSpan.Zero, 0));
        Assert.True(File.Exists(archive.GetPath(session.ReplayJournal.SessionId)));
    }

    [Fact]
    public void Superseded_unreferenced_prefixes_are_pruned_without_deleting_either_live_save_branch()
    {
        ReplayJournalArchive archive = CreateArchive();
        GameSession session = Start();
        string first = Path.Combine(directory, "first.json");
        string second = Path.Combine(directory, "second.json");
        session.Save(first, false);
        archive.Pin(first, session.ReplayJournal!);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = 5 });
        session.Save(second, false);
        archive.Pin(second, session.ReplayJournal!);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 1) { Code = 5 });
        archive.Pin(second, session.ReplayJournal!);
        session.Save(second, false);
        string snapshots = Path.Combine(directory, "archive", session.ReplayJournal!.SessionId);
        Assert.Equal(3, Directory.GetFiles(snapshots).Length);
        foreach (string snapshot in Directory.GetFiles(snapshots))
        {
            File.SetLastWriteTimeUtc(snapshot, DateTime.UtcNow.AddDays(-60));
        }
        Assert.Equal(1, archive.Prune(DateTimeOffset.UtcNow, TimeSpan.Zero, 0));
        ReplayJournal[] retained = Directory.GetFiles(snapshots).Select(path => JsonSerializer.Deserialize<ReplayJournal>(File.ReadAllText(path))!).ToArray();
        Assert.Equal(new[] { 0, 2 }, retained.Select(journal => journal.EntryCount).Order().ToArray());
    }

    private ReplayJournalArchive CreateArchive()
    {
        return new ReplayJournalArchive(Path.Combine(directory, "archive"));
    }

    private static GameSession Start()
    {
        var session = new GameSession(new GameState());
        session.StartNewGame();
        return session;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
