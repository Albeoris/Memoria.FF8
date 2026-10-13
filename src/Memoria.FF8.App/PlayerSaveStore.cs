using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record PlayerSaveEntry(int Slot, string Label, DateTime? SavedAt, string Details, bool CanLoad, bool CanRecreate = false, string? RecreateUnavailableReason = null);

/// <summary>Player saves have their own namespace; configured research checkpoints are never menu targets.</summary>
internal sealed class PlayerSaveStore
{
    public const int BankCount = 2;
    public const int SlotsPerBank = 30;
    public const int SlotCount = BankCount * SlotsPerBank;
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria.FF8", "player-saves");
    private readonly string directory;
    private readonly string researchCheckpoint;
    private readonly ReplayJournalArchive journalArchive;

    public PlayerSaveStore(string researchCheckpoint, string? directory = null, ReplayJournalArchive? journalArchive = null)
    {
        this.directory = Path.GetFullPath(directory ?? DefaultDirectory);
        this.researchCheckpoint = Path.GetFullPath(researchCheckpoint);
        this.journalArchive = journalArchive ?? new ReplayJournalArchive(ReplayJournalArchive.DefaultDirectory);
        try
        {
            this.journalArchive.RegisterSaveDirectory(this.directory);
            this.journalArchive.Prune(DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Replay journal cleanup deferred: " + exception.Message);
        }
    }

    public string GetPath(int slot)
    {
        if (slot is < 0 or > SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        string path = Path.Combine(directory, slot == 0 ? "quick.json" : $"slot-{slot:00}.json");
        if (path.Equals(researchCheckpoint, StringComparison.OrdinalIgnoreCase) || (path + ".tmp").Equals(researchCheckpoint, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This player slot conflicts with the configured research checkpoint. Choose another slot.");
        }
        return path;
    }

    public static string GetLabel(int slot)
    {
        if (slot is < 0 or > SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return slot == 0 ? "Quick Save" : $"Bank {(slot - 1) / SlotsPerBank + 1}, Slot {(slot - 1) % SlotsPerBank + 1:00}";
    }

    public bool ContainsPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return fullPath.Equals(directory, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public void Save(int slot, GameSession session)
    {
        if (session.State.Scene == SceneKind.Title)
        {
            throw new InvalidOperationException("Start or load a game before saving.");
        }
        string path = GetPath(slot);
        if (session.State.ReplayJournal is ReplayJournal journal)
        {
            journalArchive.Pin(path, journal);
        }
        session.Save(path, registerJournalReference: false);
    }

    public GameState Load(int slot)
    {
        GameState state = GameSession.Load(GetPath(slot));
        if (state.Scene == SceneKind.Title)
        {
            throw new InvalidDataException("This save contains no active game.");
        }
        return state;
    }

    public GameState Recreate(int slot, Func<GameState, GameState> recreate)
    {
        string path = GetPath(slot);
        byte[] original = File.ReadAllBytes(path);
        GameState source = GameSession.LoadReplaySource(path);
        string? reason = GetRecreateUnavailableReason(source);
        if (reason is not null)
        {
            throw new InvalidOperationException(reason);
        }
        if (!original.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new IOException("The selected save changed before recreation. Select it again.");
        }
        GameState recreated = recreate(source);
        if (recreated.Scene == SceneKind.Title)
        {
            throw new InvalidDataException("Recreation did not produce an active game.");
        }
        var candidate = new GameSession(recreated);
        string temporary = path + ".recreate-" + Guid.NewGuid().ToString("N");
        bool replaced = false;
        try
        {
            candidate.Save(temporary, registerJournalReference: false);
            if (!original.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
            {
                throw new IOException("The selected save changed during recreation. Its newer contents were preserved.");
            }
            if (recreated.ReplayJournal is ReplayJournal journal)
            {
                journalArchive.Pin(path, journal);
            }
            File.Move(temporary, path, true);
            replaced = true;
        }
        finally
        {
            if (!replaced)
            {
                File.Delete(temporary);
                File.Delete(temporary + ".tmp");
            }
        }
        return recreated;
    }

    private static string? GetRecreateUnavailableReason(GameState state)
    {
        if (state.Scene == SceneKind.Title)
        {
            return "This save contains no active game.";
        }
        if (state.ReplayJournal is null)
        {
            return "This save has no recorded history from New Game. Load normally or start a new game to record one.";
        }
        return state.ReplayJournal.IsComplete ? null : state.ReplayJournal.UnavailableReason ?? "The recorded history is incomplete.";
    }

    public IReadOnlyList<PlayerSaveEntry> ReadEntries(bool includeQuick)
    {
        var entries = new List<PlayerSaveEntry>();
        for (int slot = includeQuick ? 0 : 1; slot <= SlotCount; slot++)
        {
            string label = GetLabel(slot);
            try
            {
                string path = GetPath(slot);
                if (!File.Exists(path))
                {
                    entries.Add(new PlayerSaveEntry(slot, label, null, "Empty", false, RecreateUnavailableReason: "This slot is empty."));
                    continue;
                }
                GameState state;
                bool canLoad = true;
                string? loadFailure = null;
                try
                {
                    state = Load(slot);
                }
                catch (Exception exception) when (IsUnavailable(exception))
                {
                    state = GameSession.LoadReplaySource(path);
                    canLoad = false;
                    loadFailure = exception.Message;
                }
                string location = state.Scene switch
                {
                    SceneKind.WorldMap => "World map",
                    SceneKind.Movie => $"Movie {state.MovieIndex:00} at {state.MovieSeconds:F1}s",
                    _ => $"{state.FieldName} (field {state.FieldId})"
                };
                string? reason = GetRecreateUnavailableReason(state);
                if (!canLoad)
                {
                    location = "Normal load unavailable: " + loadFailure;
                }
                entries.Add(new PlayerSaveEntry(slot, label, File.GetLastWriteTime(path), location, canLoad, reason is null, reason));
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                entries.Add(new PlayerSaveEntry(slot, label, null, "Unavailable: " + exception.Message, false, RecreateUnavailableReason: "This save is unavailable: " + exception.Message));
            }
        }
        return entries;
    }

    private static bool IsUnavailable(Exception exception)
    {
        return exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or ArgumentException or NotSupportedException;
    }
}
