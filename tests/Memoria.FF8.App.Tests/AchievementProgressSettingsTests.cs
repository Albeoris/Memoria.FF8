using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class AchievementProgressSettingsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Interface_toggle_applies_and_persists_from_title_and_active_game(bool activeGame)
    {
        string path = Path.GetTempFileName();
        try
        {
            UiPreferences preferences = new();
            MenuData data = new();
            data.Settings["Achievement progress"] = "Off";
            data.SupportedActions.Add("config:Achievement progress");
            MenuController menus = new(() => data, action =>
            {
                preferences = preferences.WithInstallationSetting(action.SubjectId, action.Value, activeGame);
                preferences.Save(path);
                data.Settings[action.SubjectId] = action.Value;
            });
            menus.Open("config-interface");
            MenuEntry toggle = Assert.Single(menus.Entries, entry => entry.Id == "Achievement progress");
            Assert.Equal("On", toggle.Action!.Value);
            menus.Execute(toggle.Action);
            Assert.True(preferences.ShowAchievementProgress);
            Assert.True(UiPreferences.Load(path).ShowAchievementProgress);
            menus.Execute(menus.Entries.Single(entry => entry.Id == "Achievement progress").Action!);
            Assert.False(preferences.ShowAchievementProgress);
            Assert.False(UiPreferences.Load(path).ShowAchievementProgress);
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("Achievement progress", "Sometimes"));
            SettingPlacement placement = Assert.Single(SettingsPlacement.All, item => item.LegacyKey == "Achievement progress");
            Assert.Equal("config-interface", placement.Page);
            Assert.Equal("Global", placement.Scope);
        }
        finally
        {
            File.Delete(path);
        }
    }

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
