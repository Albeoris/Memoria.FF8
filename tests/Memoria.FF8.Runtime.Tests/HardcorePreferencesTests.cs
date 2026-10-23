using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class HardcorePreferencesTests
{
    [Fact]
    public void Enabling_hardcore_enables_every_optional_rule_and_preserves_atb()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetGameplayPreferences(new() { Difficulty = BattleDifficulty.Hardcore, AtbMode = BattleAtbMode.Normal });
        Assert.All(GameplayPreferences.NewGameOptions, option => Assert.True(option.IsEnabled(session.State.GameplayPreferences), option.Label));
        Assert.Equal(BattleAtbMode.Normal, session.State.GameplayPreferences.AtbMode);
        GameplayPreferences restored = JsonSerializer.Deserialize<GameplayPreferences>(JsonSerializer.Serialize(session.State.GameplayPreferences))!;
        restored.Validate();
        GameSession loaded = new(session.State with { GameplayPreferences = restored });
        Assert.Throws<InvalidOperationException>(() => loaded.SetGameplayPreferences(restored with { Difficulty = BattleDifficulty.Hard }));
        loaded.SetGameplayPreferences(restored with { AtbMode = BattleAtbMode.TurnBased });
        Assert.Equal(BattleAtbMode.TurnBased, loaded.State.GameplayPreferences.AtbMode);
    }

    [Fact]
    public void Every_boolean_preference_automatically_participates_and_cannot_be_disabled()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetGameplayPreferences(new() { Difficulty = BattleDifficulty.Hardcore });
        var properties = typeof(GameplayPreferences).GetProperties().Where(property => property.PropertyType == typeof(bool)).ToArray();
        Assert.Equal(properties.Select(property => property.Name).Order(), GameplayPreferences.NewGameOptions.Select(option => option.PropertyName).Order());
        foreach (var property in properties)
        {
            GameplayPreferences changed = session.State.GameplayPreferences with { };
            property.SetValue(changed, false);
            Assert.Throws<InvalidOperationException>(() => session.SetGameplayPreferences(changed));
            Assert.Throws<InvalidOperationException>(() => session.PreviewGameplayPreferences(changed));
            Assert.Throws<InvalidDataException>(changed.Validate);
        }
        Assert.All(GameplayPreferences.NewGameOptions, option => Assert.True(option.IsEnabled(session.State.GameplayPreferences)));
    }

    [Fact]
    public void Preview_and_apply_enforce_the_same_irreversible_rules_and_migrate_magic()
    {
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        GameSession session = new(initial.State with { CharacterVitals = MagicSystemFixture.Create() });
        MagicModePreview preview = session.PreviewGameplayPreferences(new() { Difficulty = BattleDifficulty.Hardcore });
        Assert.All(GameplayPreferences.NewGameOptions, option => Assert.True(option.IsEnabled(preview.Preferences)));
        session.ApplyGameplayPreferences(preview);
        Assert.True(session.State.CharacterVitals!.MagicSystem.SharedMagicStock);
        Assert.True(session.State.CharacterVitals.MagicSystem.InfiniteMagic);
        Assert.True(session.State.CharacterVitals.MagicSystem.SmartAttack);
        Assert.Throws<InvalidOperationException>(() => session.ApplyGameplayPreferences(preview with { Preferences = new GameplayPreferences() }));
    }
}
