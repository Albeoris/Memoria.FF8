using Memoria.FF8.Platform;
using Memoria.Launcher;

namespace Memoria.FF8.Mods;

public static class GirlNextDoorMigration
{
    private const string Component = "Memoria/Girl Next Door";
    private const string ArtworkFile = "GirlNextDoor.png";
    private static readonly string[] ResourceNames = ["mod.ini", ArtworkFile];

    public static bool IsComplete(string modsRoot) => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(modsRoot))) is not null && File.Exists(CompletionPath(modsRoot));

    public static void FromLauncher(string distributionRoot, bool beforeBackupCleanup = false)
    {
        string root = Path.TrimEndingDirectorySeparator(DistributionPaths.GetFullPath(distributionRoot));
        bool mac = DistributionPaths.IsBundleRoot(root);
        string payload = PayloadRoot(root);
        string applicationData = Directory.Exists(Path.Combine(payload, "Data")) ? Path.Combine(payload, "Data") : payload;
        string modsRoot = ApplicationInstallation.ResolveModsDirectory(payload, mac ? HostPlatform.MacOS : HostPlatforms.Current, ApplicationPaths.Current.Configuration);
        if (IsComplete(modsRoot))
        {
            return;
        }
        string[] needsBackup = ResourceNames.Where(name => !File.Exists(Path.Combine(modsRoot, Component, name)) && !LiveFiles(applicationData, payload, name).Any(File.Exists)).ToArray();
        bool hasLocal = needsBackup.Length != ResourceNames.Length;
        bool needsPreservation = (beforeBackupCleanup || !hasLocal) && needsBackup.Length != 0 && ReadBackup(root, needsBackup).Count != 0;
        if (!hasLocal && !needsPreservation)
        {
            return;
        }

        using ModDirectoryResourceLease lease = ModDirectoryResourceLease.Acquire(modsRoot, requestWriter: true);
        if (lease.IsWriter)
        {
            Migrate(root, applicationData, modsRoot);
            if (needsPreservation && File.Exists(Path.Combine(modsRoot, Component, ArtworkFile)) && !IsComplete(modsRoot))
            {
                throw new InvalidDataException("Girl Next Door migration is waiting for a durable mod receipt. Keep the previous engine backup and retry after restarting.");
            }
        }
        else if (beforeBackupCleanup && needsPreservation)
        {
            throw new IOException("Close the game before updating so Girl Next Door can be copied from the previous engine installation.");
        }
    }

    // The game already holds its exclusive startup lease for this writable root.
    public static void FromGame(string applicationDataDirectory, string modsRoot)
    {
        string applicationData = Path.TrimEndingDirectorySeparator(DistributionPaths.GetFullPath(applicationDataDirectory));
        Migrate(DistributionUpdateBackup.GameDistributionRoot(applicationData), applicationData, modsRoot);
    }

    private static void Migrate(string root, string applicationData, string modsRoot)
    {
        if (IsComplete(modsRoot))
        {
            return;
        }
        if (DistributionUpdateBackup.NeedsRecovery(root))
        {
            throw new InvalidDataException("Complete engine update recovery before migrating Girl Next Door.");
        }

        string destination = Path.Combine(modsRoot, Component);
        string state = ModPackageInstaller.StateDirectoryFor(modsRoot);
        string payload = PayloadRoot(root);
        List<string> missing = [];
        foreach (string name in ResourceNames)
        {
            string target = Path.Combine(destination, name);
            PackageManifest.EnsureNoReparsePoints(target);
            if (File.Exists(target))
            {
                continue;
            }
            string? source = LiveFiles(applicationData, payload, name).FirstOrDefault(File.Exists);
            if (source is null)
            {
                missing.Add(name);
                continue;
            }
            PackageManifest.EnsureNoReparsePoints(source);
            using FileStream input = File.OpenRead(source);
            CopyLegacyResource(input, target, state);
        }

        if (missing.Count != 0)
        {
            string prefix = BackupPrefix(root);
            IReadOnlyDictionary<string, byte[]> backup = ReadBackup(root, missing);
            foreach (string name in missing)
            {
                string? path = BackupPaths(prefix, name).FirstOrDefault(backup.ContainsKey);
                if (path is not null)
                {
                    using MemoryStream source = new(backup[path], writable: false);
                    CopyLegacyResource(source, Path.Combine(destination, name), state);
                }
            }
        }

        if (ModPackageInstaller.RecordMigratedGirlNextDoor(modsRoot))
        {
            using MemoryStream marker = new([1], writable: false);
            CopyAtomically(marker, CompletionPath(modsRoot), state);
        }
    }

    private static string PayloadRoot(string root) => DistributionPaths.IsBundleRoot(root) ? Path.Combine(root, "Contents", "Resources") : root;

    private static IEnumerable<string> LiveFiles(string applicationData, string payload, string name)
    {
        yield return Path.Combine(applicationData, "BundledMods", Component, name);
        if (applicationData != payload)
        {
            yield return Path.Combine(payload, "BundledMods", Component, name);
            yield return Path.Combine(applicationData, "Mods", Component, name);
        }
        yield return Path.Combine(payload, "Mods", Component, name);
    }

    private static string[] BackupPaths(string prefix, string name) => [$"{prefix}Data/BundledMods/{Component}/{name}", $"{prefix}Data/Mods/{Component}/{name}", $"{prefix}Mods/{Component}/{name}"];

    private static string BackupPrefix(string root) => DistributionPaths.IsBundleRoot(root) ? "Contents/Resources/" : "";

    private static IReadOnlyDictionary<string, byte[]> ReadBackup(string root, IEnumerable<string> names)
    {
        string prefix = BackupPrefix(root);
        return DistributionUpdateBackup.ReadCommittedFiles(root, names.SelectMany(name => BackupPaths(prefix, name)).ToArray());
    }

    private static string CompletionPath(string modsRoot) => Path.Combine(ModPackageInstaller.StateDirectoryFor(modsRoot), "girl-next-door-migration-v1");

    private static void CopyLegacyResource(Stream input, string target, string state)
    {
        if (Path.GetFileName(target) == "mod.ini" && input.Length is 12 or 13)
        {
            Span<byte> settings = stackalloc byte[(int)input.Length];
            input.ReadExactly(settings);
            if (settings.SequenceEqual("enabled = 1\n"u8) || settings.SequenceEqual("enabled = 1\r\n"u8))
            {
                return;
            }
            input.Position = 0;
        }
        CopyAtomically(input, target, state);
    }

    private static void CopyAtomically(Stream input, string target, string state)
    {
        PackageManifest.EnsureNoReparsePoints(target);
        string temporary = Path.Combine(state, "girl-next-door-migration.tmp");
        PackageManifest.EnsureNoReparsePoints(temporary);
        File.Delete(temporary);
        try
        {
            using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
            {
                input.CopyTo(output);
                output.Flush(true);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(temporary, target);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
