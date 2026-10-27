using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class FfnxMenuArtworkTests
{
    [Theory]
    [InlineData("face_b00")]
    [InlineData("face_b01")]
    [InlineData("gf_big00")]
    [InlineData("gf_big01")]
    public void PortraitAtlasesPreserveTheirNativeCanvas(string name)
    {
        FfnxTextureResolver resolver = new(new(1, [], [Page(name, 0, 512, new(12, 34, 56, 78))]));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context($"menu/{name}.tex", 0)));
        Assert.Equal(512, replacement.Image.Width);
        Assert.Equal(256, replacement.LogicalWidth);
        Assert.Equal(new Rgba32(12, 34, 56, 78), replacement.Image[511, 511]);
    }

    [Fact]
    public void SparsePagesPreservePaletteSelectionNativeQuadrantsAndSuppliedAlpha()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Page("iconfl01", 4, 256, new(20, 30, 40, 50)), Page("iconfl03", 4, 256, new(60, 70, 80, 90))]));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("menu/icon.tex", 4)));
        Assert.Equal(512, replacement.Image.Width);
        Assert.Equal(new Rgba32(1, 2, 3, 255), replacement.Image[255, 255]);
        Assert.Equal(new Rgba32(1, 2, 3, 255), replacement.Image[255, 511]);
        Assert.Equal(new Rgba32(20, 30, 40, 50), replacement.Image[256, 255]);
        Assert.Equal(new Rgba32(60, 70, 80, 90), replacement.Image[256, 256]);
        Assert.Null(resolver.Resolve(Context("menu/icon.tex", 0)));
        Assert.Null(resolver.Resolve(Context("menu/icon.tex", 5)));
    }

    [Fact]
    public void InconsistentPageScalesFailInsteadOfChangingSpriteCoordinates()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Page("iconfl01", 4, 256, new()), Page("iconfl03", 4, 384, new())]));
        Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context("menu/icon.tex", 4)));
    }

    [Fact]
    public void InspectionRecognizesOnlyReachablePagesAndPalettesWithoutDecoding()
    {
        string[] names = ["face_b00_00", "gf_big01_00", "iconfl01_04", "iconfl03_15"];
        ModFile[] files = names.Select(name => new ModFile("menu", ModFormat.Junction, $"mods/textures/data/eng/menu/hires/{name}.png", 1, () => throw new InvalidOperationException())).ToArray();
        Assert.Contains("menu", FfnxTextureResolver.Inspect(Source(), new(1, [], files)).UsablePackageIds);
        ModFile invalid = new("bad", ModFormat.Junction, "mods/textures/data/eng/menu/hires/iconfl01_17.png", 1, () => throw new InvalidOperationException());
        Assert.Empty(FfnxTextureResolver.Inspect(Source(), new(1, [], [invalid])).UsablePackageIds);
    }

    [Fact]
    public void Complete_icon_atlas_is_used_when_no_split_page_matches_the_requested_palette()
    {
        using MagickImage image = new(new MagickColor(40, 50, 60, 70), 512, 512);
        byte[] png = image.ToByteArray(MagickFormat.Png);
        ModFile full = new("full", ModFormat.Junction, "mods/textures/data/eng/menu/icon_04.png", 1, () => png);
        FfnxTextureResolver resolver = new(new(1, [], [full]));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("menu/icon.tex", 4)));
        Assert.Equal(512, replacement.Image.Width);
        Assert.Equal(256, replacement.LogicalWidth);
        Assert.Equal(new Rgba32(40, 50, 60, 70), replacement.Image[0, 0]);
        Assert.Contains("full", FfnxTextureResolver.Inspect(Source(), new(1, [], [full])).UsablePackageIds);
        Assert.Null(resolver.Resolve(Context("menu/icon.tex", 5)));
    }

    [Fact]
    public void Split_pages_keep_their_existing_precedence_over_the_complete_atlas()
    {
        using MagickImage image = new(new MagickColor(100, 100, 100, 255), 256, 256);
        byte[] png = image.ToByteArray(MagickFormat.Png);
        ModFile full = new("full", ModFormat.Junction, "mods/textures/data/eng/menu/icon_04.png", 1, () => png);
        FfnxTextureResolver resolver = new(new(1, [], [full, Page("iconfl01", 4, 128, new(20, 30, 40, 50))]));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("menu/icon.tex", 4)));
        Assert.Equal(new Rgba32(1, 2, 3, 255), replacement.Image[0, 0]);
        Assert.Equal(new Rgba32(20, 30, 40, 50), replacement.Image[128, 0]);
    }

    [Fact]
    public void NativeMenuImagesUseTheirAtlasAndSplitPageAddresses()
    {
        ModFile magazineImage = Page("unused", 0, 512, new(10, 20, 30, 255));
        ModFile magazine = new("menu", ModFormat.Junction, "menu/mag12.tex.png", 1, magazineImage.ReadBytes);
        ModFile iconImage = Page("unused", 0, 128, new(20, 30, 40, 255));
        ModFile icon = new("menu", ModFormat.Junction, "menu/hires/iconfl01_03.png", 1, iconImage.ReadBytes);
        FfnxTextureResolver resolver = new(new(1, [], [magazine, icon]));
        TextureReplacement atlas = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("menu/mag12.tex", 0)));
        Assert.Equal(512, atlas.Image.Width);
        Assert.Equal(256, atlas.LogicalWidth);
        Assert.Equal(new Rgba32(10, 20, 30, 255), atlas.Image[0, 0]);
        TextureReplacement icons = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("menu/icon.tex", 3)));
        Assert.Equal(new Rgba32(20, 30, 40, 255), icons.Image[128, 0]);
        Assert.Contains("menu", FfnxTextureResolver.Inspect(Source(), new(1, [], [magazine, icon])).UsablePackageIds);
        ModFile unreachable = new("menu", ModFormat.Junction, "menu/hires/iconfl01_23.png", 1, icon.ReadBytes);
        DemasterInspection inspection = FfnxTextureResolver.Inspect(Source(), new(1, [], [unreachable]));
        Assert.Empty(inspection.UsablePackageIds);
        Assert.Equal(unreachable.RelativePath, Assert.Single(inspection.Diagnostics).RelativePath);
    }

    private static TextureBuildContext Context(string owner, int palette) => new(new(new(owner), "atlas", palette), Source(), 1, "native-artwork", 256, 256, () =>
    {
        PixelImage image = new(256, 256);
        Array.Fill(image.Pixels, new Rgba32(1, 2, 3, 255));
        return image;
    });

    private static ResourceSourceDescriptor Source() => new("test", GameDataEdition.Classic, ["classic-ffnx"], "en");

    private static ModFile Page(string name, int palette, int size, Rgba32 color)
    {
        using MagickImage image = new(new MagickColor(color.R, color.G, color.B, color.A), (uint)size, (uint)size);
        byte[] png = image.ToByteArray(MagickFormat.Png);
        return new("menu", ModFormat.Junction, $"mods/textures/data/eng/menu/hires/{name}_{palette:00}.png", 1, () => png);
    }
}
