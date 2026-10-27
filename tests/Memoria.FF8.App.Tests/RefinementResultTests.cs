using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class RefinementResultTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Shared_magic_preview_shows_result_stock_without_character_ownership(int kind)
    {
        MenuData data = Data();
        data.SharedMagicStock = true;
        data.Recipes.Add(Recipe((byte)kind) with { ResultStock = 73, ResultStockByCharacter = new Dictionary<string, int> { ["squall"] = 9 } });
        UiElement root = OpenRecipes(data).BuildView().Root;
        string[] labels = Labels(ResultPanel(root));
        Assert.Contains(labels, text => System.Text.RegularExpressions.Regex.IsMatch(text, @"^Owned\s+73$"));
        Assert.DoesNotContain(labels, text => text.Contains("Squall", StringComparison.Ordinal) || text.Contains("Rinoa", StringComparison.Ordinal));
        Assert.Equal(new[] { "19" }, Commands(root).SecondaryItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Personal_magic_preview_lists_every_character_and_defaults_missing_stock_to_zero(int kind)
    {
        MenuData data = Data();
        data.Characters.Add(new MenuCharacter { Id = "zell", Name = "Zell" });
        data.Recipes.Add(Recipe((byte)kind) with
        {
            ResultStock = 73,
            ResultStockByCharacter = new Dictionary<string, int> { ["squall"] = 17, ["rinoa"] = 42, ["Squall"] = 99 }
        });
        UiElement root = OpenRecipes(data).BuildView().Root;
        string[] labels = Labels(ResultPanel(root));
        Assert.Contains(labels, text => CharacterStock(text, "Squall", 17));
        Assert.Contains(labels, text => CharacterStock(text, "Rinoa", 42));
        Assert.Contains(labels, text => CharacterStock(text, "Zell", 0));
        Assert.DoesNotContain(labels, text => System.Text.RegularExpressions.Regex.IsMatch(text, @"^Owned\s+19$"));
        Assert.Equal(new[] { "19" }, Commands(root).SecondaryItems);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void Nonmagic_preview_shows_output_owned_count_and_preserves_input_list_count(int kind)
    {
        MenuData data = Data();
        data.Recipes.Add(Recipe((byte)kind) with { ResultStock = 61 });
        UiElement root = OpenRecipes(data).BuildView().Root;
        string[] labels = Labels(ResultPanel(root));
        Assert.Contains(labels, text => System.Text.RegularExpressions.Regex.IsMatch(text, @"^Owned\s+61$"));
        Assert.DoesNotContain(labels, text => System.Text.RegularExpressions.Regex.IsMatch(text, @"^Owned\s+19$"));
        Assert.Equal(new[] { "19" }, Commands(root).SecondaryItems);
    }

    [Fact]
    public void Ability_list_gives_every_ability_its_menu_category_icon()
    {
        MenuData data = Data();
        data.RefinementAbilities.AddRange(["T Mag-RF", "Card Mod", "Tool-RF"]);
        MenuController menu = new(() => data);
        menu.Open("ability");
        UiList commands = Commands(menu.BuildView().Root);
        Assert.Equal(3, commands.Items.Count);
        Assert.Equal(Enumerable.Repeat(NativeAbilityMenuMetadata.Icon(92), 3), commands.Images);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recipe_list_uses_ingredient_icon_and_preview_uses_result_icon(bool explicitIngredientIcon)
    {
        UiImageSource itemIcon = new(UiImageKind.Icon, 198);
        UiImageSource ingredientIcon = new(UiImageKind.Card, 7);
        UiImageSource resultIcon = new(UiImageKind.Icon, 200);
        MenuData data = Data();
        data.Items.Add(new MenuItem { Id = "input", Name = "Input", Quantity = 19, Icon = itemIcon });
        data.Recipes.Add(Recipe(1) with { IngredientIcon = explicitIngredientIcon ? ingredientIcon : null, ResultIcon = resultIcon });
        UiElement root = OpenRecipes(data).BuildView().Root;
        Assert.Equal(new UiImageSource?[] { explicitIngredientIcon ? ingredientIcon : itemIcon }, Commands(root).Images);
        Assert.Contains(Descendants(ResultPanel(root)).OfType<UiImage>(), image => image.Source == resultIcon);
    }

    [Theory]
    [InlineData(640, 448, 100, false)]
    [InlineData(640, 448, 150, false)]
    [InlineData(640, 448, 150, true)]
    [InlineData(1280, 720, 150, false)]
    public void Result_stock_and_icons_render_inside_the_preview_panel(int width, int height, int scale, bool shared)
    {
        using SkiaSharp.SKBitmap bitmap = new(width, height);
        using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
        MenuData data = Data();
        data.SharedMagicStock = shared;
        data.Recipes.Add(Recipe(0) with
        {
            ResultStock = 73,
            ResultStockByCharacter = new Dictionary<string, int> { ["squall"] = 17, ["rinoa"] = 42 },
            IngredientIcon = new UiImageSource(UiImageKind.Icon, 223),
            ResultIcon = new UiImageSource(UiImageKind.Icon, 224)
        });
        MenuController menu = OpenRecipes(data);
        UiPreferences preferences = new() { TextScale = scale };
        menu.ConnectPreferences(() => preferences, _ => { });
        UiView view = menu.BuildView();
        view.Resize(width, height);
        canvas.BeginFrame(TimeSpan.Zero);
        view.Draw(canvas, menu.CreateTheme());
        UiPanel panel = ResultPanel(view.Root);
        Assert.All(Descendants(panel).Where(element => element is UiLabel or UiImage), element =>
        {
            Assert.True(element.Bounds.Width > 0 && element.Bounds.Height > 0);
            Assert.True(element.Bounds.X >= panel.Bounds.X && element.Bounds.Right <= panel.Bounds.Right + 0.1f);
            Assert.True(element.Bounds.Y >= panel.Bounds.Y && element.Bounds.Bottom <= panel.Bounds.Bottom + 0.1f);
        });
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MENU_UI_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            bitmap.SavePng(Path.Combine(output, $"refinement-result-{width}x{height}-{scale}-{shared}.png"));
        }
    }

    private static MenuData Data()
    {
        MenuData data = new();
        data.Characters.Add(new MenuCharacter { Id = "squall", Name = "Squall", InParty = true });
        data.Characters.Add(new MenuCharacter { Id = "rinoa", Name = "Rinoa", InParty = true });
        return data;
    }

    private static MenuRecipe Recipe(byte kind)
    {
        return new("recipe", "Refine", "input", 2, "output", "Output", 5)
        {
            NativeKind = kind,
            IngredientName = "Input",
            IngredientStock = 19,
            MaximumBatches = new Dictionary<int, int> { [0] = 9 }
        };
    }

    private static MenuController OpenRecipes(MenuData data)
    {
        MenuController menu = new(() => data);
        menu.Open("ability");
        menu.Navigate(MenuInput.Confirm);
        return menu;
    }

    private static bool CharacterStock(string text, string name, int stock)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text, $"^{name}\\s*[-—]\\s*{stock}$");
    }

    private static UiList Commands(UiElement root)
    {
        return Descendants(root).OfType<UiList>().Single(list => list.Id == "commands");
    }

    private static UiPanel ResultPanel(UiElement root)
    {
        return Descendants(root).OfType<UiPanel>().Single(panel => panel.GridRow == 1 && panel.GridColumn == 1);
    }

    private static string[] Labels(UiElement root)
    {
        return Descendants(root).OfType<UiLabel>().Select(label => label.Text).ToArray();
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
