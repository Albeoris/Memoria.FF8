using Memoria.FF8.Platform;

namespace Memoria.FF8.Platform.Tests;

public sealed class ApplicationInstallationTests
{
    [Fact]
    public void ExplicitDataRootIsIndependentOfTheNestedHostAndWorkingDirectory()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "package with spaces", "Data"));
        string host = Path.Combine(root, "Game", "vulkan");
        Assert.Equal(root, ApplicationInstallation.Resolve(host, root));
        Assert.Equal(host, ApplicationInstallation.Resolve(host, null));
        Assert.Throws<InvalidDataException>(() => ApplicationInstallation.Resolve(host, "relative/Data"));
    }
    [Theory]
    [InlineData(HostPlatform.Windows)]
    [InlineData(HostPlatform.Linux)]
    [InlineData(HostPlatform.MacOS)]
    public void UserModsUseConfigurationOutsideMacBundlesAndPackageDirectoryElsewhere(HostPlatform platform)
    {
        string package = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Memoria.FF8.app", "Contents", "MacOS"));
        string configuration = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "user configuration"));
        string expectedRoot = platform == HostPlatform.MacOS ? configuration : package;
        Assert.Equal(Path.Combine(expectedRoot, "Mods"), ApplicationInstallation.ResolveModsDirectory(package, platform, configuration));
    }
}
