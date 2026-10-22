using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class UiPresentationPreferencesTests
{
    [Fact]
    public void Missing_preference_defaults_to_vector_and_classic_round_trips()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            Assert.Equal(MenuPresentation.Vector, UiPreferences.Load(path).MenuPresentation);
            File.WriteAllText(path, "{}");
            Assert.Equal(MenuPresentation.Vector, UiPreferences.Load(path).MenuPresentation);
            UiPreferences preferences = UiPreferences.Load(path).WithInstallationSetting("UI presentation", "Classic", activeGame: true);
            preferences.Save(path);
            Assert.Equal(MenuPresentation.Classic, UiPreferences.Load(path).MenuPresentation);
            Assert.Equal(MenuPresentation.Vector, preferences.WithSetting("UI presentation", "Vector").MenuPresentation);
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("UI presentation", "Unknown"));
            File.WriteAllText(path, "{\"MenuPresentation\":99}");
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Live_interface_setting_dispatches_to_host_without_reopening_the_screen()
    {
        MenuData data = new();
        data.Settings["UI presentation"] = "Vector";
        data.SupportedActions.Add("config:UI presentation");
        MenuAction? applied = null;
        MenuController menus = new(() => data, action => applied = action);
        menus.Open("config-interface", false);
        MenuEntry entry = Assert.Single(menus.Entries);
        Assert.Equal("UI presentation", entry.Id);
        menus.Execute(entry.Action!);
        Assert.Equal(new MenuAction("config", "UI presentation", Value: "Classic"), applied);
        Assert.Equal("config-interface", menus.ScreenId);
        Assert.True(menus.IsOpen);
    }
}
