using System.Globalization;
using System.Text.Json;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class UiPresentationPreferencesTests
{
    [Fact]
    public void Missing_battle_fps_defaults_to_60()
    {
        Assert.Equal(60, new UiPreferences().BattleFps);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            Assert.Equal(60, UiPreferences.Load(path).BattleFps);
            File.WriteAllText(path, "{}");
            Assert.Equal(60, UiPreferences.Load(path).BattleFps);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(15)]
    [InlineData(60)]
    public void Battle_fps_applies_during_a_game_and_round_trips(int frameLimit)
    {
        UiPreferences preferences = new UiPreferences().WithInstallationSetting("Battle FPS", frameLimit.ToString(CultureInfo.InvariantCulture), activeGame: true);
        Assert.Equal(frameLimit, preferences.BattleFps);
        string path = Path.GetTempFileName();
        try
        {
            preferences.Save(path);
            Assert.Equal(preferences, UiPreferences.Load(path));
            using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(frameLimit, saved.RootElement.GetProperty("BattleFps").GetInt32());
            Assert.False(saved.RootElement.TryGetProperty("BattleMotion", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(15, 15)]
    [InlineData(30, 60)]
    [InlineData(60, 60)]
    [InlineData(90, 60)]
    [InlineData(120, 60)]
    [InlineData(144, 60)]
    [InlineData(240, 60)]
    public void Legacy_battle_limits_normalize_and_obsolete_motion_state_is_removed(int storedLimit, int expectedLimit)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { BattleFps = storedLimit, BattleMotion = 99, MusicVolume = 25 }));
            UiPreferences loaded = UiPreferences.Load(path);
            Assert.Equal(new UiPreferences { BattleFps = expectedLimit, MusicVolume = 25 }, loaded);
            loaded.Save(path);
            Assert.Equal(loaded, UiPreferences.Load(path));
            using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(expectedLimit, saved.RootElement.GetProperty("BattleFps").GetInt32());
            Assert.False(saved.RootElement.TryGetProperty("BattleMotion", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(55)]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    public void Selecting_and_saving_battle_fps_requires_15_or_60(int frameLimit)
    {
        UiPreferences preferences = new();
        Assert.Throws<ArgumentException>(() => preferences.WithSetting("Battle FPS", frameLimit.ToString(CultureInfo.InvariantCulture)));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        Assert.Throws<InvalidDataException>(() => (preferences with { BattleFps = frameLimit }).Save(path));
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("Unlimited")]
    [InlineData("Unknown")]
    [InlineData("99999999999999999999")]
    public void Battle_fps_rejects_non_numeric_or_unlimited_selections(string value)
    {
        Assert.Throws<ArgumentException>(() => new UiPreferences().WithSetting("Battle FPS", value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(55)]
    [InlineData(300)]
    public void Unknown_stored_battle_limits_remain_invalid(int frameLimit)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { BattleFps = frameLimit }));
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Battle_presentation_has_one_control_with_only_15_and_60_choices()
    {
        MenuSetting setting = Assert.Single(MenuSettings.Definitions, setting => setting.Id == "Battle FPS");
        Assert.Equal(["15", "60"], setting.Values);
        Assert.DoesNotContain(MenuSettings.Definitions, setting => setting.Id == "Battle motion");
        Assert.Single(SettingsPlacement.All, placement => placement.LegacyKey == "Battle FPS");
        Assert.DoesNotContain(SettingsPlacement.All, placement => placement.LegacyKey == "Battle motion");
        Assert.DoesNotContain(SettingsPlacement.Pages.Values, page => page.Items.Contains("Battle motion"));
        MenuData data = new();
        data.Settings["Battle FPS"] = "60";
        data.SupportedActions.Add("config:Battle FPS");
        List<MenuAction> applied = [];
        MenuController menus = new(() => data, action =>
        {
            applied.Add(action);
            data.Settings[action.SubjectId] = action.Value;
        });
        menus.Open("config-frame-limits");
        Assert.Equal("Battle FPS", Assert.Single(menus.Entries).Id);
        menus.Navigate(MenuInput.Left);
        Assert.Equal(new MenuAction("config", "Battle FPS", Value: "15"), Assert.Single(applied));
        menus.Navigate(MenuInput.Right);
        Assert.Equal(new MenuAction("config", "Battle FPS", Value: "60"), applied[1]);
        Assert.Equal("config-frame-limits", menus.ScreenId);
        Assert.True(menus.IsOpen);
    }

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
        MenuEntry entry = Assert.Single(menus.Entries, entry => entry.Id == "UI presentation");
        Assert.Equal("UI presentation", entry.Id);
        menus.Execute(entry.Action!);
        Assert.Equal(new MenuAction("config", "UI presentation", Value: "Classic"), applied);
        Assert.Equal("config-interface", menus.ScreenId);
        Assert.True(menus.IsOpen);
    }
}
