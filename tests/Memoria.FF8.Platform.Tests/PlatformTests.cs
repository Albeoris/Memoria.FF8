using System.Buffers.Binary;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Platform.Tests;

public sealed class PlatformTests
{
    [Fact]
    public void WindowsStorageRetainsReleasedPaths()
    {
        string root = Path.GetTempPath();
        ApplicationPaths paths = ApplicationPaths.Resolve(HostPlatform.Windows, root, root, _ => null);
        Assert.Equal(Path.Combine(root, "Memoria", "FF8", "launcher.json"), paths.LauncherSettings);
        Assert.Equal(Path.Combine(root, "Memoria.FF8", "checkpoint.json"), paths.DefaultCheckpoint);
    }

    [Fact]
    public void LinuxXdgOverridesMustBeAbsoluteAndStorageRolesStaySeparate()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "xdg"));
        ApplicationPaths paths = ApplicationPaths.Resolve(HostPlatform.Linux, root, root, name => name == "XDG_CONFIG_HOME" ? "relative" : name == "XDG_DATA_HOME" ? Path.Combine(root, "data") : null);
        Assert.Equal(Path.Combine(root, ".config", "Memoria", "FF8"), paths.Configuration);
        Assert.Equal(Path.Combine(root, "data", "Memoria", "FF8", "saves"), paths.Saves);
        Assert.NotEqual(paths.Cache, paths.Logs);
    }

    [Fact]
    public void MacUserDataIsOutsideApplicationBundle()
    {
        string root = Path.GetTempPath();
        ApplicationPaths paths = ApplicationPaths.Resolve(HostPlatform.MacOS, root, root, _ => null);
        Assert.Equal(Path.Combine(root, "Library", "Application Support", "Memoria", "FF8"), paths.Configuration);
        Assert.Equal(Path.Combine(root, "Library", "Caches", "Memoria", "FF8"), paths.Cache);
    }

    [Fact]
    public void ElfRejectsWrongBitnessArchitectureAndOperatingSystem()
    {
        byte[] image = new byte[64];
        "\u007fELF"u8.CopyTo(image);
        image[4] = 2;
        image[5] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(18), 62);
        NativeImage.RequireX64(new MemoryStream(image), "linux-x64");
        Assert.Throws<BadImageFormatException>(() => NativeImage.RequireX64(new MemoryStream(image), "osx-x64"));
        image[4] = 1;
        Assert.Throws<BadImageFormatException>(() => NativeImage.RequireX64(new MemoryStream(image), "linux-x64"));
        image[4] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(18), 183);
        Assert.Throws<BadImageFormatException>(() => NativeImage.RequireX64(new MemoryStream(image), "linux-x64"));
    }

    [Fact]
    public void MacUniversalRequiresActualContainedX64Slice()
    {
        byte[] image = new byte[96];
        BinaryPrimitives.WriteUInt32BigEndian(image, 0xcafebabe);
        BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(8), 0x01000007);
        BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(16), 64);
        BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(20), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(64), 0xfeedfacf);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(68), 0x01000007);
        NativeImage.RequireX64(new MemoryStream(image), "osx-x64");
        image[68] = 12;
        Assert.Throws<BadImageFormatException>(() => NativeImage.RequireX64(new MemoryStream(image), "osx-x64"));
    }

    [Fact]
    public void ManagedPortablePeIsNotANativeX86OrX64Dependency()
    {
        using FileStream assembly = File.OpenRead(typeof(PlatformTests).Assembly.Location);
        Assert.Throws<BadImageFormatException>(() => NativeImage.RequireX64(assembly, "win-x64"));
    }

    [Fact]
    public void CurrentX64AppHostHasTheExpectedOperatingSystemAbi()
    {
        using FileStream host = File.OpenRead(Environment.ProcessPath!);
        NativeImage.RequireX64(host, HostPlatforms.Current.RuntimeIdentifier());
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("sub/../../outside")]
    [InlineData("sub\\..\\outside")]
    public void DependencyDescriptorsRejectParentTraversal(string path)
    {
        Assert.Throws<InvalidDataException>(() => new NativeDependency("test", "win-x64", path).Resolve(Path.GetTempPath()));
    }
}
