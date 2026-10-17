using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ConfigSettingsTests
{
    [Fact]
    public void Window_size_uses_defaults_config_or_explicit_launch_resolution()
    {
        Assert.Equal((1024, 768), new UiPreferences().WindowSize(Memoria.FF8.Runtime.LaunchOptions.Parse([])));
        UiPreferences preferences = new() { WindowWidth = 1280, WindowHeight = 960 };
        Assert.Equal((1280, 960), preferences.WindowSize(Memoria.FF8.Runtime.LaunchOptions.Parse([])));
        Assert.Equal((1024, 768), preferences.WindowSize(Memoria.FF8.Runtime.LaunchOptions.Parse(["--resolution", "1024x768"])));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            preferences.Save(path);
            Assert.Equal(preferences, UiPreferences.Load(path));
            Assert.Throws<InvalidDataException>(() => (preferences with { WindowWidth = 0 }).Save(path));
            Assert.Throws<InvalidDataException>(() => (preferences with { WindowHeight = -1 }).Save(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

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
        Assert.Equal(["config-gameplay", "config-display", "config-audio", "config-interface", "config-controls"], controller.Entries.Select(entry => entry.Id));
        controller.Open("config-interface");
        Assert.Equal("Cursor", Assert.Single(controller.Entries).Id);
        UiView view = controller.BuildView();
        UiChoiceRow choice = Assert.Single(Descendants(view.Root).OfType<UiChoiceRow>());
        view.Pointer(choice.ValueBounds.Right - 5, choice.Bounds.Y + choice.Bounds.Height / 2, true);
        Assert.Equal("Memory", Assert.Single(applied).Value);
        controller.Open("config-controls");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("config-controller", controller.ScreenId);
    }

    [Fact]
    public void Volume_keyboard_and_pointer_share_increasing_clamped_numeric_scale()
    {
        MenuController controller = new();
        controller.Open("config-audio", demo: true);
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
        controller.Open("config-interface", demo: true);
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

    [Fact]
    public void New_game_only_offers_atb_and_begin_and_does_not_start_on_selection()
    {
        MenuData data = new();
        data.Settings["Battle ATB mode"] = "Classic";
        data.SupportedActions.UnionWith(["config:Battle ATB mode", "new-game-start"]);
        List<MenuAction> actions = [];
        MenuController controller = new(() => data, actions.Add);
        controller.Open("new-game-options");
        Assert.Equal(["Battle ATB mode", "begin"], controller.Entries.Select(entry => entry.Id));
        controller.Navigate(MenuInput.Right);
        Assert.Equal("Turn Based", Assert.Single(actions).Value);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(1, controller.SelectedIndex);
        Assert.Equal("new-game-start", actions.Last().Kind);
    }

    [Fact]
    public void Display_preferences_round_trip_and_reject_invalid_limits()
    {
        UiPreferences preferences = new UiPreferences().WithSetting("V-Sync", "Off").WithSetting("Battle FPS", "120").WithSetting("Field FPS", "30").WithSetting("Menu FPS", "Unlimited");
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            preferences.Save(path);
            Assert.Equal(preferences, UiPreferences.Load(path));
            Assert.False(preferences.VSync);
            Assert.Equal(0, preferences.MenuFps);
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("Battle FPS", "55"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void In_game_changes_do_not_replace_installation_gameplay_defaults()
    {
        UiPreferences defaults = new();
        Assert.Equal(Memoria.FF8.Runtime.BattleAtbMode.TurnBased, defaults.AtbMode);
        Assert.True(defaults.VSync);
        Assert.Equal(defaults, defaults.WithInstallationSetting("Battle ATB mode", "Classic", activeGame: true));
        Assert.Equal(Memoria.FF8.Runtime.BattleAtbMode.Normal, defaults.WithInstallationSetting("Battle ATB mode", "Classic", activeGame: false).AtbMode);
        UiPreferences volume = defaults.WithInstallationSetting("Music volume", "25%", activeGame: true);
        Assert.Equal(25, volume.MusicVolume);
        Assert.Equal(defaults.AtbMode, volume.AtbMode);
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
