using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class SettingsCompatibilityTests
{
    [Fact]
    public void Browsing_preserves_custom_stored_values_without_writes()
    {
        UiPreferences preferences = new() { MusicVolume = 63, SoundVolume = 17, UiFontSize = 23, DialogueFontSize = 11, QuickSaveCount = 2048 };
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Music volume"] = "63%";
        data.Settings["Effect volume"] = "17%";
        data.Settings["UI font size"] = "23";
        data.Settings["Dialogue font size"] = "11";
        data.Settings["Quick saves"] = "2048";
        Dictionary<string, string> before = new(data.Settings);
        int writes = 0;
        MenuController menus = new(() => data, _ => writes++);
        menus.ConnectPreferences(() => preferences, _ => writes++);
        foreach (string page in new[] { "config-audio", "config-text", "config-exploration" })
        {
            menus.Open(page);
            foreach (MenuEntry entry in menus.Entries.ToArray())
            {
                menus.SelectEntry(page, entry.Id);
                _ = menus.BuildView();
            }
        }
        Assert.Equal(0, writes);
        Assert.Equal(before.OrderBy(pair => pair.Key), data.Settings.OrderBy(pair => pair.Key));
        Assert.Equal(2048, preferences.QuickSaveCount);
        menus.Open("config-text");
        menus.SelectEntry("config-text", "UI font size");
        UiNumericSlider size = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiNumericSlider>(), row => row.Id == "UI font size");
        Assert.Equal(23, size.Value);
    }

    [Fact]
    public void New_game_projection_uses_shared_metadata_and_only_begin_starts()
    {
        MenuData data = new();
        data.SupportedActions.UnionWith(["config", "new-game-start"]);
        List<MenuAction> actions = [];
        MenuController menus = new(() => data, actions.Add);
        menus.Open("new-game-options");
        Assert.Equal(NewGameSettings.Ids.Concat(["begin"]), menus.Entries.Select(entry => entry.Id));
        UiChoiceRow atb = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiChoiceRow>(), row => row.Id == "Battle ATB mode");
        Assert.Equal("ATB mode", atb.Text);
        menus.SelectEntry("new-game-options", "Rare card markers");
        menus.Navigate(MenuInput.Right);
        Assert.DoesNotContain(actions, action => action.Kind == "new-game-start");
        menus.SelectEntry("new-game-options", "begin");
        menus.Navigate(MenuInput.Confirm);
        Assert.Single(actions, action => action.Kind == "new-game-start");
    }

    [Fact]
    public void Font_search_keeps_selected_family_identity_when_filter_is_cleared()
    {
        MenuController menus = new();
        menus.Open("config-text", true);
        menus.Execute(new("font-picker-open", "UI font"));
        string family = SystemFontCatalog.Families.Last();
        menus.SelectEntry("font-picker", "family:" + family);
        menus.Push("font-search");
        menus.EnterFontSearchCharacter('!');
        menus.Execute(new("font-search-clear"));
        menus.Back();
        Assert.Equal("family:" + family, menus.SelectedEntry?.Id);
    }

    private static IEnumerable<UiElement> Descendants(UiElement parent)
    {
        yield return parent;
        foreach (UiElement child in parent.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
