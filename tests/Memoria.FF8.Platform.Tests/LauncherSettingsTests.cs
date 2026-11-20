using Memoria.FF8.Platform;

namespace Memoria.FF8.Platform.Tests;

public sealed class LauncherSettingsTests
{
    [Theory]
    [InlineData(HostPlatform.Windows)]
    [InlineData(HostPlatform.Linux)]
    [InlineData(HostPlatform.MacOS)]
    public void ModDestinationsFollowPlatformStorageRules(HostPlatform platform)
    {
        string root = Path.GetFullPath("storage fixture");
        string game = Path.Combine(root, "Game");
        string package = platform == HostPlatform.MacOS ? Path.Combine(root, "Memoria.app", "Contents", "Resources") : root;
        ApplicationPaths paths = ApplicationPaths.Resolve(platform, root, root, _ => null);
        LauncherSettings defaults = new();
        string local = Path.Combine(root, "Mods");
        string user = Path.GetFullPath(Path.Combine(platform == HostPlatform.Windows ? paths.Configuration : paths.LegacyState, "Mods"));
        string gameMods = Path.Combine(game, "Mods");

        Assert.Equal(platform == HostPlatform.MacOS ? user : local, Resolve(defaults).Primary);
        Assert.Equal(local, Resolve(defaults.WithModLocation(ModInstallationLocation.Memoria, null)).Primary);
        Assert.Equal(gameMods, Resolve(defaults.WithModLocation(ModInstallationLocation.Game, null)).Primary);
        Assert.Equal(user, Resolve(defaults.WithModLocation(ModInstallationLocation.UserData, null)).Primary);

        ModDirectories Resolve(LauncherSettings settings) => ApplicationInstallation.ResolveModDirectories(package, game, paths, settings, platform);
    }

    [Fact]
    public void OlderSettingsDefaultToSystemAppearanceWithoutLosingPreferences()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-launcher-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "launcher.json");
            File.WriteAllText(path, """{"AchievementsLocalOnly":true,"ResourceLanguage":"fr","CloseAfterLaunch":true}""");
            LauncherSettings settings = LauncherSettings.Load(path);
            Assert.Equal("System", settings.Appearance);
            Assert.True(settings.AchievementsLocalOnly);
            Assert.Equal("fr", settings.ResourceLanguage);
            Assert.True(settings.CloseAfterLaunch);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("System")]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void AppearanceSurvivesSavingOtherPreferences(string appearance)
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-launcher-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "launcher.json");
            new LauncherSettings(ResourceLanguage: "en", Appearance: appearance).Save(path);
            LauncherSettings settings = LauncherSettings.Load(path);
            (settings with { CloseAfterLaunch = true }).Save(path);
            LauncherSettings restored = LauncherSettings.Load(path);
            Assert.Equal(appearance, restored.Appearance);
            Assert.Equal("en", restored.ResourceLanguage);
            Assert.True(restored.CloseAfterLaunch);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
