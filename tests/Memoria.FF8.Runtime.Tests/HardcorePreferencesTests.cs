using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class HardcorePreferencesTests
{
    [Fact]
    public void Enabling_hardcore_enables_mandatory_rules_and_preserves_atb()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetGameplayPreferences(new() { Difficulty = BattleDifficulty.Hardcore, AtbMode = BattleAtbMode.Normal });
        Assert.All(GameplayPreferences.NewGameOptions.Where(option => option.RequiredOnHardcore), option => Assert.True(option.IsEnabled(session.State.GameplayPreferences), option.Label));
        Assert.Equal(BattleAtbMode.Normal, session.State.GameplayPreferences.AtbMode);
        GameplayPreferences restored = JsonSerializer.Deserialize<GameplayPreferences>(JsonSerializer.Serialize(session.State.GameplayPreferences))!;
        restored.Validate();
        GameSession loaded = new(session.State with { GameplayPreferences = restored });
        Assert.Throws<InvalidOperationException>(() => loaded.SetGameplayPreferences(restored with { Difficulty = BattleDifficulty.Hard }));
        loaded.SetGameplayPreferences(restored with { AtbMode = BattleAtbMode.TurnBased });
        Assert.Equal(BattleAtbMode.TurnBased, loaded.State.GameplayPreferences.AtbMode);
    }

    [Fact]
    public void Explicit_mandatory_options_participate_and_cannot_be_disabled()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetGameplayPreferences(new() { Difficulty = BattleDifficulty.Hardcore });
        var properties = typeof(GameplayPreferences).GetProperties().Where(property => property.GetCustomAttributes(typeof(NewGameOptionAttribute), false).Cast<NewGameOptionAttribute>().Any(attribute => attribute.RequiredOnHardcore)).ToArray();
        Assert.Equal(properties.Select(property => property.Name).Order(), GameplayPreferences.NewGameOptions.Where(option => option.RequiredOnHardcore).Select(option => option.PropertyName).Order());
        foreach (var property in properties)
        {
            GameplayPreferences changed = session.State.GameplayPreferences with { };
            property.SetValue(changed, false);
            Assert.Throws<InvalidOperationException>(() => session.SetGameplayPreferences(changed));
            Assert.Throws<InvalidOperationException>(() => session.PreviewGameplayPreferences(changed));
            Assert.Throws<InvalidDataException>(changed.Validate);
        }
        Assert.All(GameplayPreferences.NewGameOptions.Where(option => option.RequiredOnHardcore), option => Assert.True(option.IsEnabled(session.State.GameplayPreferences)));
    }

    [Fact]
    public void Preview_and_apply_enforce_the_same_irreversible_rules_and_migrate_magic()
    {
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        GameSession session = new(initial.State with { CharacterVitals = MagicSystemFixture.Create() });
        MagicModePreview preview = session.PreviewGameplayPreferences(new() { Difficulty = BattleDifficulty.Hardcore });
        Assert.All(GameplayPreferences.NewGameOptions.Where(option => option.RequiredOnHardcore), option => Assert.True(option.IsEnabled(preview.Preferences)));
        session.ApplyGameplayPreferences(preview);
        Assert.True(session.State.CharacterVitals!.MagicSystem.SharedMagicStock);
        Assert.True(session.State.CharacterVitals.MagicSystem.InfiniteMagic);
        Assert.False(session.State.CharacterVitals.MagicSystem.SmartAttack);
        Assert.False(session.State.GameplayPreferences.GuardianFormsEnabled);
        session.SetGameplayPreferences(session.State.GameplayPreferences with { GuardianFormsEnabled = true });
        session.SetGameplayPreferences(session.State.GameplayPreferences with { GuardianFormsEnabled = false });
        session.SetAttackMode(0, MagicAttackMode.SmartMagic);
        Assert.Equal(MagicAttackMode.SmartMagic, session.State.CharacterVitals.MagicSystem.AttackModes[0]);
        Assert.Throws<InvalidOperationException>(() => session.ApplyGameplayPreferences(preview with { Preferences = new GameplayPreferences() }));
    }
}
