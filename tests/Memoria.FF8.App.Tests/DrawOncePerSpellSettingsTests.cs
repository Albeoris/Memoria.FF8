using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DrawOncePerSpellSettingsTests
{
    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-magic")]
    public void Option_is_selectable_before_start_and_in_config(string screen)
    {
        MenuData data = new();
        data.Settings["Draw once per spell"] = "Off";
        data.SupportedActions.Add("config:Draw once per spell");
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open(screen);
        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Id == "Draw once per spell");
        Assert.Contains("whole party", entry.Help);
        Assert.Contains("Failed Draw does not", entry.Help);
        Assert.Contains("outside battles only", entry.Help);
        menu.Navigate(MenuInput.Right);
        MenuAction action = Assert.Single(actions);
        Assert.Equal("config", action.Kind);
        Assert.Equal("Draw once per spell", action.SubjectId);
        Assert.Equal("On", action.Value);
        Assert.DoesNotContain(actions, action => action.Kind == "new-game-start");
    }

    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-magic")]
    public void Option_is_locked_until_the_active_or_pending_battle_ends(string screen)
    {
        MenuData data = new() { DrawOncePerSpellPreferenceLocked = true };
        data.SupportedActions.Add("config:Draw once per spell");
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open(screen);
        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Id == "Draw once per spell");
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
        Assert.False(defaults.DrawOncePerSpell);
        Assert.Equal(defaults, defaults.WithInstallationSetting("Draw once per spell", "On", activeGame: true));
        UiPreferences enabled = defaults.WithInstallationSetting("Draw once per spell", "On", activeGame: false);
        Assert.True(enabled.DrawOncePerSpell);
        Assert.Equal(enabled, JsonSerializer.Deserialize<UiPreferences>(JsonSerializer.Serialize(enabled)));
        Assert.False(enabled.WithSetting("Draw once per spell", "Off").DrawOncePerSpell);
        Assert.Throws<ArgumentException>(() => defaults.WithSetting("Draw once per spell", "Always"));
    }

    [Fact]
    public void Enabled_save_requires_a_destination_that_preserves_the_rule()
    {
        Assert.False(PlayerSaveStore.RequiresMemoriaFormat(new GameState()));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { GameplayPreferences = new() { DrawOncePerSpell = true } }));
    }
}
