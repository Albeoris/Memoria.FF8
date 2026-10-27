using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplayTuningPreferenceTests
{
    [Fact]
    public void ConfiguredAndEffectiveRulesRoundTripSeparatelyAndRemovingAnOverrideRestoresPersonalChoices()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        GameplayTuning personal = new() { DrawBonus = 7, ExperienceMultiplier = 6, GuardianExperienceMultiplier = 3, MaximumDamage = 15000 };
        GameplayTuning effective = personal with { DrawBonus = 100, ExperienceMultiplier = 0 };
        session.ConfigureGameplayTuning(personal, effective);
        Assert.Equal(personal, session.State.ConfiguredGameplayTuning);
        Assert.Equal(effective, session.State.GameplayPreferences.Tuning);
        string path = TestResources.ArtifactFile("numeric-gameplay-preferences.json");
        session.Save(path, compact: true);
        GameSession restored = new(GameSession.Load(path));
        Assert.Equal(personal, restored.State.ConfiguredGameplayTuning);
        Assert.Equal(effective, restored.State.GameplayPreferences.Tuning);
        GameplayTuning baseline = restored.State.ConfiguredGameplayTuning!;
        restored.ConfigureGameplayTuning(baseline, baseline);
        Assert.Equal(personal, restored.State.GameplayPreferences.Tuning);
        Assert.Equal(personal, restored.State.ConfiguredGameplayTuning);
        restored.Save(path, compact: true);
        GameSession reloaded = new(GameSession.Load(path));
        Assert.Equal(personal, reloaded.State.GameplayPreferences.Tuning);
        Assert.Equal(3, reloaded.State.GameplayPreferences.Tuning.GuardianExperienceMultiplier);
        session.StartNewGame();
        Assert.Null(session.State.ConfiguredGameplayTuning);
        Assert.Equal(new GameplayTuning(), session.State.GameplayPreferences.Tuning);
    }

    [Fact]
    public void ActiveBattleKeepsCapturedRulesWhileEditedPreferencesWaitAcrossSaveAndLoad()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        GameplayTuning original = new() { DrawBonus = 7, ExperienceMultiplier = 2 };
        session.ConfigureGameplayTuning(original, original);
        BattleSession battle = new(new BattleDefinition
        {
            Tuning = original,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        session.SetBattle(battle.Capture());
        GameplayTuning configured = original with { DrawBonus = 12, MaximumDamage = 100000 };
        GameplayTuning effective = configured with { ExperienceMultiplier = 0 };
        session.ConfigureGameplayTuning(configured, effective);
        Assert.Equal(original, session.State.GameplayPreferences.Tuning);
        Assert.Equal(original, session.State.Battle!.Definition.Tuning);
        Assert.Equal(effective, session.State.PendingGameplayPreferences!.Tuning);
        Assert.Equal(configured, session.State.ConfiguredGameplayTuning);
        string path = TestResources.ArtifactFile("pending-numeric-gameplay.json");
        session.Save(path, compact: true);
        GameSession restored = new(GameSession.Load(path));
        Assert.Equal(original, restored.State.Battle!.Definition.Tuning);
        Assert.Equal(effective, restored.State.PendingGameplayPreferences!.Tuning);
        Assert.Throws<InvalidDataException>(() => new GameSession(restored.State with { GameplayPreferences = restored.State.GameplayPreferences with { Tuning = effective } }));
        restored.SetBattle(null);
        Assert.Null(restored.State.PendingGameplayPreferences);
        Assert.Equal(effective, restored.State.GameplayPreferences.Tuning);
        Assert.Equal(configured, restored.State.ConfiguredGameplayTuning);
    }

    [Fact]
    public void InvalidConfiguredOrEffectiveRulesLeaveTheWholeSessionUnchanged()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidDataException>(() => session.ConfigureGameplayTuning(new() { DrawBonus = -1 }, new()));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Throws<InvalidDataException>(() => session.ConfigureGameplayTuning(new(), new() { MaximumDamage = 100001 }));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }
}
