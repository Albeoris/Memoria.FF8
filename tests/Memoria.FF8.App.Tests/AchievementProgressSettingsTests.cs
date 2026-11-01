using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class AchievementProgressSettingsTests
{
    [Fact]
    public void Progress_notifications_default_off_for_new_and_existing_configuration()
    {
        string path = Path.GetTempFileName();
        try
        {
            Assert.False(new UiPreferences().ShowAchievementProgress);
            File.WriteAllText(path, "{}");
            Assert.False(UiPreferences.Load(path).ShowAchievementProgress);
            File.Delete(path);
            Assert.False(UiPreferences.Load(path).ShowAchievementProgress);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_progress_notification_preference_survives_other_settings_changes(bool enabled)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, enabled ? "{\"ShowAchievementProgress\":true}" : "{\"ShowAchievementProgress\":false}");
            UiPreferences preferences = UiPreferences.Load(path);
            Assert.Equal(enabled, preferences.ShowAchievementProgress);
            preferences.WithSetting("Music volume", "25%").Save(path);
            UiPreferences reloaded = UiPreferences.Load(path);
            Assert.Equal(enabled, reloaded.ShowAchievementProgress);
            Assert.Equal(25, reloaded.MusicVolume);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
