using System.Collections.Immutable;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ChocoboWorldUiTests
{
    [Theory]
    [InlineData(GameDataEdition.Classic, ChocoboWorldMode.NativeImport)]
    [InlineData(GameDataEdition.Remastered, ChocoboWorldMode.AngeloSearch)]
    public void Legacy_mode_is_initialized_once_from_assets(GameDataEdition edition, ChocoboWorldMode expected)
    {
        GameState legacy = new();
        GameState migrated = ChocoboWorldSettings.Migrate(legacy, edition);
        Assert.Null(legacy.GameplayPreferences.ChocoboWorldMode);
        Assert.Equal(expected, migrated.GameplayPreferences.ChocoboWorldMode);
        Assert.Equal(expected, ChocoboWorldSettings.Migrate(migrated, edition == GameDataEdition.Classic ? GameDataEdition.Remastered : GameDataEdition.Classic).GameplayPreferences.ChocoboWorldMode);
    }

    [Fact]
    public void Enum_choice_is_independent_of_hardcore_boolean_rules()
    {
        Assert.Contains("Chocobo World", NewGameSettings.Ids);
        Assert.False(NewGameSettings.IsLocked("Chocobo World", BattleDifficulty.Hardcore));
        Assert.Equal(ChocoboWorldMode.Memoria, (new GameplayPreferences { Difficulty = BattleDifficulty.Hardcore, ChocoboWorldMode = ChocoboWorldMode.Memoria }).NormalizeForDifficulty().ChocoboWorldMode);
    }

    [Fact]
    public void Native_export_refuses_mode_and_reward_history()
    {
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { GameplayPreferences = new() { ChocoboWorldMode = ChocoboWorldMode.NativeImport } }));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(new GameState { ChocoboRewards = new() { AcquiredUniqueRewardIds = ImmutableHashSet.Create("ribbon") } }));
    }

    [Fact]
    public void Declining_claim_and_back_are_no_ops_and_offer_remains_available()
    {
        int claims = 0;
        MenuController menus = CreateMenus(() =>
        {
            claims++;
            return "Added";
        });
        menus.Open("config-chocobo");
        menus.Execute(new("chocobo-claim-open"));
        Assert.Equal("no", menus.SelectedEntry!.Id);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("config-chocobo", menus.ScreenId);
        Assert.Equal(0, claims);
        menus.Execute(new("chocobo-claim-open"));
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(0, claims);
        menus.Execute(new("chocobo-claim-open"));
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(1, claims);
        Assert.Equal("chocobo-result", menus.ScreenId);
        menus.Execute(new("chocobo-claim-commit"));
        Assert.Equal(1, claims);
    }

    [Fact]
    public void Completed_claim_has_close_instead_of_yes()
    {
        MenuController menus = CreateMenus(() => throw new InvalidOperationException(), obtained: true);
        menus.Open("config-chocobo");
        menus.ShowChocoboClaim();
        Assert.Equal("close", Assert.Single(menus.Entries).Id);
        Assert.Contains("All exclusive items have already been obtained.", menus.CurrentScreen.Details(menus));
    }

    [Fact]
    public void Import_preview_cancel_and_empty_discovery_are_safe()
    {
        int commits = 0;
        MenuController menus = CreateMenus(() => "");
        menus.ConnectChocoboWorld(() => [], () => "source", _ => new(["Boko level 10 → 12"], [new("Potion", "Class D · 2 incoming")], true), () =>
        {
            commits++;
            return "Added 2 Potions";
        }, () => new([], false), () => "");
        menus.Open("config-chocobo");
        menus.Execute(new("chocobo-import-open"));
        Assert.Contains("No saves found. Use Browse to choose a file.", menus.CurrentScreen.Details(menus));
        menus.Execute(new("chocobo-import-browse"));
        Assert.Equal("cancel", menus.SelectedEntry!.Id);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(0, commits);
        menus.Execute(new("chocobo-import-preview", "source"));
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(1, commits);
        menus.Execute(new("chocobo-import-commit"));
        Assert.Equal(1, commits);
    }

    [Fact]
    public void Corrupt_source_shows_result_and_does_not_commit()
    {
        MenuController menus = CreateMenus(() => "");
        menus.ConnectChocoboWorld(() => [], () => "source", _ => throw new InvalidDataException("Both banks are invalid."), () => throw new InvalidOperationException(), () => new([], false), () => "");
        menus.Open("config-chocobo");
        menus.Execute(new("chocobo-import-preview", "source"));
        Assert.Equal("chocobo-result", menus.ScreenId);
        Assert.Equal("Both banks are invalid.", menus.Message);
    }

    [Fact]
    public void Foreign_association_requires_distinct_review_with_safe_default()
    {
        int reviews = 0;
        int commits = 0;
        MenuController menus = CreateMenus(() => "");
        menus.ConnectChocoboWorld(() => [], () => null, _ => new(["Current world: 1", "Source world: 2"], [], false, "World identities differ.", true), () =>
        {
            commits++;
            return "Added";
        }, () => new([], false), () => "", () =>
        {
            reviews++;
            return new(["Reviewed source world: 2"], [], true);
        });
        menus.Open("config-chocobo");
        menus.Execute(new("chocobo-import-preview", "foreign"));
        Assert.False(menus.Entries.Single(entry => entry.Id == "import").Enabled);
        menus.Execute(new("chocobo-association-open"));
        Assert.Equal("no", menus.SelectedEntry!.Id);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(0, reviews);
        menus.Execute(new("chocobo-association-open"));
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(1, reviews);
        Assert.Equal(0, commits);
        Assert.Equal("chocobo-import-preview", menus.ScreenId);
        Assert.True(menus.Entries.Single(entry => entry.Id == "import").Enabled);
    }

    [Fact]
    public void Item_preview_keeps_icons_and_keyboard_page_navigation()
    {
        MenuController menus = CreateMenus(() => "");
        ChocoboRewardRow[] rows = Enumerable.Range(0, 30).Select(index => new ChocoboRewardRow("Item " + index, "Ordinary · Class D · Added 1 · Already imported 0 · Overflow 0", new(UiImageKind.Icon, 223))).ToArray();
        menus.ConnectChocoboWorld(() => [], () => null, _ => new(["Boko"], rows, true), () => "Added", () => new([], false), () => "");
        menus.Open("config-chocobo");
        menus.Execute(new("chocobo-import-preview", "source"));
        UiView view = menus.BuildView();
        UiList items = Descendants(view.Root).OfType<UiList>().Single(list => list.Id == "chocobo-items");
        Assert.Equal(rows.Length, items.Images.Count);
        Assert.True(items.PageCount > 1);
        for (int index = 0; index < 3 && view.FocusedElement != items; index++)
        {
            menus.Navigate(MenuInput.Tab);
        }
        Assert.Same(items, view.FocusedElement);
        menus.Navigate(MenuInput.PageDown);
        Assert.True(items.CurrentPage > 0);
    }

    [Fact]
    public void New_game_claim_opens_a_closed_menu_and_cancel_returns_to_play()
    {
        MenuController menus = CreateMenus(() => throw new InvalidOperationException());
        Assert.False(menus.IsOpen);
        menus.ShowChocoboClaim();
        Assert.True(menus.IsOpen);
        Assert.Equal("no", menus.SelectedEntry!.Id);
        menus.Navigate(MenuInput.Cancel);
        Assert.False(menus.IsOpen);
    }

    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void Claim_modal_shows_four_icons_and_legible_enlarged_text(MenuPresentation presentation)
    {
        MenuController menus = CreateMenus(() => "");
        ChocoboRewardRow[] rows = ChocoboRewardCatalog.Entries.Select(entry => new ChocoboRewardRow(entry.DisplayName, entry.Description, new(UiImageKind.Icon, 223))).ToArray();
        menus.ConnectChocoboWorld(() => [], () => null, _ => new([], [], false), () => "", () => new(rows, true), () => "");
        menus.Open("config-chocobo");
        menus.Presentation = presentation;
        menus.Data.Settings["Text scale"] = "150%";
        menus.ShowChocoboClaim();
        UiView view = menus.BuildView();
        Assert.Equal(4, Descendants(view.Root).OfType<UiImage>().Count());
        using SkiaSharp.SKBitmap bitmap = new(1280, 896);
        using BattlePreviewCanvas canvas = new(bitmap);
        foreach (UiLabel label in Descendants(view.Root).OfType<UiLabel>())
        {
            UiSize measured = canvas.MeasureText(label.Text, label.FontSize, label.Bounds.Width, menus.CreateTheme().FontFamily);
            Assert.True(measured.Height <= label.Bounds.Height, $"{label.Text}: {measured.Height} > {label.Bounds.Height}");
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static MenuController CreateMenus(Func<string> claim, bool obtained = false)
    {
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Chocobo World"] = "Memoria";
        MenuController menus = new(() => data);
        menus.ConnectChocoboWorld(() => [], () => null, _ => new([], [], false), () => "", () => new([new("Ribbon", "Teaches Ribbon", Obtained: obtained)], !obtained), claim);
        return menus;
    }
}
