using System.Text.Json;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class PerSkinFontPreferencesTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"UiFontFamily\":\"Arial\"}")]
    public void Legacy_default_uses_each_presentations_default(string json)
    {
        WithPreferencesFile(json, path =>
        {
            UiPreferences preferences = UiPreferences.Load(path);
            Assert.Equal("Georgia", preferences.UiFontFamily);
            Assert.Equal(SystemFontCatalog.DefaultFamily, preferences.WithSetting("UI presentation", "Classic").UiFontFamily);
            Assert.Null(preferences.ClassicUiFontFamily);
            Assert.Null(preferences.VectorUiFontFamily);
            preferences.Save(path);
            Assert.Equal(preferences, UiPreferences.Load(path));
        });
    }

    [Fact]
    public void Legacy_nondefault_choice_is_preserved_for_both_presentations()
    {
        WithPreferencesFile("{\"UiFontFamily\":\"Courier New\"}", path =>
        {
            UiPreferences preferences = UiPreferences.Load(path);
            Assert.Equal("Courier New", preferences.UiFontFamily);
            Assert.Equal("Courier New", preferences.WithSetting("UI presentation", "Classic").UiFontFamily);
            preferences.Save(path);
            UiPreferences restored = UiPreferences.Load(path);
            Assert.Equal(preferences, restored);
            Assert.Equal("Courier New", restored.WithSetting("UI presentation", "Classic").UiFontFamily);
        });
    }

    [Fact]
    public void Explicit_Arial_vector_override_and_independent_classic_choice_survive_switches_and_roundtrip()
    {
        UiPreferences preferences = new UiPreferences().WithSetting("UI font", "Arial");
        Assert.Equal("Arial", preferences.VectorUiFontFamily);
        Assert.Equal("Arial", preferences.UiFontFamily);
        preferences = preferences.WithSetting("UI presentation", "Classic").WithSetting("UI font", "Georgia");
        Assert.Equal("Georgia", preferences.ClassicUiFontFamily);
        Assert.Equal("Georgia", preferences.UiFontFamily);
        Assert.Equal(SystemFontCatalog.DefaultFamily, preferences.DialogueFontFamily);
        preferences = preferences with { MenuPresentation = MenuPresentation.Vector };
        Assert.Equal("Arial", preferences.UiFontFamily);
        WithPreferencesFile("{}", path =>
        {
            preferences.Save(path);
            UiPreferences restored = UiPreferences.Load(path);
            Assert.Equal(preferences, restored);
            Assert.Equal("Arial", restored.UiFontFamily);
            Assert.Equal("Georgia", (restored with { MenuPresentation = MenuPresentation.Classic }).UiFontFamily);
            using JsonDocument stored = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("Arial", stored.RootElement.GetProperty("VectorUiFontFamily").GetString());
            Assert.Equal("Georgia", stored.RootElement.GetProperty("ClassicUiFontFamily").GetString());
        });
    }

    [Fact]
    public void Current_presentation_font_matches_theme_and_settings_after_direct_and_preference_switches()
    {
        UiPreferences preferences = new();
        MenuController menu = new();
        menu.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        MenuData data = new();
        data.SupportedActions.Add("config");
        menu = new(() => data);
        menu.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menu.Open("config-text", false);
        AssertDisplayedFont("Georgia");
        menu.Presentation = MenuPresentation.Classic;
        AssertDisplayedFont(SystemFontCatalog.DefaultFamily);
        preferences = preferences.WithSetting("UI font", "Arial");
        menu.Presentation = MenuPresentation.Vector;
        AssertDisplayedFont("Arial");
        preferences = preferences.WithSetting("UI presentation", "Classic").WithSetting("UI font", "Georgia");
        menu.Presentation = preferences.MenuPresentation;
        AssertDisplayedFont("Georgia");
        preferences = preferences with { MenuPresentation = MenuPresentation.Vector };
        menu.Presentation = preferences.MenuPresentation;
        AssertDisplayedFont("Arial");

        void AssertDisplayedFont(string expected)
        {
            Assert.Equal(expected, menu.CreateTheme().FontFamily);
            Assert.Contains(expected, Assert.Single(menu.HostSettingsEntries("UI font")).Label);
        }
    }

    [Fact]
    public void Demo_font_choices_match_the_theme_and_remain_independent_when_switching()
    {
        MenuController menu = new();
        menu.Open("config-interface", true);
        Assert.Equal("Georgia", menu.CreateTheme().FontFamily);
        menu.Execute(new MenuAction("config", "UI font", Value: "Arial"));
        Assert.Equal("Arial", menu.CreateTheme().FontFamily);
        menu.Execute(new MenuAction("config", "UI presentation", Value: "Classic"));
        Assert.Equal(SystemFontCatalog.DefaultFamily, menu.CreateTheme().FontFamily);
        menu.Execute(new MenuAction("config", "UI font", Value: "Georgia"));
        Assert.Equal("Georgia", menu.CreateTheme().FontFamily);
        menu.Presentation = MenuPresentation.Vector;
        Assert.Equal("Arial", menu.CreateTheme().FontFamily);
        Assert.Contains("Arial", Assert.Single(menu.HostSettingsEntries("UI font")).Label);
        menu.Presentation = MenuPresentation.Classic;
        Assert.Equal("Georgia", menu.CreateTheme().FontFamily);
        Assert.Contains("Georgia", Assert.Single(menu.HostSettingsEntries("UI font")).Label);
    }

    [Fact]
    public void Interface_starts_with_presentation_and_preserves_the_order_of_other_settings()
    {
        MenuController menu = new();
        menu.Open("config-interface", true);
        Assert.Equal("UI presentation", menu.Entries[0].Id);
        Assert.Equal(["config-text", "Cursor"], menu.Entries.Skip(1).Select(entry => entry.Id));
    }

    private static void WithPreferencesFile(string json, Action<string> inspect)
    {
        string path = Path.Combine(Path.GetTempPath(), "ff8-skin-font-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, json, System.Text.Encoding.UTF8);
            inspect(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
