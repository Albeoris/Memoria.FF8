using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NewGamePlusNavigationTests
{
    [Fact]
    public void TranslatorCatalogIncludesDynamicPresetsCategoriesAndCompletionActions()
    {
        foreach (string identity in new[] { "new-game-plus.preset.stored", "new-game-plus.preset.native", "new-game-plus.preset.empty", "new-game-plus.preset.custom", "new-game-plus.category.characters", "new-game-plus.category.enemy-level-cap", "new-game-plus.category.character-level-cap", "new-game-plus.category.party-level-bonus", "new-game-plus.available", "new-game-plus.configure", "ngplus.completed", "ngplus.overwrite.message", "ngplus.completion.prompt" })
        {
            Assert.True(SystemTextCatalog.Defaults.ContainsKey(identity), identity);
        }
    }

    [Fact]
    public void InitialSettingsFocusConfigureAndBackUsesWizardCancellation()
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        MenuController menus = new(() => data);
        bool cancelled = false;
        menus.CancelNewGamePlusSetup = () => cancelled = true;
        menus.OpenNewGamePlusInitialSettings();
        Assert.Equal("begin", menus.SelectedEntry!.Id);
        Assert.Equal("Configure New Game+", menus.SelectedEntry.Label);
        menus.Navigate(MenuInput.Up);
        Assert.NotEqual("begin", menus.SelectedEntry!.Id);
        menus.Back();
        Assert.True(cancelled);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(150)]
    public void MainControlsFitMinimumViewportWithoutSeparateConfigButtons(int scale)
    {
        (MenuController menus, _) = Create(scale);
        UiView view = menus.BuildView();
        view.Resize(960, 672);
        UiElement[] controls = Descendants(view.Root).Where(element => element.Id?.StartsWith("config-", StringComparison.Ordinal) == true).ToArray();
        Assert.Empty(controls);
        Assert.DoesNotContain(Descendants(view.Root).OfType<UiButton>(), button => button.Text == "Config");
        Assert.Equal(5, Descendants(view.Root).OfType<UiNumericSlider>().Count());
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Right);
        Assert.Equal("exp", menus.SelectedEntry!.Id);
    }

    [Fact]
    public void LargestFontScrollsEveryActionIntoViewAndMeasuredControlsFit()
    {
        (MenuController menus, _) = Create(150, 48);
        Assert.Equal(3f, menus.TextScale);
        using SkiaSharp.SKBitmap bitmap = new(960, 672);
        using BattlePreviewCanvas canvas = new(bitmap);
        for (int index = 0; index < menus.Entries.Count; index++)
        {
            UiView view = menus.BuildView();
            view.Resize(960, 672);
            view.Draw(canvas, menus.CreateTheme());
            UiElement focused = view.FocusedElement!;
            UiScrollPanel scroll = Assert.Single(Descendants(view.Root).OfType<UiScrollPanel>(), panel => Descendants(panel).Contains(focused));
            Assert.True(focused.Bounds.Y >= scroll.Bounds.Y - 1);
            Assert.True(focused.Bounds.Bottom <= scroll.Bounds.Bottom + 1);
            foreach (UiButton button in Descendants(view.Root).OfType<UiButton>().Where(button => button.Text.Length > 0))
            {
                UiSize measured = canvas.MeasureText(button.Text, button.FontSize, float.PositiveInfinity, menus.CreateTheme().FontFamily);
                Assert.True(measured.Width <= button.Bounds.Width - button.TextInset - 8 + 1, button.Text);
                Assert.True(measured.Height <= button.Bounds.Height + 1, button.Text);
            }
            if (menus.SelectedEntry!.Id is "enemy-level-cap" or "character-level-cap" or "party-level-bonus")
            {
                UiLabel heading = Assert.Single(Descendants(view.Root).OfType<UiLabel>(), element => element.Text == menus.SelectedEntry.Label);
                UiSize measured = canvas.MeasureText(heading.Text, heading.FontSize, heading.Bounds.Width, menus.CreateTheme().FontFamily);
                Assert.True(measured.Width <= heading.Bounds.Width + 1, heading.Text);
                Assert.True(measured.Height <= heading.Bounds.Height + 1, heading.Text);
            }
            menus.Navigate(MenuInput.Down);
        }
        menus.Navigate(MenuInput.Up);
        Assert.Equal("begin", menus.SelectedEntry!.Id);
        menus.Execute(new("ng-config", "characters"));
        UiView characters = menus.BuildView();
        characters.Resize(960, 672);
        characters.Draw(canvas, menus.CreateTheme());
        UiButton world = Descendants(characters.Root).OfType<UiButton>().Single(button => button.Id == "world");
        UiLabel label = Assert.Single(world.Children.OfType<UiLabel>());
        UiSize size = canvas.MeasureText(label.Text, label.FontSize, label.Bounds.Width, menus.CreateTheme().FontFamily);
        Assert.True(size.Height <= label.Bounds.Height + 1);

    }

    [Theory]
    [InlineData(0, "characters")]
    [InlineData(1, "exp")]
    [InlineData(2, "gf")]
    [InlineData(3, "ap")]
    [InlineData(4, "magic")]
    [InlineData(5, "items")]
    [InlineData(6, "cards")]
    public void CategoriesOpenDetailsOnlyThroughTheCardAction(int index, string category)
    {
        (MenuController menus, _) = Create();
        for (int row = 0; row < index; row++)
        {
            menus.Navigate(MenuInput.Down);
        }
        Assert.Equal(category, menus.SelectedEntry!.Id);
        menus.Navigate(MenuInput.Confirm);
        Assert.NotEqual("new-game-plus-config", menus.ScreenId);
        if (menus.ScreenId == "new-game-plus-reset")
        {
            menus.Back();
        }
        menus.Navigate(MenuInput.Alternate);
        Assert.Equal("new-game-plus-config", menus.ScreenId);
        Assert.DoesNotContain(Descendants(menus.BuildView().Root).OfType<UiButton>(), button => button.Text == "Config");
    }

    [Fact]
    public void AlternateOpensConfigAndNumericPanelSharesInlineValue()
    {
        (MenuController menus, NewGamePlusDraft draft) = Create();
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Right);
        Assert.Equal(11, draft.ExperienceMultiplierTenths);
        menus.Navigate(MenuInput.Alternate);
        Assert.Equal("new-game-plus-config", menus.ScreenId);
        UiNumericSlider slider = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiNumericSlider>());
        Assert.Equal(1.1f, slider.Value);
        menus.Navigate(MenuInput.Left);
        Assert.Equal(10, draft.ExperienceMultiplierTenths);
        menus.Back();
        Assert.Equal("exp", menus.SelectedEntry!.Id);
    }

    [Theory]
    [InlineData(7, "enemy-level-cap", "Maximum Enemy Level", 1, 300, 100)]
    [InlineData(8, "character-level-cap", "Maximum Character Level", 1, 300, 100)]
    [InlineData(9, "party-level-bonus", "Party Level Bonus", 0, 200, 0)]
    public void LevelRulesShareInlineAndDetailControlsAndAppearInReview(int row, string id, string label, int min, int max, int defaultValue)
    {
        (MenuController menus, _) = Create();
        UiNumericSlider inline = Descendants(menus.BuildView().Root).OfType<UiNumericSlider>().Single(slider => slider.Id == id);
        Assert.Equal(((float)min, (float)max, (float)defaultValue), (inline.Min, inline.Max, inline.Value));
        for (int index = 0; index < row; index++)
        {
            menus.Navigate(MenuInput.Down);
        }
        Assert.Equal(id, menus.SelectedEntry!.Id);
        menus.Navigate(MenuInput.Right);
        menus.Navigate(MenuInput.Alternate);
        UiNumericSlider detail = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiNumericSlider>());
        Assert.Equal(((float)min, (float)max, defaultValue + 1f), (detail.Min, detail.Max, detail.Value));
        detail.ValueChanged!(max);
        menus.Navigate(MenuInput.Right);
        Assert.Equal((float)max, Assert.Single(Descendants(menus.BuildView().Root).OfType<UiNumericSlider>()).Value);
        menus.Back();
        Assert.Equal(id, menus.SelectedEntry!.Id);
        menus.Execute(new("ng-review"));
        Assert.Equal("new-game-plus-review", menus.ScreenId);
        Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiLabel>(), element => element.Text.Contains($"{label}: {max}", StringComparison.Ordinal));
    }

    [Fact]
    public void MagicConversionReviewUsesTheCardActionInsteadOfAConfigEntry()
    {
        (MenuController menus, NewGamePlusDraft draft) = Create();
        draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = true });
        menus.OpenNewGamePlus(draft, _ => { }, () => { });
        Assert.Equal("new-game-plus-magic-review", menus.ScreenId);
        Assert.DoesNotContain(menus.Entries, entry => entry.Id == "config");
        menus.Navigate(MenuInput.Alternate);
        Assert.Equal("new-game-plus-config", menus.ScreenId);
        Assert.DoesNotContain(Descendants(menus.BuildView().Root).OfType<UiButton>(), button => button.Text == "Config");
    }

    [Fact]
    public void GlobalOverrideGreysPortraitsAndKeepsAvailabilityOperable()
    {
        (MenuController menus, NewGamePlusDraft draft) = Create();
        menus.Navigate(MenuInput.Alternate);
        draft.SetGlobalLevel(75);
        menus.InvalidateLiveData();
        UiView view = menus.BuildView();
        view.Resize(960, 672);
        Assert.Equal(11, Descendants(view.Root).OfType<UiImage>().Count(image => image.Source?.Kind == UiImageKind.CharacterPortraitGrayscale));
        UiButton seifer = Descendants(view.Root).OfType<UiButton>().Single(button => button.Id == "available-6");
        Assert.True(seifer.Enabled);
        bool before = draft.Characters.Single(character => character.CharacterId == 6).AvailableFromStart;
        seifer.Click!();
        Assert.NotEqual(before, draft.Characters.Single(character => character.CharacterId == 6).AvailableFromStart);
        Assert.Equal(75, draft.GlobalLevel);
        Assert.All(Descendants(menus.BuildView().Root).OfType<UiNumericSlider>().Where(slider => slider.Id?.StartsWith("level-", StringComparison.Ordinal) == true), slider => Assert.True(slider.ReadOnly));
    }

    [Fact]
    public void ResetRequiresReviewAndCancelledResetPreservesCustomizedValues()
    {
        (MenuController menus, NewGamePlusDraft draft) = Create();
        draft.SetLevel(0, 38);
        menus.Navigate(MenuInput.Right);
        Assert.Equal("new-game-plus-reset", menus.ScreenId);
        Assert.Equal("cancel", menus.SelectedEntry!.Id);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("new-game-plus", menus.ScreenId);
        Assert.Equal(38, draft.Characters.Single(character => character.CharacterId == 0).IndividualLevel);
        Assert.Equal(NewGamePlusPreset.Custom, draft.CharacterPreset);
    }

    private static (MenuController Menus, NewGamePlusDraft Draft) Create(int scale = 100, int fontSize = 24)
    {
        GameState source = CompletedSaveFixture.State();
        GameState native = CompletedSaveFixture.State() with { Ff8Completion = null };
        NewGamePlusDraft draft = new(source, native, new());
        MenuData data = new();
        data.Settings["Text scale"] = scale + "%";
        MenuController menus = new(() => data);
        UiPreferences preferences = new() { TextScale = scale, UiFontSize = fontSize };
        menus.ConnectPreferences(() => preferences);
        menus.OpenNewGamePlus(draft, _ => { }, () => { });
        return (menus, draft);
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
}
