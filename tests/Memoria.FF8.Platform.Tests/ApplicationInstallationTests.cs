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
}
