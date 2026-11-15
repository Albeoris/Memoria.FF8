using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class InventoryBookTests
{
    [Theory]
    [InlineData(SceneKind.Field, MenuPresentation.Classic, 1600, 900)]
    [InlineData(SceneKind.WorldMap, MenuPresentation.Vector, 900, 1600)]
    public void EnabledGirlNextDoorOpensAndClosesWithoutConsumingInventory(SceneKind scene, MenuPresentation presentation, int width, int height)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int itemId = InventoryBookArtwork.GirlNextDoorItemId;
        string id = $"item-{itemId}";
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(itemId, 2, out _);
        GameState state = new() { Scene = scene, Economy = economy };
        List<MenuAction> actions = [];
        ModCatalogSnapshot active = new(1, [], [BundledCover()]);
        ModCatalogSnapshot current = new(0, [], []);
        catalog.FindBookArtwork = item => InventoryBookArtwork.Find(current, item);
        MenuController controller = new(() => LiveMenuData.Build(state, catalog: catalog), actions.Add) { Presentation = presentation };
        controller.Open("item-use");
        Assert.False(Assert.Single(controller.Entries, entry => entry.Id == id).Enabled);
        Assert.Equal(catalog.ItemIcon(itemId), Assert.Single(controller.Data.Items, item => item.Id == id).Icon);

        current = active;
        controller.InvalidateLiveData();
        controller.SelectEntry("item-use", id);
        for (int read = 0; read < 2; read++)
        {
            controller.Navigate(MenuInput.Confirm);
            Assert.True(controller.IsBookOpen);
            Assert.Equal(new UiImageSource(UiImageKind.Icon, 228), controller.Item?.Icon);
            UiView view = controller.BuildView();
            view.Resize(width, height);
            UiImage cover = Assert.Single(Descendants(view.Root).OfType<UiImage>());
            Assert.Equal(new UiImageSource(UiImageKind.BookCover, itemId), cover.Source);
            Assert.Equal(view.Root.Bounds, cover.Bounds);
            Assert.All(Descendants(view.Root).OfType<UiPanel>(), panel => Assert.False(panel.DrawBackground));
            controller.Navigate(MenuInput.Confirm);
            Assert.True(controller.IsBookOpen);
            controller.Navigate(MenuInput.Cancel);
            Assert.Equal("item-use", controller.ScreenId);
            Assert.Equal(id, controller.SelectedEntry?.Id);
            Assert.Equal(2, controller.Item?.Quantity);
        }
        Assert.Empty(actions);
        Assert.Equal(2, economy.Items.Single(item => item.ItemId == itemId).Count);
    }

    [Fact]
    public void BundledCoverRetainsTransparentArtworkAndFitsTheViewport()
    {
        ModCatalogSnapshot catalog = new(1, [], [BundledCover()]);
        MenuArtworkProvider artwork = new("", findBookArtwork: item => InventoryBookArtwork.Find(catalog, item));
        var image = artwork.Read(new UiImageSource(UiImageKind.BookCover, InventoryBookArtwork.GirlNextDoorItemId));
        Assert.Equal((1215, 1295), (image.Width, image.Height));
        Assert.Contains(image.Pixels, pixel => pixel.A == 0);
        Assert.Contains(image.Pixels, pixel => pixel.A == 255);
        UiRect fitted = UiImage.Fit(new UiRect(0, 0, 1600, 900), image.Width, image.Height);
        Assert.Equal(1600f, fitted.X * 2 + fitted.Width, 3);
        Assert.Equal(0f, fitted.Y, 3);
        Assert.Equal(900f, fitted.Height, 3);
        Assert.Equal((float)image.Width / image.Height, fitted.Width / fitted.Height, 5);
    }

    [Fact]
    public void HostPublishesTheEnabledComponentAndRejectsACorruptedReplacement()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("memoria-inventory-book-");
        try
        {
            string app = Path.Combine(root.FullName, "App");
            string bundled = Path.Combine(app, "BundledMods", "Memoria", "Girl Next Door");
            Directory.CreateDirectory(bundled);
            File.WriteAllText(Path.Combine(bundled, "mod.ini"), "enabled = 0");
            File.Copy(BundledCoverPath, Path.Combine(bundled, "GirlNextDoor.png"));
            string mods = Path.Combine(root.FullName, "Mods");
            using GameResourceHost host = new(TestResources.GameDirectory, app, watch: false, modsDirectory: mods);
            string packageId = InventoryBookArtwork.GirlNextDoorPackageId;
            int itemId = InventoryBookArtwork.GirlNextDoorItemId;
            Assert.Contains(host.Mods.Catalog.Packages, package => package.Id == packageId && package.Status == ModEffectiveStatus.Disabled);
            Assert.Null(InventoryBookArtwork.Find(host.Mods.Catalog, itemId));

            BehaviorModSetting setting = Assert.Single(host.Mods.ReadBehaviorSettings(ModRuntime.MemoriaPackageId), setting => setting.Parameter == "Girl Next Door");
            Assert.True(host.Mods.SetBehaviorSettings(ModRuntime.MemoriaPackageId, [setting with { Value = 1 }]).Success);
            Assert.True(host.Pump(force: true));
            Assert.Contains(host.Mods.Catalog.Packages, package => package.Id == packageId && package.Status == ModEffectiveStatus.Active);
            ModFile published = Assert.IsType<ModFile>(InventoryBookArtwork.Find(host.Mods.Catalog, itemId));

            string local = Path.Combine(mods, "Memoria", "Girl Next Door");
            Directory.CreateDirectory(local);
            File.WriteAllText(Path.Combine(local, "GirlNextDoor.png"), "invalid");
            host.Mods.RequestRefresh();
            Assert.False(host.Pump(force: true));
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidBookArtwork" && diagnostic.PackageId == packageId);
            Assert.Same(published, InventoryBookArtwork.Find(host.Mods.Catalog, itemId));

            Assert.True(host.Mods.SetBehaviorSettings(ModRuntime.MemoriaPackageId, [setting with { Value = 0 }]).Success);
            Assert.True(host.Pump(force: true));
            Assert.Null(InventoryBookArtwork.Find(host.Mods.Catalog, itemId));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static string BundledCoverPath => Path.Combine(AppContext.BaseDirectory, "BundledMods", "Memoria", "Girl Next Door", "GirlNextDoor.png");

    private static ModFile BundledCover() => new(InventoryBookArtwork.GirlNextDoorPackageId, ModFormat.Memoria, "GirlNextDoor.png", 1, () => File.ReadAllBytes(BundledCoverPath));

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
