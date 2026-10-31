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
        Assert.Throws<InvalidDataException>((manifest with { Files = manifest.Files.Select(file => file with { HashKind = "sha256" }).ToArray() }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = manifest.Files.Select(file => file with { HashKind = MacCodeSigning.UnsignedHashKind }).ToArray() }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { SchemaVersion = 2 }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { RuntimeIdentifier = "linux-x64" }).ValidatePaths);
        Assert.Throws<InvalidDataException>((manifest with { Files = [.. manifest.Files, new(MacCodeSigning.ResourceEnvelope, 0, new string('a', 64), 420)] }).ValidatePaths);
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
            new("Contents/MacOS/Data/Memoria.FF8", 0, hash, 493),
            new("Contents/MacOS/Data/audio-host/Memoria.FF8.Music", 0, hash, 493),
            new("Contents/MacOS/Data/Game/vulkan/Memoria.PsfDecoder", 0, hash, 493)
        ]) { RuntimeIdentifier = rid };
    }
}
