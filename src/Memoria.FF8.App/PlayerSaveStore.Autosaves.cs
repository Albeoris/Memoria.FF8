using System.Globalization;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class PlayerSaveStore
{
    // Keep autosave IDs disjoint from both games' manual and quick-save slots.
    private const int FirstAutoSaveSlot = 1000;

    internal static AutoSaveKind? GetAutoSaveKind(int slot)
    {
        if (slot < FirstAutoSaveSlot)
        {
            return null;
        }
        return slot % 2 == 0 ? AutoSaveKind.Location : AutoSaveKind.Interval;
    }

    private static string AutoSaveLabel(AutoSaveKind kind)
    {
        return kind == AutoSaveKind.Location ? "Location Autosave" : "Interval Autosave";
    }

    internal void SaveAuto(AutoSaveKind kind, int count, GameSession session, Action<GameState>? validateContinuation, Func<string, Action<GameState>?, DateTime?, GameState> writeGameplay)
    {
        if (Game != SessionGame.Ff8)
        {
            throw new InvalidOperationException("Use the FFVII save contract in the FFVII world.");
        }
        if (session.State.Scene == SceneKind.Title)
        {
            throw new InvalidOperationException("Start or load a game before saving.");
        }
        RotateAutoSaves(kind, count, (path, savedAt) => writeGameplay(path, validateContinuation, savedAt));
    }

    internal void SaveAutoFf7(AutoSaveKind kind, int count, CombinedSessionSave saved)
    {
        ValidateFf7Save(saved);
        RotateAutoSaves(kind, count, (path, _) => CombinedSessionCodec.Save(path, saved));
    }

    internal IReadOnlyList<PlayerSaveEntry> ReadRecentEntries()
    {
        return ReadQuickEntries().Concat(ReadAutoEntries(AutoSaveKind.Location)).Concat(ReadAutoEntries(AutoSaveKind.Interval)).OrderByDescending(entry => entry.SavedAtUtc).ToArray();
    }

    private IEnumerable<PlayerSaveEntry> ReadAutoEntries(AutoSaveKind kind)
    {
        return FindAutoSaves(kind).Select(file => ReadEntry(file.Slot, file.Path, file.SavedAtUtc.ToLocalTime()));
    }

    private void RotateAutoSaves(AutoSaveKind kind, int count, Action<string, DateTime> write)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        List<AutoSaveFile> files = FindAutoSaves(kind);
        int firstSlot = kind == AutoSaveKind.Location ? FirstAutoSaveSlot : FirstAutoSaveSlot + 1;
        int slot = files.Count == 0 ? firstSlot : NextAutoSaveSlot(files[0].Slot);
        string path = AutoSavePath(kind, slot);
        while (IsCheckpointPath(path) || File.Exists(path) || Directory.Exists(path))
        {
            slot = NextAutoSaveSlot(slot);
            path = AutoSavePath(kind, slot);
        }

        DateTime savedAt = DateTime.UtcNow;
        foreach (AutoSaveFile file in files)
        {
            if (file.SavedAtUtc >= savedAt)
            {
                savedAt = file.SavedAtUtc.AddMilliseconds(1);
            }
        }
        string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        ValidatePath(staging);
        try
        {
            write(staging, savedAt);
            File.SetLastWriteTimeUtc(staging, savedAt);
            File.Move(staging, path);
            entries.Remove(path);
        }
        finally
        {
            if (File.Exists(staging))
            {
                File.Delete(staging);
            }
        }

        TrimAutoSaves(kind, count);
    }

    private void TrimAutoSaves(AutoSaveKind kind, int count)
    {
        List<AutoSaveFile> files;
        try
        {
            files = FindAutoSaves(kind);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not list autosaves for cleanup: {exception.Message}");
            return;
        }

        foreach (AutoSaveFile file in files.Skip(count))
        {
            try
            {
                if (!IsCompletedPath(file.Path))
                {
                    File.Delete(file.Path);
                    entries.Remove(file.Path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Could not remove an older autosave: {exception.Message}");
            }
        }
    }

    private List<AutoSaveFile> FindAutoSaves(AutoSaveKind kind)
    {
        string prefix = AutoSavePrefix(kind);
        var files = new List<AutoSaveFile>();
        if (Directory.Exists(directory))
        {
            foreach (string path in Directory.EnumerateFiles(directory, prefix + "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int slot) && GetAutoSaveKind(slot) == kind && path.Equals(AutoSavePath(kind, slot), StringComparison.OrdinalIgnoreCase) && !IsCheckpointPath(path))
                {
                    files.Add(new AutoSaveFile(slot, path, File.GetLastWriteTimeUtc(path)));
                }
            }
        }
        return files.OrderByDescending(file => file.Slot).ToList();
    }

    private string AutoSavePath(AutoSaveKind kind, int slot)
    {
        return Path.Combine(directory, AutoSavePrefix(kind) + slot.ToString("D10", CultureInfo.InvariantCulture) + ".json");
    }

    private static string AutoSavePrefix(AutoSaveKind kind)
    {
        return kind switch
        {
            AutoSaveKind.Location => "autosave-location-",
            AutoSaveKind.Interval => "autosave-interval-",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static int NextAutoSaveSlot(int slot)
    {
        if (slot > int.MaxValue - 2)
        {
            throw new InvalidOperationException("The autosave sequence is exhausted.");
        }
        return slot + 2;
    }

    private sealed record AutoSaveFile(int Slot, string Path, DateTime SavedAtUtc);
}
