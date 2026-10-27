using System.Text.Json;

namespace Memoria.Launcher;

internal sealed record MacBundleJournal(string Root, string PreviousBuildId, string IncomingBuildId);

internal static class MacBundleUpdate
{
    internal static bool IsBundleRoot(string root) => Path.TrimEndingDirectorySeparator(root).EndsWith(".app", StringComparison.Ordinal);

    internal static string NormalizeRoot(string path)
    {
        string root = Path.TrimEndingDirectorySeparator(DistributionPaths.GetFullPath(path));
        if (Path.GetFileName(root) == "MacOS" && Path.GetFileName(Path.GetDirectoryName(root)) == "Contents")
        {
            string bundle = Path.GetDirectoryName(Path.GetDirectoryName(root))!;
            if (!IsBundleRoot(bundle))
            {
                throw new InvalidDataException("The macOS launcher requires the fixed application bundle layout.");
            }
            root = bundle;
        }
        UpdateInstaller.RejectLinks(root);
        return root;
    }

    internal static string WorkPath(string root)
    {
        root = NormalizeRoot(root);
        return IsBundleRoot(root) ? Path.Combine(Path.GetDirectoryName(root)!, "." + Path.GetFileName(root) + ".memoria-update") : Path.Combine(root, UpdateInstaller.WorkDirectory);
    }

    private static string Backup(string root) => Path.Combine(WorkPath(root), "backup.app");
    internal static string StagingPath(string root) => Path.Combine(WorkPath(root), IsBundleRoot(root) ? "new.app" : "new");
    private static string Stage(string root) => StagingPath(root);
    private static string JournalPath(string root) => Path.Combine(WorkPath(root), "bundle-journal.json");

    internal static bool NeedsRecovery(string root)
    {
        string work = WorkPath(root);
        UpdateInstaller.RejectLinks(work);
        UpdateInstaller.RejectLinks(JournalPath(root));
        UpdateInstaller.RejectLinks(Path.Combine(work, "committed"));
        UpdateInstaller.RejectLinks(Path.Combine(work, "rolled-back"));
        return File.Exists(JournalPath(root)) && !File.Exists(Path.Combine(work, "committed")) && !File.Exists(Path.Combine(work, "rolled-back"));
    }

    internal static string Prepare(string root)
    {
        root = RequireBundle(root);
        Recover(root);
        string work = WorkPath(root);
        if (Directory.Exists(work))
        {
            ValidateTree(work);
            foreach (string bundle in Directory.EnumerateDirectories(work, "*.app"))
            {
                VerifyExact(bundle, PackageManifest.Read(bundle));
            }
            Directory.Delete(work, recursive: true);
        }
        Directory.CreateDirectory(work);
        return work;
    }

    internal static void ValidatePrepared(string root, PackageManifest incoming)
    {
        root = RequireBundle(root);
        incoming.ValidateTarget("osx-x64");
        PackageManifest installed = PackageManifest.Read(root);
        installed.ValidateTarget("osx-x64");
        VerifyExact(root, installed);
        VerifyExact(Stage(root), incoming);
    }

    internal static void Apply(string root, Action<string>? beforeInstall = null)
    {
        root = RequireBundle(root);
        PackageManifest incoming = PackageManifest.Read(Stage(root));
        ValidatePrepared(root, incoming);
        string work = WorkPath(root);
        string backup = Backup(root);
        if (Directory.Exists(backup) || File.Exists(JournalPath(root)))
        {
            throw new IOException("A previous bundle transaction must be recovered or prepared again before updating.");
        }
        PackageManifest previous = PackageManifest.Read(root);
        WriteJson(JournalPath(root), new MacBundleJournal(root, previous.BuildId, incoming.BuildId));
        try
        {
            beforeInstall?.Invoke("before-backup");
            Directory.Move(root, backup);
            beforeInstall?.Invoke("after-backup");
            Directory.Move(Stage(root), root);
            beforeInstall?.Invoke("after-install");
            VerifyExact(root, incoming);
            WriteJson(Path.Combine(work, "committed"), new { incoming.BuildId });
        }
        catch
        {
            Recover(root);
            throw;
        }
    }

    internal static void Recover(string root)
    {
        root = RequireBundle(root);
        if (!NeedsRecovery(root))
        {
            return;
        }
        MacBundleJournal journal = JsonSerializer.Deserialize<MacBundleJournal>(File.ReadAllText(JournalPath(root))) ?? throw new InvalidDataException("Invalid bundle recovery journal.");
        if (journal.Root != root || string.IsNullOrWhiteSpace(journal.PreviousBuildId) || string.IsNullOrWhiteSpace(journal.IncomingBuildId))
        {
            throw new InvalidDataException("The bundle recovery journal does not belong to this installation.");
        }
        string backup = Backup(root);
        UpdateInstaller.RejectLinks(backup);
        if (Directory.Exists(backup))
        {
            PackageManifest previous = PackageManifest.Read(backup);
            if (previous.BuildId != journal.PreviousBuildId)
            {
                throw new InvalidDataException("The backup bundle does not match the recovery journal.");
            }
            VerifyExact(backup, previous);
            if (Directory.Exists(root))
            {
                // Preserve the failed bundle as a whole; rollback must remain possible if its payload or descriptor was damaged.
                ValidateTree(root);
                string retained = Directory.Exists(Stage(root)) ? Path.Combine(WorkPath(root), "failed-" + Guid.NewGuid().ToString("N") + ".app") : Stage(root);
                Directory.Move(root, retained);
            }
            Directory.Move(backup, root);
        }
        else
        {
            PackageManifest original = PackageManifest.Read(root);
            if (original.BuildId != journal.PreviousBuildId)
            {
                throw new InvalidDataException("The original bundle cannot be recovered from this transaction.");
            }
            VerifyExact(root, original);
        }
        WriteJson(Path.Combine(WorkPath(root), "rolled-back"), new { journal.PreviousBuildId });
    }

    private static string RequireBundle(string root)
    {
        if (!Path.IsPathFullyQualified(root))
        {
            throw new ArgumentException("Bundle updates require an absolute application path.");
        }
        root = NormalizeRoot(root);
        if (!IsBundleRoot(root))
        {
            throw new InvalidDataException("A bundle update requires an application root ending in .app.");
        }
        UpdateInstaller.RejectLinks(WorkPath(root));
        return root;
    }

    internal static void VerifyExact(string root, PackageManifest manifest)
    {
        manifest.ValidateTarget("osx-x64");
        ValidateTree(root);
        HashSet<string> expected = new(manifest.Files.Select(file => file.Path), StringComparer.Ordinal) { PackageManifest.MacManifestPath };
        HashSet<string> actual = new(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')), StringComparer.Ordinal);
        if (!expected.SetEquals(actual))
        {
            throw new IOException("The application contains unowned or missing files. Move custom files outside the bundle before updating; the current bundle has been retained.");
        }
        PackageManifest.Verify(root, manifest);
    }

    private static void ValidateTree(string root)
    {
        UpdateInstaller.RejectLinks(root);
        foreach (string path in Directory.EnumerateFileSystemEntries(root))
        {
            UpdateInstaller.RejectLinks(path);
            if (Directory.Exists(path))
            {
                ValidateTree(path);
            }
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value);
                stream.Flush(true);
            }
            File.Move(pending, path);
        }
        finally
        {
            File.Delete(pending);
        }
    }
}
