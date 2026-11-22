using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class AutoSaveSettingsTests
{
    [Fact]
    public void Existing_preferences_without_autosave_fields_use_defaults()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"QuickSaveCount\": 6}");
            UiPreferences preferences = UiPreferences.Load(path);

            Assert.True(preferences.AutoSaveEnabled);
            Assert.Equal(3, preferences.AutoSaveCount);
            Assert.True(preferences.AutoSaveOnLocationEntry);
            Assert.Equal(10, preferences.AutoSaveIntervalMinutes);
            Assert.Equal(6, preferences.QuickSaveCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Exploration_config_edits_and_persists_autosave_preferences()
    {
        string path = Path.GetTempFileName();
        try
        {
            UiPreferences preferences = new() { AutoSaveIntervalMinutes = 1 };
            MenuController controller = null!;
            controller = new(() =>
            {
                MenuData data = new();
                data.Settings["Quick saves"] = "9";
                data.SupportedActions.Add("config:Quick saves");
                controller.PopulateHostSettingValues(data, UiPreferences.AutoSaveSettingKeys);
                return data;
            }, action =>
            {
                preferences = preferences.WithInstallationSetting(action.SubjectId, action.Value, activeGame: true);
                preferences.Save(path);
            });
            controller.ConnectPreferences(() => preferences);
            controller.Open("config-exploration");

            Assert.Equal(["Quick saves", "Autosaves", "Autosaves to keep", "Autosave on location entry", "Autosave interval"], controller.Entries.Select(entry => entry.Id));
            controller.SelectEntry("config-exploration", "Autosaves");
            controller.Navigate(MenuInput.Confirm);
            controller.SelectEntry("config-exploration", "Autosaves to keep");
            controller.Navigate(MenuInput.Right);
            controller.SelectEntry("config-exploration", "Autosave on location entry");
            controller.Navigate(MenuInput.Confirm);
            controller.SelectEntry("config-exploration", "Autosave interval");
            controller.Navigate(MenuInput.Left);
            Assert.Equal("0", controller.Data.Settings["Autosave interval"]);

            UiPreferences saved = UiPreferences.Load(path);
            Assert.False(saved.AutoSaveEnabled);
            Assert.Equal(4, saved.AutoSaveCount);
            Assert.False(saved.AutoSaveOnLocationEntry);
            Assert.Equal(0, saved.AutoSaveIntervalMinutes);
            Assert.Equal(9, saved.QuickSaveCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Autosaves", "true")]
    [InlineData("Autosaves to keep", "0")]
    [InlineData("Autosaves to keep", "1000")]
    [InlineData("Autosave on location entry", "false")]
    [InlineData("Autosave interval", "-1")]
    [InlineData("Autosave interval", "1000")]
    public void Settings_reject_values_outside_the_config_domain(string key, string value)
    {
        Assert.Throws<ArgumentException>(() => new UiPreferences().WithSetting(key, value));
    }
}
