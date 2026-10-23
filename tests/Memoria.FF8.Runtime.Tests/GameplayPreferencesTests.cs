using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplayPreferencesTests
{
    [Fact]
    public void World_checkpoint_with_departed_field_transition_allows_preferences_and_round_trips()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("gameplay-preferences-world-transition")));
        GameState before = session.State;
        Assert.Equal(SceneKind.WorldMap, before.Scene);
        Assert.True(before.EventState!.Value.GetProperty("TransitionRequested").GetBoolean());
        Assert.Null(session.MagicMutationUnavailableReason);
        GameplayPreferences changed = before.GameplayPreferences with { LimitOncePerTurn = !before.GameplayPreferences.LimitOncePerTurn };
        session.SetGameplayPreferences(changed);
        Assert.Same(before.CharacterVitals, session.State.CharacterVitals);
        Assert.Equal(before.EventState, session.State.EventState);
        Assert.Equal(before.World, session.State.World);
        string path = TestResources.ArtifactFile("gameplay-preferences-world.json");
        session.Save(path, compact: true);
        GameSession restored = new(GameSession.Load(path));
        Assert.Equal(changed, restored.State.GameplayPreferences);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.CharacterVitals), JsonSerializer.SerializeToElement(restored.State.CharacterVitals)));
        Assert.True(JsonElement.DeepEquals(before.EventState.Value, restored.State.EventState!.Value));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.World), JsonSerializer.SerializeToElement(restored.State.World)));
        restored.SetGameplayPreferences(changed with { SmartAttack = !changed.SmartAttack });
        Assert.Equal(!changed.SmartAttack, restored.State.CharacterVitals!.MagicSystem.SmartAttack);
    }

    [Theory]
    [InlineData("{\"TransitionRequested\":true}")]
    [InlineData("{\"TransitionRequested\":true}", true)]
    [InlineData("{\"PartyJoin\":{}}")]
    [InlineData("{\"PartySplit\":{}}")]
    [InlineData("{\"Scene\":{\"NativeTutorial\":{}}}")]
    [InlineData("{\"Scene\":{\"NativeMenuTutorial\":{}}}")]
    public void Non_magic_preferences_preserve_party_owners_during_scripted_boundaries(string checkpoint, bool magicEnabled = false)
    {
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        GameplayPreferences preferences = new() { SharedMagicStock = magicEnabled, InfiniteMagic = magicEnabled, SmartAttack = magicEnabled };
        NativeCharacterVitals vitals = MagicModeCoordinator.Preview(MagicSystemFixture.Create(), preferences).Vitals;
        JsonObject events = JsonNode.Parse(checkpoint)!.AsObject();
        events["CharacterVitals"] = JsonSerializer.SerializeToNode(vitals);
        events["EventMemory"] = JsonSerializer.SerializeToNode(initial.State.EventMemory);
        GameSession session = new(initial.State with { CharacterVitals = vitals, GameplayPreferences = preferences, EventState = JsonSerializer.SerializeToElement(events) });
        Assert.NotNull(session.MagicMutationUnavailableReason);
        GameState before = session.State;
        GameplayPreferences changed = before.GameplayPreferences with
        {
            LimitOncePerTurn = true,
            GuardianFormsEnabled = true,
            Difficulty = BattleDifficulty.Hard,
            AtbMode = BattleAtbMode.Normal,
            GunbladeMode = BattleGunbladeMode.Manual
        };

        session.SetGameplayPreferences(changed);
        Assert.Equal(changed, session.State.GameplayPreferences);
        Assert.Same(before.CharacterVitals, session.State.CharacterVitals);
        Assert.Equal(before.EventState, session.State.EventState);
        Assert.Equal(before.DepartingFieldState, session.State.DepartingFieldState);
        Assert.Equal(before.SavedBattleFlags, session.State.SavedBattleFlags);

        session.ApplyGameplayPreferences(session.PreviewGameplayPreferences(before.GameplayPreferences));
        Assert.Equal(before.GameplayPreferences, session.State.GameplayPreferences);
        Assert.Same(before.CharacterVitals, session.State.CharacterVitals);
        Assert.Equal(before.EventState, session.State.EventState);

        foreach (GameplayPreferences magicChange in new[]
        {
            before.GameplayPreferences with { SharedMagicStock = !magicEnabled },
            before.GameplayPreferences with { InfiniteMagic = !magicEnabled },
            before.GameplayPreferences with { SmartAttack = !magicEnabled }
        })
        {
            GameState unchanged = session.State;
            Assert.Throws<InvalidOperationException>(() => session.SetGameplayPreferences(magicChange));
            Assert.Throws<InvalidOperationException>(() => session.ApplyGameplayPreferences(session.PreviewGameplayPreferences(magicChange)));
            Assert.Same(unchanged, session.State);
        }
    }

    [Fact]
    public void Saved_choices_round_trip_without_crossing_save_owners_and_new_game_resets_them()
    {
        string path = Path.Combine(Path.GetTempPath(), "ff8-gameplay-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            GameSession first = GameSession.CreateColdProcess();
            first.StartNewGame();
            first.SetGameplayPreferences(new GameplayPreferences { Difficulty = BattleDifficulty.Hard, AtbMode = BattleAtbMode.Normal, GunbladeMode = BattleGunbladeMode.Manual, GuardianFormsEnabled = true });
            first.Save(path);
            GameSession second = GameSession.CreateColdProcess();
            second.StartNewGame();
            Assert.Equal(BattleAtbMode.TurnBased, second.State.GameplayPreferences.AtbMode);
            Assert.False(second.State.GameplayPreferences.GuardianFormsEnabled);
            second.Restore(GameSession.Load(path));
            Assert.Equal(first.State.GameplayPreferences, second.State.GameplayPreferences);
            second.SetGameplayPreferences(new GameplayPreferences());
            Assert.Equal(BattleAtbMode.Normal, GameSession.Load(path).GameplayPreferences.AtbMode);
            first.StartNewGame();
            Assert.Equal(new GameplayPreferences(), first.State.GameplayPreferences);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Existing_checkpoint_without_gameplay_owner_defaults_to_turn_based_without_native_changes()
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
            Assert.Equal(BattleDifficulty.Normal, loaded.GameplayPreferences.Difficulty);
            Assert.Equal(BattleAtbMode.TurnBased, loaded.GameplayPreferences.AtbMode);
            Assert.Equal(BattleGunbladeMode.Automatic, loaded.GameplayPreferences.GunbladeMode);
            Assert.False(loaded.GameplayPreferences.GuardianFormsEnabled);
            Assert.Equal(session.State.InputConfiguration, loaded.InputConfiguration);
            Assert.Equal(session.State.LimitProgress, loaded.LimitProgress);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Legacy_preferences_without_guardian_forms_default_to_off()
    {
        GameplayPreferences preferences = System.Text.Json.JsonSerializer.Deserialize<GameplayPreferences>("{\"SmartAttack\":true}")!;
        Assert.True(preferences.SmartAttack);
        Assert.False(preferences.GuardianFormsEnabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Guardian_forms_changes_are_rejected_during_battle_without_deferral(bool enabled)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetGameplayPreferences(new GameplayPreferences { GuardianFormsEnabled = enabled });
        BattleSession battle = new(new BattleDefinition
        {
            GuardianFormsEnabled = enabled,
            Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        session.SetBattle(battle.Capture());
        GameState before = session.State;
        GameplayPreferences changed = before.GameplayPreferences with { GuardianFormsEnabled = !enabled };
        Assert.Throws<InvalidOperationException>(() => session.SetGameplayPreferences(changed));
        MagicModePreview preview = new(MagicSystemFixture.Create(), changed, [], [], [], []);
        Assert.Throws<InvalidOperationException>(() => session.ApplyGameplayPreferences(preview));
        Assert.Same(before, session.State);
        Assert.Null(session.State.PendingGameplayPreferences);
        session.SetBattle(null);
        session.SetGameplayPreferences(changed);
        Assert.Equal(!enabled, session.State.GameplayPreferences.GuardianFormsEnabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Saved_battle_guardian_forms_option_must_match_its_save_owner(bool enabled)
    {
        BattleSession battle = new(new BattleDefinition
        {
            GuardianFormsEnabled = enabled,
            Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        GameState state = session.State with
        {
            GameplayPreferences = new GameplayPreferences { GuardianFormsEnabled = !enabled },
            Battle = battle.Capture()
        };
        Assert.Contains("battle GF Transformations option differs", Assert.Throws<InvalidDataException>(() => new GameSession(state)).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Saved_pending_guardian_forms_changes_cannot_bypass_the_battle_boundary(bool enabled)
    {
        BattleSession battle = new(new BattleDefinition
        {
            GuardianFormsEnabled = enabled,
            Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        GameState state = session.State with
        {
            GameplayPreferences = new GameplayPreferences { GuardianFormsEnabled = enabled },
            Battle = battle.Capture(),
            PendingGameplayPreferences = new GameplayPreferences { GuardianFormsEnabled = !enabled }
        };
        Assert.Contains("pending GF Transformations", Assert.Throws<InvalidDataException>(() => new GameSession(state)).Message);
        string hash = new('0', 64);
        BattleEncounterOccurrence pending = new("feature-boundary", 1, hash, SceneKind.Field, 74, 1, hash, null);
        Assert.Contains("pending GF Transformations", Assert.Throws<InvalidDataException>(() => new GameSession(state with { Battle = null, PendingBattleEncounter = pending })).Message);
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
