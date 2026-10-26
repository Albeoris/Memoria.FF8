using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterFieldPageTests
{
    private const string Layout = "native-field-background-page";
    private const string Stem = "textures/field_bg/te/testmap/testmap_0";
    private static readonly ResourceSourceDescriptor Source = new("test", GameDataEdition.Classic, [Layout]);

    [Theory]
    [InlineData(256)]
    [InlineData(768)]
    [InlineData(1024)]
    public void NativePixelCoordinatesCropTheFullResolutionArtworkAndMips(int size)
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(DdsFile(Stem + "_3.dds", size)).Resolve(Context(x: 32, y: 48)));
        Assert.Equal((size / 16, size / 16, 16, 16), (replacement.Image.Width, replacement.Image.Height, replacement.LogicalWidth, replacement.LogicalHeight));
        Assert.Equal(Pixel(32 * size / 256, 48 * size / 256, 0), replacement.Image[0, 0]);
        Assert.Equal(Pixel(48 * size / 256 - 1, 64 * size / 256 - 1, 0), replacement.Image[size / 16 - 1, size / 16 - 1]);
        Assert.NotEmpty(replacement.MipLevels!);
        Assert.All(replacement.MipLevels!, mip => Assert.True(mip.Width > 0 && mip.Height > 0));
        if (size == 1024)
        {
            Assert.Equal(new[] { 64, 32, 16, 8, 4, 2, 1 }, replacement.MipLevels!.Select(mip => mip.Width));
            Assert.Equal(Pixel(64, 96, 1), replacement.MipLevels![1][0, 0]);
        }
        Assert.Null(replacement.Animation);
    }

    [Fact]
    public void MipsStopBeforeAnyFractionalSourcePixelBoundary()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(DdsFile(Stem + "_3.dds", 1024)).Resolve(Context(x: 1, y: 3)));
        Assert.Equal(new[] { 64, 32, 16 }, replacement.MipLevels!.Select(mip => mip.Width));
    }

    [Fact]
    public void BitDepthDoesNotChangeNativePixelUvsAndAnimationTilesKeepDistinctUvs()
    {
        DemasterTextureResolver resolver = Resolver(DdsFile(Stem + "_3.dds", 1024));
        foreach (int bpp in new[] { 4, 8, 16 })
        {
            TextureReplacement first = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(x: 16, y: 32, bpp: bpp, tile: 7)));
            TextureReplacement next = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(x: 80, y: 96, bpp: bpp, tile: 8)));
            Assert.Equal(Pixel(64, 128, 0), first.Image[0, 0]);
            Assert.Equal(Pixel(320, 384, 0), next.Image[0, 0]);
            Assert.Null(first.Animation);
            Assert.Null(next.Animation);
        }
    }

    [Fact]
    public void ExactPaletteThenPlainPageUsesDdsBeforePngWithoutPaletteZeroFallback()
    {
        string[] paths = [Stem + "_3.dds", Stem + "_3.png", Stem + ".dds", Stem + ".png"];
        Assert.Equal(paths, DemasterTextureResolver.CandidatePaths(Context()));
        ModFile[] files = paths.Select(path => path.EndsWith(".dds", StringComparison.Ordinal) ? DdsFile(path, 256) : PngFile(path)).ToArray();
        for (int start = 0; start < files.Length; start++)
        {
            TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(files[start..]).Resolve(Context()));
            Assert.Contains(paths[start] + "@1", replacement.Provenance);
        }
        Assert.Null(Resolver(PngFile(Stem + "_0.png")).Resolve(Context()));
        Assert.Null(Resolver().Resolve(Context()));
    }

    [Fact]
    public void CorruptPreferredFileRetainsOriginalPageWhileValidPagesPublishAndReportsAfterNotification()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        AssetHandle<TextureValue> first = resources.GetTexture(Context(tile: 0));
        TextureBuildContext secondContext = Context(tile: 1, palette: 4);
        AssetHandle<TextureValue> second = resources.GetTexture(secondContext);
        TextureValue oldFirst = first.Value;
        TextureValue oldSecond = second.Value;
        ModFile corrupt = new("Demaster/Broken", ModFormat.Demaster, Stem + "_4.dds", 1, () => [0]);
        DemasterTextureResolver resolver = Resolver(PngFile(Stem + "_3.png"), corrupt, PngFile(Stem + "_4.png"));
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(secondContext));
        Assert.Equal("Demaster/Broken", failure.PackageId);
        Assert.Equal(Stem + "_4.dds", failure.RelativePath);
        List<(TextureKey Key, Exception Exception)> reports = [];
        resources.ReplacementFailed += (key, exception) => reports.Add((key, exception));
        ResourceService.PreparedResources prepared = resources.Prepare(resolver);
        Assert.Same(oldFirst, first.Value);
        Assert.Same(oldSecond, second.Value);
        Assert.Equal(0, resources.Generation);
        Assert.Empty(reports);
        prepared.Commit(notify: false);
        Assert.True(first.Value.IsReplacement);
        Assert.False(second.Value.IsReplacement);
        Assert.Same(oldSecond.Image, second.Value.Image);
        Assert.Equal(1, resources.Generation);
        Assert.Empty(reports);
        prepared.Notify();
        var report = Assert.Single(reports);
        Assert.Equal(secondContext.Key, report.Key);
        ModTextureLoadException reported = Assert.IsType<ModTextureLoadException>(report.Exception);
        Assert.Equal(failure.PackageId, reported.PackageId);
        Assert.Equal(failure.RelativePath, reported.RelativePath);
    }

    [Theory]
    [InlineData(128, 128)]
    [InlineData(300, 300)]
    [InlineData(256, 512)]
    public void UnsupportedArtworkDimensionsFailWithFileOwnership(int width, int height)
    {
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(PngFile(Stem + "_3.png", width, height)).Resolve(Context()));
        Assert.Equal("Demaster/Field", failure.PackageId);
        Assert.Equal(Stem + "_3.png", failure.RelativePath);
    }

    [Theory]
    [InlineData("field/testmap/other.mim", "page/16", 0, 0, true)]
    [InlineData("world/testmap/testmap.mim", "page/16", 0, 0, true)]
    [InlineData("field/testmap/testmap.mim", "page/15", 0, 0, true)]
    [InlineData("field/testmap/testmap.mim", "page/32", 0, 0, true)]
    [InlineData("field/testmap/testmap.mim", "page/016", 0, 0, true)]
    [InlineData("field/testmap/testmap.mim", "page/16", -1, 0, true)]
    [InlineData("field/testmap/testmap.mim", "page/16", 241, 0, true)]
    [InlineData("field/testmap/testmap.mim", "page/16", 0, 241, true)]
    [InlineData("field/testmap/testmap.mim", "page/16", 0, 0, false)]
    public void UnsupportedMetadataNeverOpensArtwork(string owner, string page, int x, int y, bool published)
    {
        ModFile lazy = new("Demaster/Field", ModFormat.Demaster, Stem + "_3.png", 1, () => throw new InvalidOperationException("Unsupported metadata must stay lazy."));
        Assert.Null(Resolver(lazy).Resolve(Context(owner: owner, page: page, x: x, y: y, published: published)));
    }

    [Fact]
    public void AliasPaletteAndOwnerMustMatchTileIdentity()
    {
        TextureBuildContext original = Context();
        TextureBuildContext mismatch = new(original.Key, Source, 1, original.Layout, 16, 16, () => new(16, 16)) { NativeAlias = original.NativeAlias! with { Palette = 0 } };
        Assert.Empty(DemasterTextureResolver.CandidatePaths(mismatch));
        mismatch = new(original.Key, Source, 1, original.Layout, 16, 16, () => new(16, 16)) { NativeAlias = original.NativeAlias! with { Owner = new("field/another/another.mim") } };
        Assert.Empty(DemasterTextureResolver.CandidatePaths(mismatch));
    }

    [Fact]
    public void InspectionRequiresPublishedLayoutAndCanonicalPagePaletteAndMapIdentity()
    {
        string[] valid = [Stem + ".dds", Stem + "_0.png", Stem + "_15.png", "textures/field_bg/te/testmap/testmap_15_15.dds"];
        string[] invalid = [Stem + "_16.png", Stem + "_01.png", Stem + "_frame.png", "textures/field_bg/xx/testmap/testmap_0.png", "textures/field_bg/te/testmap/other_0.png", "textures/field_bg/te/testmap/testmap_16.png", "textures/field_bg/te/testmap/testmap_00.png"];
        ModCatalogSnapshot catalog = new(1, [], valid.Concat(invalid).Select(path => new ModFile("Demaster/Field", ModFormat.Demaster, path, 1, () => throw new InvalidOperationException("Inspection must stay lazy."))).ToArray());
        DemasterInspection inspection = DemasterTextureResolver.Inspect(Source, catalog);
        Assert.Single(inspection.UsablePackageIds);
        Assert.Equal(invalid.Order(), inspection.Diagnostics.Select(diagnostic => diagnostic.RelativePath).Order());
        Assert.Equal(valid.Length + invalid.Length, DemasterTextureResolver.Inspect(new("test", GameDataEdition.Remastered, ["demaster-direct"]), catalog).Diagnostics.Count);
    }

    private static TextureBuildContext Context(string owner = "field/testmap/testmap.mim", string page = "page/16", int palette = 3, int x = 0, int y = 0, int bpp = 4, int tile = 0, bool published = true)
    {
        return new(new(new(owner), $"tile/{tile}", palette), published ? Source : new("test", GameDataEdition.Classic, []), 1, "classic-field-tile", 16, 16, () => new(16, 16), new(BitsPerPixel: bpp))
        {
            NativeAlias = new(new(owner), page, palette, x, y, 16, 16, 256, 256)
        };
    }

    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));

    private static ModFile PngFile(string path, int width = 256, int height = 256)
    {
        using MagickImage image = new(MagickColors.Blue, (uint)width, (uint)height);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/Field", ModFormat.Demaster, path, 1, () => bytes);
    }

    private static Rgba32 Pixel(int x, int y, int level) => new((byte)(x % 251), (byte)(y % 251), (byte)level);

    private static ModFile DdsFile(string path, int size)
    {
        using MemoryStream stream = new();
        byte[] header = new byte[128];
        Write(0, 0x20534444);
        Write(4, 124);
        Write(12, (uint)size);
        Write(16, (uint)size);
        Write(28, (uint)(1 + (int)Math.Floor(Math.Log2(size))));
        Write(76, 32);
        Write(80, 0x41);
        Write(88, 32);
        Write(92, 0xFF);
        Write(96, 0xFF00);
        Write(100, 0xFF0000);
        Write(104, 0xFF000000);
        stream.Write(header);
        int level = 0;
        for (int width = size; ; width = Math.Max(1, width / 2), level++)
        {
            for (int y = 0; y < width; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Rgba32 pixel = Pixel(x, y, level);
                    stream.WriteByte(pixel.R);
                    stream.WriteByte(pixel.G);
                    stream.WriteByte(pixel.B);
                    stream.WriteByte(255);
                }
            }
            if (width == 1)
            {
                break;
            }
        }
        byte[] bytes = stream.ToArray();
        return new("Demaster/Field", ModFormat.Demaster, path, 1, () => bytes);

        void Write(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(offset), value);
        }
    }
}
