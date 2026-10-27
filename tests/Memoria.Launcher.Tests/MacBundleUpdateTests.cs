using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.Launcher.Tests;

public sealed class MacBundleUpdateTests : IDisposable
{
    private readonly string directory = DistributionPaths.GetFullPath(Path.Combine(Path.GetTempPath(), "Memoria Mac bundle tests", Guid.NewGuid().ToString("N")));
    private string Root => Path.Combine(directory, "Memoria.FF8.app");

    [Fact]
    public void FixedBundleLayoutNormalizesThePackagedLauncherDirectory()
    {
        Assert.Equal(Root, MacBundleUpdate.NormalizeRoot(Path.Combine(Root, "Contents", "MacOS")));
        Assert.Equal(Root, MacBundleUpdate.NormalizeRoot(Root));
        Assert.Throws<InvalidDataException>(() => MacBundleUpdate.NormalizeRoot(Path.Combine(directory, "not-bundle", "Contents", "MacOS")));
        Assert.Equal(Path.Combine(directory, ".Memoria.FF8.app.memoria-update"), MacBundleUpdate.WorkPath(Root));
    }

    [Fact]
    public void ResumeIntentAndRestartEntrypointRemainOutsideAndInsideTheProperBundleBoundaries()
    {
        Prepare();
        string game = Path.Combine(directory, "game");
        string state = Path.Combine(directory, "game.ff8state");
        File.WriteAllText(state, "standalone state");
        new UpdateResume(game, state).Write(Root);
        Assert.False(UpdateResume.IntentPath(Root).StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        Assert.Equal(state, UpdateResume.Read(Root).SavePath);
        System.Diagnostics.ProcessStartInfo restart = UpdateInstaller.CreateRestartInfo(Root, recovery: false);
        Assert.Equal(Path.Combine(Root, "Contents", "MacOS", "Memoria.Launcher"), Path.GetFullPath(restart.FileName));
        Assert.Equal(["--resume-update"], restart.ArgumentList);
        Assert.Null(UpdateResume.TryDelete(Root));
    }

    [Fact]
    public void WholeBundleSwapRetainsOriginalAndKeepsTransactionOutsideTheBundle()
    {
        Prepare();
        UpdateInstaller.Apply(Root);
        Assert.Equal("new", PackageManifest.Read(Root).BuildId);
        string backup = Path.Combine(MacBundleUpdate.WorkPath(Root), "backup.app");
        Assert.Equal("old", PackageManifest.Read(backup).BuildId);
        MacBundleUpdate.VerifyExact(Root, PackageManifest.Read(Root));
        Assert.False(UpdateInstaller.NeedsRecovery(Root));
        Assert.False(Directory.Exists(Path.Combine(Root, UpdateInstaller.WorkDirectory)));
    }

    [Theory]
    [InlineData("before-backup")]
    [InlineData("after-backup")]
    [InlineData("after-install")]
    public void InterruptedSwapRollsBackAnEntireBundle(string boundary)
    {
        Prepare();
        Assert.Throws<IOException>(() => UpdateInstaller.Apply(Root, phase =>
        {
            if (phase == boundary)
            {
                throw new IOException("Injected interruption");
            }
        }));
        Assert.Equal("old", PackageManifest.Read(Root).BuildId);
        MacBundleUpdate.VerifyExact(Root, PackageManifest.Read(Root));
        Assert.False(UpdateInstaller.NeedsRecovery(Root));
        UpdateInstaller.Recover(Root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DurableJournalRecoversAfterProcessDeathBetweenRenames(bool installedNew)
    {
        Prepare();
        string work = MacBundleUpdate.WorkPath(Root);
        File.WriteAllText(Path.Combine(work, "bundle-journal.json"), JsonSerializer.Serialize(new MacBundleJournal(Root, "old", "new")));
        Directory.Move(Root, Path.Combine(work, "backup.app"));
        if (installedNew)
        {
            Directory.Move(MacBundleUpdate.StagingPath(Root), Root);
        }
        Assert.True(UpdateInstaller.NeedsRecovery(Root));
        UpdateInstaller.Recover(Root);
        Assert.Equal("old", PackageManifest.Read(Root).BuildId);
        Assert.False(UpdateInstaller.NeedsRecovery(Root));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("hash")]
    [InlineData("rid")]
    [InlineData("mode")]
    public void InvalidOrUnownedPayloadIsRejectedBeforeAnyBundleMove(string defect)
    {
        Prepare();
        string stage = MacBundleUpdate.StagingPath(Root);
        if (defect == "unknown")
        {
            File.WriteAllText(Path.Combine(Root, "Contents", "MacOS", "personal.txt"), "keep this");
        }
        else if (defect == "hash")
        {
            File.AppendAllText(Path.Combine(stage, "Contents", "Info.plist"), "tampered");
        }
        else
        {
            PackageManifest manifest = PackageManifest.Read(stage);
            manifest = defect == "rid" ? manifest with { RuntimeIdentifier = "linux-x64" } : manifest with { Files = manifest.Files.Select((file, index) => index == 0 ? file with { UnixMode = 511 } : file).ToArray() };
            File.WriteAllText(Path.Combine(stage, PackageManifest.MacManifestPath), JsonSerializer.Serialize(manifest));
        }
        Exception error = Record.Exception(() => UpdateInstaller.Apply(Root))!;
        Assert.True(error is IOException or InvalidDataException, error?.ToString());
        Assert.Equal("old", PackageManifest.Read(Root).BuildId);
        Assert.False(File.Exists(Path.Combine(MacBundleUpdate.WorkPath(Root), "bundle-journal.json")));
        if (defect == "unknown")
        {
            Assert.Equal("keep this", File.ReadAllText(Path.Combine(Root, "Contents", "MacOS", "personal.txt")));
        }
    }

    [Fact]
    public void DamagedIncomingBundleCannotPreventRestoringTheVerifiedOriginal()
    {
        Prepare();
        Assert.Throws<IOException>(() => UpdateInstaller.Apply(Root, phase =>
        {
            if (phase == "after-install")
            {
                File.WriteAllText(Path.Combine(Root, PackageManifest.MacManifestPath), "damaged descriptor");
                File.WriteAllText(Path.Combine(Root, "Contents", "MacOS", "personal.txt"), "retained user file");
                throw new IOException("Injected corruption after rename");
            }
        }));
        Assert.Equal("old", PackageManifest.Read(Root).BuildId);
        Assert.Equal("retained user file", File.ReadAllText(Path.Combine(MacBundleUpdate.StagingPath(Root), "Contents", "MacOS", "personal.txt")));
        Assert.Throws<InvalidDataException>(() => UpdateInstaller.Prepare(Root));
        Assert.True(File.Exists(Path.Combine(MacBundleUpdate.StagingPath(Root), "Contents", "MacOS", "personal.txt")));
    }

    [Fact]
    public void ForeignRecoveryJournalCannotMoveEitherBundle()
    {
        Prepare();
        File.WriteAllText(Path.Combine(MacBundleUpdate.WorkPath(Root), "bundle-journal.json"), JsonSerializer.Serialize(new MacBundleJournal(Path.Combine(directory, "other.app"), "old", "new")));
        Assert.Throws<InvalidDataException>(() => UpdateInstaller.Recover(Root));
        Assert.Equal("old", PackageManifest.Read(Root).BuildId);
        Assert.Equal("new", PackageManifest.Read(MacBundleUpdate.StagingPath(Root)).BuildId);
    }

    [Fact]
    public void MacArchiveStripsExactlyOneKnownBundlePrefixAndChecksInventory()
    {
        string source = Path.Combine(directory, "source.app");
        WriteBundle(source, "archive");
        string archive = Path.Combine(directory, "update.tar.gz");
        using (FileStream file = File.Create(archive))
        using (GZipStream gzip = new(file, CompressionMode.Compress))
        using (TarWriter writer = new(gzip, leaveOpen: false))
        {
            PackageManifest manifest = PackageManifest.Read(source);
            foreach (string path in manifest.Files.Select(file => file.Path).Append(PackageManifest.MacManifestPath))
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(source, path));
                PaxTarEntry entry = new(TarEntryType.RegularFile, "Memoria.FF8.app/" + path) { Mode = (UnixFileMode)(Path.GetFileName(path) is "Memoria.Launcher" or "Memoria.FF8" or "Memoria.FF8.Music" ? 493 : 420), DataStream = new MemoryStream(bytes) };
                writer.WriteEntry(entry);
                entry.DataStream.Dispose();
            }
        }
        string destination = Path.Combine(directory, "extracted.app");
        PackageManifest.Extract(archive, destination, "osx-x64");
        MacBundleUpdate.VerifyExact(destination, PackageManifest.Read(destination));
        Assert.Equal("archive", PackageManifest.Read(destination).BuildId);
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(archive, Path.Combine(directory, "wrong"), "linux-x64"));
    }

    private void Prepare()
    {
        WriteBundle(Root, "old");
        UpdateInstaller.Prepare(Root);
        WriteBundle(MacBundleUpdate.StagingPath(Root), "new");
    }

    private static void WriteBundle(string root, string build)
    {
        Dictionary<string, string> files = new()
        {
            ["Contents/Info.plist"] = "plist " + build,
            ["Contents/MacOS/Memoria.Launcher"] = "launcher " + build,
            ["Contents/MacOS/Memoria.Launcher.dll"] = "runtime " + build,
            ["Contents/MacOS/Data/Memoria.FF8"] = "bootstrap " + build,
            ["Contents/MacOS/Data/audio-host/Memoria.FF8.Music"] = "music " + build
        };
        List<PackageFile> inventory = [];
        foreach ((string relative, string text) in files)
        {
            byte[] content = Encoding.UTF8.GetBytes(text);
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
            int mode = Path.GetFileName(path) is "Memoria.Launcher" or "Memoria.FF8" or "Memoria.FF8.Music" ? 493 : 420;
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, (UnixFileMode)mode);
            }
            inventory.Add(new PackageFile(relative, content.Length, Convert.ToHexString(SHA256.HashData(content)), mode));
        }
        PackageManifest manifest = new(2, build, "1.0", DateTimeOffset.UtcNow, inventory.ToArray()) { RuntimeIdentifier = "osx-x64" };
        File.WriteAllText(Path.Combine(root, PackageManifest.MacManifestPath), JsonSerializer.Serialize(manifest));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
