using System.Globalization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record PlayerSaveEntry(int Slot, string Label, DateTime? SavedAt, string Details, bool CanLoad)
{
    public DateTime? SavedAtUtc { get; init; }
    public DateTimeOffset? SavedLocalAt { get; init; }
    public TimeSpan? PlayTime { get; init; }
    public IReadOnlyList<int> PartyPortraits { get; init; } = [];
    public string SavedAtText => (SavedLocalAt?.DateTime ?? SavedAt)?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";
    public string PlayTimeText => PlayTime is { } time ? FormattableString.Invariant($"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}") : "--:--:--";
}

internal sealed record QuickSaveCleanupResult(int Removed, int Remaining, string? Error)
{
    public string Message => Error is not null ? $"Quick-save limit saved. Removed {Removed} quick saves; {Remaining} still need cleanup. {Error}" : Remaining > 0 ? $"Quick-save limit saved. {Remaining} older quick saves remain pending cleanup." : "Quick-save limit saved.";
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
        quickSaveCount = count;
    }

    internal int QuickSaveCount => quickSaveCount;

    public int PreviewQuickSaveCount(int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        List<QuickSaveFile> files = FindQuickSaves();
        QuickSaveFile[] obsolete = count > quickSaveCount ? [] : files.Skip(count).ToArray();
        ValidateRemoval(obsolete);
        return obsolete.Length;
    }

    public QuickSaveCleanupResult CommitQuickSaveCount(int count, int confirmedRemovalCount, Action persist)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        List<QuickSaveFile> files = FindQuickSaves();
        QuickSaveFile[] obsolete = count > quickSaveCount ? [] : files.Skip(count).ToArray();
        ValidateRemoval(obsolete);
        if (obsolete.Length != confirmedRemovalCount)
        {
            throw new InvalidOperationException("The quick-save list changed. Review the number to remove again.");
        }
        persist();
        SetQuickSaveCount(count);
        int removed = 0;
        foreach (QuickSaveFile file in obsolete)
        {
            try
            {
                File.Delete(file.Path);
                entries.Remove(file.Path);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new QuickSaveCleanupResult(removed, obsolete.Length - removed, exception.Message);
            }
        }
        return new QuickSaveCleanupResult(removed, Math.Max(0, files.Count - count - removed), null);
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

    public static bool RequiresMemoriaFormat(GameState state, bool allowGuardianForceProjection = false)
    {
        return (!allowGuardianForceProjection && state.CharacterVitals?.GuardianForceRequiresMemoriaFormat == true) || state.GameplayPreferences.LimitOncePerTurn || state.GameplayPreferences.SelphieCardSlots || state.GameplayPreferences.WeaponMagazineRequired || state.GameplayPreferences.WeaponUpgradeOrder || state.WeaponRecipes?.ObtainedWeaponsBits is > 0 || state.WeaponRecipes?.MagazineRecipeBits is > 0 || state.GameplayPreferences.CardModOnce || !state.RefinedCardIds.IsEmpty || state.GameplayPreferences.DrawOncePerSpell || state.GameplayPreferences.Difficulty != BattleDifficulty.Normal || state.CharacterVitals?.MagicSystem.RequiresMemoriaFormat == true || state.GameplayPreferences.SharedMagicStock || state.GameplayPreferences.InfiniteMagic || state.GameplayPreferences.SmartAttack || state.GameplayPreferences.GuardianFormsEnabled || state.PendingGameplayPreferences is not null;
    }

    public void Save(int slot, GameSession session, Action<GameState>? validateContinuation = null, NativeCharacterVitals? targetSource = null, bool allowGuardianForceLoss = false)
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
                GameState snapshot = session.PrepareSnapshot();
                if (RequiresMemoriaFormat(snapshot, allowGuardianForceProjection: true))
                {
                    throw new NotSupportedException("This game's gameplay rules require a Memoria save slot.");
                }
                if (allowGuardianForceLoss && targetSource is null)
                {
                    throw new InvalidOperationException("Reviewed GF conversion requires the target game's source definitions.");
                }
                byte[] bytes = targetSource is null ? NativePcSaveConverter.Export(snapshot) : NativePcSaveConverter.Export(snapshot, targetSource, allowGuardianForceLoss);
                SaveOriginal(target, bytes, validateContinuation);
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
            entries.AddRange(ReadQuickEntries());
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
        return includeQuick && bank == 0 ? entries.OrderByDescending(entry => entry.SavedAtUtc.HasValue).ThenByDescending(entry => entry.SavedAtUtc).ThenBy(entry => entry.Slot > 0 ? entry.Slot : 0).ToArray() : entries;
    }

    private PlayerSaveEntry ReadEntry(int slot, string path, DateTime? savedAt)
    {
        string label = GetLabel(slot);
        DateTime? savedAtUtc = savedAt?.ToUniversalTime();
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
            savedAtUtc = file.LastWriteTimeUtc;
            if (entries.TryGetValue(path, out CachedSaveEntry? cached) && cached.Length == file.Length && cached.ModifiedUtc == file.LastWriteTimeUtc)
            {
                return cached.Entry with { Slot = slot, Label = label, SavedAt = savedAt, SavedAtUtc = savedAtUtc };
            }
            PlayerSaveEntry entry;
            SaveSummary? summary = null;
            try
            {
                summary = slot > MemoriaSlotCount ? ReadOriginalSummary(path) : GameSession.ReadSummary(path);
                GameState state = summary?.ToLocationState() ?? LoadPath(path);
                if (state.Scene == SceneKind.Title)
                {
                    throw new InvalidDataException("This save contains no active game.");
                }
                string location = locationName?.Invoke(state) ?? "Unavailable";
                NativePlaytimeState? playtime = summary?.Playtime ?? state.Playtime;
                entry = new PlayerSaveEntry(slot, label, savedAt, location, true)
                {
                    SavedAtUtc = savedAtUtc,
                    SavedLocalAt = summary?.SavedLocalAt,
                    PlayTime = playtime is not null ? TimeSpan.FromSeconds(playtime.Seconds) : null,
                    PartyPortraits = summary?.PartyPortraits ?? (state.CharacterVitals is { } vitals ? vitals.CombatParty.Where(row => row != 255).Select(row => (int)vitals.Records[row].CharacterId).ToArray() : [])
                };
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                entry = new PlayerSaveEntry(slot, label, savedAt, "Unavailable: " + exception.Message, false) { SavedAtUtc = savedAtUtc, SavedLocalAt = summary?.SavedLocalAt };
            }
            entries[path] = new CachedSaveEntry(file.Length, file.LastWriteTimeUtc, entry);
            return entry;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return new PlayerSaveEntry(slot, label, savedAt, "Unavailable: " + exception.Message, false) { SavedAtUtc = savedAtUtc };
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
