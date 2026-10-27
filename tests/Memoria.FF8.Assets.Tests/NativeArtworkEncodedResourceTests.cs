using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeArtworkEncodedResourceTests
{
    [Fact]
    public void TutorialAtlasesConsumeSelectedTexAndKeepTheOriginalTimBaselineOtherwise()
    {
        string directory = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceKey key = new("menu/mag12.tex");
        byte[] originalBytes = Read(source, key);
        using GameAssets assets = new(directory);
        NativeTutorialBrowserPage page = assets.ReadNativeTutorialBrowserCatalog().Categories.SelectMany(category => category.Pages).First(page => page.ContentGroup == 5 && page.ContentIndex == 2);
        PixelImage original = NativeTutorialArtwork.Load(directory).Read(page.Id);
        ResourceService unchanged = new(source);
        Assert.Equal(original.Pixels, NativeTutorialArtwork.Load(directory, unchanged).Read(page.Id).Pixels);
        ResourceService selected = new(source);
        selected.Prepare(null, encodedResources: [PaintedAtlas(key, originalBytes)]).Commit();
        PixelImage translated = NativeTutorialArtwork.Load(directory, selected).Read(page.Id);
        Assert.Contains(new Rgba32(0, 255, 0, 255), translated.Pixels);
        Assert.NotEqual(original.Pixels, translated.Pixels);
        Assert.Throws<ResourceReloadRequiredException>(() => selected.Prepare(null, encodedResources: [new(key, "tex", 2, "test", originalBytes)]));
    }

    [Fact]
    public void MagazineAtlasesConsumeSelectedEncodedTexBeforeSpriteComposition()
    {
        string directory = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceKey key = new("menu/mag00.tex");
        ResourceService resources = new(source);
        resources.Prepare(null, encodedResources: [PaintedAtlas(key, Read(source, key))]).Commit();
        PixelImage illustration = NativeMagazineArtwork.Load(directory, resources).GetImage(0);
        Assert.Contains(new Rgba32(0, 255, 0, 255), illustration.Pixels);
        Assert.Throws<ResourceReloadRequiredException>(() => resources.Prepare(null, encodedResources: []));
    }

    private static EncodedResource PaintedAtlas(ResourceKey key, byte[] bytes)
    {
        TexFile texture = TexFile.Read(bytes);
        foreach (uint[] palette in texture.Palettes)
        {
            Array.Fill(palette, 0xFF00FF00u);
        }
        return new(key, "tex", 1, "test", texture.Write());
    }

    private static byte[] Read(IResourceSource source, ResourceKey key)
    {
        using Stream stream = (source.Find(key) ?? throw new FileNotFoundException()).OpenRead();
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }
}
