using Memoria.FF8.Platform;

namespace Memoria.FF8.Platform.Tests;

public sealed class LauncherSettingsTests
{
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
