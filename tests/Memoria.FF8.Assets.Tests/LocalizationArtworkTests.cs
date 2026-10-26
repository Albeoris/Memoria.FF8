using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class LocalizationArtworkTests
{
    [Fact]
    public void TimPaletteSourcesExportLosslessPngWithCanonicalSectionIdentities()
    {
        ushort[] colors = new ushort[32];
        colors[1] = 0x001F;
        colors[17] = 0x03E0;
        TimImage tim = new(TimPixelMode.Indexed4, 0, 0, 1, 1, [1, 1, 1, 1], new TimClut(0, 0, 16, 2, colors));
        byte[] bytes = new TimFile([tim]).Write();
        byte[] header = new byte[13 * 8];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12 * 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12 * 8 + 4), (uint)bytes.Length);
        var sources = LocalizationArtworkExporter.ReadMenuSections(header, bytes, TestContext.Current.CancellationToken);
        Assert.Equal(2, sources.Count);
        for (int palette = 0; palette < 2; palette++)
        {
            Assert.Equal(new TextureKey(new("menu/mngrp.bin"), "slot012", palette), sources[palette].Key);
            Assert.Equal(64, sources[palette].SourceSha256.Length);
            PixelImage decoded = TextureImageDecoder.Decode(sources[palette].Png, "source.png").Image;
            Assert.Equal(4, decoded.Width);
            Assert.Equal(1, decoded.Height);
            Assert.Equal(tim.Decode(palette).Pixels, decoded.Pixels);
        }
    }

    [Fact]
    public void ImageCatalogPublishesWithTextAndChangesFingerprintWithoutMutatingOriginalPixels()
    {
        TextureKey key = new(new("menu/mag00.tex"), "atlas");
        PixelImage original = new(2, 1, [new(1, 2, 3, 255), new(4, 5, 6, 255)]);
        ImmutableArray<Rgba32> pixels = [new(9, 8, 7, 255), new(6, 5, 4, 255)];
        LocalizationArtworkValue image = new(key, 2, 1, 2, 1, pixels, new string('A', 64), "package", "project");
        LocalizationCatalog catalog = new([], artwork: new Dictionary<TextureKey, LocalizationArtworkValue> { [key] = image });
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        TextureBuildContext context = new(key, source.Descriptor, 1, "native-artwork", 2, 1, () => original);
        AssetHandle<TextureValue> handle = resources.GetTexture(context);
        Assert.Equal(original.Pixels, handle.Value.Image.Pixels);
        ResourceService.PreparedResources prepared = resources.Prepare(null, localization: catalog);
        Assert.Equal(original.Pixels, handle.Value.Image.Pixels);
        prepared.Commit();
        Assert.Equal(pixels, handle.Value.Image.Pixels);
        Assert.Equal(new Rgba32(1, 2, 3, 255), original.Pixels[0]);
        PixelImage mutableCopy = catalog.ResolveArtwork(context)!.Image;
        mutableCopy.Pixels[0] = default;
        Assert.Equal(pixels[0], catalog.ResolveArtwork(context)!.Image.Pixels[0]);
        LocalizationCatalog changed = new([], artwork: new Dictionary<TextureKey, LocalizationArtworkValue> { [key] = image with { Pixels = [pixels[1], pixels[0]] } });
        Assert.NotEqual(catalog.Fingerprint, changed.Fingerprint);
        resources.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal(original.Pixels, handle.Value.Image.Pixels);
    }

    [Fact]
    public void LocalizationImageDeltaRebuildsOnlyItsResidentDependenciesAndTextKeepsThem()
    {
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        TextureKey first = new(new("menu/mag00.tex"), "atlas");
        TextureKey other = new(new("menu/mag01.tex"), "atlas");
        TextureKey derived = new(new("menu/composed"), "native-sprite");
        var firstHandle = resources.GetTexture(new(first, source.Descriptor, 1, "native-artwork", 1, 1, () => new(1, 1)));
        var otherHandle = resources.GetTexture(new(other, source.Descriptor, 1, "native-artwork", 1, 1, () => new(1, 1)));
        TextureValue unchanged = otherHandle.Value;
        int builds = 0;
        var derivedHandle = resources.GetTransformedTexture(new(derived, [first], dependencies =>
        {
            builds++;
            return dependencies[0] with { Provenance = "native-sprite" };
        }));
        LocalizationArtworkValue image = new(first, 1, 1, 1, 1, [new(1, 2, 3, 255)], new string('A', 64), "fixture", "fixture");
        LocalizationCatalog Artwork(string text) => new(new Dictionary<string, LocalizationValue> { ["$en_sample_0000"] = new(text, "fixture", "fixture") }, artwork: new Dictionary<TextureKey, LocalizationArtworkValue> { [first] = image });
        List<ResourceChangeSet> changes = [];
        using IDisposable subscription = resources.Subscribe(changes.Add);
        resources.PrepareLocalization(Artwork("first")).Commit();
        Assert.Equal(2, builds);
        Assert.Equal(image.Pixels[0], derivedHandle.Value.Image.Pixels[0]);
        Assert.Same(unchanged, otherHandle.Value);
        Assert.Contains(first, changes[^1].Textures);
        Assert.Contains(derived, changes[^1].Textures);
        Assert.DoesNotContain(other, changes[^1].Textures);
        TextureValue retained = firstHandle.Value;
        resources.PrepareLocalization(Artwork("second")).Commit();
        Assert.Equal(2, builds);
        Assert.Same(retained, firstHandle.Value);
        Assert.Empty(changes[^1].Textures);
        resources.PrepareLocalization(LocalizationCatalog.Empty).Commit();
        Assert.Equal(3, builds);
        Assert.Equal(default, derivedHandle.Value.Image.Pixels[0]);
        Assert.Same(unchanged, otherHandle.Value);
    }

    [Fact]
    public void OptionalInstalledArtworkExportsNativeTimTexAndStartupCanvases()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        Assert.SkipUnless(source.Descriptor.Edition == GameDataEdition.Classic, "The Classic artwork inventory is required for this installed corpus check.");
        IGameArchiveSource archives = Assert.IsAssignableFrom<IGameArchiveSource>(source);
        var artwork = LocalizationArtworkExporter.Read(archives, TestContext.Current.CancellationToken);
        Assert.Equal(38, artwork.Where(image => image.Key.Owner.Path == "menu/mngrp.bin").Select(image => image.Key.Subresource).Distinct().Count());
        Assert.Contains(artwork, image => image.Key.Owner.Path == "menu/sysfnt.tex");
        Assert.Equal(3, artwork.Count(image => image.Key.Owner.Path == "cards/cards" && image.Key.Subresource.StartsWith("outcome/", StringComparison.Ordinal)));
        Assert.Equal(30, artwork.Count(image => image.Key.Owner.Path.StartsWith("main/", StringComparison.Ordinal)));
        foreach (LocalizationArtworkSource image in artwork)
        {
            PixelImage decoded = TextureImageDecoder.Decode(image.Png, "source.png").Image;
            Assert.Equal(image.Width, decoded.Width);
            Assert.Equal(image.Height, decoded.Height);
        }
        Console.WriteLine($"Artwork sources: {artwork.Count} palettes/images;38 native TIM sections, menu TEX atlases,30 startup LZS images.");
    }

    [Fact]
    public void ReplacementMustKeepNativeCanvasAndIntegerScale()
    {
        TextureKey key = new(new("menu/mngrp.bin"), "slot020");
        LocalizationArtworkValue image = new(key, 2, 1, 4, 2, Enumerable.Repeat(new Rgba32(1, 2, 3, 255), 8).ToImmutableArray(), new string('A', 64), "package", "project");
        LocalizationCatalog catalog = new([], artwork: new Dictionary<TextureKey, LocalizationArtworkValue> { [key] = image });
        TextureBuildContext good = new(key, new("synthetic", GameDataEdition.Classic, []), 1, "native-artwork", 2, 1, () => new(2, 1));
        Assert.Equal(4, catalog.ResolveArtwork(good)!.Image.Width);
        Assert.Throws<InvalidDataException>(() => catalog.ResolveArtwork(new(key, good.Source, 1, "native-artwork", 4, 1, () => new(4, 1))));
        Assert.Throws<InvalidDataException>(() => new LocalizationCatalog([], artwork: new Dictionary<TextureKey, LocalizationArtworkValue> { [key] = image with { LogicalHeight = 2 } }));
    }
}
