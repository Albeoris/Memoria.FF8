using System.Diagnostics;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class InventoryItemOverrideTests : IDisposable
{
    private const int FixtureItem = 1;
    private const string FixtureId = "another-mod";
    private const string FixturePackage = "Memoria/" + FixtureId;
    private const string FixtureDocument = "items/chapters/retitled.json";
    private const string FixtureImage = "items/chapters/illustration.png";
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("memoria-item-override-");

    [Theory]
    [InlineData(SceneKind.Field, MenuPresentation.Classic, 1600, 900)]
    [InlineData(SceneKind.WorldMap, MenuPresentation.Vector, 900, 1600)]
    public void ActionOpensAndClosesWithoutConsumingTheNativeItem(SceneKind scene, MenuPresentation presentation, int width, int height)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int itemId = FixtureItem;
        string id = $"item-{itemId}";
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(itemId, 2, out _);
        int stock = economy.Items.Single(item => item.ItemId == itemId).Count;
        GameState state = new() { Scene = scene, Economy = economy };
        List<MenuAction> actions = [];
        string package = WritePackage("any-folder", """{"id":1,"type":"Magazine","use":{"action":"ShowImage","image":"./illustration.png"}}""");
        ItemOverrideMods active = Prepare(Entry(FixturePackage, package, FixtureDocument), Entry(FixturePackage, package, FixtureImage));
        Assert.True(catalog.FieldItem(itemId)!.SupportsCharacterUse);
        byte originalType = catalog.FieldItem(itemId)!.Type;

        ItemOverride? change = null;
        catalog.FindItemOverride = _ => change;
        MenuController controller = new(() => LiveMenuData.Build(state, catalog: catalog), actions.Add) { Presentation = presentation };
        controller.Open("item-use");
        Assert.Equal(originalType, catalog.ItemDefinition(itemId)!.Type);

        // Type by itself keeps the original menu action. Use by itself keeps the original type.
        ItemOverride patch = active.Find(itemId)!;
        change = patch with { Use = null };
        Assert.Equal((byte)ItemType.Magazine, catalog.ItemDefinition(itemId)!.Type);
        Assert.True(catalog.ItemDefinition(itemId)!.NativeUse.SupportsCharacterUse);
        Assert.Null(catalog.ItemDefinition(itemId)!.Use);
        change = patch with { Type = null };
        Assert.Equal(originalType, catalog.ItemDefinition(itemId)!.Type);
        Assert.NotNull(catalog.ItemDefinition(itemId)!.Use);

        change = patch;
        controller.InvalidateLiveData();
        controller.SelectEntry("item-use", id);
        for (int read = 0; read < 2; read++)
        {
            controller.Navigate(MenuInput.Confirm);
            Assert.True(controller.IsItemImageOpen);
            Assert.Equal(new UiImageSource(UiImageKind.Icon, 228), controller.Item?.Icon);
            UiView view = controller.BuildView();
            view.Resize(width, height);
            UiImage cover = Assert.Single(Descendants(view.Root).OfType<UiImage>());
            Assert.Equal(new UiImageSource(UiImageKind.ItemUseImage, itemId), cover.Source);
            Assert.Equal(view.Root.Bounds, cover.Bounds);
            Assert.All(Descendants(view.Root).OfType<UiPanel>(), panel => Assert.False(panel.DrawBackground));
            controller.Navigate(MenuInput.Confirm);
            Assert.True(controller.IsItemImageOpen);
            controller.Navigate(MenuInput.Cancel);
            Assert.Equal("item-use", controller.ScreenId);
            Assert.Equal(id, controller.SelectedEntry?.Id);
            Assert.Equal(stock, controller.Item?.Quantity);
        }
        Assert.Empty(actions);
        Assert.Equal(stock, economy.Items.Single(item => item.ItemId == itemId).Count);
    }

    [Theory]
    [InlineData("./illustration.png")]
    [InlineData("illustration.png")]
    [InlineData("/items/chapters/illustration.png")]
    [InlineData("../chapters/illustration.png")]
    public void PreparedImageRetainsCanonicalPhysicalIdentityAndSnapshotBytes(string reference)
    {
        string package = WritePackage("interesting folder", $$$"""{"id":1,"use":{"action":"ShowImage","image":"{{{reference}}}"}}""");
        ItemOverrideMods catalog = Prepare(Entry(FixturePackage, package, FixtureDocument), Entry(FixturePackage, package, FixtureImage));
        string imagePath = Path.GetFullPath(FixtureImage, package);
        Assert.Equal(imagePath, catalog.Find(FixtureItem)?.Use?.Image.AbsolutePath);
        File.WriteAllText(imagePath, "live bytes replaced after preparation");

        MenuArtworkProvider artwork = new("", findItemUseImage: itemId => catalog.Find(itemId)?.Use?.Image);
        var image = artwork.Read(new UiImageSource(UiImageKind.ItemUseImage, FixtureItem));
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
    public void HostKeepsTheLastGoodGenerationAfterARejectedReload()
    {
        string app = Path.Combine(root.FullName, "App");
        string mods = Path.Combine(root.FullName, "Mods");
        string local = WritePackage("Mods/Memoria/renamable-folder", """{"id":1,"use":{"action":"ShowImage","image":"./illustration.png"}}""");
        string image = Path.GetFullPath(FixtureImage, local);
        File.WriteAllText(Path.Combine(local, "mod.ini"), "enabled = 0");
        using GameResourceHost host = new(TestResources.GameDirectory, app, watch: false, modsDirectory: mods);
        Assert.Contains(host.Mods.Catalog.Packages, package => package.Id == FixturePackage && package.Status == ModEffectiveStatus.Disabled);
        Assert.Null(host.ItemOverrides.Find(FixtureItem));

        Assert.True(host.Mods.SetEnabled(FixturePackage, true).Success);
        Assert.True(host.Pump(force: true));
        ModFile published = Assert.IsType<ModFile>(host.ItemOverrides.Find(FixtureItem)?.Use?.Image);

        File.WriteAllText(image, "invalid");
        host.Mods.RequestRefresh();
        Assert.False(host.Pump(force: true));
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidItemOverride" && diagnostic.PackageId == FixturePackage);
        Assert.Same(published, host.ItemOverrides.Find(FixtureItem)?.Use?.Image);
        Assert.Equal(CreateImageFixture(), published.ReadBytes());

        Assert.True(host.Mods.SetEnabled(FixturePackage, false).Success);
        Assert.True(host.Pump(force: true));
        Assert.Null(host.ItemOverrides.Find(FixtureItem));
    }

    [Fact]
    public void DescriptionLanguageFollowsTheCommittedRussianLocalization()
    {
        string game = TestResources.GameDirectory;
        using IResourceSource selected = GameDataSourceFactory.Open(game);
        string sourceLanguage = selected.Descriptor.Language;
        string mods = Path.Combine(root.FullName, "Mods");
        string app = Path.Combine(root.FullName, "App");
        string package = WritePackage("Mods/Memoria/any-root", """{"id":1,"type":"Magazine"}""");
        File.WriteAllText(Path.Combine(package, "mod.json"), """{"id":"another-mod","name":"Fixture","package":"Memoria","description":{"en":"English description","ru":"Russian description"}}""");

        string localization = Path.Combine(mods, "Memoria", "Russian");
        LocalizationProject project = new()
        {
            Manifest = new() { TargetLanguage = "ru", MappingVersion = FieldCorrespondenceCatalog.Version, FieldScriptBaseLanguage = sourceLanguage },
            Entries = [new() { Category = "System", File = "fixture", Key = "$en_fixture_0000", Source = "Original", Target = "Translation" }]
        };
        LocalizationProjectIO.WriteNew(project, localization, "json", "split", TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(localization, "mod.ini"), "enabled = 0");

        using GameResourceHost host = new(game, app, watch: false, modsDirectory: mods);
        NativeModDescriptor descriptor = Assert.Single(host.Mods.Catalog.Packages, candidate => candidate.Id == FixturePackage).Descriptor!;
        Assert.Equal("English description", descriptor.DescriptionForLanguage(host.EffectiveContentLanguage));
        Assert.True(host.Mods.SetLocalizationSelection(["Memoria/Russian"], "ru").Success);
        PumpUntil(() => host.EffectiveContentLanguage == "ru");
        Assert.Equal("Russian description", descriptor.DescriptionForLanguage(host.EffectiveContentLanguage));

        Assert.True(host.Mods.SetLocalizationSelection([], "ru").Success);
        PumpUntil(() => host.EffectiveContentLanguage != "ru");
        Assert.Equal("English description", descriptor.DescriptionForLanguage(host.EffectiveContentLanguage));

        void PumpUntil(Func<bool> condition)
        {
            Assert.True(SpinWait.SpinUntil(() =>
            {
                host.Pump(force: true);
                return condition();
            }, TimeSpan.FromSeconds(20)), string.Join(", ", host.Mods.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }
    }

    [Fact]
    public void CatalogOrderComposesEachPropertyAndDisablingRevealsTheNextValue()
    {
        const string secondId = "Memoria/runner-up";
        const string thirdId = "Memoria/least-preferred";
        string high = WritePackage("a", """{"id":1,"use":{"action":"ShowImage","image":"./illustration.png"}}""");
        string middle = WritePackage("b", """{"id":1,"type":"Magazine","use":{"action":"ShowImage","image":"./illustration.png"}}""");
        string low = WritePackage("c", """{"id":1,"type":"Magazine"}""");
        ModFile preferredImage = Entry(FixturePackage, high, FixtureImage);
        ModFile fallbackImage = Entry(secondId, middle, FixtureImage);
        ModFile[] remaining = [Entry(secondId, middle, FixtureDocument), fallbackImage, Entry(thirdId, low, FixtureDocument)];
        ItemOverrideMods active = Prepare([Entry(FixturePackage, high, FixtureDocument), preferredImage, .. remaining]);
        Assert.Equal(ItemType.Magazine, active.Find(FixtureItem)?.Type);
        Assert.Same(preferredImage, active.Find(FixtureItem)?.Use?.Image);
        ItemOverrideMods disabled = Prepare(remaining);
        Assert.Equal(ItemType.Magazine, disabled.Find(FixtureItem)?.Type);
        Assert.Same(fallbackImage, disabled.Find(FixtureItem)?.Use?.Image);
    }

    [Theory]
    [InlineData("""{"id":0,"type":"Magazine"}""")]
    [InlineData("""{"id":250,"type":"Magazine"}""")]
    [InlineData("""{"id":1}""")]
    [InlineData("""{"id":1,"id":1,"type":"Magazine"}""")]
    [InlineData("""{"id":1,"type":"magazine"}""")]
    [InlineData("""{"id":1,"type":9}""")]
    [InlineData("""{"id":1,"unknown":true}""")]
    [InlineData("""{"id":1,"use":{"action":"ShowImage","image":"../../../outside.png"}}""")]
    [InlineData("""{"id":1,"use":{"action":"ShowImage","image":"/../package-other/items/chapters/illustration.png"}}""")]
    [InlineData("""{"id":1,"use":{"action":"ShowImage","image":"C:/outside/illustration.png"}}""")]
    [InlineData("""{"id":1,"use":{"action":"ShowImage","image":"//server/illustration.png"}}""")]
    [InlineData("""{"id":1,"use":{"action":"ShowImage","image":"file:///another/illustration.png"}}""")]
    [InlineData("""{"id":1,"use":{"action":"ShowImage","image":"./Illustration.png"}}""")]
    [InlineData("""{"id":1,"use":{"action":"Eat","image":"./illustration.png"}}""")]
    [InlineData("""{"id":1,"use":{"action":"ShowImage","image":"./illustration.png","consume":false}}""")]
    public void InvalidDocumentRejectsTheCandidate(string json)
    {
        string local = WritePackage("package", json);
        Reject(Entry(FixturePackage, local, FixtureDocument), Entry(FixturePackage, local, FixtureImage));
    }

    [Fact]
    public void DuplicateDocumentsAndCrossModPathsAreRejected()
    {
        string local = WritePackage("package", """{"id":1,"use":{"action":"ShowImage","image":"./illustration.png"}}""");
        const string duplicate = "items/copy.json";
        File.WriteAllText(Path.GetFullPath(duplicate, local), """{"id":1,"type":"Magazine"}""");
        Reject(Entry(FixturePackage, local, FixtureDocument), Entry(FixturePackage, local, FixtureImage), Entry(FixturePackage, local, duplicate));

        string other = WritePackage("foreign", """{"id":2,"type":"Magazine"}""");
        Reject(Entry(FixturePackage, local, FixtureDocument), Entry("Memoria/foreign", other, FixtureImage));
    }

    [Fact]
    public void LinkedPngCannotPointToAPrefixSibling()
    {
        string local = WritePackage("package", """{"id":1,"use":{"action":"ShowImage","image":"./illustration.png"}}""");
        string sibling = WritePackage("package-other", """{"id":1,"type":"Magazine"}""");
        string link = Path.GetFullPath(FixtureImage, local);
        File.Delete(link);
        try
        {
            File.CreateSymbolicLink(link, Path.GetFullPath(FixtureImage, sibling));
        }
        catch (Exception exception) when (OperatingSystem.IsWindows() && exception is (UnauthorizedAccessException or IOException))
        {
            string junction = Path.Combine(local, "external");
            using Process process = Process.Start(new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c mklink /J \"{junction}\" \"{Path.GetDirectoryName(Path.GetFullPath(FixtureImage, sibling))}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            })!;
            Assert.True(process.WaitForExit(10000));
            Assert.True(process.ExitCode == 0, process.StandardOutput.ReadToEnd());
            try
            {
                File.WriteAllText(Path.GetFullPath(FixtureDocument, local), """{"id":1,"use":{"action":"ShowImage","image":"/external/illustration.png"}}""");
                Reject(Entry(FixturePackage, local, FixtureDocument), Entry(FixturePackage, local, "external/illustration.png"));
            }
            finally
            {
                Directory.Delete(junction);
            }
            return;
        }
        Reject(Entry(FixturePackage, local, FixtureDocument), Entry(FixturePackage, local, FixtureImage));
    }

    private static byte[] CreateImageFixture() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAADCAYAAAC56t6BAAAAFElEQVR4nGNgYGBgMEhY8B9EozIAR20GjgleNaUAAAAASUVORK5CYII=");

    private string WritePackage(string directory, string document)
    {
        string package = Path.Combine(root.FullName, directory);
        string manifest = Path.GetFullPath(FixtureDocument, package);
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.WriteAllText(Path.Combine(package, "mod.json"), """{"id":"another-mod","name":"Any name","package":"Memoria","description":{"en":"A fixture."}}""");
        File.WriteAllText(manifest, document);
        File.WriteAllBytes(Path.GetFullPath(FixtureImage, package), CreateImageFixture());
        return package;
    }

    private static ModFile Entry(string id, string root, string relative)
    {
        string path = Path.GetFullPath(relative, root);
        return new ModFile(id, ModFormat.Memoria, relative, 1, () => File.ReadAllBytes(path)) { AbsolutePath = path };
    }

    private static ModCatalogSnapshot Catalog(ModFile[] files)
    {
        return new(1, files.GroupBy(file => file.PackageId).Select(group =>
        {
            ModFile file = group.First();
            string root = file.AbsolutePath![..^(file.RelativePath.Length + 1)];
            return new ModPackage(group.Key, group.Key, ModFormat.Memoria, true, true, ModEffectiveStatus.Active, root);
        }), files);
    }

    private static ItemOverrideMods Prepare(params ModFile[] files)
    {
        Assert.True(ItemOverrideMods.TryPrepare(Catalog(files), 240, out ItemOverrideMods result, out var diagnostics), string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Message)));
        return result;
    }

    private static void Reject(params ModFile[] files)
    {
        Assert.False(ItemOverrideMods.TryPrepare(Catalog(files), 240, out ItemOverrideMods result, out var diagnostics));
        Assert.Same(ItemOverrideMods.Empty, result);
        ModDiagnostic error = Assert.Single(diagnostics);
        Assert.Equal("Memoria.InvalidItemOverride", error.Code);
        Assert.Equal(FixturePackage, error.PackageId);
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

    public void Dispose()
    {
        root.Delete(recursive: true);
    }
}
