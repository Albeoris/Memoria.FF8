using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadSettingsTests
{
    [Fact]
    public void Existing_preferences_default_to_original_board_and_hand()
    {
        GameplayPreferences preferences = JsonSerializer.Deserialize<GameplayPreferences>("{}")!;
        Assert.Equal(new TripleTriadSettings(), preferences.TripleTriad);
        Assert.Equal(3, preferences.TripleTriad.Width);
        Assert.Equal(3, preferences.TripleTriad.Height);
        Assert.Equal(5, preferences.TripleTriad.HandSize);
        preferences.Validate();
    }

    [Fact]
    public void Save_owned_settings_round_trip_and_new_game_resets_them()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        TripleTriadSettings settings = new() { Width = 5, Height = 4, HandSize = 50, RandomRedraws = 3, Assistance = TripleTriadAssistance.Hand };
        session.SetGameplayPreferences(session.State.GameplayPreferences with { TripleTriad = settings });
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            session.Save(path);
            Assert.Equal(settings, GameSession.Load(path).GameplayPreferences.TripleTriad);
            session.StartNewGame();
            Assert.Equal(new TripleTriadSettings(), session.State.GameplayPreferences.TripleTriad);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(2, 2, 2)]
    [InlineData(3, 3, 5)]
    [InlineData(4, 5, 10)]
    [InlineData(5, 5, 13)]
    public void Hand_minimum_tracks_board_capacity(int width, int height, int minimum)
    {
        TripleTriadSettings settings = new() { Width = width, Height = height, HandSize = 2 };
        Assert.Equal(minimum, settings.MinimumHandSize);
        Assert.Equal(minimum, settings.NormalizeForDifficulty(BattleDifficulty.Normal).HandSize);
        if (minimum > 2)
        {
            Assert.Throws<InvalidDataException>(() => settings.Validate());
        }
    }

    [Fact]
    public void Hardcore_enforces_board_hand_redraw_and_assistance_limits()
    {
        GameplayPreferences preferences = new GameplayPreferences { Difficulty = BattleDifficulty.Hardcore, TripleTriad = new() { RandomRedraws = 3, Assistance = TripleTriadAssistance.Hand } }.NormalizeForDifficulty();
        Assert.Equal(4, preferences.TripleTriad.Width);
        Assert.Equal(4, preferences.TripleTriad.Height);
        Assert.Equal(8, preferences.TripleTriad.HandSize);
        Assert.Equal(1, preferences.TripleTriad.RandomRedraws);
        Assert.Equal(TripleTriadAssistance.Direct, preferences.TripleTriad.Assistance);
        preferences.Validate();
        Assert.Throws<InvalidDataException>(() => (preferences with { TripleTriad = new() }).Validate());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_hardcore_snapshot_completes_only_missing_triad_settings(bool pending)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        GameplayPreferences hardcore = new GameplayPreferences { Difficulty = BattleDifficulty.Hardcore }.NormalizeForDifficulty();
        session.SetGameplayPreferences(hardcore);
        System.Text.Json.Nodes.JsonObject saved = JsonSerializer.SerializeToNode(session.State)!.AsObject();
        saved[nameof(GameState.GameplayPreferences)]!.AsObject().Remove(nameof(GameplayPreferences.TripleTriad));
        if (pending)
        {
            saved[nameof(GameState.PendingGameplayPreferences)] = JsonSerializer.SerializeToNode(hardcore);
            saved[nameof(GameState.PendingGameplayPreferences)]!.AsObject().Remove(nameof(GameplayPreferences.TripleTriad));
        }
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, saved.ToJsonString());
            GameState loaded = GameSession.Load(path);
            Assert.Equal(hardcore.TripleTriad, loaded.GameplayPreferences.TripleTriad);
            if (pending)
            {
                Assert.Equal(hardcore.TripleTriad, loaded.PendingGameplayPreferences!.TripleTriad);
            }
            saved[nameof(GameState.GameplayPreferences)]![nameof(GameplayPreferences.TripleTriad)] = JsonSerializer.SerializeToNode(new TripleTriadSettings());
            File.WriteAllText(path, saved.ToJsonString());
            Assert.Throws<InvalidDataException>(() => GameSession.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void New_game_options_remain_exactly_boolean_gameplay_properties()
    {
        Assert.Equal(typeof(GameplayPreferences).GetProperties().Where(property => property.PropertyType == typeof(bool)).Select(property => property.Name).Order(), GameplayPreferences.NewGameOptions.Select(option => option.PropertyName).Order());
        Assert.DoesNotContain(GameplayPreferences.NewGameOptions, option => option.PropertyName == nameof(GameplayPreferences.TripleTriad));
    }

    [Fact]
    public void Unsupported_dimensions_redraws_and_assistance_fail_validation()
    {
        foreach (TripleTriadSettings settings in new[]
        {
            new TripleTriadSettings { Width = 1 },
            new TripleTriadSettings { Height = 6 },
            new TripleTriadSettings { HandSize = 51 },
            new TripleTriadSettings { RandomRedraws = 4 },
            new TripleTriadSettings { Assistance = (TripleTriadAssistance)5 }
        })
        {
            Assert.Throws<InvalidDataException>(() => settings.Validate());
        }
    }
}
