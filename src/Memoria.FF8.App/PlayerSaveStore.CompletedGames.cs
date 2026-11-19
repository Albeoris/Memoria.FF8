using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class PlayerSaveStore
{
    private readonly Dictionary<string, string> completedOverwriteApprovals = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PlayerSaveEntry> ReadCompletedEntries()
    {
        return Game == Memoria.FF7.Runtime.SessionGame.Ff8 ? ReadEntries(0, true).Where(entry => entry.CanLoad && entry.IsCompletedGame).ToArray() : [];
    }

    internal string? CompletedRevision(int slot)
    {
        string path = GetPath(slot);
        return IsCompletedPath(path) ? Revision(path) : null;
    }

    internal void ApproveCompletedOverwrite(int slot, string reviewedRevision)
    {
        string path = GetPath(slot);
        if (!IsCompletedPath(path) || Revision(path) != reviewedRevision)
        {
            throw new InvalidOperationException("The destination changed. Review the completed save again.");
        }
        completedOverwriteApprovals[path] = reviewedRevision;
    }

    private string? ValidateCompletedOverwrite(string path)
    {
        string? revision = File.Exists(path) ? Revision(path) : null;
        bool approved = completedOverwriteApprovals.Remove(path, out string? reviewed) && revision == reviewed;
        if (IsCompletedPath(path) && !approved)
        {
            throw new InvalidOperationException("This save was created after completing the game. Confirm Overwrite before replacing it.");
        }
        return revision;
    }

    private static string Revision(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool IsCompletedPath(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }
        try
        {
            return CrossoverSaveAdapter.ReadSummary(path)?.IsCompletedGame == true;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return false;
        }
    }

    private static void PublishReviewed(string temporary, string destination, string? revision)
    {
        if ((File.Exists(destination) ? Revision(destination) : null) != revision)
        {
            throw new InvalidOperationException("The destination changed while saving. Select the slot again.");
        }
        File.Move(temporary, destination, overwrite: true);
    }

    private void WriteReviewedCombined(string destination, Memoria.FF7.Runtime.CombinedSessionSave saved)
    {
        string? revision = ValidateCompletedOverwrite(destination);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Memoria.FF7.Runtime.CombinedSessionCodec.Save(temporary, saved);
            PublishReviewed(temporary, destination, revision);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public void SaveCompleted(int slot, GameplaySaveComponentDocument document)
    {
        if (Game != Memoria.FF7.Runtime.SessionGame.Ff8 || slot is < 1 or > MemoriaSlotCount || document.CompletedGame is null)
        {
            throw new InvalidOperationException("Completed games require a manual Memoria slot.");
        }
        document.Validate();
        string destination = GetPath(slot);
        string? revision = ValidateCompletedOverwrite(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                GameplaySaveCodec.Write(stream, document with { SavedLocalAt = DateTimeOffset.Now });
                stream.Flush(flushToDisk: true);
            }
            using (FileStream check = File.OpenRead(temporary))
            {
                _ = GameplaySaveCodec.Read(check);
            }
            PublishReviewed(temporary, destination, revision);
            entries.Remove(destination);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
