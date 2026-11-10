using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverLaunchResourcesTests
{
    [Fact]
    public void ExplicitResourcesOverrideTheLauncherAndSteam()
    {
        LaunchOptions options = LaunchOptions.Parse(["--ff7-game-dir", Path.GetFullPath("explicit-ff7")]);
        Assert.Same(options, Ff7LaunchResources.Resolve(options, "unused-launcher.json", () => throw new InvalidOperationException("Unexpected Steam discovery.")));
    }

    [Fact]
    public void ADirectLaunchUsesTheSavedLauncherSelection()
    {
        string settings = Path.Combine(Path.GetTempPath(), $"memoria-ff7-settings-{Guid.NewGuid():N}.json");
        string selected = Path.GetFullPath("selected-ff7");
        try
        {
            new LauncherSettings(Ff7GameDirectory: selected).Save(settings);
            LaunchOptions options = Ff7LaunchResources.Resolve(new() { Ff7GameDirectory = null }, settings, () => throw new InvalidOperationException("Unexpected Steam discovery."));
            Assert.Equal(selected, options.Ff7GameDirectory);
        }
        finally
        {
            File.Delete(settings);
        }
    }

    [Fact]
    public void AnUnconfiguredLaunchUsesTheSupportedSteamInstallationWhenPresent()
    {
        string settings = Path.Combine(Path.GetTempPath(), $"memoria-no-settings-{Guid.NewGuid():N}.json");
        string detected = Path.GetFullPath("detected-ff7");
        LaunchOptions options = new() { Ff7GameDirectory = null };

        Assert.Equal(detected, Ff7LaunchResources.Resolve(options, settings, () => detected).Ff7GameDirectory);
        Assert.Null(Ff7LaunchResources.Resolve(options, settings, () => null).Ff7GameDirectory);
    }
}
