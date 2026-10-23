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
    public void Retention_editor_stages_changes_and_cancel_preserves_preference()
    {
        MenuData data = new();
        data.Settings["Quick saves"] = "9";
        data.SupportedActions.Add("config:Quick saves");
        int previews = 0;
        int commits = 0;
        MenuController controller = new(() => data, _ => throw new InvalidOperationException("Unexpected live write"));
        controller.ConnectQuickSaveRetention(count =>
        {
            previews++;
            return 0;
        }, (count, removals) =>
        {
            commits++;
            return "Saved";
        });
        controller.Open("config-exploration");
        controller.Execute(new MenuAction("quick-save-open"));
        UiNumericSlider slider = Assert.Single(Descendants(controller.BuildView().Root).OfType<UiNumericSlider>());
        Assert.Equal(1, slider.Min);
        Assert.Equal(999, slider.Max);
        slider.ValueChanged!(1);
        controller.Navigate(MenuInput.Right);
        Assert.Equal(0, previews);
        Assert.Equal(0, commits);
        controller.Execute(new MenuAction("menu-back"));
        Assert.Equal("9", data.Settings["Quick saves"]);
        Assert.Equal(0, commits);
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(2048, true)]
    public void Config_count_opens_editor_without_changing_the_saved_limit(int count, bool pointer)
    {
        MenuData data = new();
        string value = count.ToString(CultureInfo.InvariantCulture);
        data.Settings["Quick saves"] = value;
        data.SupportedActions.Add("config:Quick saves");
        MenuController controller = new(() => data, _ => throw new InvalidOperationException("Unexpected live write"));
        controller.Open("config-exploration");
        controller.SelectEntry("config-exploration", "Quick saves");
        UiActionSettingRow row = Assert.Single(Descendants(controller.BuildView().Root).OfType<UiActionSettingRow>());
        Assert.Equal(value + "…", row.Value);

        if (pointer)
        {
            row.Pointer(row.Bounds.X + row.Bounds.Width / 2, row.Bounds.Y + row.Bounds.Height / 2);
        }
        else
        {
            controller.Navigate(MenuInput.Confirm);
        }

        Assert.Equal("quick-save-editor", controller.ScreenId);
        Assert.Contains(value, controller.Entries[0].Label);
        controller.Navigate(MenuInput.Left);
        controller.Navigate(MenuInput.Cancel);
        Assert.Equal("config-exploration", controller.ScreenId);
        Assert.Equal(value, data.Settings["Quick saves"]);
        row = Assert.Single(Descendants(controller.BuildView().Root).OfType<UiActionSettingRow>());
        Assert.Equal(value + "…", row.Value);
    }

    [Fact]
    public void Retention_deletion_requires_exact_count_confirmation_and_reports_owner_result()
    {
        MenuData data = new();
        data.Settings["Quick saves"] = "9";
        MenuController controller = new(() => data);
        int commits = 0;
        controller.ConnectQuickSaveRetention(_ => 3, (count, removals) =>
        {
            Assert.Equal(8, count);
            Assert.Equal(3, removals);
            commits++;
            data.Settings["Quick saves"] = "8";
            return "Limit saved; cleanup incomplete.";
        });
        controller.Open("config-exploration");
        controller.Execute(new MenuAction("quick-save-open"));
        controller.Navigate(MenuInput.Left);
        controller.Execute(new MenuAction("quick-save-review"));
        Assert.Equal("quick-save-confirm", controller.ScreenId);
        Assert.Contains("3 quick saves", controller.Entries[1].Label);
        Assert.Equal(0, commits);
        controller.Execute(new MenuAction("menu-back"));
        Assert.Equal("9", data.Settings["Quick saves"]);
        controller.Execute(new MenuAction("quick-save-review"));
        controller.Execute(new MenuAction("quick-save-commit"));
        Assert.Equal(1, commits);
        Assert.Equal("8", data.Settings["Quick saves"]);
        Assert.Equal("Limit saved; cleanup incomplete.", controller.Message);
    }

    [Fact]
    public void Unchanged_out_of_preset_limit_survives_open_and_save_without_confirmation()
    {
        MenuData data = new();
        data.Settings["Quick saves"] = int.MaxValue.ToString(CultureInfo.InvariantCulture);
        MenuController controller = new(() => data);
        int commits = 0;
        controller.ConnectQuickSaveRetention(count => 0, (count, removals) =>
        {
            Assert.Equal(int.MaxValue, count);
            Assert.Equal(0, removals);
            commits++;
            return "Saved";
        });
        controller.Open("config-exploration");
        controller.Execute(new MenuAction("quick-save-open"));
        UiNumericSlider slider = Assert.Single(Descendants(controller.BuildView().Root).OfType<UiNumericSlider>());
        Assert.Equal("'2147483647'", slider.ValueFormat);
        controller.Execute(new MenuAction("quick-save-review"));
        Assert.Equal(1, commits);
        Assert.Equal("config-exploration", controller.ScreenId);
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
