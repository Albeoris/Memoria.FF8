using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class FfnxWorldVramTests
{
    [Fact]
    public void PaletteCoordinatesSelectExactPageBeforeZeroFallback()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("0_0_0", 1), Sheet("0_832_233", 2), Sheet("0_832_234", 3)]));
        Assert.Equal((byte)2, resolver.Resolve(Context(832, 233))!.Image[0, 0].R);
        Assert.Equal((byte)3, resolver.Resolve(Context(832, 234))!.Image[0, 0].R);
        Assert.Equal((byte)1, resolver.Resolve(Context(848, 235))!.Image[0, 0].R);
    }

    [Fact]
    public void VramPageOffsetsCropAndComposeTheNativeRectangle()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("0_0_0", 1), Sheet("1_0_0", 2), Sheet("16_0_0", 3), Sheet("17_0_0", 4)]));
        TextureBuildContext context = new(new(new("world/wmsetus.obj"), "section/38/texture/11"), Source(), 1, "classic-world-section", 256, 256, () => new(256, 256), new(848, 128, 4, PaletteX: 832, PaletteY: 233));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        Assert.Equal((byte)1, replacement.Image[0, 0].R);
        Assert.Equal((byte)2, replacement.Image[255, 0].R);
        Assert.Equal((byte)3, replacement.Image[0, 255].R);
        Assert.Equal((byte)4, replacement.Image[255, 255].R);
        Assert.Equal(256, replacement.LogicalWidth);
    }

    [Fact]
    public void OtherPaletteCoordinatesDoNotSubstituteForMissingExactOrZeroPage()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("0_832_234", 3)]));
        Assert.Null(resolver.Resolve(Context(832, 233)));
    }

    [Fact]
    public void InspectionPreservesUnsupportedWorldMemberBoundary()
    {
        ModFile[] files = [Sheet("0_832_233", 1), new("unknown", ModFormat.Junction, "mods/textures/world/dat/wmset/section38/texture29_0_0_0.png", 1, () => throw new InvalidOperationException())];
        DemasterInspection result = FfnxTextureResolver.Inspect(Source(), new(1, [], files));
        Assert.Contains("test", result.UsablePackageIds);
        Assert.DoesNotContain("unknown", result.UsablePackageIds);
        Assert.Equal("ffnx-unsupported-target", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void InstalledWorldMapPagesFollowBothNativePalettesAndResourceToggle()
    {
        string? fixtures = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(File.Exists(fixtures) && Directory.Exists(game), "Configure optional external fixture catalog and game data.");
        Dictionary<string, string> paths = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(fixtures!))!;
        Assert.SkipUnless(paths.ContainsKey("junction-world-map"), "Configure the optional world map fixture.");
        JunctionModPackage package = JunctionModImporter.Open(paths["junction-world-map"]);
        Assert.Empty(package.Diagnostics);
        ModFile[] files = package.Files.Where(file => file.RelativePath.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase)).Select(file => new ModFile("fixture", ModFormat.Junction, file.RelativePath, 1, () =>
        {
            using MemoryStream output = new();
            file.CopyTo(output);
            return output.ToArray();
        })).ToArray();
        Assert.NotEmpty(files);
        using ClassicGameDataSource source = new(game!);
        FfnxTextureResolver resolver = new(new(1, [], files));
        DemasterInspection inspection = FfnxTextureResolver.Inspect(source.Descriptor, new(1, [], files), source);
        Assert.Empty(inspection.Diagnostics);
        ResourceService resources = new(source);
        using WorldAssets world = new(game!, resources);
        PixelImage[] native = [world.ReadNativeSectionTexture(38, 11, 0), world.ReadNativeSectionTexture(38, 11, 1)];
        List<Exception> failures = [];
        resources.ReplacementFailed += (_, exception) => failures.Add(exception);
        resources.Prepare(resolver).Commit();
        for (int palette = 0; palette < 2; palette++)
        {
            PixelImage actual = world.ReadNativeSectionTexture(38, 11, palette);
            ModFile expectedFile = Assert.Single(files, file => file.RelativePath.EndsWith($"texture11_0_832_{233 + palette}.dds", StringComparison.OrdinalIgnoreCase));
            PixelImage expected = ModImageDecoder.Decode(expectedFile).Image;
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(native[palette].Height * (expected.Width / 256), actual.Height);
            Assert.Equal(expected.Pixels.AsSpan(0, actual.Pixels.Length).ToArray(), actual.Pixels);
            Assert.True(actual.Width > native[palette].Width);
        }
        Assert.Empty(failures);
        resources.Prepare(null).Commit();
        Assert.Same(native[0], world.ReadNativeSectionTexture(38, 11));
        Assert.Same(native[1], world.ReadNativeSectionTexture(38, 11, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CardOutcomeCropsOnlyItsAddressedPage(int outcome)
    {
        using MagickImage image = new(new MagickColor((byte)13, (byte)27, (byte)39), 768, 768);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        ModFile file = new("test", ModFormat.Junction, "mods/textures/cardgame/icons_1_0_255.png", 1, () => bytes);
        using MagickImage fullImage = new(new MagickColor((byte)200, (byte)0, (byte)0), 768, 192);
        byte[] fullBytes = fullImage.ToByteArray(MagickFormat.Png);
        ModFile full = new("test", ModFormat.Junction, "mods/textures/cardgame/icons_00.png", 1, () => fullBytes);
        FfnxTextureResolver resolver = new(new(1, [], [full, file]));
        TextureBuildContext context = new(new(new("cards/cards"), $"outcome/{outcome}"), Source(), 1, "native-artwork", 256, 48, () => new(256, 48));
        TextureReplacement value = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        Assert.Equal(768, value.Image.Width);
        Assert.Equal(144, value.Image.Height);
        Assert.Equal(new Rgba32(13, 27, 39), value.Image[0, 0]);
        Assert.Equal(256, value.LogicalWidth);
        Assert.Empty(FfnxTextureResolver.Inspect(Source(), new(1, [], [file])).Diagnostics);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CardCaptureUsesItsExactPageAndPalette(int capture)
    {
        using MagickImage image = new(new MagickColor((byte)13, (byte)27, (byte)39), 768, 768);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        ModFile file = new("test", ModFormat.Junction, $"mods/textures/cardgame/icons_2_32_{224 + capture}.png", 1, () => bytes);
        FfnxTextureResolver resolver = new(new(1, [], [file]));
        TextureBuildContext context = new(new(new("cards/cards"), $"capture/{capture}"), Source(), 1, "native-artwork", 255, 64, () => new(255, 64));
        TextureReplacement value = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        Assert.Equal(765, value.Image.Width);
        Assert.Equal(192, value.Image.Height);
        Assert.Equal(new Rgba32(13, 27, 39), value.Image[0, 0]);
        Assert.Equal(255, value.LogicalWidth);
        Assert.Equal(64, value.LogicalHeight);
    }

    [Fact]
    public void InstalledCardOutcomeVariantUsesExactPagePixels()
    {
        string? fixtures = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(File.Exists(fixtures) && Directory.Exists(game), "Configure optional external fixture catalog and game data.");
        Dictionary<string, string> paths = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(fixtures!))!;
        Assert.SkipUnless(paths.ContainsKey("junction-card-artwork-names"), "Configure the optional card artwork fixture.");
        JunctionModPackage package = JunctionModImporter.Open(paths["junction-card-artwork-names"], new Dictionary<string, int> { ["LANG_ID"] = 3, ["ICON_ID"] = 1 });
        Assert.Empty(package.Diagnostics);
        ModFile[] files = package.Files.Where(file => file.RelativePath.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase)).Select(file => new ModFile("fixture", ModFormat.Junction, file.RelativePath, 1, () =>
        {
            using MemoryStream output = new();
            file.CopyTo(output);
            return output.ToArray();
        })).ToArray();
        FfnxTextureResolver resolver = new(new(1, [], files));
        Assert.Empty(FfnxTextureResolver.Inspect(Source(), new(1, [], files)).Diagnostics);
        ModFile page = Assert.Single(files, file => file.RelativePath.EndsWith("icons_1_0_255.png", StringComparison.OrdinalIgnoreCase));
        PixelImage sheet = ModImageDecoder.Decode(page).Image;
        using ClassicGameDataSource source = new(game!);
        ResourceService resources = new(source);
        resources.Prepare(resolver).Commit();
        NativeTripleTriadArtwork artwork = NativeTripleTriadArtwork.Load(game!, resources);
        for (int outcome = 0; outcome < 3; outcome++)
        {
            PixelImage actual = artwork.GetOutcomeBanner(outcome);
            Assert.Equal(sheet.Width, actual.Width);
            int height = 48 * (sheet.Width / 256);
            Assert.Equal(height, actual.Height);
            Assert.Equal(sheet.Pixels.AsSpan(outcome * height * sheet.Width, height * sheet.Width).ToArray(), actual.Pixels);
        }
    }

    private static ResourceSourceDescriptor Source() => new("test", GameDataEdition.Classic, ["classic-ffnx"], "en");

    private static TextureBuildContext Context(int x, int y) => new(new(new("world/wmsetus.obj"), "section/38/texture/11"), Source(), 1, "classic-world-section", 256, 256, () => new(256, 256), new(832, 0, 4, PaletteX: x, PaletteY: y));

    private static ModFile Sheet(string suffix, byte red)
    {
        using MagickImage image = new(new MagickColor(red, (byte)0, (byte)0), 256, 256);
        byte[] png = image.ToByteArray(MagickFormat.Png);
        return new("test", ModFormat.Junction, $"mods/textures/world/dat/wmset/section38/texture11_{suffix}.png", 1, () => png);
    }
}
