using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BorderlessWindowSettingsTests
{
    [Fact]
    public void Missing_window_mode_defaults_to_windowed()
    {
        Assert.False(new UiPreferences().BorderlessWindow);
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{}");
            Assert.False(UiPreferences.Load(path).BorderlessWindow);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Windowed", false)]
    [InlineData("Borderless", true)]
    public void Window_mode_is_global_and_round_trips_without_changing_window_dimensions(string mode, bool borderless)
    {
        UiPreferences preferences = new UiPreferences { WindowWidth = 1280, WindowHeight = 720 }.WithInstallationSetting("Window mode", mode, activeGame: true);
        Assert.Equal(borderless, preferences.BorderlessWindow);
        Assert.Equal(1280, preferences.WindowWidth);
        Assert.Equal(720, preferences.WindowHeight);
        string path = Path.GetTempFileName();
        try
        {
            preferences.Save(path);
            Assert.Equal(preferences, UiPreferences.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
        Assert.False(preferences.WithSetting("Window mode", "Windowed").BorderlessWindow);
        Assert.Throws<ArgumentException>(() => preferences.WithSetting("Window mode", "Invalid"));
    }

    [Fact]
    public void Graphics_menu_applies_window_mode_through_the_global_preference_owner()
    {
        UiPreferences preferences = new();
        MenuData data = new();
        data.Settings["Window mode"] = "Windowed";
        data.SupportedActions.Add("config:Window mode");
        List<MenuAction> actions = [];
        MenuController controller = new(() => data, action =>
        {
            actions.Add(action);
            preferences = preferences.WithInstallationSetting(action.SubjectId, action.Value, activeGame: true);
            data.Settings[action.SubjectId] = action.Value;
        });
        controller.Open("config-graphics");
        Assert.Equal("Window mode", Assert.Single(controller.Entries, entry => entry.Id == "Window mode").Id);
        controller.SelectEntry("config-graphics", "Window mode");
        controller.Navigate(MenuInput.Right);
        Assert.True(preferences.BorderlessWindow);
        Assert.Equal("Borderless", data.Settings["Window mode"]);
        Assert.Equal("Window mode", Assert.Single(actions).SubjectId);
        SettingPlacement placement = Assert.Single(SettingsPlacement.All, item => item.LegacyKey == "Window mode");
        Assert.Equal("config-graphics", placement.Page);
        Assert.Equal("Global", placement.Scope);
        Assert.Equal("UiPreferences.BorderlessWindow", placement.Target);
        Assert.Equal(["Windowed", "Borderless"], Assert.Single(MenuSettings.Definitions, item => item.Id == "Window mode").Values);
    }
}
