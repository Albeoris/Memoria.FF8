using System.Globalization;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class SpeedSettingsTests
{
    [Fact]
    public void Missing_speed_preferences_keep_native_speed_and_triple_fast_forward()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{}");
            Assert.Equal(new UiPreferences(), UiPreferences.Load(path));
            Assert.Equal(100, UiPreferences.Load(path).WorldMovementSpeedPercent);
            Assert.Equal(100, UiPreferences.Load(path).FieldMovementSpeedPercent);
            Assert.Equal(100, UiPreferences.Load(path).BattleSpeedPercent);
            Assert.Equal(3, UiPreferences.Load(path).FastForwardMultiplier);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("World movement speed")]
    [InlineData("Field movement speed")]
    [InlineData("Battle speed")]
    public void Movement_and_battle_speed_support_percent_ranges_without_changing_other_settings(string key)
    {
        UiPreferences original = new();
        foreach (int percent in new[] { 50, 100, 150, 300 })
        {
            UiPreferences configured = original.WithSetting(key, percent.ToString(CultureInfo.InvariantCulture) + "%");
            Assert.Equal(original, configured.WithSetting(key, "100%"));
            Assert.Equal(percent, key switch
            {
                "World movement speed" => configured.WorldMovementSpeedPercent,
                "Field movement speed" => configured.FieldMovementSpeedPercent,
                _ => configured.BattleSpeedPercent
            });
        }
        foreach (string value in new[] { "49%", "301%", "invalid", "999999999999999999999" })
        {
            Assert.Throws<ArgumentException>(() => original.WithSetting(key, value));
        }
        MenuSetting setting = Assert.Single(MenuSettings.Definitions, setting => setting.Id == key);
        Assert.Equal(Enumerable.Range(5, 26).Select(value => (value * 10).ToString(CultureInfo.InvariantCulture) + "%"), setting.Values);
    }

    [Fact]
    public void Multiplier_range_is_independent_of_scene_speed()
    {
        UiPreferences preferences = new() { BattleSpeedPercent = 200 };
        for (int multiplier = 2; multiplier <= 10; multiplier++)
        {
            Assert.Equal(preferences with { FastForwardMultiplier = multiplier }, preferences.WithSetting("Fast-forward multiplier", multiplier.ToString(CultureInfo.InvariantCulture)));
        }
        foreach (string value in new[] { "1", "11", "3x", "invalid" })
        {
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("Fast-forward multiplier", value));
        }
    }

    [Fact]
    public void Persisted_speeds_clamp_to_supported_ranges_and_round_trip()
    {
        UiPreferences preferences = new() { WorldMovementSpeedPercent = 0, FieldMovementSpeedPercent = 1000, BattleSpeedPercent = 149, FastForwardMultiplier = 0 };
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(preferences));
            UiPreferences loaded = UiPreferences.Load(path);
            Assert.Equal(preferences with { WorldMovementSpeedPercent = 50, FieldMovementSpeedPercent = 300, FastForwardMultiplier = 2 }, loaded);
            loaded.Save(path);
            Assert.Equal(loaded, UiPreferences.Load(path));
            File.WriteAllText(path, JsonSerializer.Serialize(preferences with { FastForwardMultiplier = 100 }));
            Assert.Equal(10, UiPreferences.Load(path).FastForwardMultiplier);
            Assert.Throws<InvalidDataException>(() => preferences.Save(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Speed_has_a_global_gameplay_page_with_percentage_and_multiplier_sliders()
    {
        string[] keys = ["World movement speed", "Field movement speed", "Battle speed", "Fast-forward multiplier"];
        Assert.Contains("config-speed", SettingsPlacement.Pages["config-gameplay"].Items);
        Assert.Equal(keys, SettingsPlacement.Pages["config-speed"].Items);
        Assert.All(SettingsPlacement.All.Where(placement => keys.Contains(placement.LegacyKey)), placement =>
        {
            Assert.Equal(SettingsAudience.Player, placement.Audience);
            Assert.Equal("Global", placement.Scope);
            Assert.Equal("config-speed", placement.Page);
        });
        MenuController controller = new();
        controller.Open("config-speed", demo: true);
        Assert.Equal(keys, controller.Entries.Select(entry => entry.Id));
        Assert.All(keys.Take(3), key => Assert.Equal("100%", controller.Data.Settings[key]));
        Assert.Equal("3", controller.Data.Settings["Fast-forward multiplier"]);
        UiNumericSlider[] sliders = Descendants(controller.BuildView().Root).OfType<UiNumericSlider>().ToArray();
        Assert.Equal(4, sliders.Length);
        Assert.All(sliders.Take(3), slider =>
        {
            Assert.Equal(50, slider.Min);
            Assert.Equal(300, slider.Max);
            Assert.Equal(10, slider.Step);
            Assert.Equal("0'%'", slider.ValueFormat);
        });
        Assert.Equal("0'x'", sliders[3].ValueFormat);
        Assert.Equal(2, sliders[3].Min);
        Assert.Equal(10, sliders[3].Max);
        Assert.Equal(1, sliders[3].Step);
        controller.Navigate(MenuInput.Right);
        Assert.Equal("110%", controller.Data.Settings[keys[0]]);
        Assert.Contains("F1", controller.Entries[0].Help);
        Assert.Contains("sound effects", controller.Entries[2].Help);
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
