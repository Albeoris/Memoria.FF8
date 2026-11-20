using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Msd;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class ModCatalogComparisonTests
{
    [Fact]
    public async Task JunctionMovieCameraUsesTheOriginalTrackAndHonorsTheConfiguredVideoExtension()
    {
        string game = TestResources.GameDirectory;
        CancellationToken token = TestContext.Current.CancellationToken;
        using IResourceSource source = GameDataSourceFactory.Open(game);
        IGameFileSource original = new ResourceService(source).GetSource<IGameFileSource>()!;
        byte[] camera = await File.ReadAllBytesAsync(original.ResolveFile("movies/disc00_00.cam"), token);
        ushort focal = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(camera.AsSpan(8 + 36, 2));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(8 + 36, 2), (ushort)(focal ^ 1));
        string root = Path.Combine(Path.GetTempPath(), "catalog-movie-camera-" + Guid.NewGuid().ToString("N"));
        string payload = Path.Combine(root, "payload");
        string folder = Path.Combine(payload, "movies");
        Directory.CreateDirectory(folder);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(payload, "mod.xml"), "<ModInfo><Name>Movie sample</Name><FFNxConfig><ffmpeg_video_ext>qt</ffmpeg_video_ext></FFNxConfig></ModInfo>", token);
            await File.WriteAllBytesAsync(Path.Combine(folder, "disc00_00.cam"), camera, token);
            await File.WriteAllBytesAsync(Path.Combine(folder, "disc00_00h.qt"), [], token);
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            ModPackage package = new("Junction/synthetic", "Movie sample", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, payload);
            ModPreviewDocument preview = await library.OpenPreviewAsync(null, package, token);
            ModPreviewEntry movie = Assert.Single(preview.Entries, entry => entry.IsMedia);
            using ModPreviewMediaSources media = await preview.OpenMediaAsync(movie, new(game), token);
            Assert.Equal("disc00_00h.avi", media.Before?.Name);
            Assert.Equal("disc00_00h.qt", media.After.Name);

            ModPreviewEntry track = Assert.Single(preview.Entries, entry => entry.Group == "Camera tracks");
            Assert.False(track.IsMedia);
            ModPreviewComparison pair = await preview.CompareAsync(track, new(game), token);
            ModPreviewTextRow row = Assert.Single(pair.TextRows);
            Assert.Contains($"Focal length {focal}", row.Before);
            Assert.Contains($"Focal length {focal ^ 1}", row.After);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task MemoriaItemActionComparesStockItemAndOnlyTheSelectedPackageImage()
    {
        string game = TestResources.GameDirectory;
        string root = Path.Combine(Path.GetTempPath(), "catalog-preview-" + Guid.NewGuid().ToString("N"));
        string payload = Path.Combine(root, "Mods", "Memoria", "Inventory example");
        string mod = Path.Combine(payload, "items");
        Directory.CreateDirectory(mod);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(payload, "mod.json"), """{"id":"inventory-example","name":"Inventory example","package":"Memoria"}""", TestContext.Current.CancellationToken);
            string document = Path.Combine(mod, "example.json");
            await File.WriteAllTextAsync(document, """{"id":1,"type":"Magazine","use":{"action":"ShowImage","image":"sample.png"}}""", TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(mod, "sample.png"), LocalizationArtworkExporter.EncodePng(new PixelImage(1, 1, [new(10, 20, 30)])), TestContext.Current.CancellationToken);
            for (int index = 0; index < 32; index++)
            {
                File.WriteAllBytes(Path.Combine(mod, $"test-{index:00}.bin"), []);
            }
            using ModRuntime mods = new(root, watch: false);
            Assert.True(mods.PreparePending(force: true));
            Assert.True(mods.CommitPending());
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            ModPackage package = new("Memoria/synthetic", "Inventory example", ModFormat.Memoria, null, false, ModEffectiveStatus.Disabled, payload);
            ModPreviewDocument preview = await library.OpenPreviewAsync(null, package, TestContext.Current.CancellationToken);
            ModPreviewEntry entry = Assert.Single(preview.Entries, change => change.Id == "items/example.json");
            ModPreviewComparison pair = await preview.CompareAsync(entry, new(game), TestContext.Current.CancellationToken);
            Assert.False(pair.Before.CanUseItem);
            Assert.Null(pair.Before.Image);
            Assert.True(pair.After.CanUseItem);
            Assert.Equal(new Rgba32(10, 20, 30), Assert.IsType<PixelImage>(pair.After.Image)[0, 0]);
            Assert.Equal("Inventory example", preview.Name);
            Assert.Empty(library.View.Installations);
            Assert.Empty(library.View.Jobs);
            Assert.Equal(34, Directory.GetFiles(mod).Length);
            MenuController menu = new();
            menu.ConnectMods(mods);
            menu.ConnectModLibrary(library);
            menu.ConnectModPreview(new(game));
            menu.Open("mods");
            try
            {
                UiView view = menu.BuildView();
                view.Resize(1280, 720);
                Assert.True(Find(view.Root, "mod-preview").Visible);
                Find(view.Root, "mod-preview").Key(UiKey.Confirm);
                Assert.Equal("mods-preview", menu.ScreenId);
                await WaitForPreviewAsync();
                view = menu.BuildView();
                view.Resize(1280, 720);
                UiList tree = Assert.IsType<UiList>(Find(view.Root, "preview-tree"));
                Assert.True(tree.Visible);
                Assert.Contains("Inventory (34)", tree.Items[0], StringComparison.Ordinal);
                Assert.Contains("Items (34)", tree.Items[1], StringComparison.Ordinal);
                Assert.Equal("example.json", tree.Items[tree.SelectedIndex].Trim());
                Assert.InRange(tree.SelectedIndex, tree.ScrollOffset, tree.ScrollOffset + tree.VisibleRows - 1);
                Assert.True(Find(view.Root, "preview-use").Visible);

                menu.HandleModsWheel((int)tree.Bounds.X + 12, (int)tree.Bounds.Y + 12, -120);
                Assert.Equal(3, tree.ScrollOffset);
                Find(view.Root, "preview-expand").Key(UiKey.Confirm);
                menu.Navigate(MenuInput.Cancel);
                view = menu.BuildView();
                view.Resize(1280, 720);
                tree = Assert.IsType<UiList>(Find(view.Root, "preview-tree"));
                Assert.Equal(3, tree.ScrollOffset);
                Assert.Equal("example.json", tree.Items[tree.SelectedIndex].Trim());

                tree.Key(UiKey.Up);
                tree.Key(UiKey.Confirm);
                view = menu.BuildView();
                view.Resize(1280, 720);
                tree = Assert.IsType<UiList>(Find(view.Root, "preview-tree"));
                Assert.Equal(2, tree.Items.Count);
                Assert.Equal(1, tree.SelectedIndex);
                tree.Pointer(tree.Bounds.X + 100, tree.Bounds.Y + tree.RowHeight * 1.5f);
                view = menu.BuildView();
                view.Resize(1280, 720);
                tree = Assert.IsType<UiList>(Find(view.Root, "preview-tree"));
                Assert.Equal(2, tree.Items.Count);
                Assert.Equal(1, tree.SelectedIndex);
                Assert.True(Find(view.Root, "preview-use").Visible);
                Find(view.Root, "preview-expand").Key(UiKey.Confirm);
                view = menu.BuildView();
                view.Resize(1280, 720);
                Assert.False(Find(view.Root, "preview-tree").Visible);
                Assert.Equal("preview-expand", view.FocusedElement?.Id);
                menu.Navigate(MenuInput.Cancel);
                view = menu.BuildView();
                view.Resize(1280, 720);
                tree = Assert.IsType<UiList>(Find(view.Root, "preview-tree"));
                Assert.True(tree.Visible);
                Assert.Equal(2, tree.Items.Count);
                Assert.Equal(1, tree.SelectedIndex);
                tree.Pointer(tree.Bounds.X + 35, tree.Bounds.Y + tree.RowHeight * 1.5f);
                view = menu.BuildView();
                view.Resize(1280, 720);
                tree = Assert.IsType<UiList>(Find(view.Root, "preview-tree"));
                Assert.Equal(36, tree.Items.Count);
                Assert.Equal(1, tree.SelectedIndex);
                Assert.True(Find(view.Root, "preview-use").Visible);
                menu.Navigate(MenuInput.Cancel);
                Assert.Equal("mods", menu.ScreenId);
            }
            finally
            {
                menu.Close();
            }

            async Task WaitForPreviewAsync()
            {
                for (int retry = 0; retry < 100; retry++)
                {
                    menu.RefreshModView();
                    if (Find(menu.BuildView().Root, "preview-use").Visible)
                    {
                        return;
                    }
                    await Task.Delay(20, TestContext.Current.CancellationToken);
                }
                Assert.Fail("The comparison never exposed the package item action.");
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task JunctionDialogueUsesTheOriginalFieldAtTheMatchingAddress()
    {
        string game = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(game);
        ResourceService resources = new(source);
        using GameAssets assets = new(game, resources: resources);
        string field = assets.LoadField(229).Name;
        ResourceKey key = new($"field/{field}/{field}.msd");
        MsdFile modified = MsdFile.Read(resources.Read(key), NativeTextEncoding.CreateField(resources.Source.Language, field));
        string original = modified.Texts[0];
        modified.Texts[0] = "Comparison sample.";
        string root = Path.Combine(Path.GetTempPath(), "catalog-preview-" + Guid.NewGuid().ToString("N"));
        string payload = Path.Combine(root, "payload");
        string folder = Path.Combine(payload, "direct", "field", "mapdata", field[..2], field);
        Directory.CreateDirectory(folder);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(payload, "mod.xml"), "<ModInfo><Name>Dialogue sample</Name><Version>1.0</Version></ModInfo>", TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(folder, field + ".msd"), modified.Write(), TestContext.Current.CancellationToken);
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", true);
            await library.StopAsync();
            ModPackage package = new("Junction/synthetic", "Dialogue sample", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, payload);
            ModPreviewDocument preview = await library.OpenPreviewAsync(null, package, TestContext.Current.CancellationToken);
            ModPreviewComparison pair = await preview.CompareAsync(Assert.Single(preview.Entries), new(game), TestContext.Current.CancellationToken);
            Assert.Equal("Message 0\n" + original, pair.Before.Text);
            Assert.Equal("Message 0\nComparison sample.", pair.After.Text);
            Assert.Equal(0, resources.Generation);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void NativeTextureComparisonUsesTheInstalledOriginalAndOneReplacement()
    {
        string game = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(game);
        ResourceService resources = new(source);
        using GameAssets assets = new(game, resources: resources);
        FieldAssets location = assets.LoadField(229);
        TextureKey key = location.Background.First(tile => tile.Texture?.TextureKey is not null).Texture!.TextureKey!.Value;
        TextureSourceSnapshot original = Assert.Single(resources.CaptureTextureSources([key]));
        PixelImage pixels = original.Original;
        PixelImage edit = new(pixels.Width, pixels.Height, Enumerable.Repeat(new Rgba32(240, 16, 128), pixels.Pixels.Length).ToArray());
        byte[] png = LocalizationArtworkExporter.EncodePng(edit);
        ModPackage package = new("Memoria/synthetic", "A single texture", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, Path.GetTempPath());
        ModFile file = new(package.Id, package.Format, original.Target.CanonicalPath(), 1, () => png);
        ModPreviewComparison preview = Assert.IsType<ModPreviewComparison>(ModPreviewResources.CompareTexture(game, null, null, new(1, [package], [file]), file.RelativePath, TestContext.Current.CancellationToken));
        PixelImage before = Assert.IsType<PixelImage>(preview.Before.Image);
        PixelImage after = Assert.IsType<PixelImage>(preview.After.Image);
        Assert.Equal((pixels.Width, pixels.Height), (before.Width, before.Height));
        Assert.Equal((pixels.Width, pixels.Height), (after.Width, after.Height));
        Assert.Contains(before.Pixels.Zip(after.Pixels), pair => pair.First != pair.Second);
        Assert.Equal(0, resources.Generation);
    }

    private static UiElement Find(UiElement root, string id)
    {
        return Assert.Single(Descendants(root), element => element.Id == id);
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
    public void WorldComparisonUsesSameCameraAndLocationOnBothSides()
    {
        var profile = new ModPreviewResourcesProfile(TestResources.GameDirectory);
        ModPackage package = new("Memoria/synthetic", "Empty comparison", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, Path.GetTempPath());
        using WorldMapModPreview preview = WorldMapModPreview.Open(profile, new(1, [package], []), TestContext.Current.CancellationToken);
        var first = preview.Render(new(Width: 320, Height: 180), TestContext.Current.CancellationToken);
        Assert.Equal(first.Original.Pixels, first.Modded.Pixels);
        var second = preview.Render(new(new(first.Position.X + 900, first.Position.Z + 300), first.Camera, 320, 180), TestContext.Current.CancellationToken);
        Assert.NotEqual(first.Position, second.Position);
        Assert.Equal(first.Camera, second.Camera);
        Assert.Equal(second.Original.Pixels, second.Modded.Pixels);
        Assert.Contains(first.Original.Pixels.Zip(second.Original.Pixels), pair => pair.First != pair.Second);
    }

    [Fact]
    public async Task WorldComparisonFliesAndRotatesWithBothSidesInSync()
    {
        var profile = new ModPreviewResourcesProfile(TestResources.GameDirectory);
        ModPackage package = new("Memoria/synthetic", "Empty comparison", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, Path.GetTempPath());
        int width = 480;
        using CatalogPreviewPresentation.WorldComparison preview = new(profile, new(1, [package], []), () => width);
        CancellationToken token = TestContext.Current.CancellationToken;
        ModPreviewComparison previous = await preview.RenderAsync(0, 0, token);
        foreach (Vector3 step in new Vector3[] { new(0, 0, 1000), new(1000, 0, 0) })
        {
            ModPreviewComparison moved = await preview.FlyAsync(step, 0, token);
            Assert.Equal(moved.Before.Image!.Pixels, moved.After.Image!.Pixels);
            Assert.Contains(previous.Before.Image!.Pixels.Zip(moved.Before.Image.Pixels), pair => pair.First != pair.Second);
            previous = moved;
        }
        ModPreviewComparison raised = await preview.FlyAsync(new(0, 1000, 0), 0, token);
        Assert.Equal(raised.Before.Image!.Pixels, raised.After.Image!.Pixels);
        Assert.Contains(previous.Before.Image!.Pixels.Zip(raised.Before.Image.Pixels), pair => pair.First != pair.Second);

        ModPreviewComparison lowered = await preview.FlyAsync(new(0, -1000, 0), 0, token);
        Assert.Equal(lowered.Before.Image!.Pixels, lowered.After.Image!.Pixels);
        Assert.Equal(previous.Before.Image.Pixels, lowered.Before.Image.Pixels);

        ModPreviewComparison turned = await preview.FlyAsync(Vector3.Zero, MathF.PI / 4, token);
        Assert.Equal(turned.Before.Image!.Pixels, turned.After.Image!.Pixels);
        Assert.Contains(lowered.Before.Image.Pixels.Zip(turned.Before.Image.Pixels), pair => pair.First != pair.Second);

        width = 1280;
        ModPreviewComparison resized = await preview.RenderAsync(0, 0, token);
        Assert.Equal((1280, 720), (resized.Before.Image!.Width, resized.Before.Image.Height));
        Assert.Equal(resized.Before.Image.Pixels, resized.After.Image!.Pixels);
        width = 480;
        ModPreviewComparison restored = await preview.RenderAsync(0, 0, token);
        Assert.Equal(turned.Before.Image.Pixels, restored.Before.Image!.Pixels);
    }
}
