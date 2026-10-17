using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record PlayerSaveEntry(int Slot, string Label, DateTime? SavedAt, string Details, bool CanLoad);

/// <summary>Player saves have their own namespace; configured research checkpoints are never menu targets.</summary>
internal sealed class PlayerSaveStore
{
    public const int BankCount = 2;
    public const int SlotsPerBank = 30;
    public const int SlotCount = BankCount * SlotsPerBank;
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria.FF8", "player-saves");
    private readonly string directory;
    private readonly string researchCheckpoint;
    public PlayerSaveStore(string researchCheckpoint, string? directory = null)
    {
        this.directory = Path.GetFullPath(directory ?? DefaultDirectory);
        this.researchCheckpoint = Path.GetFullPath(researchCheckpoint);
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

    public void Save(int slot, GameSession session, Action<GameState>? validateContinuation = null)
    {
        if (session.State.Scene == SceneKind.Title)
        {
            throw new InvalidOperationException("Start or load a game before saving.");
        }
        string path = GetPath(slot);
        session.Save(path, compact: false, validateContinuation);
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
                    entries.Add(new PlayerSaveEntry(slot, label, null, "Empty", false));
                    continue;
                }
                GameState state = Load(slot);
                string location = state.Scene switch
                {
                    SceneKind.WorldMap => "World map",
                    SceneKind.Movie => $"Movie {state.MovieIndex:00} at {state.MovieSeconds:F1}s",
                    _ => $"{state.FieldName} (field {state.FieldId})"
                };
                entries.Add(new PlayerSaveEntry(slot, label, File.GetLastWriteTime(path), location, true));
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                entries.Add(new PlayerSaveEntry(slot, label, null, "Unavailable: " + exception.Message, false));
            }
        }
        return entries;
    }

    private static bool IsUnavailable(Exception exception)
    {
        return exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or ArgumentException or NotSupportedException;
    }
}
