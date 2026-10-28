using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadSettingsTests
{
    [Fact]
    public void Dedicated_config_section_owns_triad_settings_and_rare_card_markers()
    {
        MenuController menus = new();
        menus.Open("config-triple-triad", demo: true);
        Assert.Equal([.. UiPreferences.TripleTriadSettingKeys, "Rare card markers"], menus.Entries.Select(entry => entry.Id));
        menus.Open("config-exploration", demo: true);
        Assert.DoesNotContain(menus.Entries, entry => entry.Id == "Rare card markers");
        menus.Open("new-game-options", demo: true);
        foreach (string key in UiPreferences.TripleTriadSettingKeys)
        {
            Assert.Contains(menus.Entries, entry => entry.Id == key);
        }
    }

    [Theory]
    [InlineData("config-triple-triad")]
    [InlineData("new-game-options")]
    public void Hardcore_menu_limits_numeric_domains_and_cycles_assistance(string screen)
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Hardcore";
        data.Settings["Triple Triad width"] = "5";
        data.Settings["Triple Triad height"] = "5";
        data.Settings["Triple Triad hand size"] = "13";
        data.Settings["Triple Triad Random redraws"] = "1";
        data.Settings["Triple Triad assistance"] = "Direct";
        List<MenuAction> actions = [];
        MenuController menus = new(() => data, action =>
        {
            actions.Add(action);
            data.Settings[action.SubjectId] = action.Value;
        });
        menus.Open(screen);
        foreach ((string key, float minimum, float maximum) in new[]
        {
            ("Triple Triad width", 4f, 5f),
            ("Triple Triad height", 4f, 5f),
            ("Triple Triad hand size", 13f, 50f),
            ("Triple Triad Random redraws", 0f, 1f)
        })
        {
            menus.SelectEntry(screen, key);
            UiNumericSlider slider = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiNumericSlider>(), row => row.Id == key);
            Assert.Equal(minimum, slider.Min);
            Assert.Equal(maximum, slider.Max);
        }
        menus.SelectEntry(screen, "Triple Triad hand size");
        menus.Navigate(MenuInput.Left);
        Assert.Equal("13", actions.Last().Value);
        menus.SelectEntry(screen, "Triple Triad Random redraws");
        menus.Navigate(MenuInput.Right);
        Assert.Equal("1", actions.Last().Value);
        menus.SelectEntry(screen, "Triple Triad assistance");
        Assert.Equal("Off", menus.SelectedEntry!.Action!.Value);
        UiChoiceRow assistance = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiChoiceRow>(), row => row.Id == "Triple Triad assistance");
        Assert.Equal(["Off", "Direct"], assistance.Items);
        menus.Navigate(MenuInput.Right);
        Assert.Equal("Off", actions.Last().Value);
        menus.Navigate(MenuInput.Right);
        Assert.Equal("Direct", actions.Last().Value);
        menus.Navigate(MenuInput.Left);
        Assert.Equal("Off", actions.Last().Value);
    }

    [Fact]
    public void Normal_hand_slider_minimum_tracks_board_capacity()
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Normal";
        data.Settings["Triple Triad width"] = "2";
        data.Settings["Triple Triad height"] = "5";
        data.Settings["Triple Triad hand size"] = "5";
        MenuController menus = new(() => data, _ => { });
        menus.Open("config-triple-triad");
        UiNumericSlider hand = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiNumericSlider>(), row => row.Id == "Triple Triad hand size");
        Assert.Equal(5, hand.Min);
        UiChoiceRow assistance = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiChoiceRow>(), row => row.Id == "Triple Triad assistance");
        Assert.Equal(["Off", "Direct", "Rules", "Combo", "Hand"], assistance.Items);
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    [Fact]
    public void Title_defaults_normalize_hand_capacity_and_hardcore_limits()
    {
        UiPreferences preferences = new UiPreferences().WithSetting("Triple Triad width", "5").WithSetting("Triple Triad height", "5");
        Assert.Equal(13, preferences.TripleTriad.HandSize);
        preferences = preferences.WithSetting("Triple Triad hand size", "50").WithSetting("Triple Triad Random redraws", "3").WithSetting("Triple Triad assistance", "Hand").WithSetting("Difficulty", "Hardcore");
        Assert.Equal(50, preferences.TripleTriad.HandSize);
        Assert.Equal(1, preferences.TripleTriad.RandomRedraws);
        Assert.Equal(TripleTriadAssistance.Direct, preferences.TripleTriad.Assistance);
        preferences = preferences.WithSetting("Triple Triad width", "2").WithSetting("Triple Triad hand size", "2");
        Assert.Equal(4, preferences.TripleTriad.Width);
        Assert.Equal(10, preferences.TripleTriad.HandSize);
    }
}
