using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.Launcher.Tests;

public sealed class PortableUpdateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.PortableUpdate.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("win-x64", "Memoria-FF8-win-x64.zip")]
    [InlineData("linux-x64", "Memoria-FF8-linux-x64.tar.gz")]
    [InlineData("osx-x64", "Memoria-FF8-osx-x64.tar.gz")]
    [InlineData("osx-arm64", "Memoria-FF8-osx-arm64.tar.gz")]
    public void ReleaseAssetSelectionIsRidSpecific(string rid, string asset)
    {
        Assert.Equal(asset, ReleaseClient.AssetNameFor(rid));
    }

    [Fact]
    public void LinuxArchiveRestoresExecutableModesAndVerifiesItsInventory()
    {
        string archive = Archive();
        string destination = Path.Combine(root, "extracted");
        PackageManifest.Extract(archive, destination, "linux-x64");
        PackageManifest manifest = PackageManifest.Read(destination);
        Assert.Equal("linux-x64", manifest.RuntimeIdentifier);
        Assert.Equal("Memoria.Launcher", manifest.LauncherEntryPoint);
        PackageManifest.Verify(destination, manifest);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(493, (int)File.GetUnixFileMode(Path.Combine(destination, "Data", "audio-host", "Memoria.FF8.Music")));
        }
    }

    [Fact]
    public void WrongRidIsRejectedBeforeExtractionCreatesDestination()
    {
        string archive = Archive();
        string destination = Path.Combine(root, "rejected");
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(archive, destination, "win-x64"));
        Assert.False(Directory.Exists(destination));
    }

    [Theory]
    [InlineData("Data/../escape", false)]
    [InlineData("Data/link", true)]
    public void TraversalAndLinksAreRejectedBeforeExtraction(string name, bool link)
    {
        string archive = Archive((name, link));
        string destination = Path.Combine(root, "rejected");
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(archive, destination, "linux-x64"));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public void TransactionRejectsDifferentRidBeforeCreatingAJournal()
    {
        string archive = Archive();
        string installation = Path.Combine(root, "installed");
        PackageManifest.Extract(archive, installation, "linux-x64");
        PackageManifest installed = PackageManifest.Read(installation);
        Assert.Throws<InvalidDataException>(() => UpdateInstaller.CreateJournal(installation, installed with { RuntimeIdentifier = "osx-x64" }));
        Assert.False(File.Exists(Path.Combine(installation, UpdateInstaller.WorkDirectory, "journal.json")));
    }

    [Fact]
    public void LinuxTransactionRollsBackAndPreservesUserFiles()
    {
        string archive = Archive();
        string installation = Path.Combine(root, "installed");
        PackageManifest.Extract(archive, installation, "linux-x64");
        string stage = Path.Combine(installation, UpdateInstaller.WorkDirectory, "new");
        PackageManifest.Extract(archive, stage, "linux-x64");
        string user = Path.Combine(installation, "Data", "user-settings.json");
        File.WriteAllText(user, "user settings");
        int count = 0;
        Assert.Throws<IOException>(() => UpdateInstaller.Apply(installation, _ =>
        {
            if (++count == 2)
            {
                throw new IOException("Injected interrupted transaction.");
            }
        }));
        PackageManifest.Verify(installation, PackageManifest.Read(installation));
        Assert.Equal("user settings", File.ReadAllText(user));
        Assert.False(UpdateInstaller.NeedsRecovery(installation));
    }

    private string Archive((string Name, bool Link)? extra = null)
    {
        Directory.CreateDirectory(root);
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
        {
            ["Memoria.Launcher"] = Encoding.UTF8.GetBytes("launcher"),
            ["Memoria.Launcher.dll"] = Encoding.UTF8.GetBytes("runtime closure"),
            ["Data/Memoria.FF8"] = Encoding.UTF8.GetBytes("bootstrap"),
            ["Data/audio-host/Memoria.FF8.Music"] = Encoding.UTF8.GetBytes("portable music")
        };
        PackageFile[] inventory = files.Select(file => new PackageFile(file.Key, file.Value.Length, Convert.ToHexStringLower(SHA256.HashData(file.Value)), Mode(file.Key))).ToArray();
        PackageManifest manifest = new(2, "source", "test", DateTimeOffset.UtcNow, inventory) { RuntimeIdentifier = "linux-x64" };
        files.Add(PackageManifest.ManifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest));
        string archive = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tar.gz");
        using FileStream output = File.Create(archive);
        using GZipStream gzip = new(output, CompressionLevel.Optimal);
        using TarWriter writer = new(gzip, TarEntryFormat.Pax);
        foreach (var file in files)
        {
            using MemoryStream content = new(file.Value);
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, file.Key) { DataStream = content, Mode = (UnixFileMode)Mode(file.Key) });
        }
        if (extra is { } injected)
        {
            PaxTarEntry entry = new(injected.Link ? TarEntryType.SymbolicLink : TarEntryType.RegularFile, injected.Name);
            if (injected.Link)
            {
                entry.LinkName = "outside";
            }
            writer.WriteEntry(entry);
        }
        return archive;
    }

    private static int Mode(string path) => Path.GetFileName(path) is "Memoria.Launcher" or "Memoria.FF8" or "Memoria.FF8.Music" ? 493 : 420;

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
