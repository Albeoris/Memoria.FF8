namespace Memoria.Launcher.Tests;

public sealed class GameInstallationTextTests
{
    [Fact]
    public void InstallationLabelSeparatesReleaseAndSelectedDirectoryWithReadableText()
    {
        string directory = Path.Combine("Selected game", "FINAL FANTASY VIII");

        Assert.Equal($"Steam (2013) · {directory}", new GameInstallation(directory, "Steam (2013)").ToString());
    }
}
