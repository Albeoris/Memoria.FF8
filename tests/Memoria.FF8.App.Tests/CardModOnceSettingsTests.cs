using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CardModOnceSettingsTests
{
    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-exploration")]
    public void Option_is_selectable_before_start_and_in_config(string screen)
    {
        MenuData data = new();
        data.Settings["Card Mod once"] = "Off";
        data.SupportedActions.Add("config:Card Mod once");
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open(screen);
        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Id == "Card Mod once");
        Assert.Contains("without consuming", entry.Help);
        Assert.Contains("even while Off", entry.Help);
        menu.Navigate(MenuInput.Right);
        MenuAction action = Assert.Single(actions);
        Assert.Equal("config", action.Kind);
        Assert.Equal("Card Mod once", action.SubjectId);
        Assert.Equal("On", action.Value);
    }

    [Fact]
    public void Old_preferences_default_off_and_active_game_edits_preserve_title_default()
    {
        UiPreferences defaults = JsonSerializer.Deserialize<UiPreferences>("{}")!;
        Assert.False(defaults.CardModOnce);
        Assert.Equal(defaults, defaults.WithInstallationSetting("Card Mod once", "On", activeGame: true));
        UiPreferences enabled = defaults.WithInstallationSetting("Card Mod once", "On", activeGame: false);
        Assert.True(enabled.CardModOnce);
        Assert.Equal(enabled, JsonSerializer.Deserialize<UiPreferences>(JsonSerializer.Serialize(enabled)));
        Assert.False(enabled.WithSetting("Card Mod once", "Off").CardModOnce);
        Assert.Throws<ArgumentException>(() => defaults.WithSetting("Card Mod once", "Always"));
    }

    [Fact]
    public void History_requires_memoria_format_even_after_disabling_mode()
    {
        Assert.False(PlayerSaveStore.RequiresMemoriaFormat(new GameState()));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { GameplayPreferences = new() { CardModOnce = true } }));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { RefinedCardIds = [82] }));
    }

    [Fact]
    public void Native_card_quantity_help_describes_preservation_and_one_use()
    {
        MenuData data = new();
        data.Settings["Card Mod once"] = "On";
        data.Recipes.Add(new MenuRecipe("native-refine-115-0", "Card Mod", "card-0", 1, "item-1", "Item", 5) { NativeAbility = 115, NativeRecipeIndex = 0, NativeKind = 4, CardModOnce = true, IngredientName = "Card", MaximumBatches = new Dictionary<int, int> { [0] = 1 } });
        MenuController menu = new(() => data);
        menu.Open("ability");
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Confirm);
        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Enabled);
        Assert.Contains("Cards are kept", entry.Help);
        Assert.Contains("once per game", entry.Help);
    }
    [Fact]
    public void Exhausted_native_card_recipe_cannot_dispatch_a_conversion()
    {
        MenuData data = new();
        data.Recipes.Add(new MenuRecipe("native-refine-115-0", "Card Mod", "card-0", 1, "item-1", "Item", 5) { NativeAbility = 115, NativeRecipeIndex = 0, NativeKind = 4, CardModOnce = true, CardAlreadyRefined = true, MaximumBatches = new Dictionary<int, int> { [0] = 0 } });
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open("ability");
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Confirm);
        actions.Clear();
        Assert.All(menu.Entries, entry =>
        {
            Assert.False(entry.Enabled);
            Assert.Contains("Already refined", entry.Help);
        });
        menu.Navigate(MenuInput.Confirm);
        Assert.Empty(actions);
    }

}
