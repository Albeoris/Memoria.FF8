using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NewGameCombatDefaultsTests
{
    [Fact]
    public void New_game_defaults_enable_full_combat_and_random_encounters()
    {
        AssertDefaults(new UiPreferences());
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{}");
            AssertDefaults(UiPreferences.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Explicit_instant_victory_and_disabled_encounters_remain_saved_choices()
    {
        UiPreferences preferences = new UiPreferences().WithSetting("Battle policy", "Instant victory").WithSetting("Random encounters", "Off");
        string path = Path.GetTempFileName();
        try
        {
            preferences.Save(path);
            UiPreferences loaded = UiPreferences.Load(path);
            Assert.Equal(preferences, loaded);
            BattleInput[] inputs = loaded.CreateBattleSettingsInputs();
            Assert.False(Assert.Single(inputs, input => input.Kind == BattleInputKind.ConfigurePlaythrough).FullCombat);
            Assert.False(Assert.Single(inputs, input => input.Kind == BattleInputKind.ConfigureEncounters).RandomEncounters);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertDefaults(UiPreferences preferences)
    {
        Assert.Equal("Full combat", preferences.BattlePolicyLabel);
        Assert.True(preferences.RandomEncounters);
        BattleInput[] inputs = preferences.CreateBattleSettingsInputs();
        Assert.True(Assert.Single(inputs, input => input.Kind == BattleInputKind.ConfigurePlaythrough).FullCombat);
        Assert.True(Assert.Single(inputs, input => input.Kind == BattleInputKind.ConfigureEncounters).RandomEncounters);
    }
}
