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
    public void Frame_counter_is_owned_by_graphics_and_immediately_updates_the_host()
    {
        UiPreferences preferences = new();
        MenuData data = new();
        data.Settings["Show FPS"] = "Off";
        data.SupportedActions.Add("config:Show FPS");
        MenuController menus = new(() => data, action =>
        {
            preferences = preferences.WithSetting(action.SubjectId, action.Value);
            data.Settings[action.SubjectId] = action.Value;
        });
        menus.Open("config-graphics");
        MenuEntry toggle = Assert.Single(menus.Entries, entry => entry.Id == "Show FPS");
        menus.Execute(toggle.Action!);
        Assert.True(preferences.ShowFps);
        menus.Execute(menus.Entries.Single(entry => entry.Id == "Show FPS").Action!);
        Assert.False(preferences.ShowFps);
        menus.Open("config-frame-limits");
        Assert.DoesNotContain(menus.Entries, entry => entry.Id == "Show FPS");
        menus.Open("debug-gallery", true);
        Assert.DoesNotContain(menus.Entries, entry => entry.Action?.SubjectId == "Show FPS");
    }
}
