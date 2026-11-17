using System.Text.Json;
using System.Reflection;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DifficultySettingsTests
{
    [Fact]
    public void Live_difficulty_comes_from_the_loaded_game_after_new_game_defaults_change()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-save-difficulty-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetGameplayPreferences(new() { Difficulty = BattleDifficulty.Hardcore });
            GameState saved = session.PrepareSnapshot();
            session.StartNewGame();
            session.SetGameplayPreferences(new() { Difficulty = BattleDifficulty.Normal });
            session.Restore(saved);
            using RecreateGame game = new(new LaunchOptions { GameDirectory = AppContext.BaseDirectory, MuteAudio = true, AchievementsLocalOnly = true, SettingsDirectory = directory }, session);
            UiPreferences defaults = new() { Difficulty = BattleDifficulty.Hard };
            typeof(RecreateGame).GetField("uiPreferences", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, defaults);
            UiPreferences current = (UiPreferences)typeof(RecreateGame).GetMethod("ReadCurrentUiPreferences", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, null)!;
            Assert.Equal(BattleDifficulty.Hardcore, current.Difficulty);
            Assert.All(GameplayPreferences.NewGameOptions.Where(option => option.RequiredOnHardcore && typeof(UiPreferences).GetProperty(option.PropertyName) is not null), option => Assert.True((bool)typeof(UiPreferences).GetProperty(option.PropertyName)!.GetValue(current)!));
            Assert.Throws<InvalidOperationException>(() => current.WithSetting("Difficulty", "Hard"));
            Assert.Equal(defaults, defaults.WithInstallationSetting("Battle ATB mode", "Normal", activeGame: true));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Triple Triad width", "2")]
    [InlineData("Triple Triad height", "2")]
    [InlineData("Triple Triad blocked cells", "0")]
    [InlineData("Triple Triad Random redraws", "3")]
    [InlineData("Triple Triad assistance", "Hand")]
    public void Editing_beyond_hardcore_limits_preserves_difficulty_and_enforces_its_limits(string key, string value)
    {
        UiPreferences hardcore = new UiPreferences().WithSetting("Difficulty", "Hardcore");
        UiPreferences changed = hardcore.WithSetting(key, value);
        Assert.Equal(BattleDifficulty.Hardcore, changed.Difficulty);
        changed.TripleTriad.Validate(BattleDifficulty.Hardcore);
    }

    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-combat")]
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
