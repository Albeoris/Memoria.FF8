using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.Launcher.Tests;

public sealed class MacSignedPackageTests : IDisposable
{
    private readonly string directory = DistributionPaths.GetFullPath(Path.Combine(Path.GetTempPath(), "Memoria signed package tests", Guid.NewGuid().ToString("N")));

    [Fact]
    public void SignedBundleInventorySurvivesLauncherArchiveExtraction()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.SkipUnless(false, "Native macOS code-signature validation requires macOS.");
            return;
        }
        string root = PrepareBundle();
        PackageManifest original = PackageManifest.Read(root);
        MacBundleUpdate.VerifyExact(root, original);
        PackageFile launcher = Assert.Single(original.Files, file => file.Path == MacCodeSigning.Launcher);
        Assert.Equal(MacCodeSigning.UnsignedHashKind, launcher.HashKind);
        Assert.True(new FileInfo(Path.Combine(root, launcher.Path)).Length > launcher.Size);

        string archive = CreateArchive(root, "signed.tar.gz");
        string staged = Path.Combine(directory, "staged.app");
        PackageManifest.Extract(archive, staged, ReleaseClient.CurrentRuntimeIdentifier);
        PackageManifest extracted = PackageManifest.Read(staged);
        Assert.Equal(3, extracted.SchemaVersion);
        Assert.Equal(original.BuildId, extracted.BuildId);
        Assert.Equal(launcher, Assert.Single(extracted.Files, file => file.Path == MacCodeSigning.Launcher));
        Assert.Equal(File.ReadAllBytes(Path.Combine(root, MacCodeSigning.ResourceEnvelope)), File.ReadAllBytes(Path.Combine(staged, MacCodeSigning.ResourceEnvelope)));
        MacBundleUpdate.VerifyExact(staged, extracted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SignedArchiveRequiresExactlyTheGeneratedResourceEnvelope(bool extraEnvelope)
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.SkipUnless(false, "Native macOS code-signature validation requires macOS.");
            return;
        }
        string root = PrepareBundle();
        if (extraEnvelope)
        {
            File.WriteAllText(Path.Combine(root, "Contents", "_CodeSignature", "Unexpected"), "Unowned signature resource.");
        }
        else
        {
            File.Delete(Path.Combine(root, MacCodeSigning.ResourceEnvelope));
        }
        string archive = CreateArchive(root, "invalid.tar.gz");
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract(archive, Path.Combine(directory, "invalid.app"), ReleaseClient.CurrentRuntimeIdentifier));
    }

    [SupportedOSPlatform("macos")]
    private string PrepareBundle()
    {
        string root = Path.Combine(directory, "Memoria.FF8.app");
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("The native test process path is unavailable.");
        Assert.True(MacCodeSigning.IsMachO(executable));
        foreach (string relative in new[] { MacCodeSigning.Launcher, "Contents/MacOS/Data/Memoria.FF8", "Contents/MacOS/Data/audio-host/Memoria.FF8.Music", "Contents/MacOS/Data/Game/vulkan/Memoria.FF8", "Contents/MacOS/Data/Game/vulkan/Memoria.PsfDecoder" })
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(executable, path);
            File.SetUnixFileMode(path, (UnixFileMode)493);
            MacCodeSigning.Sign(path);
        }
        string plist = Path.Combine(root, "Contents", "Info.plist");
        File.WriteAllText(plist, """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0"><dict>
            <key>CFBundleExecutable</key><string>Memoria.Launcher</string>
            <key>CFBundleIdentifier</key><string>org.memoria.ff8.signingtests</string>
            <key>CFBundleName</key><string>Memoria signing tests</string>
            <key>CFBundlePackageType</key><string>APPL</string>
            <key>CFBundleShortVersionString</key><string>1.2.3</string>
            <key>CFBundleVersion</key><string>1.2.3</string>
            </dict></plist>
            """);
        File.SetUnixFileMode(plist, (UnixFileMode)420);
        PackageFile[] files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path =>
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative == MacCodeSigning.Launcher)
            {
                (long size, string hash) = MacCodeSigning.UnsignedHash(path);
                return new PackageFile(relative, size, hash, 493, MacCodeSigning.UnsignedHashKind);
            }
            using FileStream input = File.OpenRead(path);
            return new PackageFile(relative, input.Length, Convert.ToHexStringLower(SHA256.HashData(input)), (int)File.GetUnixFileMode(path));
        }).ToArray();
        PackageManifest manifest = new(3, new string('a', 40), "v1.2.3", DateTimeOffset.UtcNow, files) { RuntimeIdentifier = ReleaseClient.CurrentRuntimeIdentifier };
        manifest.ValidatePaths();
        string manifestPath = Path.Combine(root, PackageManifest.MacManifestPath);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        File.SetUnixFileMode(manifestPath, (UnixFileMode)420);
        MacCodeSigning.Sign(root);
        return root;
    }

    [SupportedOSPlatform("macos")]
    private string CreateArchive(string root, string name)
    {
        string archive = Path.Combine(directory, name);
        using FileStream output = File.Create(archive);
        using GZipStream gzip = new(output, CompressionLevel.Optimal);
        using TarWriter writer = new(gzip, TarEntryFormat.Pax);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            using FileStream input = File.OpenRead(path);
            string relative = "Memoria.FF8.app/" + Path.GetRelativePath(root, path).Replace('\\', '/');
            PaxTarEntry entry = new(TarEntryType.RegularFile, relative) { DataStream = input, Mode = File.GetUnixFileMode(path) };
            writer.WriteEntry(entry);
        }
        return archive;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
