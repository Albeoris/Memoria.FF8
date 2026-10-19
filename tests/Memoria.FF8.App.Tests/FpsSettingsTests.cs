using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class FpsSettingsTests
{
    [Fact]
    public void ExistingPreferencesDefaultOffAndTogglePersists()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{}");
            Assert.False(UiPreferences.Load(path).ShowFps);
            UiPreferences.Load(path).WithSetting("Show FPS", "On").Save(path);
            Assert.True(UiPreferences.Load(path).ShowFps);
            UiPreferences.Load(path).WithSetting("Show FPS", "Off").Save(path);
            Assert.False(UiPreferences.Load(path).ShowFps);
            Assert.Throws<ArgumentException>(() => new UiPreferences().WithSetting("Show FPS", "Sometimes"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void F2UiToggleImmediatelyUpdatesHostAndDisplayConfigContainsTheSetting()
    {
        UiPreferences preferences = new();
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.Open("debug-ui", true);
        MenuEntry toggle = menus.Entries.Single(entry => entry.Id == "Show FPS");
        Assert.Contains("Off", toggle.Label);
        menus.Execute(toggle.Action!);
        Assert.True(preferences.ShowFps);
        toggle = menus.Entries.Single(entry => entry.Id == "Show FPS");
        Assert.Contains("On", toggle.Label);
        menus.Execute(toggle.Action!);
        Assert.False(preferences.ShowFps);
        menus.Open("config-display", true);
        Assert.Contains(menus.Entries, entry => entry.Id == "Show FPS");
        menus.Open("config-gameplay", true);
        Assert.DoesNotContain(menus.Entries, entry => entry.Id == "Show FPS");
    }
}
