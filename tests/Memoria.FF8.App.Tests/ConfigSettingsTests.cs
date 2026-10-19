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
    public void New_game_offers_atb_and_card_markers_and_only_starts_on_begin()
    {
        MenuData data = new();
        data.Settings["Battle ATB mode"] = "Classic";
        data.Settings["Rare card markers"] = "On";
        data.SupportedActions.UnionWith(["config:Battle ATB mode", "config:Rare card markers", "new-game-start"]);
        List<MenuAction> actions = [];
        MenuController controller = new(() => data, actions.Add);
        controller.Open("new-game-options");
        Assert.Equal(["Battle ATB mode", "Rare card markers", "begin"], controller.Entries.Select(entry => entry.Id));
        controller.Navigate(MenuInput.Right);
        Assert.Equal("Turn Based", Assert.Single(actions).Value);
        controller.Navigate(MenuInput.Down);
        Assert.Contains("Triple Triad", controller.SelectedEntry!.Help);
        controller.Navigate(MenuInput.Right);
        Assert.Equal("Rare card markers", actions.Last().SubjectId);
        Assert.Equal("Off", actions.Last().Value);
        Assert.DoesNotContain(actions, action => action.Kind == "new-game-start");
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(2, controller.SelectedIndex);
        Assert.Equal("new-game-start", actions.Last().Kind);
    }

    [Theory]
    [InlineData("100%", 640, 448)]
    [InlineData("150%", 1920, 1080)]
    public void New_game_shows_both_settings_and_begin_together(string scale, int width, int height)
    {
        MenuController controller = new();
        controller.Open("new-game-options", demo: true);
        controller.Data.Settings["Text scale"] = scale;
        UiView view = controller.BuildView();
        view.Resize(width, height);
        UiChoiceRow[] rows = Descendants(view.Root).OfType<UiChoiceRow>().ToArray();
        Assert.Equal(["Battle ATB mode", "Rare card markers"], rows.Select(row => row.Id));
        UiButton begin = Assert.Single(Descendants(view.Root).OfType<UiButton>(), button => button.Id == "begin");
        foreach (UiElement row in rows.Cast<UiElement>().Append(begin))
        {
            Assert.True(row.Bounds.Height >= 24 * controller.TextScale);
            Assert.True(row.Bounds.Y >= view.Root.Bounds.Y);
            Assert.True(row.Bounds.Bottom <= view.Root.Bounds.Bottom);
        }
        Assert.True(rows[1].Bounds.Bottom <= begin.Bounds.Y);
    }

    [Fact]
    public void Display_preferences_round_trip_and_reject_invalid_limits()
    {
        UiPreferences preferences = new UiPreferences().WithSetting("V-Sync", "Off").WithSetting("Battle FPS", "120").WithSetting("Field FPS", "90").WithSetting("World FPS", "30").WithSetting("Menu FPS", "Unlimited");
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            preferences.Save(path);
            Assert.Equal(preferences, UiPreferences.Load(path));
            Assert.False(preferences.VSync);
            Assert.Equal(0, preferences.MenuFps);
            Assert.Equal(30, preferences.WorldFps);
            Assert.Equal(90, preferences.FieldFps);
            Assert.Equal(60, preferences.WithSetting("World FPS", "60").WorldFps);
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("Battle FPS", "55"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Existing_preferences_without_world_limit_default_to_60_independently_of_fields()
    {
        Assert.Equal(60, new UiPreferences().WorldFps);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{\"FieldFps\":30}");
            UiPreferences preferences = UiPreferences.Load(path);
            Assert.Equal(30, preferences.FieldFps);
            Assert.Equal(60, preferences.WorldFps);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(55)]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    public void World_preferences_reject_limits_outside_30_and_60(int frameLimit)
    {
        UiPreferences preferences = new();
        Assert.Throws<ArgumentException>(() => preferences.WithSetting("World FPS", frameLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            Assert.Throws<InvalidDataException>(() => (preferences with { WorldFps = frameLimit }).Save(path));
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(preferences with { WorldFps = frameLimit }));
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Display_menu_exposes_world_limit_with_only_30_and_60_choices()
    {
        MenuSetting setting = Assert.Single(MenuSettings.Definitions, setting => setting.Id == "World FPS");
        Assert.Equal(["30", "60"], setting.Values);
        Assert.Throws<ArgumentException>(() => new UiPreferences().WithSetting("World FPS", "Unlimited"));
        MenuData data = new();
        data.Settings["World FPS"] = "60";
        data.SupportedActions.Add("config:World FPS");
        List<MenuAction> applied = [];
        MenuController controller = new(() => data, applied.Add);
        controller.Open("config-display");
        Assert.Equal("World FPS", Assert.Single(controller.Entries).Id);
        controller.Navigate(MenuInput.Left);
        Assert.Equal("World FPS", Assert.Single(applied).SubjectId);
        Assert.Equal("30", applied[0].Value);
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
