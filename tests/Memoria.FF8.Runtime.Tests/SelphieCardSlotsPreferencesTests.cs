using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SelphieCardSlotsPreferencesTests
{
    [Theory]
    [InlineData(BattleDifficulty.Normal, false)]
    [InlineData(BattleDifficulty.Hard, false)]
    [InlineData(BattleDifficulty.Hardcore, true)]
    public void New_game_defaults_follow_difficulty_and_round_trip(BattleDifficulty difficulty, bool enabled)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetGameplayPreferences(new() { Difficulty = difficulty });
        Assert.Equal(enabled, session.State.GameplayPreferences.SelphieCardSlots);
        GameplayPreferences restored = JsonSerializer.Deserialize<GameplayPreferences>(JsonSerializer.Serialize(session.State.GameplayPreferences))!;
        restored.Validate();
        Assert.Equal(session.State.GameplayPreferences, restored);
        if (difficulty == BattleDifficulty.Hardcore)
        {
            Assert.Throws<InvalidDataException>(() => (restored with { SelphieCardSlots = false }).Validate());
            Assert.Throws<InvalidOperationException>(() => session.SetGameplayPreferences(restored with { SelphieCardSlots = false }));
        }
    }

    [Theory]
    [InlineData(BattleDifficulty.Normal, false)]
    [InlineData(BattleDifficulty.Hard, false)]
    [InlineData(BattleDifficulty.Hardcore, true)]
    public void Missing_legacy_rule_is_completed_before_validation_for_current_and_pending_owners(BattleDifficulty difficulty, bool enabled)
    {
        string path = Path.Combine(Path.GetTempPath(), "ff8-slots-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetGameplayPreferences(new() { Difficulty = difficulty });
            session.Save(path);
            JsonObject saved = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            JsonObject preferences = saved["GameplayPreferences"]!.AsObject();
            preferences.Remove("SelphieCardSlots");
            saved["PendingGameplayPreferences"] = preferences.DeepClone();
            File.WriteAllText(path, saved.ToJsonString());
            GameState loaded = GameSession.Load(path);
            Assert.Equal(enabled, loaded.GameplayPreferences.SelphieCardSlots);
            Assert.Equal(enabled, loaded.PendingGameplayPreferences!.SelphieCardSlots);
            _ = new GameSession(loaded);

            preferences["SelphieCardSlots"] = false;
            saved["PendingGameplayPreferences"]!["SelphieCardSlots"] = false;
            File.WriteAllText(path, saved.ToJsonString());
            if (difficulty == BattleDifficulty.Hardcore)
            {
                Assert.Throws<InvalidDataException>(() => new GameSession(GameSession.Load(path)));
            }
            else
            {
                Assert.False(new GameSession(GameSession.Load(path)).State.GameplayPreferences.SelphieCardSlots);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Enabled_normal_save_round_trips_and_does_not_leak_to_another_save()
    {
        string path = Path.Combine(Path.GetTempPath(), "ff8-slots-enabled-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            GameSession first = GameSession.CreateColdProcess();
            first.StartNewGame();
            first.SetGameplayPreferences(new() { SelphieCardSlots = true });
            first.Save(path);
            GameSession second = GameSession.CreateColdProcess();
            second.StartNewGame();
            Assert.False(second.State.GameplayPreferences.SelphieCardSlots);
            second.Restore(GameSession.Load(path));
            Assert.True(second.State.GameplayPreferences.SelphieCardSlots);
            second.SetGameplayPreferences(second.State.GameplayPreferences with { SelphieCardSlots = false });
            Assert.True(GameSession.Load(path).GameplayPreferences.SelphieCardSlots);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rule_cannot_change_during_active_or_pending_battle(bool enabled)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        GameplayPreferences preferences = new() { SelphieCardSlots = enabled };
        session.SetGameplayPreferences(preferences);
        BattleSession battle = new(new BattleDefinition
        {
            SelphieCardSlots = enabled,
            Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        session.SetBattle(battle.Capture());
        GameState before = session.State;
        GameplayPreferences changed = preferences with { SelphieCardSlots = !enabled };
        Assert.Throws<InvalidOperationException>(() => session.SetGameplayPreferences(changed));
        Assert.Throws<InvalidOperationException>(() => session.ApplyGameplayPreferences(new(MagicSystemFixture.Create(), changed, [], [], [], [])));
        Assert.Same(before, session.State);
        Assert.Throws<InvalidDataException>(() => new GameSession(before with { GameplayPreferences = changed }));
        Assert.Throws<InvalidDataException>(() => new GameSession(before with { PendingGameplayPreferences = changed }));
        string hash = new('0', 64);
        BattleEncounterOccurrence pending = new("slots-rule-boundary", 1, hash, SceneKind.Field, 74, 1, hash, null);
        GameSession pendingSession = new(before with { Battle = null, PendingBattleEncounter = pending });
        Assert.Throws<InvalidOperationException>(() => pendingSession.SetGameplayPreferences(changed));
        Assert.Throws<InvalidDataException>(() => new GameSession(pendingSession.State with { PendingGameplayPreferences = changed }));
        session.SetBattle(null);
        session.SetGameplayPreferences(changed);
        Assert.Equal(!enabled, session.State.GameplayPreferences.SelphieCardSlots);
        session.StartNewGame();
        Assert.False(session.State.GameplayPreferences.SelphieCardSlots);
    }
}
