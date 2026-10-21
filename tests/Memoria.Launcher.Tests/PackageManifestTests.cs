using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.Launcher.Tests;

public sealed class PackageManifestTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.Package.Tests", Guid.NewGuid().ToString("N"));

    public PackageManifestTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public void ValidPackageExtractsAndVerifiesWithOptionalEmptyModDirectories()
    {
        string archive = CreateArchive();
        string destination = Path.Combine(directory, "extracted");

        PackageManifest.Extract(archive, destination);

        PackageManifest manifest = PackageManifest.Read(destination);
        Assert.Equal("local-build", manifest.BuildId);
        Assert.Equal(3, manifest.Files.Length);
        Assert.True(Directory.Exists(Path.Combine(destination, "Mods", "Tonberry")));
        PackageManifest.Verify(destination, manifest);
    }

    [Fact]
    public void OptionalManifestOnlyIgnoresAbsenceAndSupportsCamelCaseJson()
    {
        Assert.Null(PackageManifest.LoadOptional(directory));
        string data = Directory.CreateDirectory(Path.Combine(directory, "Data")).FullName;
        string path = Path.Combine(data, "distribution.json");
        File.WriteAllText(path, "{broken");
        Assert.Throws<InvalidDataException>(() => PackageManifest.LoadOptional(directory));
        File.WriteAllText(path, JsonSerializer.Serialize(Manifest(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        Assert.Equal("local-build", PackageManifest.LoadOptional(directory)!.BuildId);
    }

    [Theory]
    [InlineData("Mods/Memoria/Depixelation/fields/bghall_6.json")]
    [InlineData("Data/BundledMods/Memoria/Depixelation/fields/bghall_6.json")]
    public void ArchiveIncludesOnlyInventoriedNativeModFiles(string path)
    {
        PackageManifest manifest = Manifest();
        manifest = manifest with { Files = [.. manifest.Files, FileRecord(path, "payload")] };
        string destination = Path.Combine(directory, "native-mod");
        PackageManifest.Extract(CreateArchive(extraPath: path, manifest: manifest), destination);
        Assert.Equal("payload", File.ReadAllText(Path.Combine(destination, path)));
        PackageManifest.Verify(destination, PackageManifest.Read(destination));
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(CreateArchive(extraPath: path), Path.Combine(directory, "unlisted-native-mod")));
    }

    [Theory]
    [InlineData("../escape.exe")]
    [InlineData("/Data/escape.exe")]
    [InlineData("C:/Data/escape.exe")]
    [InlineData("Data/../escape.exe")]
    [InlineData("Data\\escape.exe")]
    [InlineData("Data/payload.exe:stream")]
    [InlineData("Data/CON.txt")]
    [InlineData("Data/com1.log")]
    [InlineData("Data/LPT².log")]
    [InlineData("Data/ends. /file")]
    [InlineData("Data/ends./file")]
    [InlineData("Data//file")]
    [InlineData("Mods/Tonberry/payload.exe")]
    [InlineData("Mods/Memoria/mods.ini")]
    [InlineData("Mods/mods.ini")]
    [InlineData("settings.json")]
    [InlineData("Data/distribution.json")]
    [InlineData("Data/distribution.json/child")]
    public void ManifestRejectsUnsafeOrUnownedPaths(string path)
    {
        PackageManifest manifest = Manifest();
        manifest = manifest with { Files = [.. manifest.Files, FileRecord(path, "payload")] };

        Assert.Throws<InvalidDataException>(manifest.ValidatePaths);
    }

    [Fact]
    public void ManifestRejectsDuplicateWindowsPathsMissingExecutablesAndUnsupportedSchema()
    {
        PackageManifest manifest = Manifest();
        Assert.Throws<InvalidDataException>((manifest with { Files = [.. manifest.Files, FileRecord("data/MEMORIA.FF8.exe", "game")] }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = manifest.Files[..2] }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { SchemaVersion = 2 }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { BuildId = " " }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = [.. manifest.Files, FileRecord("Data/large", "") with { Size = 2L * 1024 * 1024 * 1024 + 1 }] }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = [.. manifest.Files, FileRecord("Data/hash", "") with { Sha256 = new string('x', 64) }] }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = [.. manifest.Files, FileRecord("Data/parent", ""), FileRecord("Data/parent/child", "")] }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = manifest.Files.Select(file => file with { Size = 2L * 1024 * 1024 * 1024 }).ToArray() }).ValidatePaths);
    }

    [Theory]
    [InlineData("../escape.exe")]
    [InlineData("Data/../../escape.exe")]
    [InlineData("Data\\escape.exe")]
    [InlineData("Data/AUX.txt")]
    [InlineData("Data/payload:stream")]
    [InlineData("Mods/Tonberry/payload.exe")]
    [InlineData("Mods/Memoria/mods.ini")]
    [InlineData("Mods/mods.ini")]
    [InlineData("unexpected.exe")]
    [InlineData("Mods/unknown/")]
    [InlineData("Mods/Tonberry/nested/")]
    [InlineData("Data/unlisted.bin")]
    public void ArchiveRejectsUnsafeOrUnexpectedEntriesBeforeExtracting(string path)
    {
        string archive = CreateArchive(extraPath: path);
        string destination = Path.Combine(directory, "rejected");

        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(archive, destination));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public void ArchiveRejectsCaseInsensitiveDuplicatesAndSymbolicLinks()
    {
        string duplicate = CreateArchive(extraPath: "data/MEMORIA.FF8.exe");
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(duplicate, Path.Combine(directory, "duplicate")));
        string link = CreateArchive(extraPath: "Data/link", externalAttributes: unchecked((int)0xA1FF0000));
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(link, Path.Combine(directory, "link")));
        string reparse = CreateArchive(extraPath: "Data/reparse", externalAttributes: (int)FileAttributes.ReparsePoint);
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(reparse, Path.Combine(directory, "reparse")));
    }

    [Fact]
    public void ArchiveRejectsAdvertisedOversizedFilesBeforeWriting()
    {
        string archive = CreateArchive();
        byte[] bytes = File.ReadAllBytes(archive);
        for (int index = 0; index <= bytes.Length - 46; index++)
        {
            if (BitConverter.ToUInt32(bytes, index) == 0x02014B50)
            {
                BitConverter.GetBytes((uint)(2L * 1024 * 1024 * 1024 + 1)).CopyTo(bytes, index + 24);
                break;
            }
        }

        File.WriteAllBytes(archive, bytes);
        string destination = Path.Combine(directory, "oversized");
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(archive, destination));
        Assert.False(Directory.Exists(destination));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"SchemaVersion\":1,\"BuildId\":\"local\",\"Version\":\"1\",\"BuiltAt\":\"2026-01-01T00:00:00Z\",\"Files\":null}")]
    public void ManifestRejectsMissingJsonIdentityOrInventory(string json)
    {
        Directory.CreateDirectory(Path.Combine(directory, "Data"));
        File.WriteAllText(Path.Combine(directory, "Data", "distribution.json"), json);
        Assert.Throws<InvalidDataException>(() => PackageManifest.Read(directory));
    }

    [Fact]
    public void ArchiveRejectsMissingManifestMissingPayloadAndIncorrectSizes()
    {
        string absentManifest = CreateArchive(skipPath: PackageManifest.ManifestPath);
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(absentManifest, Path.Combine(directory, "no-manifest")));
        string absentPayload = CreateArchive(skipPath: "Data/Memoria.FF8.exe");
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(absentPayload, Path.Combine(directory, "no-payload")));
        PackageManifest manifest = Manifest();
        manifest.Files[0] = manifest.Files[0] with { Size = 99 };
        string sizeMismatch = CreateArchive(manifest: manifest);
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(sizeMismatch, Path.Combine(directory, "wrong-size")));
    }

    [Fact]
    public void ArchiveRejectsWrongHashAndNonemptyDestination()
    {
        PackageManifest manifest = Manifest();
        manifest.Files[0] = manifest.Files[0] with { Sha256 = new string('0', 64) };
        string wrongHash = CreateArchive(manifest: manifest);
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(wrongHash, Path.Combine(directory, "wrong-hash")));
        string archive = CreateArchive();
        string destination = Directory.CreateDirectory(Path.Combine(directory, "existing")).FullName;
        string existing = Path.Combine(destination, "keep.txt");
        File.WriteAllText(existing, "preserve");
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(archive, destination));
        Assert.Equal("preserve", File.ReadAllText(existing));
    }

    [Fact]
    public void VerifyRejectsChangedAndMissingFiles()
    {
        string destination = Path.Combine(directory, "verify");
        PackageManifest.Extract(CreateArchive(), destination);
        PackageManifest manifest = PackageManifest.Read(destination);
        string launcher = Path.Combine(destination, PackageManifest.LauncherPath);
        File.WriteAllText(launcher, "tampered");
        Assert.Throws<InvalidDataException>(() => PackageManifest.Verify(destination, manifest));
        File.Delete(launcher);
        Assert.Throws<FileNotFoundException>(() => PackageManifest.Verify(destination, manifest));
    }

    private string CreateArchive(string? extraPath = null, string? skipPath = null, PackageManifest? manifest = null, int? externalAttributes = null)
    {
        string archivePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".zip");
        using ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        Dictionary<string, string> contents = new()
        {
            [PackageManifest.LauncherPath] = "launcher",
            ["Data/Memoria.FF8.exe"] = "game",
            ["Data/audio-host/Memoria.FF8.Music.exe"] = "music",
            [PackageManifest.ManifestPath] = JsonSerializer.Serialize(manifest ?? Manifest())
        };
        foreach ((string path, string content) in contents)
        {
            if (path == skipPath)
            {
                continue;
            }

            WriteEntry(archive, path, content);
        }

        archive.CreateEntry("Mods/Tonberry/");
        archive.CreateEntry("Mods/Demaster/");
        archive.CreateEntry("Mods/Junction/");
        if (extraPath is not null)
        {
            ZipArchiveEntry extra = WriteEntry(archive, extraPath, extraPath.EndsWith('/') ? "" : "payload");
            if (externalAttributes.HasValue)
            {
                extra.ExternalAttributes = externalAttributes.Value;
            }
        }

        return archivePath;
    }

    private static ZipArchiveEntry WriteEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using Stream stream = entry.Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
        return entry;
    }

    private static PackageManifest Manifest()
    {
        return new PackageManifest(1, "local-build", "1.2.3", DateTimeOffset.UtcNow, [FileRecord(PackageManifest.LauncherPath, "launcher"), FileRecord("Data/Memoria.FF8.exe", "game"), FileRecord("Data/audio-host/Memoria.FF8.Music.exe", "music")]);
    }

    private static PackageFile FileRecord(string path, string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return new PackageFile(path, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }
}
