using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class LooseFieldTextureTests
{
    [Theory]
    [InlineData("field/mapdata/te/test/test_0.png", true)]
    [InlineData("field/mapdata/te/test/test_25.dds", true)]
    [InlineData("field/mapdata/te/test/test_26.png", false)]
    [InlineData("field/mapdata/xx/test/test_0.png", false)]
    [InlineData("field/mapdata/te/test/other_0.png", false)]
    [InlineData("field/mapdata/te/test/test_0_1.png", false)]
    public void NativeLoosePagesHaveDistinctValidatedAddresses(string path, bool supported)
    {
        Assert.Equal(supported, FfnxTextureResolver.IsNativeFieldBackgroundPath(path));
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(12, 28)]
    public void NativeLoosePagesUseOriginalSourceCoordinatesAndCoverage(int externalPage, int nativePage)
    {
        ModFile file = Page(externalPage, 512);
        FfnxTextureResolver resolver = new(new(1, [], [file]));
        TextureReplacement result = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(nativePage)));
        Assert.Equal(32, result.Image.Width);
        Assert.Equal(16, result.LogicalWidth);
        Assert.Equal(new Rgba32(32, 48, 9, 128), result.Image[0, 0]);
        Assert.Contains(file.RelativePath, result.Provenance);
        Assert.Contains("test", FfnxTextureResolver.Inspect(Source(), new(1, [], [file])).UsablePackageIds);
    }

    [Fact]
    public void OlderPageNumbersTakePrecedenceAndMalformedCanvasesFail()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Page(0, 512), Page(13, 256)]));
        TextureReplacement result = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(16)));
        Assert.Equal(16, result.Image.Width);
        Assert.Contains("test_13.png", result.Provenance);
        resolver = new(new(1, [], [Page(0, 128)]));
        Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context(16)));
    }

    [Fact]
    public void UnprovedVariantsHaveOwningDiagnosticsWithoutBeingMarkedUsable()
    {
        const string path = "field/mapdata/te/test/test_0_1.png";
        ModFile file = new("variant", ModFormat.Junction, path, 1, () => throw new InvalidOperationException());
        Assert.True(FfnxTextureResolver.IsNativeFieldBackgroundFamilyPath(path));
        DemasterInspection inspection = FfnxTextureResolver.Inspect(Source(), new(1, [], [file]));
        Assert.Empty(inspection.UsablePackageIds);
        ModDiagnostic diagnostic = Assert.Single(inspection.Diagnostics);
        Assert.Equal("ffnx-unsupported-native-target", diagnostic.Code);
        Assert.Equal("variant", diagnostic.PackageId);
        Assert.Equal(path, diagnostic.RelativePath);
    }

    private static ResourceSourceDescriptor Source() => new("test", GameDataEdition.Classic, ["classic-ffnx"], "en");

    private static TextureBuildContext Context(int page) => new(new(new("field/test/test.mim"), "tile/0"), Source(), 1, "classic-field-tile", 16, 16, () => new(16, 16))
    {
        NativeAlias = new(new("field/test/test.mim"), $"page/{page}", 0, 32, 48, 16, 16, 256, 256)
    };

    private static ModFile Page(int page, int width)
    {
        byte[] pixels = new byte[width * width * 4];
        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = (y * width + x) * 4;
                pixels[offset] = (byte)(x * 256 / width);
                pixels[offset + 1] = (byte)(y * 256 / width);
                pixels[offset + 2] = 9;
                pixels[offset + 3] = 255;
            }
        }
        using MagickImage image = new();
        image.ReadPixels(pixels, new PixelReadSettings((uint)width, (uint)width, StorageType.Char, PixelMapping.RGBA));
        byte[] png = image.ToByteArray(MagickFormat.Png);
        return new("test", ModFormat.Junction, $"field/mapdata/te/test/test_{page}.png", 1, () => png);
    }
}
