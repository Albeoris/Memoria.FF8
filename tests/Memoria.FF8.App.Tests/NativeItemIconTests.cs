using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeItemIconTests
{
    [Fact]
    public void InstalledItemTypesResolveToOriginalMenuSprites()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(assets.GameDirectory);
        int[] kinds = [0, 0, 0, 1, 1, 1, 2, 3, 4, 5, 6, 1, 3, 3, 3, 3, 6, 1, 1, 6, 6, 0];
        HashSet<int> reachedTypes = [];
        for (int itemId = 1; catalog.FieldItem(itemId) is { } item; itemId++)
        {
            reachedTypes.Add(item.Type);
            UiImageSource source = Assert.IsType<UiImageSource>(catalog.ItemIcon(itemId));
            Assert.Equal(new UiImageSource(UiImageKind.Icon, 223 + kinds[item.Type]), source);
            Assert.Contains(artwork.GetIcon(source.Id).Pixels, pixel => pixel.A != 0);
        }
        Assert.Contains(21, reachedTypes);
        Assert.Equal(kinds.Select(kind => (byte)kind), NativeMagazineArtwork.ReadItemIconKinds(File.ReadAllBytes(Path.Combine(assets.GameDirectory, "FF8_EN.exe"))));
        Assert.Null(catalog.ItemIcon(0));
        Assert.Null(catalog.ItemIcon(-1));
        Assert.Null(catalog.ItemIcon(int.MaxValue));
    }

    [Theory]
    [InlineData("item", "inventory-preview")]
    [InlineData("item-use", "commands")]
    [InlineData("item-rearrange", "commands")]
    [InlineData("item-target", "inventory-preview")]
    public void LiveInventoryRetainsIconsThroughMenuListProjection(string screen, string listId)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(21, 1, out _).AddItem(60, 1, out _).AddItem(177, 1, out _).AddItem(100, 1, out _);
        MenuData data = LiveMenuData.Build(new GameState { Scene = SceneKind.Field, Economy = economy }, catalog: catalog);
        Assert.NotEmpty(data.Items);
        foreach (MenuItem item in data.Items)
        {
            int itemId = int.Parse(item.Id.AsSpan("item-".Length), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(catalog.ItemIcon(itemId), item.Icon);
            Assert.NotNull(item.Icon);
        }
        MenuController controller = new(() => data, _ => { });
        controller.Open(screen);
        UiList list = Assert.Single(Descendants(controller.BuildView().Root).OfType<UiList>(), value => value.Id == listId);
        Assert.Equal(data.Items.Select(item => item.Icon), list.Images);
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
