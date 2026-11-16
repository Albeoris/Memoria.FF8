using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class InventoryBookTests
{
    private const int FixtureItem = 222;
    private const string FixturePackage = "Memoria/FixtureBook";
    private const string FixtureImage = "art/covers/example.png";
    [Theory]
    [InlineData(SceneKind.Field, MenuPresentation.Classic, 1600, 900)]
    [InlineData(SceneKind.WorldMap, MenuPresentation.Vector, 900, 1600)]
    public void EnabledBookModOpensAndClosesWithoutConsumingInventory(SceneKind scene, MenuPresentation presentation, int width, int height)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int itemId = FixtureItem;
        string id = $"item-{itemId}";
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(itemId, 2, out _);
        GameState state = new() { Scene = scene, Economy = economy };
        List<MenuAction> actions = [];
        Assert.True(BookArtworkMods.TryPrepare(new(1, [], [FixtureManifest(), FixtureCover()]), out BookArtworkMods active, out _));
        BookArtworkMods current = BookArtworkMods.Empty;
        catalog.FindBookArtwork = item => current.Find(item);
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
    public void BookCoverRetainsTransparentArtworkAndFitsTheViewport()
    {
        Assert.True(BookArtworkMods.TryPrepare(new(1, [], [FixtureManifest(), FixtureCover()]), out BookArtworkMods catalog, out _));
        MenuArtworkProvider artwork = new("", findBookArtwork: catalog.Find);
        var image = artwork.Read(new UiImageSource(UiImageKind.BookCover, FixtureItem));
        Assert.Equal((2, 3), (image.Width, image.Height));
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
            string mods = Path.Combine(root.FullName, "Mods");
            string local = Path.Combine(mods, FixturePackage);
            string image = Path.Combine(local, FixtureImage);
            Directory.CreateDirectory(Path.GetDirectoryName(image)!);
            string manifest = Path.Combine(local, BookArtworkMods.ManifestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            File.WriteAllText(Path.Combine(local, "mod.ini"), "enabled = 0");
            File.WriteAllBytes(image, CreateCoverFixture());
            File.WriteAllBytes(manifest, FixtureManifest().ReadBytes());
            using GameResourceHost host = new(TestResources.GameDirectory, app, watch: false, modsDirectory: mods);
            string packageId = FixturePackage;
            int itemId = FixtureItem;
            Assert.Contains(host.Mods.Catalog.Packages, package => package.Id == packageId && package.Status == ModEffectiveStatus.Disabled);
            Assert.Null(host.BookArtwork.Find(itemId));

            Assert.True(host.Mods.SetEnabled(packageId, true).Success);
            Assert.True(host.Pump(force: true));
            Assert.Contains(host.Mods.Catalog.Packages, package => package.Id == packageId && package.Status == ModEffectiveStatus.Active);
            ModFile published = Assert.IsType<ModFile>(host.BookArtwork.Find(itemId));

            File.WriteAllText(image, "invalid");
            host.Mods.RequestRefresh();
            Assert.False(host.Pump(force: true));
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidBookArtwork" && diagnostic.PackageId == packageId);
            Assert.Same(published, host.BookArtwork.Find(itemId));

            Assert.True(host.Mods.SetEnabled(packageId, false).Success);
            Assert.True(host.Pump(force: true));
            Assert.Null(host.BookArtwork.Find(itemId));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static byte[] CreateCoverFixture() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAADCAYAAAC56t6BAAAAFElEQVR4nGNgYGBgMEhY8B9EozIAR20GjgleNaUAAAAASUVORK5CYII=");

    [Fact]
    public void FirstActiveBindingWinsAndDisabledPackagesFallThrough()
    {
        const string other = "Memoria/OtherBook";
        ModFile first = FixtureCover();
        ModFile second = new(other, ModFormat.Memoria, FixtureImage, 1, CreateCoverFixture);
        ModFile otherManifest = new(other, ModFormat.Memoria, BookArtworkMods.ManifestPath, 1, () => FixtureManifest().ReadBytes());
        Assert.True(BookArtworkMods.TryPrepare(new(1, [], [FixtureManifest(), first, otherManifest, second]), out BookArtworkMods active, out _));
        Assert.Same(first, active.Find(FixtureItem));
        Assert.Null(active.Find(163));
        Assert.True(BookArtworkMods.TryPrepare(new(2, [], [otherManifest, second]), out BookArtworkMods disabled, out _));
        Assert.Same(second, disabled.Find(FixtureItem));
    }

    [Theory]
    [InlineData("""{"schemaVersion":2,"items":[]}""")]
    [InlineData("""{"schemaVersion":1,"items":[{"itemId":222,"image":"art/covers/example.png"},{"itemId":222,"image":"art/covers/example.png"}]}""")]
    [InlineData("""{"schemaVersion":1,"items":[{"itemId":256,"image":"art/covers/example.png"}]}""")]
    [InlineData("""{"schemaVersion":1,"items":[{"itemId":222,"image":"../example.png"}]}""")]
    [InlineData("""{"schemaVersion":1,"items":[{"itemId":222,"image":"art/covers/Example.png"}]}""")]
    public void InvalidBindingsDoNotEnterTheSnapshot(string json)
    {
        ModFile manifest = new(FixturePackage, ModFormat.Memoria, BookArtworkMods.ManifestPath, 1, () => System.Text.Encoding.UTF8.GetBytes(json));
        Assert.False(BookArtworkMods.TryPrepare(new(1, [], [manifest, FixtureCover()]), out BookArtworkMods rejected, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(rejected.Find(FixtureItem));
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Memoria.InvalidBookArtwork", diagnostic.Code);
        Assert.Equal(FixturePackage, diagnostic.PackageId);
    }

    [Fact]
    public void BindingCannotUseImagesOwnedByAnotherPackage()
    {
        ModFile foreign = new("Memoria/Foreign", ModFormat.Memoria, FixtureImage, 1, CreateCoverFixture);
        Assert.False(BookArtworkMods.TryPrepare(new(1, [], [FixtureManifest(), foreign]), out _, out _));
    }

    private static ModFile FixtureManifest() => new(FixturePackage, ModFormat.Memoria, BookArtworkMods.ManifestPath, 1, () => System.Text.Encoding.UTF8.GetBytes($$"""{"schemaVersion":1,"items":[{"itemId":{{FixtureItem}},"image":"{{FixtureImage}}"}]}"""));

    private static ModFile FixtureCover() => new(FixturePackage, ModFormat.Memoria, FixtureImage, 1, CreateCoverFixture);

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
