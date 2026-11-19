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
        foreach (string identity in new[] { "new-game-plus.preset.stored", "new-game-plus.preset.native", "new-game-plus.preset.empty", "new-game-plus.preset.custom", "new-game-plus.category.characters", "new-game-plus.available", "new-game-plus.configure", "ngplus.completed", "ngplus.overwrite.message", "ngplus.completion.prompt" })
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
    public void MainControlsFitMinimumViewportAndExposeMouseConfig(int scale)
    {
        (MenuController menus, _) = Create(scale);
        UiView view = menus.BuildView();
        view.Resize(960, 672);
        UiElement[] controls = Descendants(view.Root).Where(element => element.Id?.StartsWith("config-", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(7, controls.Length);
        Assert.All(controls, element => Assert.False(element.Bounds.IsEmpty));
        Assert.Equal(2, Descendants(view.Root).OfType<UiNumericSlider>().Count());
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Right);
        Assert.Equal("exp", menus.SelectedEntry!.Id);
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
        seifer.Click!();
        Assert.True(draft.Characters.Single(character => character.CharacterId == 6).AvailableFromStart);
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

    private static (MenuController Menus, NewGamePlusDraft Draft) Create(int scale = 100)
    {
        GameState source = CompletedSaveFixture.State();
        GameState native = CompletedSaveFixture.State() with { Ff8Completion = null };
        NewGamePlusDraft draft = new(source, native, new());
        MenuData data = new();
        data.Settings["Text scale"] = scale + "%";
        MenuController menus = new(() => data);
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
