using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SelphieCardSlotsSettingsTests
{
    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-combat")]
    public void Option_is_selectable_before_start_and_in_config(string screen)
    {
        MenuData data = new();
        data.Settings["Selphie card Slots"] = "Off";
        data.SupportedActions.Add("config:Selphie card Slots");
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open(screen);
        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Id == "Selphie card Slots");
        Assert.Contains("native Slot results", entry.Help);
        menu.Navigate(MenuInput.Right);
        MenuAction action = Assert.Single(actions);
        Assert.Equal("config", action.Kind);
        Assert.Equal("Selphie card Slots", action.SubjectId);
        Assert.Equal("On", action.Value);
        Assert.DoesNotContain(actions, action => action.Kind == "new-game-start");
    }

    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-combat")]
    public void Option_is_locked_until_the_active_or_pending_battle_ends(string screen)
    {
        MenuData data = new() { SelphieCardSlotsPreferenceLocked = true };
        data.SupportedActions.Add("config:Selphie card Slots");
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open(screen);
        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Id == "Selphie card Slots");
        Assert.False(entry.Enabled);
        Assert.Contains("battle ends", entry.Help);
        menu.Navigate(MenuInput.Right);
        menu.Navigate(MenuInput.Left);
        menu.Navigate(MenuInput.Confirm);
        Assert.Empty(actions);
    }

    [Fact]
    public void Title_default_is_off_for_existing_preferences_and_round_trips_without_save_edits_replacing_it()
    {
        UiPreferences defaults = JsonSerializer.Deserialize<UiPreferences>("{}")!;
        Assert.False(defaults.SelphieCardSlots);
        Assert.Equal(defaults, defaults.WithInstallationSetting("Selphie card Slots", "On", activeGame: true));
        UiPreferences enabled = defaults.WithInstallationSetting("Selphie card Slots", "On", activeGame: false);
        Assert.True(enabled.SelphieCardSlots);
        Assert.Equal(enabled, JsonSerializer.Deserialize<UiPreferences>(JsonSerializer.Serialize(enabled)));
        Assert.False(enabled.WithSetting("Selphie card Slots", "Off").SelphieCardSlots);
        Assert.Throws<ArgumentException>(() => defaults.WithSetting("Selphie card Slots", "Always"));
    }

    [Fact]
    public void Enabled_save_requires_a_destination_that_preserves_the_rule()
    {
        Assert.False(PlayerSaveStore.RequiresMemoriaFormat(new GameState()));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { GameplayPreferences = new() { SelphieCardSlots = true } }));
    }
    [Theory]
    [InlineData(BattleDifficulty.Normal, false)]
    [InlineData(BattleDifficulty.Hard, false)]
    [InlineData(BattleDifficulty.Hardcore, true)]
    public void Title_defaults_follow_difficulty_and_hardcore_locks_the_rule(BattleDifficulty difficulty, bool enabled)
    {
        UiPreferences preferences = new UiPreferences().WithSetting("Difficulty", difficulty.ToString());
        Assert.Equal(enabled, preferences.SelphieCardSlots);
        if (difficulty == BattleDifficulty.Hardcore)
        {
            Assert.Throws<InvalidOperationException>(() => preferences.WithSetting("Selphie card Slots", "Off"));
        }
        else
        {
            Assert.True(preferences.WithSetting("Selphie card Slots", "On").SelphieCardSlots);
            Assert.False(preferences.WithSetting("Selphie card Slots", "On").WithSetting("Selphie card Slots", "Off").SelphieCardSlots);
        }
    }

    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-combat")]
    public void Hardcore_locks_the_setting_and_lists_it_in_confirmation(string screen)
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Hardcore";
        List<MenuAction> applied = [];
        MenuController menu = new(() => data, applied.Add);
        menu.Open(screen);
        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Id == "Selphie card Slots");
        Assert.False(entry.Enabled);
        Assert.Contains("Hardcore", entry.Help);
        Assert.Empty(applied);
    }

    [Fact]
    public void Combat_setting_follows_limit_rule()
    {
        string[] ids = SettingsPlacement.Pages["config-combat"].Items;
        Assert.Equal(Array.IndexOf(ids, "Limit once per turn") + 1, Array.IndexOf(ids, "Selphie card Slots"));
    }

}
