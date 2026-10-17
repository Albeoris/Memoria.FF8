using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record PlayerSaveEntry(int Slot, string Label, DateTime? SavedAt, string Details, bool CanLoad);

/// <summary>Player saves have their own namespace; configured checkpoints are never menu targets.</summary>
internal sealed class PlayerSaveStore
{
    public const int BankCount = 2;
    public const int SlotsPerBank = 30;
    public const int SlotCount = BankCount * SlotsPerBank;
    public const int DefaultQuickSaveCount = 9;
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria.FF8", "player-saves");
    private readonly string directory;
    private readonly string researchCheckpoint;
    private int quickSaveCount = DefaultQuickSaveCount;

    public PlayerSaveStore(string researchCheckpoint, string? directory = null)
    {
        this.directory = Path.GetFullPath(directory ?? DefaultDirectory);
        this.researchCheckpoint = Path.GetFullPath(researchCheckpoint);
    }

    public void SetQuickSaveCount(int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        List<QuickSaveFile> files = FindQuickSaves();
        QuickSaveFile[] obsolete = files.Skip(count).ToArray();
        ValidateRemoval(obsolete);
        foreach (QuickSaveFile file in obsolete)
        {
            File.Delete(file.Path);
        }
        quickSaveCount = count;
    }

    public string GetPath(int slot)
    {
        if (slot > SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        string path = slot switch
        {
            0 => FindQuickSaves().FirstOrDefault()?.Path ?? Path.Combine(directory, "quick.json"),
            int.MinValue => Path.Combine(directory, "quick.json"),
            < 0 => GetQuickPath(-slot),
            _ => Path.Combine(directory, $"slot-{slot:00}.json")
        };
        ValidatePath(path);
        return path;
    }

    public static string GetLabel(int slot)
    {
        if (slot > SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return slot <= 0 ? "Quick Save" : $"Bank {(slot - 1) / SlotsPerBank + 1}, Slot {(slot - 1) % SlotsPerBank + 1:00}";
    }

    public bool ContainsPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return fullPath.Equals(directory, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public void Save(int slot, GameSession session, Action<GameState>? validateContinuation = null)
    {
        if (slot < 0 || slot > SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        if (session.State.Scene == SceneKind.Title)
        {
            throw new InvalidOperationException("Start or load a game before saving.");
        }
        if (slot != 0)
        {
            session.Save(GetPath(slot), compact: false, validateContinuation);
            return;
        }
        List<QuickSaveFile> files = FindQuickSaves();
        int sequence = files.Where(file => file.Slot != int.MinValue).Select(file => -file.Slot).DefaultIfEmpty(0).Max();
        if (sequence == int.MaxValue)
        {
            throw new InvalidOperationException("The quick save sequence is exhausted.");
        }
        string path = GetQuickPath(sequence + 1);
        ValidatePath(path);
        QuickSaveFile[] obsolete = files.Skip(quickSaveCount - 1).ToArray();
        ValidateRemoval(obsolete);
        DateTime savedAt = DateTime.UtcNow;
        if (files.Count > 0 && files[0].SavedAtUtc >= savedAt)
        {
            savedAt = files[0].SavedAtUtc.AddMilliseconds(1);
        }
        session.Save(path, compact: false, candidate =>
        {
            validateContinuation?.Invoke(candidate);
            File.SetLastWriteTimeUtc(path + ".tmp", savedAt);
        });
        foreach (QuickSaveFile file in obsolete)
        {
            File.Delete(file.Path);
        }
    }

    public GameState Load(int slot)
    {
        return LoadPath(GetPath(slot));
    }

    public IReadOnlyList<PlayerSaveEntry> ReadQuickEntries()
    {
        List<QuickSaveFile> files = FindQuickSaves();
        var entries = new List<PlayerSaveEntry>(files.Count);
        for (int index = 0; index < files.Count; index++)
        {
            QuickSaveFile file = files[index];
            entries.Add(ReadEntry(index == 0 ? 0 : file.Slot, file.Path, file.SavedAtUtc.ToLocalTime()));
        }
        return entries;
    }

    public IReadOnlyList<PlayerSaveEntry> ReadEntries(bool includeQuick)
    {
        var entries = new List<PlayerSaveEntry>();
        if (includeQuick)
        {
            entries.Add(ReadQuickEntries().FirstOrDefault() ?? new PlayerSaveEntry(0, GetLabel(0), null, "Empty", false));
        }
        for (int slot = 1; slot <= SlotCount; slot++)
        {
            entries.Add(ReadEntry(slot, Path.Combine(directory, $"slot-{slot:00}.json"), null));
        }
        return entries;
    }

    private PlayerSaveEntry ReadEntry(int slot, string path, DateTime? savedAt)
    {
        string label = GetLabel(slot);
        try
        {
            ValidatePath(path);
            if (!File.Exists(path))
            {
                return new PlayerSaveEntry(slot, label, null, "Empty", false);
            }
            savedAt ??= File.GetLastWriteTime(path);
            GameState state = LoadPath(path);
            string location = state.Scene switch
            {
                SceneKind.WorldMap => "World map",
                SceneKind.Movie => $"Movie {state.MovieIndex:00} at {state.MovieSeconds:F1}s",
                _ => $"{state.FieldName} (field {state.FieldId})"
            };
            return new PlayerSaveEntry(slot, label, savedAt, location, true);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return new PlayerSaveEntry(slot, label, savedAt, "Unavailable: " + exception.Message, false);
        }
    }

    private static GameState LoadPath(string path)
    {
        GameState state = GameSession.Load(path);
        if (state.Scene == SceneKind.Title)
        {
            throw new InvalidDataException("This save contains no active game.");
        }
        return state;
    }

    private List<QuickSaveFile> FindQuickSaves()
    {
        var files = new List<QuickSaveFile>();
        if (!Directory.Exists(directory))
        {
            return files;
        }
        string legacyPath = Path.Combine(directory, "quick.json");
        if (File.Exists(legacyPath))
        {
            files.Add(new QuickSaveFile(int.MinValue, legacyPath, File.GetLastWriteTimeUtc(legacyPath)));
        }
        foreach (string path in Directory.EnumerateFiles(directory, "quick-*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (int.TryParse(name.AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence) && sequence > 0 && path.Equals(GetQuickPath(sequence), StringComparison.OrdinalIgnoreCase))
            {
                files.Add(new QuickSaveFile(-sequence, path, File.GetLastWriteTimeUtc(path)));
            }
        }
        return files.OrderByDescending(file => file.SavedAtUtc).ThenByDescending(file => file.Slot == int.MinValue ? 0 : -file.Slot).ToList();
    }

    private string GetQuickPath(int sequence)
    {
        return Path.Combine(directory, "quick-" + sequence.ToString("D10", CultureInfo.InvariantCulture) + ".json");
    }

    private void ValidateRemoval(IEnumerable<QuickSaveFile> files)
    {
        foreach (QuickSaveFile file in files)
        {
            ValidatePath(file.Path);
        }
    }

    private void ValidatePath(string path)
    {
        if (path.Equals(researchCheckpoint, StringComparison.OrdinalIgnoreCase) || (path + ".tmp").Equals(researchCheckpoint, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This player slot conflicts with the configured checkpoint. Choose another slot.");
        }
    }

    private static bool IsUnavailable(Exception exception)
    {
        return exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or ArgumentException or NotSupportedException;
    }

    private sealed record QuickSaveFile(int Slot, string Path, DateTime SavedAtUtc);
}
