using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ConfigSettingsTests
{
    [Fact]
    public void Live_config_exposes_supported_settings_and_global_control_editors()
    {
        MenuData data = new();
        data.Settings["Cursor"] = "Initial";
        data.Settings["Music volume"] = "75%";
        data.SupportedActions.UnionWith(["config:Cursor", "config:Music volume"]);
        List<MenuAction> applied = [];
        MenuController controller = new(() => data, applied.Add);
        controller.Open("config");
        Assert.Equal(["Cursor", "Music volume", "config-controller", "config-keyboard"], controller.Entries.Select(entry => entry.Id));
        UiView view = controller.BuildView();
        UiChoiceRow choice = Assert.Single(Descendants(view.Root).OfType<UiChoiceRow>());
        view.Pointer(choice.ValueBounds.Right - 5, choice.Bounds.Y + choice.Bounds.Height / 2, true);
        Assert.Equal("Memory", Assert.Single(applied).Value);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("config-controller", controller.ScreenId);
    }

    [Fact]
    public void Volume_keyboard_and_pointer_share_increasing_clamped_numeric_scale()
    {
        MenuController controller = new();
        controller.Open("config", demo: true);
        int index = controller.Entries.ToList().FindIndex(entry => entry.Id == "Music volume");
        for (int step = 0; step < index; step++)
        {
            controller.Navigate(MenuInput.Down);
        }
        controller.Navigate(MenuInput.Right);
        Assert.Equal("100%", controller.Data.Settings["Music volume"]);
        controller.Navigate(MenuInput.Left);
        Assert.Equal("75%", controller.Data.Settings["Music volume"]);
        for (int step = 0; step < 5; step++)
        {
            controller.Navigate(MenuInput.Left);
        }
        Assert.Equal("0%", controller.Data.Settings["Music volume"]);
        UiView view = controller.BuildView();
        UiNumericSlider slider = Descendants(view.Root).OfType<UiNumericSlider>().Single(row => row.Id == "Music volume");
        Assert.Equal("0'%'", slider.ValueFormat);
        view.Pointer(slider.ValueBounds.X + 5, slider.Bounds.Y + slider.Bounds.Height / 2, true);
        Assert.Equal(index, controller.SelectedIndex);
        controller.Navigate(MenuInput.Right);
        Assert.Equal("25%", controller.Data.Settings["Music volume"]);
    }

    [Theory]
    [InlineData("100%", 640, 448)]
    [InlineData("150%", 1920, 1080)]
    public void Paged_rows_align_columns_and_fit_at_supported_text_scales(string scale, int width, int height)
    {
        MenuController controller = new();
        controller.Open("config", demo: true);
        controller.Data.Settings["Text scale"] = scale;
        HashSet<string> seen = [];
        for (int index = 0; index < controller.Entries.Count; index++)
        {
            UiView view = controller.BuildView();
            view.Resize(width, height);
            Assert.Equal(controller.SelectedEntry!.Id, view.FocusedElement?.Id);
            UiSettingRow[] rows = Descendants(view.Root).OfType<UiSettingRow>().ToArray();
            foreach (UiSettingRow row in rows)
            {
                seen.Add(row.Id!);
                Assert.Equal(rows[0].ValueBounds.X, row.ValueBounds.X);
                Assert.True(row.Bounds.Height >= row.FontSize);
                Assert.True(row.ValueBounds.Right <= row.Bounds.Right);
                Assert.True(row.Bounds.Width > 800);
            }
            controller.Navigate(MenuInput.Down);
        }
        Assert.Contains("Dialogue font size", seen);
        Assert.Contains("Field message", seen);
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
