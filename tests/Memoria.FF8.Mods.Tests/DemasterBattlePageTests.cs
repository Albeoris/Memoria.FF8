using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterBattlePageTests
{
    private const string Layout = "native-battle-stage-page";
    private const string Stem = "textures/battle.fs/hd_new/a0stg001_16";

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    public void NativeStageAndPageIdentityIgnoresClut(int page)
    {
        string path = $"textures/battle.fs/hd_new/a0stg001_{page}.png";
        DemasterTextureResolver resolver = Resolver(File(path));
        foreach (int palette in new[] { 0, 37, 32767 })
        {
            TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(page: $"page/{page}", palette: palette)));
            Assert.Contains(path, replacement.Provenance);
            Assert.Equal(256, replacement.LogicalWidth);
            Assert.Null(replacement.Animation);
        }
        Assert.Null(resolver.Resolve(Context(owner: "battle/a0stg002.x", page: $"page/{page}")));
    }

    [Theory]
    [InlineData("battle/a0stg01.x", "page/16", Layout, true, 256)]
    [InlineData("battle/a0stg001.x", "page/15", Layout, true, 256)]
    [InlineData("battle/a0stg001.x", "page/22", Layout, true, 256)]
    [InlineData("battle/a0stg001.x", "page/016", Layout, true, 256)]
    [InlineData("battle/a0stg001.x", "page/16", "other", true, 256)]
    [InlineData("battle/a0stg001.x", "page/16", Layout, false, 256)]
    [InlineData("battle/a0stg001.x", "page/16", Layout, true, 128)]
    public void UnsupportedIdentityNeverReadsReplacement(string owner, string page, string layout, bool published, int size)
    {
        ModFile file = new("Demaster/Stage", ModFormat.Demaster, Stem + ".png", 1, () => throw new InvalidOperationException("Unsupported identity must remain lazy."));
        Assert.Null(Resolver(file).Resolve(Context(owner, page, layout: layout, published: published, size: size)));
    }

    [Fact]
    public void InspectionRequiresPublishedLayoutAndExactTargetNames()
    {
        string[] valid = [Stem + ".dds", Stem + "_0.png", Stem + "_123.png"];
        string[] invalid = [Stem + "_01.png", Stem + "_x.png", "textures/battle.fs/hd_new/a0stg01_16.png", "textures/battle.fs/hd_new/a0stg001_22.png"];
        ModFile[] files = valid.Concat(invalid).Select(path => new ModFile("Demaster/Stage", ModFormat.Demaster, path, 1, () => throw new InvalidOperationException("Inspection must remain lazy."))).ToArray();
        ModCatalogSnapshot catalog = new(1, [], files);
        DemasterInspection supported = DemasterTextureResolver.Inspect(new("test", GameDataEdition.Classic, [Layout]), catalog);
        Assert.Single(supported.UsablePackageIds);
        Assert.Equal(invalid.Order(), supported.Diagnostics.Select(diagnostic => diagnostic.RelativePath).Order());
        Assert.Equal(files.Length, DemasterTextureResolver.Inspect(new("test", GameDataEdition.Remastered, ["demaster-direct"]), catalog).Diagnostics.Count);
    }

    [Fact]
    public void DdsPrecedesPngAndInvalidPreferredFileFailsWithOwnership()
    {
        ModFile dds = new("Demaster/Stage", ModFormat.Demaster, Stem + ".dds", 1, Dds);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(File(Stem + ".png"), dds).Resolve(Context()));
        Assert.EndsWith(".dds@1", replacement.Provenance);
        Assert.Equal(new Rgba32(255, 0, 0), replacement.Image[0, 0]);
        ModFile invalid = new("Demaster/Broken", ModFormat.Demaster, Stem + ".dds", 1, () => [0]);
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(invalid, File(Stem + ".png")).Resolve(Context()));
        Assert.Equal("Demaster/Broken", failure.PackageId);
        Assert.Equal(Stem + ".dds", failure.RelativePath);
    }

    [Fact]
    public void NumberedFramesPrecedeBaseAndPublishLoopTiming()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(File(Stem + ".dds"), File(Stem + "_0.png"), File(Stem + "_1.png", color: MagickColors.Blue)).Resolve(Context()));
        TextureAnimation animation = Assert.IsType<TextureAnimation>(replacement.Animation);
        Assert.Equal(2, animation.Frames.Count);
        Assert.Equal(100, animation.FrameDurationMilliseconds);
        Assert.Same(replacement.Image, animation.Frames[0]);
        Assert.Same(animation.Frames[0], animation.At(TimeSpan.FromMilliseconds(200)));
        Assert.Equal(new Rgba32(0, 0, 255), animation.At(TimeSpan.FromMilliseconds(100))[0, 0]);
        Assert.DoesNotContain(Stem + ".dds", replacement.Provenance);
    }

    [Fact]
    public void MissingFrameRestartsAtZeroAndOrphansUseBaseFallback()
    {
        ModFile unreachable = new("Demaster/Stage", ModFormat.Demaster, Stem + "_2.png", 1, () => throw new InvalidOperationException("Frames after a gap are unreachable."));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(File(Stem + "_0.png"), unreachable).Resolve(Context()));
        Assert.Single(replacement.Animation!.Frames);
        replacement = Assert.IsType<TextureReplacement>(Resolver(File(Stem + ".png"), unreachable).Resolve(Context()));
        Assert.Null(replacement.Animation);
        Assert.Null(Resolver(unreachable).Resolve(Context()));
    }

    [Theory]
    [InlineData(128, 128)]
    [InlineData(300, 300)]
    [InlineData(256, 512)]
    [InlineData(512, 512)]
    public void MismatchedAnimationFrameDimensionsFailWithFileOwnership(int width, int height)
    {
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(File(Stem + "_0.png"), File(Stem + "_1.png", width, height)).Resolve(Context()));
        Assert.Equal("Demaster/Stage", failure.PackageId);
        Assert.Equal(Stem + "_1.png", failure.RelativePath);
    }

    [Theory]
    [InlineData(128, 128)]
    [InlineData(300, 300)]
    [InlineData(256, 512)]
    [InlineData(513, 509)]
    [InlineData(7, 11)]
    public void IndependentPhysicalDimensionsPreserveNativeCanvasForStaticAndAnimatedPages(int width, int height)
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(File(Stem + ".png", width, height)).Resolve(Context()));
        Assert.Equal(width, replacement.Image.Width);
        Assert.Equal(height, replacement.Image.Height);
        Assert.Equal(256, replacement.LogicalWidth);
        Assert.Equal(256, replacement.LogicalHeight);
        replacement = Assert.IsType<TextureReplacement>(Resolver(File(Stem + "_0.png", width, height), File(Stem + "_1.png", width, height)).Resolve(Context()));
        Assert.Equal(2, replacement.Animation!.Frames.Count);
        Assert.All(replacement.Animation.Frames, frame =>
        {
            Assert.Equal(width, frame.Width);
            Assert.Equal(height, frame.Height);
        });
    }

    private static TextureBuildContext Context(string owner = "battle/a0stg001.x", string page = "page/16", int palette = 0, string layout = Layout, bool published = true, int size = 256)
    {
        return new(new(new(owner), page, palette), new("test", GameDataEdition.Classic, published ? [Layout] : []), 1, layout, size, size, () => throw new InvalidOperationException("Semantic lookup must not decode native pixels."));
    }

    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));

    private static ModFile File(string path, int width = 256, int height = 256, IMagickColor<byte>? color = null)
    {
        using MagickImage image = new(color ?? MagickColors.Red, (uint)width, (uint)height);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/Stage", ModFormat.Demaster, path, 1, () => bytes);
    }

    private static byte[] Dds()
    {
        byte[] bytes = new byte[128 + 64 * 64 * 8];
        Write(0, 0x20534444);
        Write(4, 124);
        Write(12, 256);
        Write(16, 256);
        Write(28, 1);
        Write(76, 32);
        Write(80, 4);
        Write(84, 0x31545844);
        for (int offset = 128; offset < bytes.Length; offset += 8)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), 0xf800);
        }
        return bytes;

        void Write(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
