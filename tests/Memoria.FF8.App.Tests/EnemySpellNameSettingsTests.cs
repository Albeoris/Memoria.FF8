using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class EnemySpellNameSettingsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Interface_toggle_applies_and_persists_from_title_and_active_game(bool activeGame)
    {
        string path = Path.GetTempFileName();
        try
        {
            UiPreferences preferences = new();
            MenuData data = new();
            data.Settings["Enemy spell names"] = "Off";
            data.SupportedActions.Add("config:Enemy spell names");
            MenuController menus = new(() => data, action =>
            {
                preferences = preferences.WithInstallationSetting(action.SubjectId, action.Value, activeGame);
                preferences.Save(path);
                data.Settings[action.SubjectId] = action.Value;
            });
            menus.Open("config-interface");
            MenuEntry toggle = Assert.Single(menus.Entries, entry => entry.Id == "Enemy spell names");
            Assert.Equal("On", toggle.Action!.Value);
            menus.Execute(toggle.Action);
            Assert.True(preferences.ShowEnemySpellNames);
            Assert.True(UiPreferences.Load(path).ShowEnemySpellNames);
            menus.Execute(menus.Entries.Single(entry => entry.Id == "Enemy spell names").Action!);
            Assert.False(preferences.ShowEnemySpellNames);
            Assert.False(UiPreferences.Load(path).ShowEnemySpellNames);
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("Enemy spell names", "Sometimes"));
            Assert.Equal(new[] { "Off", "On" }, MenuSettings.Definitions.Single(setting => setting.Id == "Enemy spell names").Values);
            SettingPlacement placement = Assert.Single(SettingsPlacement.All, item => item.LegacyKey == "Enemy spell names");
            Assert.Equal("config-interface", placement.Page);
            Assert.Equal("Global", placement.Scope);
            Assert.Equal("UiPreferences.ShowEnemySpellNames", placement.Target);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Enemy_spell_names_default_off_for_new_and_existing_configuration()
    {
        string path = Path.GetTempFileName();
        try
        {
            Assert.False(new UiPreferences().ShowEnemySpellNames);
            File.WriteAllText(path, "{}");
            Assert.False(UiPreferences.Load(path).ShowEnemySpellNames);
            File.Delete(path);
            Assert.False(UiPreferences.Load(path).ShowEnemySpellNames);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_enemy_spell_name_preference_survives_other_settings_changes(bool enabled)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, enabled ? "{\"ShowEnemySpellNames\":true}" : "{\"ShowEnemySpellNames\":false}");
            UiPreferences preferences = UiPreferences.Load(path);
            Assert.Equal(enabled, preferences.ShowEnemySpellNames);
            preferences.WithSetting("Music volume", "25%").Save(path);
            UiPreferences reloaded = UiPreferences.Load(path);
            Assert.Equal(enabled, reloaded.ShowEnemySpellNames);
            Assert.Equal(25, reloaded.MusicVolume);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
