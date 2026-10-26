using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class HardcoreSettingsTests
{
    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-combat")]
    public void Hardcore_requires_confirmation_and_cancel_preserves_values(string screen)
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Hard";
        List<MenuAction> applied = [];
        MenuController menus = new(() => data, action =>
        {
            applied.Add(action);
            data.Settings[action.SubjectId] = action.Value;
        });
        menus.Open(screen);
        menus.Navigate(MenuInput.Right);
        Assert.Equal("hardcore-confirm", menus.ScreenId);
        Assert.Equal("no", menus.SelectedEntry!.Id);
        Assert.Empty(applied);
        Assert.Equal("Hard", data.Settings["Difficulty"]);
        Assert.Contains("cannot be disabled", menus.CurrentScreen.Help);
        Assert.Equal(GameplayPreferences.NewGameOptions.Count + 1, menus.CurrentScreen.Details(menus).Count);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(screen, menus.ScreenId);
        Assert.Equal("Difficulty", menus.SelectedEntry!.Id);
        menus.Execute(new("hardcore-confirm"));
        Assert.Empty(applied);
        UiChoiceRow difficulty = Descendants(menus.BuildView().Root).OfType<UiChoiceRow>().Single(row => row.Id == "Difficulty");
        difficulty.Pointer(difficulty.ValueBounds.Right - 1, difficulty.Bounds.Y + 1);
        Assert.Equal("hardcore-confirm", menus.ScreenId);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(screen, menus.ScreenId);
        Assert.Empty(applied);
        menus.Navigate(MenuInput.Right);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(screen, menus.ScreenId);
        Assert.Equal("Hardcore", Assert.Single(applied).Value);
        menus.Execute(new("hardcore-confirm"));
        Assert.Single(applied);
    }

    [Theory]
    [InlineData(MenuPresentation.Classic, 24, 100)]
    [InlineData(MenuPresentation.Vector, 24, 100)]
    [InlineData(MenuPresentation.Classic, 32, 150)]
    [InlineData(MenuPresentation.Vector, 32, 150)]
    public void Confirmation_displays_warning_and_every_rule_without_clipping(MenuPresentation presentation, int fontSize, int textScale)
    {
        MenuController menus = new();
        menus.Open("config-combat", demo: true);
        menus.Presentation = presentation;
        menus.Data.Settings["Text scale"] = textScale + "%";
        menus.Execute(new("config", "UI font size", Value: fontSize.ToString()));
        menus.Execute(new("config", "Difficulty", Value: "Hardcore"));
        UiView view = menus.BuildView();
        using SkiaSharp.SKBitmap bitmap = new(1280, 896);
        using BattlePreviewCanvas canvas = new(bitmap);
        UiLabel[] labels = Descendants(view.Root).OfType<UiLabel>().ToArray();
        Assert.Equal(GameplayPreferences.NewGameOptions.Count + 1, labels.Length);
        foreach (UiLabel label in labels)
        {
            UiSize measured = canvas.MeasureText(label.Text, label.FontSize, label.Bounds.Width, menus.CreateTheme().FontFamily);
            Assert.True(measured.Height <= label.Bounds.Height, $"{label.Text}: {measured.Height} > {label.Bounds.Height}");
        }
        UiList commands = Descendants(view.Root).OfType<UiList>().Single();
        Assert.True(commands.VisibleRows >= commands.Items.Count);
    }

    [Fact]
    public void Hardcore_rows_remain_focusable_and_allow_navigation_through_locked_options()
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Hardcore";
        data.Settings["Limit once per turn"] = "On";
        List<MenuAction> applied = [];
        MenuController menus = new(() => data, applied.Add);
        menus.Open("config-combat");
        foreach (string id in new[] { "Difficulty", "Battle ATB mode", "Limit once per turn", "Selphie card Slots", "Gunblade trigger" })
        {
            Assert.Equal(id, menus.SelectedEntry!.Id);
            UiView view = menus.BuildView();
            UiChoiceRow row = Descendants(view.Root).OfType<UiChoiceRow>().Single(row => row.Id == id);
            Assert.Same(row, view.FocusedElement);
            if (row.ReadOnly)
            {
                menus.Navigate(MenuInput.Left);
                menus.Navigate(MenuInput.Right);
                menus.Navigate(MenuInput.Confirm);
                row.Pointer(row.ValueBounds.Right - 1, row.Bounds.Y + 1);
            }
            menus.Navigate(MenuInput.Down);
        }
        Assert.Empty(applied);
        Assert.Equal("Difficulty", menus.SelectedEntry!.Id);
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

    [Fact]
    public void Hardcore_is_selectable_and_permanently_forces_all_save_owned_options()
    {
        UiPreferences defaults = new();
        UiPreferences hardcore = defaults.WithSetting("Difficulty", "Hardcore");
        Assert.Equal(BattleDifficulty.Hardcore, hardcore.Difficulty);
        foreach (NewGameOption option in GameplayPreferences.NewGameOptions)
        {
            var property = typeof(UiPreferences).GetProperty(option.PropertyName);
            if (property is not null)
            {
                Assert.Equal(true, property.GetValue(hardcore));
                Assert.Throws<InvalidOperationException>(() => hardcore.WithSetting(option.Label, "Off"));
            }
        }
        Assert.Throws<InvalidOperationException>(() => hardcore.WithSetting("Difficulty", "Normal"));
        Assert.Throws<InvalidOperationException>(() => hardcore.WithSetting("Difficulty", "Hard"));
        Assert.Equal(defaults.AtbMode, hardcore.AtbMode);
        Assert.Equal(defaults.RareCardMarkers, hardcore.RareCardMarkers);
        Assert.Equal(BattleAtbMode.Normal, hardcore.WithSetting("Battle ATB mode", "Classic").AtbMode);
        Assert.True(hardcore.WithSetting("Rare card markers", "On").RareCardMarkers);
    }

    [Theory]
    [InlineData("new-game-options")]
    [InlineData("config-combat")]
    [InlineData("config-magic")]
    [InlineData("config-exploration")]
    public void Hardcore_locks_every_optional_row_and_keeps_exceptions_editable(string screen)
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Hardcore";
        foreach (NewGameOption option in GameplayPreferences.NewGameOptions)
        {
            data.Settings[option.Label] = "On";
        }
        MenuController menus = new(() => data, _ => { });
        menus.Open(screen);
        foreach (MenuEntry entry in menus.Entries.Where(entry => NewGameSettings.IsLocked(entry.Id, BattleDifficulty.Hardcore)))
        {
            Assert.False(entry.Enabled);
            Assert.Contains("permanent", entry.Help);
        }
        foreach (MenuEntry entry in menus.Entries.Where(entry => entry.Id is "Battle ATB mode" or "Rare card markers" or "begin"))
        {
            Assert.True(entry.Enabled);
        }
    }
}
