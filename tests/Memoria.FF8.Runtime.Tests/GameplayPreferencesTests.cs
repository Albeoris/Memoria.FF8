using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplayPreferencesTests
{
    [Fact]
    public void Saved_choices_round_trip_without_crossing_save_owners_and_new_game_resets_them()
    {
        string path = Path.Combine(Path.GetTempPath(), "ff8-gameplay-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            GameSession first = GameSession.CreateColdProcess();
            first.StartNewGame();
            first.SetGameplayPreferences(new GameplayPreferences { AtbMode = BattleAtbMode.TurnBased, GunbladeMode = BattleGunbladeMode.Manual });
            first.Save(path);
            GameSession second = GameSession.CreateColdProcess();
            second.StartNewGame();
            Assert.Equal(BattleAtbMode.Normal, second.State.GameplayPreferences.AtbMode);
            second.Restore(GameSession.Load(path));
            Assert.Equal(first.State.GameplayPreferences, second.State.GameplayPreferences);
            second.SetGameplayPreferences(new GameplayPreferences());
            Assert.Equal(BattleAtbMode.TurnBased, GameSession.Load(path).GameplayPreferences.AtbMode);
            first.StartNewGame();
            Assert.Equal(new GameplayPreferences(), first.State.GameplayPreferences);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Existing_checkpoint_without_gameplay_owner_defaults_to_classic_without_native_changes()
    {
        string path = Path.Combine(Path.GetTempPath(), "ff8-legacy-gameplay-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.Save(path);
            JsonObject original = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            original.Remove(nameof(GameState.GameplayPreferences));
            File.WriteAllText(path, original.ToJsonString());
            GameState loaded = GameSession.Load(path);
            Assert.Equal(BattleAtbMode.Normal, loaded.GameplayPreferences.AtbMode);
            Assert.Equal(BattleGunbladeMode.Automatic, loaded.GameplayPreferences.GunbladeMode);
            Assert.Equal(session.State.InputConfiguration, loaded.InputConfiguration);
            Assert.Equal(session.State.LimitProgress, loaded.LimitProgress);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Invalid_gameplay_choices_are_rejected_before_state_changes()
    {
        GameSession session = GameSession.CreateColdProcess();
        Assert.Throws<InvalidDataException>(() => session.SetGameplayPreferences(new GameplayPreferences { AtbMode = (BattleAtbMode)255 }));
        Assert.Throws<InvalidDataException>(() => new GameSession(session.State with { GameplayPreferences = new GameplayPreferences { GunbladeMode = (BattleGunbladeMode)255 } }));
        Assert.Equal(new GameplayPreferences(), session.State.GameplayPreferences);
    }
}
