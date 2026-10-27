using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.Launcher;

internal sealed record UpdateOperation(string Path, bool HadOriginal);
internal sealed record UpdateJournal(UpdateOperation[] Operations);

internal static class UpdateInstaller
{
    internal const string WorkDirectory = ".memoria-update";

    internal static string MutexName(string root)
    {
        string identity = Path.TrimEndingDirectorySeparator(DistributionPaths.GetFullPath(root));
        if (OperatingSystem.IsWindows())
        {
            identity = identity.ToUpperInvariant();
        }
        return "Local\\Memoria.FF8.Launcher." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    internal static void RejectLinks(string path)
    {
        for (DirectoryInfo? directory = new(DistributionPaths.GetFullPath(path)); directory is not null; directory = directory.Parent)
        {
            try
            {
                if ((File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Updates cannot use linked paths: " + directory.FullName);
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }

    internal static void Recover(string root)
    {
        if (MacBundleUpdate.IsBundleRoot(root))
        {
            MacBundleUpdate.Recover(root);
            return;
        }
        string work = Path.Combine(root, WorkDirectory);
        RejectLinks(work);
        string journalPath = Path.Combine(work, "journal.json");
        if (File.Exists(journalPath) && !File.Exists(Path.Combine(work, "committed")) && !File.Exists(Path.Combine(work, "rolled-back")))
        {
            UpdateJournal journal = JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(journalPath)) ?? throw new InvalidDataException("Invalid update recovery journal.");
            Rollback(root, journal);
        }
    }

    internal static bool NeedsRecovery(string root)
    {
        if (MacBundleUpdate.IsBundleRoot(root))
        {
            return MacBundleUpdate.NeedsRecovery(root);
        }
        string work = Path.Combine(root, WorkDirectory);
        RejectLinks(work);
        return File.Exists(Path.Combine(work, "journal.json")) && !File.Exists(Path.Combine(work, "committed")) && !File.Exists(Path.Combine(work, "rolled-back"));
    }

    internal static string Prepare(string root)
    {
        if (MacBundleUpdate.IsBundleRoot(root))
        {
            return MacBundleUpdate.Prepare(root);
        }
        Recover(root);
        string work = Path.Combine(root, WorkDirectory);
        if (Directory.Exists(work))
        {
            // This is the updater's dedicated directory, never the installation root.
            ValidateCleanupTree(work);
            Directory.Delete(work, true);
        }
        Directory.CreateDirectory(work);
        return work;
    }

    private static void ValidateCleanupTree(string directory)
    {
        RejectLinks(directory);
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            RejectLinks(path);
            if (Directory.Exists(path))
            {
                ValidateCleanupTree(path);
            }
        }
    }

    internal static UpdateJournal CreateJournal(string root, PackageManifest incoming)
    {
        root = DistributionPaths.GetFullPath(root);
        if (MacBundleUpdate.IsBundleRoot(root))
        {
            MacBundleUpdate.ValidatePrepared(root, incoming);
            return new UpdateJournal([]);
        }
        PackageManifest? previous = PackageManifest.LoadOptional(root);
        if (previous is null)
        {
            throw new InvalidOperationException("This installation has no distribution inventory. Extract a complete current release into a new folder before using automatic updates.");
        }
        previous.ValidatePaths();
        incoming.ValidateTarget(previous.RuntimeIdentifier);
        StringComparer comparer = previous.RuntimeIdentifier == "win-x64" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, PackageFile> previousFiles = previous.Files.ToDictionary(file => file.Path, comparer);
        HashSet<string> oldPaths = new(previousFiles.Keys, comparer);
        oldPaths.Add(PackageManifest.ManifestPath);
        HashSet<string> paths = new(oldPaths, comparer);
        HashSet<string> newPaths = new(incoming.Files.Select(file => file.Path), comparer);
        paths.UnionWith(newPaths);
        UpdateOperation[] operations = paths.Where(path => path != PackageManifest.ManifestPath && !PreserveEditedMod(root, path, previousFiles)).OrderBy(path => newPaths.Contains(path) ? 1 : 0).ThenBy(path => path, comparer).Append(PackageManifest.ManifestPath).Select(path =>
        {
            string target = PackageManifest.ResolvePath(root, path);
            RejectLinks(target);
            if (Directory.Exists(target))
            {
                ValidateOwnedDirectory(root, target, oldPaths);
            }
            if (!oldPaths.Contains(path) && File.Exists(target))
            {
                throw new IOException("A new package file conflicts with an unowned file. Move it aside before updating: " + path);
            }
            return new UpdateOperation(path, File.Exists(target));
        }).ToArray();
        return new UpdateJournal(operations);
    }

    private static bool PreserveEditedMod(string root, string relative, IReadOnlyDictionary<string, PackageFile> previousFiles)
    {
        if (!PackageManifest.IsBundledModPath(relative))
        {
            return false;
        }
        string target = PackageManifest.ResolvePath(root, relative);
        RejectLinks(target);
        if (!File.Exists(target))
        {
            return false;
        }
        if (!previousFiles.TryGetValue(relative, out PackageFile? previous))
        {
            return true;
        }
        using FileStream stream = File.OpenRead(target);
        return stream.Length != previous.Size || !Convert.ToHexString(SHA256.HashData(stream)).Equals(previous.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    internal static void Apply(string root, Action<string>? beforeInstall = null)
    {
        if (MacBundleUpdate.IsBundleRoot(root))
        {
            MacBundleUpdate.Apply(root, beforeInstall);
            return;
        }
        string work = Path.Combine(root, WorkDirectory);
        RejectLinks(work);
        string staged = Path.Combine(work, "new");
        PackageManifest incoming = PackageManifest.Read(staged);
        PackageManifest.Verify(staged, incoming);
        UpdateJournal journal = CreateJournal(root, incoming);
        string journalPath = Path.Combine(work, "journal.json");
        string pendingJournalPath = Path.Combine(work, "journal.tmp");
        using (FileStream stream = new(pendingJournalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, journal);
            stream.Flush(true);
        }
        File.Move(pendingJournalPath, journalPath);
        try
        {
            foreach (UpdateOperation operation in journal.Operations)
            {
                string target = PackageManifest.ResolvePath(root, operation.Path);
                string backup = PackageManifest.ResolvePath(Path.Combine(work, "backup"), operation.Path);
                string source = PackageManifest.ResolvePath(staged, operation.Path);
                RejectLinks(target);
                beforeInstall?.Invoke(operation.Path);
                if (operation.HadOriginal && File.Exists(source))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    // Replace atomically so a crash never leaves the launcher path missing.
                    File.Replace(source, target, backup);
                }
                else if (operation.HadOriginal)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Move(target, backup);
                }
                else if (File.Exists(source))
                {
                    if (Directory.Exists(target))
                    {
                        RemoveEmptyTree(target);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Move(source, target);
                }
            }
            WriteMarker(work, "committed");
        }
        catch
        {
            Rollback(root, journal);
            throw;
        }
    }

    private static void Rollback(string root, UpdateJournal journal)
    {
        string work = Path.Combine(root, WorkDirectory);
        foreach (UpdateOperation operation in journal.Operations.Reverse())
        {
            // ResolvePath validates journal paths as well as archive paths.
            string target = PackageManifest.ResolvePath(root, operation.Path);
            string backup = PackageManifest.ResolvePath(Path.Combine(work, "backup"), operation.Path);
            string source = PackageManifest.ResolvePath(Path.Combine(work, "new"), operation.Path);
            RejectLinks(target);
            RejectLinks(backup);
            if (File.Exists(backup))
            {
                if (Directory.Exists(target))
                {
                    // Children installed in place of an old file have already been rolled back.
                    RemoveEmptyTree(target);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target))
                {
                    File.Replace(backup, target, null);
                }
                else
                {
                    File.Move(backup, target);
                }
            }
            else if (!operation.HadOriginal && !File.Exists(source) && File.Exists(target))
            {
                File.Delete(target);
            }
        }
        WriteMarker(work, "rolled-back");
    }

    private static void RemoveEmptyTree(string directory)
    {
        RejectLinks(directory);
        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            RemoveEmptyTree(child);
        }
        Directory.Delete(directory, false);
    }

    private static void ValidateOwnedDirectory(string root, string directory, HashSet<string> owned)
    {
        RejectLinks(directory);
        string prefix = Path.GetRelativePath(root, directory).Replace('\\', '/') + "/";
        if (!owned.Any(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            throw new IOException("An unowned directory conflicts with a new package file: " + prefix);
        }
        foreach (string child in Directory.EnumerateFileSystemEntries(directory))
        {
            RejectLinks(child);
            if (Directory.Exists(child))
            {
                ValidateOwnedDirectory(root, child, owned);
            }
            else if (!owned.Contains(Path.GetRelativePath(root, child).Replace('\\', '/')))
            {
                throw new IOException("An unowned file prevents replacing an old package directory: " + child);
            }
        }
    }

    private static void WriteMarker(string work, string name)
    {
        string temporary = Path.Combine(work, name + ".tmp");
        using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            stream.WriteByte(1);
            stream.Flush(true);
        }
        File.Move(temporary, Path.Combine(work, name), true);
    }

    internal static async Task StartHelperAsync(string root, bool recovery = false)
    {
        string work = MacBundleUpdate.WorkPath(root);
        PackageManifest installed = PackageManifest.Read(root);
        installed.ValidateTarget(ReleaseClient.CurrentRuntimeIdentifier);
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the launcher executable.");
        if (!Path.GetFullPath(executable).Equals(Path.Combine(Path.GetFullPath(root), installed.LauncherEntryPoint), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Automatic installation requires the packaged Memoria.Launcher.exe.");
        }
        string helper = Path.Combine(work, recovery ? "recovery.exe" : "updater.exe");
        if (!OperatingSystem.IsWindows())
        {
            string closure = Path.Combine(work, recovery ? "recovery-runtime" : "updater-runtime");
            Directory.CreateDirectory(closure);
            foreach (PackageFile file in installed.Files.Where(file => MacBundleUpdate.IsBundleRoot(root) ? file.Path.StartsWith("Contents/MacOS/", StringComparison.Ordinal) && !file.Path.StartsWith("Contents/MacOS/Data/", StringComparison.Ordinal) : !file.Path.Contains('/')))
            {
                string source = PackageManifest.ResolvePath(root, file.Path);
                RejectLinks(source);
                string relative = MacBundleUpdate.IsBundleRoot(root) ? file.Path["Contents/MacOS/".Length..] : file.Path;
                string destination = Path.Combine(closure, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
                File.SetUnixFileMode(destination, (UnixFileMode)file.UnixMode);
            }
            helper = Path.Combine(closure, "Memoria.Launcher");
        }
        File.Delete(Path.Combine(work, "ready"));
        File.Delete(Path.Combine(work, "accepted"));
        if (OperatingSystem.IsWindows())
        {
            File.Copy(executable, helper, true);
        }
        using Process parent = Process.GetCurrentProcess();
        ProcessStartInfo start = new(helper) { UseShellExecute = false, WorkingDirectory = work };
        foreach (string argument in new[] { recovery ? "--recover-update" : "--apply-update", root, parent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), parent.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start) ?? throw new IOException("Could not start the update helper.");
        for (int attempt = 0; attempt < 300; attempt++)
        {
            if (File.Exists(Path.Combine(work, "ready")))
            {
                File.WriteAllText(Path.Combine(work, "accepted"), "accepted");
                return;
            }
            if (process.HasExited)
            {
                throw new IOException("The update helper exited before it was ready.");
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("The update helper did not become ready. The current installation has not been changed.");
    }

    internal static void RunHelper(string[] arguments)
    {
        if (arguments.Length != 4 || !int.TryParse(arguments[2], out int parentId) || !long.TryParse(arguments[3], out long parentStarted))
        {
            throw new ArgumentException("Invalid update helper arguments.");
        }
        if (!Path.IsPathFullyQualified(arguments[1]))
        {
            throw new ArgumentException("The update helper requires an absolute installation path.");
        }
        string root = MacBundleUpdate.NormalizeRoot(arguments[1]);
        string work = MacBundleUpdate.WorkPath(root);
        bool recovery = arguments[0] == "--recover-update";
        string expectedHelper = OperatingSystem.IsWindows() ? Path.Combine(work, recovery ? "recovery.exe" : "updater.exe") : Path.Combine(work, recovery ? "recovery-runtime" : "updater-runtime", "Memoria.Launcher");
        bool retainedMacRecoveryHelper = recovery && MacBundleUpdate.IsBundleRoot(root) && string.Equals(Environment.ProcessPath, Path.Combine(work, "updater-runtime", "Memoria.Launcher"), StringComparison.Ordinal);
        if (!retainedMacRecoveryHelper && !string.Equals(Environment.ProcessPath, expectedHelper, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The update helper must run from its prepared installation directory.");
        }
        RejectLinks(work);
        using Process? parent = FindParent(parentId, parentStarted);
        File.WriteAllText(Path.Combine(work, "ready"), "ready");
        if (parent is not null && !parent.WaitForExit(60000))
        {
            throw new TimeoutException("The launcher has not closed. No files have been changed.");
        }
        if (!File.Exists(Path.Combine(work, "accepted")))
        {
            return;
        }
        using Mutex mutex = new(false, MutexName(root));
        bool acquired;
        try
        {
            acquired = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }
        if (!acquired)
        {
            throw new IOException("Another launcher is open. Close it and try the update again.");
        }
        try
        {
            if (recovery)
            {
                Recover(root);
            }
            else
            {
                Apply(root);
            }
        }
        finally
        {
            mutex.ReleaseMutex();
        }
        Process.Start(CreateRestartInfo(root, recovery))?.Dispose();
    }

    internal static ProcessStartInfo CreateRestartInfo(string root, bool recovery)
    {
        string launcher = PackageManifest.LoadOptional(root)?.LauncherEntryPoint ?? PackageManifest.LauncherPath;
        ProcessStartInfo restart = new(Path.Combine(root, launcher)) { UseShellExecute = false, WorkingDirectory = root };
        if (!recovery && File.Exists(UpdateResume.IntentPath(root)))
        {
            restart.ArgumentList.Add("--resume-update");
        }
        return restart;
    }

    private static Process? FindParent(int id, long started)
    {
        if (id == 0 && started == 0)
        {
            return null;
        }
        try
        {
            Process process = Process.GetProcessById(id);
            if (process.StartTime.ToUniversalTime().Ticks == started)
            {
                return process;
            }
            process.Dispose();
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
