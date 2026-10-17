using System.Globalization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class QuickSaveSettingsTests
{
    [Fact]
    public void Default_and_existing_settings_without_quick_saves_retain_nine()
    {
        string path = Path.GetTempFileName();
        try
        {
            Assert.Equal(9, new UiPreferences().QuickSaveCount);
            File.WriteAllText(path, "{}");
            Assert.Equal(9, UiPreferences.Load(path).QuickSaveCount);
            File.Delete(path);
            Assert.Equal(9, UiPreferences.Load(path).QuickSaveCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(999)]
    [InlineData(int.MaxValue)]
    public void Positive_counts_persist_globally_including_in_game_changes(int count)
    {
        string path = Path.GetTempFileName();
        try
        {
            UiPreferences preferences = new UiPreferences().WithInstallationSetting("Quick saves", count.ToString(CultureInfo.InvariantCulture), activeGame: true);
            preferences.Save(path);
            Assert.Equal(count, UiPreferences.Load(path).QuickSaveCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("many")]
    public void Invalid_setting_values_are_rejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new UiPreferences().WithSetting("Quick saves", value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_counts_are_rejected_on_load_and_save(int count)
    {
        string path = Path.GetTempFileName();
        try
        {
            Assert.Throws<InvalidDataException>(() => (new UiPreferences() with { QuickSaveCount = count }).Save(path));
            File.WriteAllText(path, "{\"QuickSaveCount\":" + count.ToString(CultureInfo.InvariantCulture) + "}");
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Interface_exposes_quick_saves_as_an_integer_slider_and_clamps_at_one()
    {
        MenuData data = new();
        data.Settings["Quick saves"] = "9";
        data.SupportedActions.Add("config:Quick saves");
        List<MenuAction> applied = [];
        MenuController controller = new(() => data, action =>
        {
            applied.Add(action);
            data.Settings[action.SubjectId] = action.Value;
        });
        controller.Open("config-interface");
        Assert.Equal("Quick saves", Assert.Single(controller.Entries).Id);
        UiNumericSlider slider = Assert.Single(Descendants(controller.BuildView().Root).OfType<UiNumericSlider>());
        Assert.Equal(1, slider.Min);
        Assert.Equal(999, slider.Max);
        Assert.Equal(1, slider.Step);
        Assert.Equal(9, slider.Value);
        controller.Navigate(MenuInput.Right);
        Assert.Equal("10", Assert.Single(applied).Value);
        data.Settings["Quick saves"] = "1";
        controller.Navigate(MenuInput.Left);
        Assert.Equal("1", applied.Last().Value);
        controller.Open("config-gameplay");
        Assert.DoesNotContain(controller.Entries, entry => entry.Id == "Quick saves");
        controller.Open("new-game-options");
        Assert.DoesNotContain(controller.Entries, entry => entry.Id == "Quick saves");
    }

    [Fact]
    public void Host_setting_reads_the_installation_value()
    {
        MenuController controller = new();
        controller.ConnectPreferences(() => new UiPreferences { QuickSaveCount = 17 }, _ => { });
        MenuEntry entry = Assert.Single(controller.HostSettingsEntries("Quick saves"));
        Assert.Contains("17", entry.Label);
        Assert.Equal("18", entry.Action!.Value);
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
