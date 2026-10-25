using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class FfnxBattleTextureTests
{
    [Theory]
    [InlineData("a0stg000.x", 0)]
    [InlineData("c0m016.dat", 2)]
    [InlineData("d0c001.dat", 1)]
    [InlineData("d4w009.dat", 3)]
    [InlineData("dac021.dat", 0)]
    [InlineData("daw039.dat", 0)]
    public void OriginalFilenameAndUploadOrdinalAddressNativeTim(string owner, int member)
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet($"battle/{owner}-{member}_00", new(12, 34, 56, 255))]));
        TextureReplacement result = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(owner, member)));
        Assert.Equal(new Rgba32(12, 34, 56, 255), result.Image[0, 0]);
        Assert.Equal(64, result.LogicalWidth);
        Assert.Equal(128, result.Image.Width);
        Assert.Null(resolver.Resolve(Context(owner, member + 1)));
    }

    [Fact]
    public void SourceAliasSeparatesDecodedPaletteIndexFromNativeUploadOrdinal()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("battle/a0stg030.x-0_01", new(10, 0, 0, 255)), Sheet("battle/a0stg030.x-0_03", new(30, 0, 0, 255))]));
        TextureBuildContext context = Context("a0stg030.x", 7, new(new("battle/a0stg030.x"), "texture/0", 3, 0, 0, 64, 32, 64, 32));
        Assert.Equal(new Rgba32(30, 0, 0, 255), resolver.Resolve(context)!.Image[0, 0]);
        Assert.Equal(new Rgba32(10, 0, 0, 255), resolver.Resolve(context)!.Variants!["palette/1"][0, 0]);
        TextureBuildContext fallback = Context("a0stg030.x", 7, new(new("battle/a0stg030.x"), "texture/0", 2, 0, 0, 64, 32, 64, 32));
        Assert.Equal(new Rgba32(10, 0, 0, 255), resolver.Resolve(fallback)!.Image[0, 0]);
    }

    [Fact]
    public void SourceBackedInspectionDistinguishesAbsentFilesFromUnsupportedLiveOwners()
    {
        using InMemoryResourceSource source = new(Source());
        source.Set(new("battle/d017"), [1], "dat");
        string[] names = ["battle/a0stg000.x-0_00", "battle/.a0stg030.x-0_01", "battle/d017-0_00", "battle/d018-0_00"];
        ModFile[] files = names.Select(name => new ModFile(name, ModFormat.Junction, $"mods/textures/{name}.dds", 1, () => throw new InvalidOperationException())).ToArray();
        DemasterInspection inspection = FfnxTextureResolver.Inspect(Source(), new(1, [], files), source);
        Assert.Equal(3, inspection.Diagnostics.Count(item => item.Code == "ffnx-unused-native-target"));
        Assert.Equal("battle/d017-0_00", Assert.Single(inspection.Diagnostics, item => item.Code == "ffnx-unsupported-target").PackageId);
        Assert.Empty(inspection.UsablePackageIds);
    }

    [Fact]
    public void InspectionRecognizesOnlyImplementedBattleFamilies()
    {
        string[] names = ["battle/a0stg000.x-0_00", "battle/c0m016.dat-2_01", "battle/d0c001.dat-1_00", "battle/d3w007.dat-0_00"];
        ModFile[] files = names.Select(name => new ModFile("test", ModFormat.Junction, $"mods/textures/{name}.dds", 1, () => throw new InvalidOperationException())).ToArray();
        Assert.Empty(FfnxTextureResolver.Inspect(Source(), new(1, [], files)).Diagnostics);
        ModFile invalid = new("bad", ModFormat.Junction, "mods/textures/battle/.a0stg030.x-0_01.dds", 1, () => throw new InvalidOperationException());
        Assert.Equal("ffnx-unsupported-target", Assert.Single(FfnxTextureResolver.Inspect(Source(), new(1, [], [invalid])).Diagnostics).Code);
    }

    [Fact]
    public void InspectionUsesTheOriginalCountedTextureSection()
    {
        using InMemoryResourceSource source = new(Source());
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(5);
            for (int section = 0; section < 5; section++)
            {
                writer.Write(28);
            }
            writer.Write(40);
            writer.Write(2);
            writer.Write(12);
            writer.Write(12);
        }
        source.Set(new("battle/d0w001.dat"), stream.ToArray(), "dat");
        ModFile[] files = Enumerable.Range(0, 3).Select(member => new ModFile("test", ModFormat.Junction, $"mods/textures/battle/d0w001.dat-{member}_00.dds", 1, () => throw new InvalidOperationException())).ToArray();
        DemasterInspection result = FfnxTextureResolver.Inspect(Source(), new(1, [], files), source);
        Assert.Contains("test", result.UsablePackageIds);
        Assert.EndsWith("-2_00.dds", Assert.Single(result.Diagnostics).RelativePath);
        Assert.Equal("ffnx-unused-native-target", Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData(746)]
    [InlineData(768)]
    public void StageCanvasAcceptsExactOrCompleteNativeVramPages(int canvasWidth)
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("battle/a0stg000.x-0_00", new(12, 34, 56, 255), canvasWidth * 2, 512)]));
        TextureBuildContext context = new(new(new("battle/a0stg000.x"), "texture/0"), Source(), 1, "classic-battle", 746, 256, () => new(746, 256), new(BitsPerPixel: 8));
        TextureReplacement result = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        Assert.Equal(1492, result.Image.Width);
        Assert.Equal(512, result.Image.Height);
        Assert.Equal(746, result.LogicalWidth);
    }

    [Theory]
    [InlineData("d0c001.dat", 128, 96, 128, 128)]
    [InlineData("a0stg030.x", 768, 208, 768, 256)]
    public void BattleCanvasCompletesTheNativeSlotHeightBeforeCropping(string owner, int width, int height, int canvasWidth, int canvasHeight)
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet($"battle/{owner}-0_00", new(12, 34, 56, 255), canvasWidth * 2, canvasHeight * 2)]));
        TextureBuildContext context = new(new(new("battle/" + owner), "texture/0"), Source(), 1, "classic-battle", width, height, () => new(width, height), new(BitsPerPixel: 8));
        TextureReplacement result = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        Assert.Equal(width * 2, result.Image.Width);
        Assert.Equal(height * 2, result.Image.Height);
        Assert.Equal(width, result.LogicalWidth);
        Assert.Equal(height, result.LogicalHeight);
    }

    [Theory]
    [InlineData("a0stg098.x", 448, 256, 1796, 1020)]
    [InlineData("a0stg160.x", 512, 256, 1024, 1024)]
    [InlineData("d0c001.dat", 128, 96, 1024, 769)]
    [InlineData("c0m016.dat", 128, 128, 97, 43)]
    public void ArbitraryBattleResolutionPreservesTheNativeLogicalMaterialRectangle(string owner, int width, int height, int imageWidth, int imageHeight)
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet($"battle/{owner}-0_00", new(12, 34, 56, 255), imageWidth, imageHeight)]));
        TextureBuildContext context = new(new(new("battle/" + owner), "texture/0"), Source(), 1, "classic-battle", width, height, () => new(width, height), new(BitsPerPixel: 8));
        TextureReplacement result = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        Assert.Equal(imageWidth, result.Image.Width);
        Assert.Equal(imageHeight, result.Image.Height);
        Assert.Equal(width, result.LogicalWidth);
        Assert.Equal(height, result.LogicalHeight);
        Assert.Equal(new Rgba32(12, 34, 56, 255), result.Image[imageWidth - 1, imageHeight - 1]);
    }

    [Fact]
    public void IndependentBattleDensitiesDoNotBroadenWorldImageAspectRules()
    {
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("world/esk/chara_one/model0-0_00", new(12, 34, 56, 255), 97, 43)]));
        TextureBuildContext context = new(new(new("world/chara.one"), "model/0/texture/0"), Source(), 1, "classic-model", 128, 128, () => new(128, 128));
        Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(context));
    }

    private static TextureBuildContext Context(string owner, int member, TextureSourceAddress? alias = null) => new(new(new("battle/" + owner), $"texture/{member}"), Source(), 1, "classic-battle", 64, 32, () => new(64, 32)) { NativeAlias = alias };

    private static ResourceSourceDescriptor Source() => new("test", GameDataEdition.Classic, ["classic-ffnx"], "en");

    private static ModFile Sheet(string name, Rgba32 color, int width = 128, int height = 64)
    {
        using MagickImage image = new(new MagickColor(color.R, color.G, color.B, color.A), (uint)width, (uint)height);
        byte[] png = image.ToByteArray(MagickFormat.Png);
        return new("test", ModFormat.Junction, $"mods/textures/{name}.png", 1, () => png);
    }
}
