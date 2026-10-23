using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class HardcoreSettingsTests
{
    [Fact]
    public void Hardcore_is_selectable_and_permanently_forces_all_save_owned_options()
    {
        UiPreferences defaults = new();
        UiPreferences hardcore = defaults.WithSetting("Difficulty", "Hardcore");
        Assert.Equal(BattleDifficulty.Hardcore, hardcore.Difficulty);
        foreach (NewGameOption option in GameplayPreferences.NewGameOptions)
        {
            var property = typeof(UiPreferences).GetProperty(option.PropertyName);
            if (property is not null)
            {
                Assert.Equal(true, property.GetValue(hardcore));
                Assert.Throws<InvalidOperationException>(() => hardcore.WithSetting(option.Label, "Off"));
            }
        }
        Assert.Throws<InvalidOperationException>(() => hardcore.WithSetting("Difficulty", "Normal"));
        Assert.Throws<InvalidOperationException>(() => hardcore.WithSetting("Difficulty", "Hard"));
        Assert.Equal(defaults.AtbMode, hardcore.AtbMode);
        Assert.Equal(defaults.RareCardMarkers, hardcore.RareCardMarkers);
        Assert.Equal(BattleAtbMode.Normal, hardcore.WithSetting("Battle ATB mode", "Classic").AtbMode);
        Assert.True(hardcore.WithSetting("Rare card markers", "On").RareCardMarkers);
    }

    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-combat")]
    [InlineData("config-magic")]
    [InlineData("config-exploration")]
    public void Hardcore_locks_every_optional_row_and_keeps_exceptions_editable(string screen)
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Hardcore";
        foreach (NewGameOption option in GameplayPreferences.NewGameOptions)
        {
            data.Settings[option.Label] = "On";
        }
        MenuController menus = new(() => data, _ => { });
        menus.Open(screen);
        foreach (MenuEntry entry in menus.Entries.Where(entry => NewGameSettings.IsLocked(entry.Id, BattleDifficulty.Hardcore)))
        {
            Assert.False(entry.Enabled);
            Assert.Contains("permanent", entry.Help);
        }
        foreach (MenuEntry entry in menus.Entries.Where(entry => entry.Id is "Battle ATB mode" or "Rare card markers" or "begin"))
        {
            Assert.True(entry.Enabled);
        }
    }
}
