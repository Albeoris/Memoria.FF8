using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class RefinementListTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Available_cards_sort_first_and_insufficient_cards_refuse_selection(bool once)
    {
        MenuData data = new();
        data.Recipes.Add(Recipe("insufficient", 0, once));
        data.Recipes.Add(Recipe("available", 3, once));
        data.Recipes.Add(Recipe("also-insufficient", 0, once));
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open("ability");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(new[] { "available", "insufficient", "also-insufficient" }, menu.Entries.Select(entry => entry.Id));
        menu.Navigate(MenuInput.Down);
        List<MenuSound> sounds = [];
        menu.SoundRequested += sounds.Add;
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(MenuSound.Error, Assert.Single(sounds));
        Assert.Empty(actions);
        Assert.Equal("ability-recipes", menu.ScreenId);
        Assert.Contains("Not enough", menu.Message);
        UiList list = Descendants(menu.BuildView().Root).OfType<UiList>().Single(list => list.Id == "commands");
        Assert.Equal(new[] { true, false, false }, list.EnabledItems);
        Assert.Equal(new[] { "Geezard", "Geezard", "Geezard" }, list.Items);
        Assert.Equal(new[] { "3", "3", "3" }, list.SecondaryItems);
    }

    [Fact]
    public void Removing_the_last_selected_card_keeps_the_remaining_card_selectable()
    {
        MenuData data = new();
        data.Recipes.Add(Recipe("first", 1, true));
        data.Recipes.Add(Recipe("last", 1, true));
        data.SupportedActions.Add("native-refine");
        int conversions = 0;
        MenuController menu = new(() => data, _ =>
        {
            conversions++;
            data.Recipes.RemoveAt(data.Recipes.Count - 1);
        });
        menu.Open("ability");
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(0, menu.SelectedIndex);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(2, conversions);
        Assert.Empty(menu.Entries);
    }

    [Fact]
    public void Recipe_preview_follows_highlight_and_uses_names()
    {
        MenuData data = new();
        data.Recipes.Add(Recipe("first", 1, false));
        data.Recipes.Add(Recipe("second", 1, false) with { IngredientName = "Funguar", ResultName = "M-Stone Piece" });
        MenuController menu = new(() => data);
        menu.Open("ability");
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Down);
        string text = string.Join("\n", Descendants(menu.BuildView().Root).OfType<UiLabel>().Select(label => label.Text));
        Assert.Contains("Funguar", text);
        Assert.Contains("M-Stone Piece", text);
        Assert.DoesNotContain("card-0", text);
    }

    [Theory]
    [InlineData(640, 448, 100)]
    [InlineData(1280, 720, 100)]
    [InlineData(1280, 720, 150)]
    public void Owned_card_list_renders_with_installed_artwork(int width, int height, int scale)
    {
        string directory = TestResources.GameDirectory;
        using Bitmap bitmap = new(width, height);
        using BattlePreviewCanvas canvas = new(bitmap, directory);
        MenuData data = new();
        data.Recipes.Add(Recipe("first", 1, true) with { IngredientQuantity = 1, IngredientStock = 12 });
        data.Recipes.Add(Recipe("second", 0, true) with { IngredientName = "Blobra", IngredientQuantity = 4, IngredientStock = 2, ResultName = "Rune Armlet", ResultQuantity = 1 });
        MenuController menu = new(() => data);
        UiPreferences preferences = new() { TextScale = scale };
        menu.ConnectPreferences(() => preferences, _ => { });
        menu.Open("ability");
        menu.Navigate(MenuInput.Confirm);
        UiView view = menu.BuildView();
        view.Resize(width, height);
        canvas.BeginFrame(TimeSpan.Zero);
        view.Draw(canvas, menu.CreateTheme());
        Assert.All(Descendants(view.Root), element => Assert.True(element.Bounds.Width >= 0 && element.Bounds.Height >= 0));
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MENU_UI_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            bitmap.Save(Path.Combine(output, $"refinement-{width}x{height}-{scale}.png"), ImageFormat.Png);
        }
    }

    private static MenuRecipe Recipe(string id, int maximum, bool once)
    {
        return new(id, "Card Mod", "card-0", 4, "item-1", "Screw", 5) { NativeAbility = 115, NativeRecipeIndex = 0, NativeKind = 4, CardModOnce = once, IngredientName = "Geezard", IngredientStock = 3, MaximumBatches = new Dictionary<int, int> { [0] = maximum } };
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }
}
