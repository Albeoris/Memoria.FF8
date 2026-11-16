using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.Launcher;

internal sealed record UpdateOperation(string Path, bool HadOriginal);
internal sealed record UpdateJournal(UpdateOperation[] Operations);
internal sealed record MacBundleJournal(string Root, string PreviousBuildId, string IncomingBuildId);

internal static class DistributionUpdateBackup
{
    internal const string WorkDirectory = ".memoria-update";

    internal static string GameDistributionRoot(string applicationData)
    {
        string? parent = Path.GetDirectoryName(applicationData);
        if (!Path.GetFileName(applicationData).Equals("Data", StringComparison.OrdinalIgnoreCase) || parent is null)
        {
            return applicationData;
        }
        string? contents = Path.GetDirectoryName(parent);
        string? bundle = contents is null ? null : Path.GetDirectoryName(contents);
        if (Path.GetFileName(contents) == "Contents" && Path.GetFileName(parent) is "Resources" or "MacOS" && bundle is not null && DistributionPaths.IsBundleRoot(bundle))
        {
            return bundle;
        }
        return File.Exists(Path.Combine(applicationData, "distribution.json")) || Directory.Exists(Path.Combine(parent, WorkDirectory)) ? parent : applicationData;
    }

    internal static string WorkPath(string root) => DistributionPaths.IsBundleRoot(root)
        ? Path.Combine(Path.GetDirectoryName(root)!, "." + Path.GetFileName(root) + WorkDirectory)
        : Path.Combine(root, WorkDirectory);

    internal static bool NeedsRecovery(string root)
    {
        string work = WorkPath(root);
        string journal = JournalPath(root, work);
        string committed = Path.Combine(work, "committed");
        string rolledBack = Path.Combine(work, "rolled-back");
        PackageManifest.EnsureNoReparsePoints(journal);
        PackageManifest.EnsureNoReparsePoints(committed);
        PackageManifest.EnsureNoReparsePoints(rolledBack);
        return File.Exists(journal) && !File.Exists(committed) && !File.Exists(rolledBack);
    }

    internal static IReadOnlyDictionary<string, byte[]> ReadCommittedFiles(string root, IReadOnlyList<string> requestedPaths)
    {
        string work = WorkPath(root);
        string marker = Path.Combine(work, "committed");
        string journalPath = JournalPath(root, work);
        string rolledBack = Path.Combine(work, "rolled-back");
        PackageManifest.EnsureNoReparsePoints(marker);
        if (!File.Exists(marker))
        {
            return new Dictionary<string, byte[]>();
        }
        PackageManifest.EnsureNoReparsePoints(journalPath);
        PackageManifest.EnsureNoReparsePoints(rolledBack);
        if (File.Exists(rolledBack) || !File.Exists(journalPath))
        {
            throw new InvalidDataException("The committed engine backup has no valid update journal.");
        }

        PackageManifest current = PackageManifest.Read(root);
        bool mac = DistributionPaths.IsBundleRoot(root);
        string backup = Path.Combine(work, mac ? "backup.app" : "backup");
        PackageManifest previous = PackageManifest.Read(backup);
        previous.ValidateTarget(current.RuntimeIdentifier);
        StringComparer comparer = current.RuntimeIdentifier == "win-x64" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, PackageFile> previousFiles = previous.Files.ToDictionary(file => file.Path, comparer);
        HashSet<string>? backedUpPaths = mac ? null : ValidateFileJournal(root, marker, journalPath, current, previousFiles, comparer);
        if (mac)
        {
            ValidateBundleJournal(root, marker, journalPath, current, previous);
        }

        Dictionary<string, byte[]> files = new(comparer);
        foreach (string path in requestedPaths)
        {
            if (!previousFiles.TryGetValue(path, out PackageFile? item) || backedUpPaths is not null && !backedUpPaths.Contains(path))
            {
                continue;
            }
            string source = PackageManifest.ResolvePath(backup, item.Path);
            PackageManifest.EnsureNoReparsePoints(source);
            using FileStream input = File.OpenRead(source);
            if (item.Size > 16 * 1024 * 1024 || input.Length != item.Size)
            {
                throw new InvalidDataException("The engine backup file size does not match its previous inventory: " + path);
            }
            byte[] bytes = new byte[item.Size];
            input.ReadExactly(bytes);
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(item.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The engine backup file hash does not match its previous inventory: " + path);
            }
            files.Add(path, bytes);
        }
        return files;
    }

    private static HashSet<string> ValidateFileJournal(string root, string marker, string journalPath, PackageManifest current, IReadOnlyDictionary<string, PackageFile> previous, StringComparer comparer)
    {
        using (FileStream stream = File.OpenRead(marker))
        {
            if (stream.Length != 1 || stream.ReadByte() != 1)
            {
                throw new InvalidDataException("Invalid engine update commit marker.");
            }
        }
        UpdateJournal journal = ReadJournal<UpdateJournal>(journalPath);
        if (journal.Operations is not { Length: > 0 } || journal.Operations[^1] is not { Path: PackageManifest.ManifestPath, HadOriginal: true })
        {
            throw new InvalidDataException("Invalid engine update journal inventory operation.");
        }
        HashSet<string> installed = current.Files.Select(file => file.Path).ToHashSet(comparer);
        HashSet<string> seen = new(comparer);
        HashSet<string> backedUp = new(comparer);
        foreach (UpdateOperation operation in journal.Operations)
        {
            if (operation?.Path is not { Length: > 0 } path || !seen.Add(path) || (!comparer.Equals(path, PackageManifest.ManifestPath) && !previous.ContainsKey(path) && (!installed.Contains(path) || operation.HadOriginal)))
            {
                throw new InvalidDataException("The engine update journal does not match the installation inventories.");
            }
            _ = PackageManifest.ResolvePath(root, path);
            if (operation.HadOriginal)
            {
                backedUp.Add(path);
            }
        }
        return backedUp;
    }

    private static void ValidateBundleJournal(string root, string marker, string journalPath, PackageManifest current, PackageManifest previous)
    {
        MacBundleJournal journal = ReadJournal<MacBundleJournal>(journalPath);
        BundleCommit commit = ReadJournal<BundleCommit>(marker);
        if (journal.Root != root || journal.PreviousBuildId != previous.BuildId || journal.IncomingBuildId != current.BuildId || commit.BuildId != current.BuildId)
        {
            throw new InvalidDataException("The committed bundle backup does not match this installation.");
        }
    }

    private static string JournalPath(string root, string work) => Path.Combine(work, DistributionPaths.IsBundleRoot(root) ? "bundle-journal.json" : "journal.json");

    private static T ReadJournal<T>(string path) where T : class
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > 16 * 1024 * 1024)
        {
            throw new InvalidDataException("The engine update journal is too large.");
        }
        return JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidDataException("Invalid engine update journal.");
    }

    private sealed record BundleCommit(string BuildId);
}
