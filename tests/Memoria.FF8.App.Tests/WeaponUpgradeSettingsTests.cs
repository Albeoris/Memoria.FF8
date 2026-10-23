using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WeaponUpgradeSettingsTests
{
    [Theory]
    [InlineData("Weapon magazine required", "new-game-options")]
    [InlineData("Weapon upgrade order", "new-game-options")]
    [InlineData("Weapon magazine required", "config-exploration")]
    [InlineData("Weapon upgrade order", "config-exploration")]
    public void Optional_restrictions_are_available_in_both_editors(string key, string screen)
    {
        MenuData data = new();
        data.Settings[key] = "Off";
        data.SupportedActions.Add("config:" + key);
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open(screen);
        Assert.True(Assert.Single(menu.Entries, entry => entry.Id == key).Enabled);
        menu.Navigate(MenuInput.Right);
        Assert.Equal(new MenuAction("config", key, Value: "On"), Assert.Single(actions));
    }

    [Theory]
    [InlineData("Weapon magazine required")]
    [InlineData("Weapon upgrade order")]
    public void Defaults_are_off_and_save_edits_preserve_installation_defaults(string key)
    {
        UiPreferences defaults = JsonSerializer.Deserialize<UiPreferences>("{}")!;
        Assert.False(defaults.WeaponMagazineRequired);
        Assert.False(defaults.WeaponUpgradeOrder);
        Assert.Equal(defaults, defaults.WithInstallationSetting(key, "On", activeGame: true));
        UiPreferences enabled = defaults.WithInstallationSetting(key, "On", activeGame: false);
        Assert.True(key == "Weapon magazine required" ? enabled.WeaponMagazineRequired : enabled.WeaponUpgradeOrder);
        Assert.Equal(enabled, JsonSerializer.Deserialize<UiPreferences>(JsonSerializer.Serialize(enabled)));
        Assert.Equal(defaults, enabled.WithSetting(key, "Off"));
        Assert.Throws<ArgumentException>(() => defaults.WithSetting(key, "Invalid"));
    }

    [Fact]
    public void Restrictions_and_obtained_history_need_a_save_that_preserves_them()
    {
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { GameplayPreferences = new() { WeaponMagazineRequired = true } }));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { GameplayPreferences = new() { WeaponUpgradeOrder = true } }));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { WeaponRecipes = new(new string('A', 64), 0) { ObtainedWeaponsBits = 1 } }));
    }
}
