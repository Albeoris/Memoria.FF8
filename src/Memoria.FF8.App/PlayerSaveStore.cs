using System.Globalization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record PlayerSaveEntry(int Slot, string Label, DateTime? SavedAt, string Details, bool CanLoad)
{
    public TimeSpan? PlayTime { get; init; }
    public IReadOnlyList<int> PartyPortraits { get; init; } = [];
    public string PlayTimeText => PlayTime is { } time ? FormattableString.Invariant($"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}") : "--:--:--";
}

/// <summary>Player saves have their own namespace; configured checkpoints are never menu targets.</summary>
internal sealed partial class PlayerSaveStore
{
    public const int BankCount = 3;
    public const int MemoriaSlotCount = 100;
    public const int OriginalSlotsPerBank = 30;
    public const int SlotCount = MemoriaSlotCount;
    public const int TotalSlotCount = MemoriaSlotCount + 2 * OriginalSlotsPerBank;
    public const int DefaultQuickSaveCount = 9;
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria.FF8", "player-saves");
    private readonly string directory;
    private readonly string researchCheckpoint;
    private readonly Func<GameState, string?>? locationName;
    private readonly Func<string, GameState>? loadOriginal;
    private readonly Dictionary<string, CachedSaveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private int quickSaveCount = DefaultQuickSaveCount;

    public PlayerSaveStore(string researchCheckpoint, string? directory = null, Func<GameState, string?>? locationName = null, string? originalDirectory = null, Func<string, GameState>? loadOriginal = null)
    {
        this.directory = Path.GetFullPath(directory ?? DefaultDirectory);
        this.researchCheckpoint = Path.GetFullPath(researchCheckpoint);
        this.locationName = locationName;
        this.loadOriginal = loadOriginal;
        OriginalDirectoryCandidates = OriginalSaveLocation.FindCandidates();
        OriginalDirectory = originalDirectory is not null ? OriginalSaveLocation.ValidateDirectory(originalDirectory) : OriginalDirectoryCandidates.Count == 1 ? OriginalDirectoryCandidates[0] : null;
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
            entries.Remove(file.Path);
        }
        quickSaveCount = count;
    }

    public string GetPath(int slot)
    {
        if (slot > TotalSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        string path = slot switch
        {
            0 => FindQuickSaves().FirstOrDefault()?.Path ?? Path.Combine(directory, "quick.json"),
            int.MinValue => Path.Combine(directory, "quick.json"),
            < 0 => GetQuickPath(-slot),
            > MemoriaSlotCount => GetOriginalPath(slot),
            _ => Path.Combine(directory, $"slot-{slot:00}.json")
        };
        ValidatePath(path);
        return path;
    }

    public static string GetLabel(int slot)
    {
        if (slot > TotalSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        if (slot <= 0)
        {
            return "Quick Save";
        }
        int bank = GetBank(slot);
        return bank == 0 ? $"Memoria, Slot {slot:00}" : $"ORIGINAL FF8 {(bank == 1 ? "I" : "II")}, Slot {slot - GetFirstSlot(bank) + 1:00}";
    }

    public bool ContainsPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return IsInDirectory(fullPath, directory) || OriginalDirectory is { } original && IsInDirectory(fullPath, original);
    }

    public void Save(int slot, GameSession session, Action<GameState>? validateContinuation = null)
    {
        if (slot < 0 || slot > TotalSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        if (session.State.Scene == SceneKind.Title)
        {
            throw new InvalidOperationException("Start or load a game before saving.");
        }
        if (slot != 0)
        {
            string target = GetPath(slot);
            if (slot > MemoriaSlotCount)
            {
                SaveOriginal(target, NativePcSaveConverter.Export(session.State), validateContinuation);
            }
            else
            {
                session.Save(target, compact: true, validateContinuation);
            }
            entries.Remove(target);
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
        session.Save(path, compact: true, candidate =>
        {
            validateContinuation?.Invoke(candidate);
            File.SetLastWriteTimeUtc(path + ".tmp", savedAt);
        });
        entries.Remove(path);
        foreach (QuickSaveFile file in obsolete)
        {
            File.Delete(file.Path);
            entries.Remove(file.Path);
        }
    }

    public GameState Load(int slot)
    {
        string path = GetPath(slot);
        return slot > MemoriaSlotCount ? LoadOriginal(path) : LoadPath(path);
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
        return ReadEntries(0, includeQuick);
    }

    public IReadOnlyList<PlayerSaveEntry> ReadEntries(int bank, bool includeQuick)
    {
        var entries = new List<PlayerSaveEntry>();
        int firstSlot = GetFirstSlot(bank);
        if (includeQuick && bank == 0)
        {
            entries.Add(ReadQuickEntries().FirstOrDefault() ?? new PlayerSaveEntry(0, GetLabel(0), null, "Empty", false));
        }
        for (int slot = firstSlot; slot < firstSlot + GetBankSlotCount(bank); slot++)
        {
            if (bank != 0 && OriginalDirectory is null)
            {
                entries.Add(new PlayerSaveEntry(slot, GetLabel(slot), null, "Choose the original FF8 save folder.", false));
            }
            else
            {
                entries.Add(ReadEntry(slot, bank == 0 ? Path.Combine(directory, $"slot-{slot:00}.json") : GetOriginalPath(slot), null));
            }
        }
        return entries;
    }

    private PlayerSaveEntry ReadEntry(int slot, string path, DateTime? savedAt)
    {
        string label = GetLabel(slot);
        try
        {
            ValidatePath(path);
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                entries.Remove(path);
                return new PlayerSaveEntry(slot, label, null, "Empty", false);
            }
            savedAt ??= file.LastWriteTime;
            if (entries.TryGetValue(path, out CachedSaveEntry? cached) && cached.Length == file.Length && cached.ModifiedUtc == file.LastWriteTimeUtc)
            {
                return cached.Entry with { Slot = slot, Label = label, SavedAt = savedAt };
            }
            PlayerSaveEntry entry;
            try
            {
                SaveSummary? summary = slot > MemoriaSlotCount ? ReadOriginalSummary(path) : GameSession.ReadSummary(path);
                GameState state = summary?.ToLocationState() ?? LoadPath(path);
                if (state.Scene == SceneKind.Title)
                {
                    throw new InvalidDataException("This save contains no active game.");
                }
                string location = locationName?.Invoke(state) ?? "Unavailable";
                NativePlaytimeState? playtime = summary?.Playtime ?? state.Playtime;
                entry = new PlayerSaveEntry(slot, label, savedAt, location, true)
                {
                    PlayTime = playtime is not null ? TimeSpan.FromSeconds(playtime.Seconds) : null,
                    PartyPortraits = summary?.PartyPortraits ?? (state.CharacterVitals is { } vitals ? vitals.CombatParty.Where(row => row != 255).Select(row => (int)vitals.Records[row].CharacterId).ToArray() : [])
                };
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                entry = new PlayerSaveEntry(slot, label, savedAt, "Unavailable: " + exception.Message, false);
            }
            entries[path] = new CachedSaveEntry(file.Length, file.LastWriteTimeUtc, entry);
            return entry;
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
    private sealed record CachedSaveEntry(long Length, DateTime ModifiedUtc, PlayerSaveEntry Entry);
}
