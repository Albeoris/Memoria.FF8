using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterRemasteredArtworkTests
{
    [Fact]
    public void SplitIconOverridesOnlyItsQuadrantAndKeepsTheSelectedLanguageAndPalette()
    {
        ModFile whole = File("textures/icon.tex/fr/4.png", 256, MagickColors.Blue);
        ModFile quadrant = File("textures/hires/iconfl02.tex/fr/4.png", 256, MagickColors.Red);
        ModFile wrongLanguage = File("textures/hires/iconfl00.tex/en/4.png", 256, MagickColors.Red);
        ModFile wrongPalette = File("textures/hires/iconfl01.tex/fr/3.png", 256, MagickColors.Red);
        ResourceSourceDescriptor source = new("test", GameDataEdition.Remastered, [RemasteredArtworkPaths.Layout], "fr");
        ModCatalogSnapshot catalog = new(1, [], [whole, quadrant, wrongLanguage, wrongPalette]);
        DemasterTextureResolver resolver = new(catalog);
        TextureBuildContext context = new(new(new("menu/icon.tex"), "atlas", 4), source, 1, "native-artwork", 256, 256, () => throw new InvalidOperationException());

        DemasterInspection inspection = DemasterTextureResolver.Inspect(source, catalog);
        Assert.Contains(inspection.Diagnostics, diagnostic => diagnostic.RelativePath == wrongLanguage.RelativePath);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        Assert.Equal(512, replacement.Image.Width);
        Assert.Equal(new Rgba32(0, 0, 255), replacement.Image[255, 255]);
        Assert.Equal(new Rgba32(255, 0, 0), replacement.Image[255, 256]);
        Assert.Equal(new Rgba32(0, 0, 255), replacement.Image[256, 256]);
        Assert.DoesNotContain(wrongLanguage.RelativePath, replacement.Provenance);
        Assert.DoesNotContain(wrongPalette.RelativePath, replacement.Provenance);
    }

    [Fact]
    public void CloudPageRequiresThePublishedNativeCrop()
    {
        const string layout = "demaster-world-wmset-cloud";
        const string path = "textures/world/dat/wmset/wmset_014_0.png";
        ResourceSourceDescriptor source = new("test", GameDataEdition.Remastered, [layout]);
        PixelImage original = new(256, 64);
        TextureBuildContext Context(TextureSourcePageAddress page) => new(new(new("world/wmsetus.obj"), "section/38/texture/10"), source, 1, "classic-world-section", 256, 64, () => original) { NativePage = page };
        TextureSourcePageAddress region = new(14, 0, 64, 256, 64, 832, 224, []) { Namespace = "world-wmset" };
        using MagickImage image = new(MagickColors.Blue, 256, 256);
        using MagickImage strip = new(MagickColors.Red, 256, 64);
        image.Composite(strip, 0, 64, CompositeOperator.Copy);
        ModFile file = new("Demaster/Fixture", ModFormat.Demaster, path, 1, () => image.ToByteArray(MagickFormat.Png));
        ModCatalogSnapshot catalog = new(1, [], [file]);
        DemasterTextureResolver resolver = new(catalog);

        Assert.Contains(file.PackageId, DemasterTextureResolver.Inspect(source, catalog).UsablePackageIds);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(region)));
        Assert.Equal((256, 64), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(new Rgba32(255, 0, 0), replacement.Image[0, 0]);
        Assert.Equal(new Rgba32(255, 0, 0), replacement.Image[255, 63]);
        Assert.Null(resolver.Resolve(Context(region with { PaletteY = 225 })));
    }

    [Fact]
    public void StaticBoatUsesItsFieldTextureAliasIndependentlyOfTheWorldCloudPage()
    {
        const string layout = "demaster-world-static-sheet";
        const string stem = RemasteredGameDataSource.WorldStaticSheetStem;
        ModFile boat = File(stem + ".png", 256, MagickColors.Red);
        ModFile cloud = File("textures/world/dat/wmset/wmset_014_0.png", 256, MagickColors.Blue);
        ResourceSourceDescriptor source = new("test", GameDataEdition.Remastered, [layout]);
        TextureBuildContext context = new(new(new("world/wmsetus.obj"), "static/78/palette/2"), source, 1, "classic-model", 256, 256, () => throw new InvalidOperationException())
        {
            NativeAlias = new(new(stem), "", 0, 0, 0, 256, 256, 256, 256)
        };
        ModCatalogSnapshot catalog = new(1, [], [boat, cloud]);
        DemasterTextureResolver resolver = new(catalog);

        Assert.Contains(boat.PackageId, DemasterTextureResolver.Inspect(source, catalog).UsablePackageIds);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        Assert.Contains(boat.RelativePath, replacement.Provenance);
        Assert.Equal(new Rgba32(255, 0, 0), replacement.Image[0, 0]);
    }

    private static ModFile File(string path, uint size, IMagickColor<byte> color)
    {
        using MagickImage image = new(color, size, size);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/Fixture", ModFormat.Demaster, path, 1, () => bytes);
    }
}
