using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class FfnxEffectTextureTests
{
    [Theory]
    [InlineData("magic/mag012.tim", "texture/2", "magic/mag012.tim-2_03")]
    [InlineData("magic/MAG012_B.04-9", "image", "magic/MAG012_B.04-9_03")]
    [InlineData("battle/MAG012_B.1T4", "texture/0", "battle/MAG012_B.1T4-0_03")]
    [InlineData("battle/A8DEF1.TIM", "image", "battle/A8DEF1.TIM_03")]
    [InlineData("battle/MA8DEF_7.TIM", "texture/0", "battle/MA8DEF_7.TIM-0_03")]
    public void ExplicitNativeOwnerAndMemberSelectTheExternalTexture(string owner, string member, string external)
    {
        ModFile file = Sheet(external, new(12, 34, 56, 255));
        TextureReplacement result = Assert.IsType<TextureReplacement>(new FfnxTextureResolver(new(1, [], [file])).Resolve(Context(owner, member, 3)));
        Assert.Equal(new Rgba32(12, 34, 56, 127), result.Image[0, 0]);
        Assert.Contains(file.RelativePath, result.Provenance);
        Assert.Equal(4, result.LogicalWidth);
    }

    [Fact]
    public void EffectIdentityIsNeverGuessedFromTheConsumerKey()
    {
        ModFile file = Sheet("magic/mag012.tim-0_00", new(20, 30, 40));
        FfnxTextureResolver resolver = new(new(1, [], [file]));
        TextureBuildContext context = new(new(new("magic/mag012.tim"), "texture/0"), Source(), 1, "native-effect-texture", 4, 4, () => new(4, 4));
        Assert.Null(resolver.Resolve(context));
        TextureBuildContext untyped = new(context.Key, Source(), 1, "classic-battle", 4, 4, () => new(4, 4)) { NativeAlias = new(new("magic/mag012.tim"), "texture/0", 0, 0, 0, 4, 4, 4, 4) };
        Assert.Null(resolver.Resolve(untyped));
    }

    [Fact]
    public void ExactPaletteAndFirstAvailableFallbackRetainActualProvenance()
    {
        ModFile first = Sheet("magic/mag012.tim-0_02", new(20, 0, 0));
        ModFile selected = Sheet("magic/mag012.tim-0_05", new(50, 0, 0));
        FfnxTextureResolver resolver = new(new(1, [], [selected, first]));
        TextureReplacement exact = resolver.Resolve(Context("magic/mag012.tim", "texture/0", 5))!;
        TextureReplacement fallback = resolver.Resolve(Context("magic/mag012.tim", "texture/0", 4))!;
        Assert.Equal((byte)50, exact.Image[0, 0].R);
        Assert.Equal((byte)20, fallback.Image[0, 0].R);
        Assert.Contains(first.RelativePath, fallback.Provenance);
        Assert.Contains(selected.RelativePath, exact.Provenance);
    }

    [Fact]
    public void EffectAlphaUsesTheDriverThresholdWithoutMutatingTheDecodedCache()
    {
        byte[] alphas = [0, 126, 127, 128, 255];
        byte[] bytes = RgbaDds(5, 1, alphas.SelectMany(alpha => new byte[] { 0, 0, 0, alpha }).ToArray());
        ModFile file = new("test", ModFormat.Junction, "mods/textures/magic/mag012.tim-0_00.dds", 1, () => bytes);
        DecodedModImage original = ModImageDecoder.Decode(file);
        TextureBuildContext context = Context("magic/mag012.tim", "texture/0", 0, 5, 1);
        TextureReplacement result = new FfnxTextureResolver(new(1, [], [file])).Resolve(context)!;
        Assert.Equal(new byte[] { 0, 0, 127, 127, 127 }, result.Image.Pixels.Select(pixel => pixel.A));
        Assert.Equal(alphas, original.Image.Pixels.Select(pixel => pixel.A));
        Assert.Same(result.Image, Assert.Single(result.MipLevels!));
    }

    [Fact]
    public void DdsPrecedesPngAndAliasesCropTheirNativeCanvas()
    {
        byte[] pixels = Enumerable.Range(0, 16).SelectMany(index => new byte[] { (byte)index, 20, 30, 255 }).ToArray();
        ModFile dds = new("dds", ModFormat.Junction, "mods/textures/magic/mag012.tim-0_00.dds", 1, () => RgbaDds(4, 4, pixels));
        ModFile png = Sheet("magic/mag012.tim-0_00", new(200, 0, 0));
        TextureBuildContext context = new(new(new("consumer"), "page"), Source(), 1, "native-effect-texture", 2, 2, () => new(2, 2)) { NativeAlias = new(new("magic/mag012.tim"), "texture/0", 0, 1, 1, 2, 2, 4, 4) };
        TextureReplacement result = new FfnxTextureResolver(new(1, [], [png, dds])).Resolve(context)!;
        Assert.Equal(new byte[] { 5, 6, 9, 10 }, result.Image.Pixels.Select(pixel => pixel.R));
        Assert.StartsWith("dds/", result.Provenance);
        Assert.Equal(2, result.LogicalWidth);
    }

    [Fact]
    public void AlphaNormalizationPreservesEveryDecodedMip()
    {
        byte[] bytes = RgbaDds(2, 2, Enumerable.Repeat(new byte[] { 10, 20, 30, 255 }, 5).SelectMany(pixel => pixel).ToArray(), 2);
        ModFile file = new("test", ModFormat.Junction, "mods/textures/magic/mag012.tim-0_00.dds", 1, () => bytes);
        TextureReplacement result = new FfnxTextureResolver(new(1, [], [file])).Resolve(Context("magic/mag012.tim", "texture/0", 0, 2, 2))!;
        Assert.Equal(2, result.MipLevels!.Count);
        Assert.Equal(1, result.MipLevels[1].Width);
        Assert.All(result.MipLevels.SelectMany(level => level.Pixels), color => Assert.Equal(new Rgba32(10, 20, 30, 127), color));
        Assert.All(ModImageDecoder.Decode(file).MipLevels.SelectMany(level => level.Pixels), color => Assert.Equal((byte)255, color.A));
    }

    [Fact]
    public void PageJoinsMembersInVramOrderAndUsesExactClutBeforeZeroFallback()
    {
        TexturePageMember[] members = [new("effect", "MAG013_B.02-3", -1, 100, 300, 16, 16, 8, -1), new("effect", "MAG012_B.01-2", -1, 200, 20, 16, 16, 8, -1)];
        string stem = "magic/MAG012_B.01-2_MAG013_B.02-3_23";
        FfnxTextureResolver resolver = new(new(1, [], [Sheet(stem + "_320_224", new(90, 0, 0), 256, 256), Sheet(stem + "_0_0", new(10, 0, 0), 256, 256)]));
        TextureBuildContext exact = PageContext(members, 23, 320, 224);
        TextureBuildContext fallback = PageContext(members, 23, 336, 224);
        Assert.Equal((byte)90, resolver.Resolve(exact)!.Image[0, 0].R);
        Assert.Equal((byte)10, resolver.Resolve(fallback)!.Image[0, 0].R);
        Assert.Equal(4, resolver.Resolve(exact)!.Image.Width);
    }

    [Fact]
    public void SinglePageUsesRelativePageAndRetainedCopyFrame()
    {
        TexturePageMember[] members = [new("effect", "MAG012_E.DAT", 0, 640, 256, 64, 256, 8, 2)];
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("magic/MAG012_E.DAT-0_f2_1_320_224", new(70, 0, 0), 512, 512)]));
        TextureReplacement result = resolver.Resolve(PageContext(members, 27, 320, 224))!;
        Assert.Equal((byte)70, result.Image[0, 0].R);
        Assert.Equal(8, result.Image.Width);
        Assert.Equal(4, result.LogicalWidth);
    }

    [Fact]
    public void AnotherPaletteCoordinatesCannotReplaceAnUnmatchedPage()
    {
        TexturePageMember[] members = [new("effect", "MAG012_B.01-2", -1, 0, 0, 16, 16, 8, -1)];
        FfnxTextureResolver resolver = new(new(1, [], [Sheet("magic/MAG012_B.01-2_0_320_225", new(70, 0, 0), 256, 256)]));
        Assert.Null(resolver.Resolve(PageContext(members, 0, 320, 224)));
    }

    [Fact]
    public void PageCropUsesTheRegisteredLocalPixelOrigin()
    {
        TexturePageMember[] members = [new("effect", "MAG012_B.01-2", -1, 0, 0, 64, 256, 8, -1)];
        byte[] pixels = Enumerable.Range(0, 256 * 256).SelectMany(index => new byte[] { (byte)(index % 256), (byte)(index / 256), 30, 255 }).ToArray();
        ModFile file = new("test", ModFormat.Junction, "mods/textures/magic/MAG012_B.01-2_0_320_224.dds", 1, () => RgbaDds(256, 256, pixels));
        TextureReplacement result = new FfnxTextureResolver(new(1, [], [file])).Resolve(PageContext(members, 0, 320, 224))!;
        Assert.Equal(new Rgba32(8, 12, 30, 127), result.Image[0, 0]);
        Assert.Equal(new Rgba32(11, 15, 30, 127), result.Image[3, 3]);
    }

    [Fact]
    public void MalformedPaletteArtifactsDoNotBecomeFallbacks()
    {
        ModFile artifact = new("invalid", ModFormat.Junction, "mods/textures/magic/mag012.tim-0_000.dds", 1, () => throw new InvalidOperationException("Noncanonical palette must remain unread."));
        FfnxTextureResolver resolver = new(new(1, [], [artifact]));
        Assert.Null(resolver.Resolve(Context("magic/mag012.tim", "texture/0", 0)));
        Assert.Equal("invalid", Assert.Single(FfnxTextureResolver.Inspect(Source(), new(1, [], [artifact])).Diagnostics).PackageId);
    }

    [Theory]
    [InlineData("magic/mag012.tim-0_00", true)]
    [InlineData("magic/mag012-034.tim-1_255", true)]
    [InlineData("battle/MAG012_B.1T4-0_00", true)]
    [InlineData("battle/MA8DEF_0.TIM-0_01", true)]
    [InlineData("magic/MAG012_B.01-2_MAG013_B.02-3_23_320_224", true)]
    [InlineData("battle/MAG012_E.DAT-0_f2_1_320_224", true)]
    [InlineData("magic/mag012.tim-0_256", false)]
    [InlineData("magic/mag012.tim-0_000", false)]
    [InlineData("battle/MAG012_E.DAT-0_002222", false)]
    [InlineData("magic/MAG012_B.01-2_32_320_224", false)]
    [InlineData("magic/MAG012_B.01-2_0_1024_224", false)]
    [InlineData("magic/MAG012_B.01-2_0_320_512", false)]
    [InlineData("magic/mag012.tim-99999999999999999_00", false)]
    public void InspectionReportsIndividualMalformedTargetsWithoutDecoding(string name, bool supported)
    {
        ModFile file = new("fixture", ModFormat.Junction, $"mods/textures/{name}.dds", 1, () => throw new InvalidOperationException());
        DemasterInspection result = FfnxTextureResolver.Inspect(Source(), new(1, [], [file]));
        Assert.Equal(supported, result.UsablePackageIds.Contains("fixture"));
        Assert.Equal(supported ? 0 : 1, result.Diagnostics.Count);
        if (!supported)
        {
            Assert.Equal(file.RelativePath, Assert.Single(result.Diagnostics).RelativePath);
        }
    }

    private static TextureBuildContext Context(string owner, string subresource, int palette, int width = 4, int height = 4) => new(new(new("consumer"), "page"), Source(), 1, "native-effect-texture", width, height, () => new(width, height)) { NativeAlias = new(new(owner), subresource, palette, 0, 0, width, height, width, height) };

    private static TextureBuildContext PageContext(TexturePageMember[] members, int page, int paletteX, int paletteY) => new(new(new("consumer"), "page"), Source(), 1, "native-effect-texture", 4, 4, () => new(4, 4)) { NativeAlias = new(new("magic/MAG012_B.01-2"), "image", 0, 0, 0, 4, 4, 4, 4), NativePage = new(page, 8, 12, 4, 4, paletteX, paletteY, members) };

    private static ResourceSourceDescriptor Source() => new("test", GameDataEdition.Classic, ["classic-ffnx"], "en");

    private static ModFile Sheet(string name, Rgba32 color, int width = 4, int height = 4)
    {
        using MagickImage image = new(new MagickColor(color.R, color.G, color.B, color.A), (uint)width, (uint)height);
        byte[] png = image.ToByteArray(MagickFormat.Png);
        return new("test", ModFormat.Junction, $"mods/textures/{name}.png", 1, () => png);
    }

    private static byte[] RgbaDds(int width, int height, byte[] pixels, int mips = 1)
    {
        byte[] bytes = new byte[128 + pixels.Length];
        Write(0, 0x20534444);
        Write(4, 124);
        Write(8, 0x100F);
        Write(12, (uint)height);
        Write(16, (uint)width);
        Write(20, (uint)width * 4);
        Write(28, (uint)mips);
        Write(76, 32);
        Write(80, 0x41);
        Write(88, 32);
        Write(92, 0xFF);
        Write(96, 0xFF00);
        Write(100, 0xFF0000);
        Write(104, 0xFF000000);
        Write(108, 0x1000);
        pixels.CopyTo(bytes, 128);
        return bytes;

        void Write(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
