using Memoria.FF8.Platform;

namespace Memoria.Launcher.Tests;

public sealed class MacSignatureManifestTests
{
    [Theory]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public void Signed_inventory_requires_canonical_hash_only_for_the_outer_launcher(string rid)
    {
        PackageManifest manifest = Create(rid);
        manifest.ValidatePaths();
        Assert.Throws<InvalidDataException>((manifest with { Files = manifest.Files.Where(file => file.Path != "Contents/Resources/Memoria.Launcher").ToArray() }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = manifest.Files.Select(file => file with { HashKind = "sha256" }).ToArray() }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = manifest.Files.Select(file => file with { HashKind = MacCodeSigning.UnsignedHashKind }).ToArray() }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { SchemaVersion = 2 }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { RuntimeIdentifier = "linux-x64" }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = [.. manifest.Files, new(MacCodeSigning.ResourceEnvelope, 0, new string('a', 64), 420)] }).ValidatePaths);
    }

    [Fact]
    public void MacOS_directory_contains_only_the_bundle_entrypoint()
    {
        PackageManifest manifest = Create("osx-x64");
        manifest.ValidatePaths();
        foreach (string path in new[] { "Contents/MacOS/SigningFixture.dll", "Contents/MacOS/Data/extra.json", "Contents/Frameworks/extra.dylib" })
        {
            Assert.Throws<InvalidDataException>((manifest with { Files = [.. manifest.Files, new(path, 0, new string('a', 64), 420)] }).ValidatePaths);
        }
    }

    [Fact]
    public void Native_signature_checks_cannot_silently_succeed_on_another_platform()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Throws<PlatformNotSupportedException>(() => MacCodeSigning.VerifyBundle("unused.app"));
            Assert.Throws<PlatformNotSupportedException>(() => MacCodeSigning.UnsignedHash("unused"));
        }
    }

    private static PackageManifest Create(string rid)
    {
        string hash = new('a', 64);
        return new PackageManifest(3, "build", "v1.2.3", DateTimeOffset.UnixEpoch,
        [
            new("Contents/Info.plist", 0, hash, 420),
            new(MacCodeSigning.Launcher, 0, hash, 493, MacCodeSigning.UnsignedHashKind),
            new("Contents/Resources/Memoria.Launcher", 0, hash, 493),
            new("Contents/Resources/SigningFixture.dll", 0, hash, 420),
            new("Contents/Resources/Data/Memoria.FF8", 0, hash, 493),
            new("Contents/Resources/Data/audio-host/Memoria.FF8.Music", 0, hash, 493),
            new("Contents/Resources/Data/Game/vulkan/Memoria.PsfDecoder", 0, hash, 493)
        ]) { RuntimeIdentifier = rid };
    }
}
