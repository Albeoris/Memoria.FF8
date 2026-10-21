using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DifficultySettingsTests
{
    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-gameplay")]
    public void Difficulty_is_selectable_before_start_and_in_config(string screen)
    {
        MenuData data = new();
        data.Settings["Difficulty"] = "Normal";
        data.SupportedActions.Add("config:Difficulty");
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open(screen);
        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Id == "Difficulty");
        Assert.Contains("explicit do-not-attack", entry.Help);
        menu.Navigate(MenuInput.Right);
        MenuAction action = Assert.Single(actions);
        Assert.Equal("config", action.Kind);
        Assert.Equal("Difficulty", action.SubjectId);
        Assert.Equal("Hard", action.Value);
        Assert.DoesNotContain(actions, action => action.Kind == "new-game-start");
    }

    [Fact]
    public void Save_setting_does_not_replace_new_game_default_and_preferences_round_trip()
    {
        UiPreferences defaults = new();
        Assert.Equal(BattleDifficulty.Normal, defaults.Difficulty);
        Assert.Equal(defaults, defaults.WithInstallationSetting("Difficulty", "Hard", activeGame: true));
        UiPreferences hard = defaults.WithInstallationSetting("Difficulty", "Hard", activeGame: false);
        Assert.Equal(BattleDifficulty.Hard, hard.Difficulty);
        Assert.Equal(hard, JsonSerializer.Deserialize<UiPreferences>(JsonSerializer.Serialize(hard)));
        Assert.Equal(BattleDifficulty.Normal, hard.WithSetting("Difficulty", "Normal").Difficulty);
        Assert.Throws<ArgumentException>(() => defaults.WithSetting("Difficulty", "Impossible"));
        Assert.Throws<InvalidDataException>(() => (defaults with { Difficulty = (BattleDifficulty)99 }).CreateBattleSettingsInputs());
    }

    [Fact]
    public void Hard_save_requires_a_destination_that_preserves_difficulty()
    {
        Assert.False(PlayerSaveStore.RequiresMemoriaFormat(new GameState()));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { GameplayPreferences = new() { Difficulty = BattleDifficulty.Hard } }));
    }
}
