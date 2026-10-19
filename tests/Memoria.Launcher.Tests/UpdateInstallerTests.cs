using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.Launcher.Tests;

public sealed class UpdateInstallerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.Updater.Tests", Guid.NewGuid().ToString("N"));
    private string Work => Path.Combine(root, UpdateInstaller.WorkDirectory);
    private string Staged => Path.Combine(Work, "new");

    public UpdateInstallerTests()
    {
        WritePackage(root, "old", "Data/obsolete.dll");
        WritePackage(Staged, "new", "Data/added.dll");
        Write(root, "Mods/custom/mod.ini", "user mod");
        Write(root, "notes.txt", "user notes");
        Write(root, "Data/custom/user.bin", "user data");
    }

    [Fact]
    public void ApplyReplacesAddsAndRemovesOnlyInventoryFiles()
    {
        UpdateInstaller.Apply(root);

        Assert.Equal("new:Memoria.Launcher.exe", Read("Memoria.Launcher.exe"));
        Assert.Equal("new:Data/Memoria.FF8.exe", Read("Data/Memoria.FF8.exe"));
        Assert.Equal("new:Data/added.dll", Read("Data/added.dll"));
        Assert.False(File.Exists(Path.Combine(root, "Data/obsolete.dll")));
        Assert.Equal("new", PackageManifest.Read(root).BuildId);
        Assert.True(File.Exists(Path.Combine(Work, "committed")));
        AssertUnknownFiles();
        UpdateInstaller.Recover(root);
        Assert.Equal("new", PackageManifest.Read(root).BuildId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RelocatingBundledDefaultsRetainsUserOverridesAndRollsBack(bool edited, bool fail)
    {
        const string oldPath = "Mods/Memoria/Example/fields/example.json";
        const string newPath = "Data/BundledMods/Memoria/Example/fields/example.json";
        const string configuration = "Mods/Memoria/Example/mod.ini";
        AddPackageFile(root, oldPath, "old default");
        Write(root, configuration, "enabled = 0");
        if (edited)
        {
            Write(root, oldPath, "user edit");
        }
        AddPackageFile(Staged, newPath, "new default");
        if (fail)
        {
            Assert.Throws<IOException>(() => UpdateInstaller.Apply(root, path =>
            {
                if (path == PackageManifest.ManifestPath)
                {
                    throw new IOException("injected relocation failure");
                }
            }));
            Assert.Equal(edited ? "user edit" : "old default", Read(oldPath));
            Assert.False(File.Exists(Path.Combine(root, newPath)));
            Assert.Equal("old", PackageManifest.Read(root).BuildId);
        }
        else
        {
            UpdateInstaller.Apply(root);
            Assert.Equal(edited, File.Exists(Path.Combine(root, oldPath)));
            if (edited)
            {
                Assert.Equal("user edit", Read(oldPath));
            }
            Assert.Equal("new default", Read(newPath));
            Assert.Equal("new", PackageManifest.Read(root).BuildId);
        }
        Assert.Equal("enabled = 0", Read(configuration));
        AssertUnknownFiles();
    }

    [Fact]
    public void NewPackageFileCannotOverwriteUnownedFile()
    {
        Write(root, "Data/added.dll", "user file");

        IOException error = Assert.Throws<IOException>(() => UpdateInstaller.Apply(root));

        Assert.Contains("unowned", error.Message);
        Assert.Equal("user file", Read("Data/added.dll"));
        AssertOriginalPackage();
        Assert.False(File.Exists(Path.Combine(Work, "journal.json")));
    }

    [Theory]
    [InlineData("Mods/Memoria/Example/")]
    [InlineData("Data/BundledMods/Memoria/Example/")]
    public void BundledNativeModsUpdateUntouchedDefaultsAndInstallMissingFiles(string prefix)
    {
        string existing = prefix + "fields/existing.json";
        string added = prefix + "fields/added.json";
        AddPackageFile(root, existing, "old default");
        AddPackageFile(Staged, existing, "new default");
        AddPackageFile(Staged, added, "added default");
        UpdateInstaller.Apply(root);
        Assert.Equal("new default", Read(existing));
        Assert.Equal("added default", Read(added));
        PackageManifest.Verify(root, PackageManifest.Read(root));
    }

    [Theory]
    [InlineData("Mods/Memoria/Example/")]
    [InlineData("Data/BundledMods/Memoria/Example/")]
    public void BundledNativeModsPreserveEditsAndPreexistingFilesAcrossUpdates(string prefix)
    {
        string edited = prefix + "fields/edited.json";
        string unowned = prefix + "mod.ini";
        AddPackageFile(root, edited, "old default");
        Write(root, edited, "user edit");
        Write(root, unowned, "enabled = 0");
        AddPackageFile(Staged, edited, "new default");
        AddPackageFile(Staged, unowned, "enabled = 1");
        UpdateInstaller.Apply(root);
        Assert.Equal("user edit", Read(edited));
        Assert.Equal("enabled = 0", Read(unowned));
        UpdateInstaller.Prepare(root);
        WritePackage(Staged, "next", "Data/added.dll");
        AddPackageFile(Staged, edited, "next default");
        AddPackageFile(Staged, unowned, "enabled = 1");
        UpdateInstaller.Apply(root);
        Assert.Equal("user edit", Read(edited));
        Assert.Equal("enabled = 0", Read(unowned));
        Assert.Equal("next", PackageManifest.Read(root).BuildId);
    }

    [Theory]
    [InlineData("Mods/Memoria/Example/", false)]
    [InlineData("Mods/Memoria/Example/", true)]
    [InlineData("Data/BundledMods/Memoria/Example/", false)]
    [InlineData("Data/BundledMods/Memoria/Example/", true)]
    public void RemovingBundledNativeModFilesPreservesOnlyUserEdits(string prefix, bool edited)
    {
        string relative = prefix + "fields/obsolete.json";
        AddPackageFile(root, relative, "old default");
        if (edited)
        {
            Write(root, relative, "user edit");
        }
        UpdateInstaller.Apply(root);
        Assert.Equal(edited, File.Exists(Path.Combine(root, relative)));
        if (edited)
        {
            Assert.Equal("user edit", Read(relative));
        }
    }

    [Theory]
    [InlineData("Mods/Memoria/Example/")]
    [InlineData("Data/BundledMods/Memoria/Example/")]
    public void FailedUpdateRestoresUntouchedBundledDefaultsAndPreservesEdits(string prefix)
    {
        string untouched = prefix + "fields/untouched.json";
        string edited = prefix + "fields/edited.json";
        AddPackageFile(root, untouched, "old default");
        AddPackageFile(root, edited, "old default");
        Write(root, edited, "user edit");
        AddPackageFile(Staged, untouched, "new default");
        AddPackageFile(Staged, edited, "new default");
        Assert.Throws<IOException>(() => UpdateInstaller.Apply(root, path =>
        {
            if (path == PackageManifest.ManifestPath)
            {
                throw new IOException("Injected after mod installation");
            }
        }));
        Assert.Equal("old default", Read(untouched));
        Assert.Equal("user edit", Read(edited));
        Assert.Equal("old", PackageManifest.Read(root).BuildId);
    }

    [Fact]
    public void DirectoryConflictRejectsUpdateBeforeChanges()
    {
        Directory.CreateDirectory(Path.Combine(root, "Data/added.dll"));

        Assert.Throws<IOException>(() => UpdateInstaller.Apply(root));

        AssertOriginalPackage();
        Assert.True(Directory.Exists(Path.Combine(root, "Data/added.dll")));
        Assert.False(File.Exists(Path.Combine(Work, "journal.json")));
    }

    [Theory]
    [InlineData("Memoria.Launcher.exe")]
    [InlineData("Data/distribution.json")]
    public void MidOperationFailureRollsBackFilesAndInventory(string failurePath)
    {
        Assert.Throws<IOException>(() => UpdateInstaller.Apply(root, path =>
        {
            if (path == failurePath)
            {
                throw new IOException("Injected installation failure");
            }
        }));

        AssertOriginalPackage();
        Assert.False(File.Exists(Path.Combine(root, "Data/added.dll")));
        Assert.True(File.Exists(Path.Combine(Work, "rolled-back")));
        UpdateInstaller.Recover(root);
        AssertOriginalPackage();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(5, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    public void PersistedJournalRecoversInterruptedMovesAndIsIdempotent(int operationCount, bool stopAfterBackup)
    {
        UpdateJournal journal = UpdateInstaller.CreateJournal(root, PackageManifest.Read(Staged));
        File.WriteAllText(Path.Combine(Work, "journal.json"), JsonSerializer.Serialize(journal));
        for (int index = 0; index < operationCount; index++)
        {
            UpdateOperation operation = journal.Operations[index];
            string target = PackageManifest.ResolvePath(root, operation.Path);
            string backup = PackageManifest.ResolvePath(Path.Combine(Work, "backup"), operation.Path);
            string source = PackageManifest.ResolvePath(Staged, operation.Path);
            if (operation.HadOriginal)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Move(target, backup);
            }
            if (index == operationCount - 1 && stopAfterBackup)
            {
                break;
            }
            if (File.Exists(source))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(source, target);
            }
        }

        UpdateInstaller.Recover(root);
        AssertOriginalPackage();
        Assert.False(File.Exists(Path.Combine(root, "Data/added.dll")));
        UpdateInstaller.Recover(root);
        AssertOriginalPackage();
        // Simulate interruption after restoration but before the rollback marker persisted.
        File.Delete(Path.Combine(Work, "rolled-back"));
        UpdateInstaller.Recover(root);
        AssertOriginalPackage();
    }

    [Fact]
    public void LockedOwnedFileRollsBackEarlierOperations()
    {
        using FileStream locked = new(Path.Combine(root, "Data/Memoria.FF8.exe"), FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Throws<IOException>(() => UpdateInstaller.Apply(root));

        AssertOriginalPackage();
        Assert.False(File.Exists(Path.Combine(root, "Data/added.dll")));
    }

    [Theory]
    [InlineData("Data/obsolete.dll/added.dll")]
    [InlineData("Data/obsolete.dll/nested/added.dll")]
    public void FileToDirectoryTransitionCannotPreventRestorationOnFailure(string addedPath)
    {
        Directory.Delete(Staged, true);
        WritePackage(Staged, "new", addedPath);

        Assert.ThrowsAny<IOException>(() => UpdateInstaller.Apply(root, path =>
        {
            if (path == PackageManifest.LauncherPath)
            {
                throw new IOException("Injected installation failure");
            }
        }));

        AssertOriginalPackage();
    }

    [Fact]
    public void IncompleteTemporaryJournalDoesNotRequestRecovery()
    {
        File.WriteAllText(Path.Combine(Work, "journal.tmp"), "{truncated");

        Assert.False(UpdateInstaller.NeedsRecovery(root));
        UpdateInstaller.Recover(root);
        AssertOriginalPackage();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedDirectoryCanBecomeAFileAndRollBack(bool fail)
    {
        string nested = Path.Combine(root, "Data/obsolete.dll/nested/old.dll");
        File.Delete(Path.Combine(root, "Data/obsolete.dll"));
        WritePackage(root, "old", "Data/obsolete.dll/nested/old.dll");
        Directory.Delete(Staged, true);
        WritePackage(Staged, "new", "Data/obsolete.dll");

        if (fail)
        {
            Assert.Throws<IOException>(() => UpdateInstaller.Apply(root, path =>
            {
                if (path == PackageManifest.ManifestPath)
                {
                    throw new IOException("Injected after installing replacement file");
                }
            }));
            PackageManifest.Verify(root, PackageManifest.Read(root));
            Assert.Equal("old:Data/obsolete.dll/nested/old.dll", File.ReadAllText(nested));
            File.Delete(Path.Combine(Work, "rolled-back"));
            UpdateInstaller.Recover(root);
            PackageManifest.Verify(root, PackageManifest.Read(root));
        }
        else
        {
            UpdateInstaller.Apply(root);
            Assert.Equal("new:Data/obsolete.dll", Read("Data/obsolete.dll"));
            PackageManifest.Verify(root, PackageManifest.Read(root));
        }
        AssertUnknownFiles();
    }

    [Fact]
    public void UnknownFileInsideReplacedDirectoryStopsBeforeMutation()
    {
        File.Delete(Path.Combine(root, "Data/obsolete.dll"));
        WritePackage(root, "old", "Data/obsolete.dll/nested/old.dll");
        Write(root, "Data/obsolete.dll/user.txt", "keep");
        Directory.Delete(Staged, true);
        WritePackage(Staged, "new", "Data/obsolete.dll");

        Assert.Throws<IOException>(() => UpdateInstaller.Apply(root));
        Assert.Equal("keep", Read("Data/obsolete.dll/user.txt"));
        Assert.False(File.Exists(Path.Combine(Work, "journal.json")));
        PackageManifest.Verify(root, PackageManifest.Read(root));
    }

    [Fact]
    public void PrepareRecoversBeforeRemovingItsWorkDirectory()
    {
        UpdateJournal journal = UpdateInstaller.CreateJournal(root, PackageManifest.Read(Staged));
        File.WriteAllText(Path.Combine(Work, "journal.json"), JsonSerializer.Serialize(journal));
        string launcher = Path.Combine(root, PackageManifest.LauncherPath);
        string backup = Path.Combine(Work, "backup", PackageManifest.LauncherPath);
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.Move(launcher, backup);

        Assert.Equal(Work, UpdateInstaller.Prepare(root));

        AssertOriginalPackage();
        Assert.Empty(Directory.EnumerateFileSystemEntries(Work));
    }

    private void AssertOriginalPackage()
    {
        PackageManifest manifest = PackageManifest.Read(root);
        Assert.Equal("old", manifest.BuildId);
        PackageManifest.Verify(root, manifest);
        Assert.Equal("old:Data/obsolete.dll", Read("Data/obsolete.dll"));
        AssertUnknownFiles();
    }

    private void AssertUnknownFiles()
    {
        Assert.Equal("user mod", Read("Mods/custom/mod.ini"));
        Assert.Equal("user notes", Read("notes.txt"));
        Assert.Equal("user data", Read("Data/custom/user.bin"));
    }

    private string Read(string relative)
    {
        return File.ReadAllText(Path.Combine(root, relative));
    }

    private static void WritePackage(string directory, string buildId, string extra)
    {
        string[] paths = [PackageManifest.LauncherPath, "Data/Memoria.FF8.exe", "Data/audio-host/Memoria.FF8.Music.exe", extra];
        PackageFile[] files = paths.Select(path =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes(buildId + ":" + path);
            Write(directory, path, Encoding.UTF8.GetString(bytes));
            return new PackageFile(path, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        }).ToArray();
        PackageManifest manifest = new(1, buildId, "1.0", new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), files);
        Write(directory, PackageManifest.ManifestPath, JsonSerializer.Serialize(manifest));
    }

    private static void Write(string directory, string relative, string contents)
    {
        string path = Path.Combine(directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static void AddPackageFile(string directory, string relative, string content)
    {
        Write(directory, relative, content);
        PackageManifest manifest = PackageManifest.Read(directory);
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        PackageFile file = new(relative, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        manifest = manifest with { Files = [.. manifest.Files.Where(existing => !existing.Path.Equals(relative, StringComparison.OrdinalIgnoreCase)), file] };
        Write(directory, PackageManifest.ManifestPath, JsonSerializer.Serialize(manifest));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
