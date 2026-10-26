using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class MissingFontFallbackTests
{
    [Theory]
    [InlineData("UI font")]
    [InlineData("Dialogue font")]
    public void Picker_displays_the_missing_request_and_actual_fallback_without_changing_preferences(string setting)
    {
        const string missing = "Memoria Missing Family For Fallback Regression";
        UiPreferences preferences = new() { VectorUiFontFamily = missing, DialogueFontFamily = missing };
        MenuController menu = new();
        menu.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menu.ConnectLocalizationText((key, fallback) => key == SystemTextCatalog.Key("font.missing-fallback") ? "{0} 不在 → {1}" : fallback);
        menu.Open("config-text", false);
        menu.Execute(new MenuAction("font-picker-open", setting));
        Assert.Contains(menu.CurrentScreen.Details(menu), line => line.Contains(missing + " 不在 → " + SystemFontCatalog.DefaultFamily, StringComparison.Ordinal));
        Assert.Equal(missing, preferences.VectorUiFontFamily);
        Assert.Equal(missing, preferences.DialogueFontFamily);
    }
}
