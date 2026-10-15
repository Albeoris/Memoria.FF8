using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterTextureTests
{
    [Fact]
    public void ImmutableImageRevisionIsSharedAcrossResolverGenerations()
    {
        byte[] png = Png(256, 256, MagickColors.Red);
        ModFile file = new("Demaster/A", ModFormat.Demaster, "textures/world/dat/texl/texl_001_0.png", 1, () => png);
        TextureBuildContext context = Context("world/texl.obj", "texl/0", "classic-world-texl");
        DemasterTextureResolver first = new(new(1, [], [file]));
        DemasterTextureResolver second = new(new(2, [], [file]));
        TextureReplacement original = Assert.IsType<TextureReplacement>(first.Resolve(context));
        TextureReplacement reused = Assert.IsType<TextureReplacement>(second.Resolve(context));
        Assert.Same(original.Image, reused.Image);
        Assert.Same(original.MipLevels![0], reused.MipLevels![0]);
        ModFile changed = new(file.PackageId, file.Format, file.RelativePath, 2, () => Png(256, 256, MagickColors.Blue));
        DemasterTextureResolver third = new(new(3, [], [changed]));
        Assert.NotSame(original.Image, Assert.IsType<TextureReplacement>(third.Resolve(context)).Image);
        Assert.Equal(new Rgba32(0, 0, 255), Assert.IsType<TextureReplacement>(third.Resolve(context)).Image[0, 0]);
    }

    [Fact]
    public void DecodedPixelsCanBeCollectedWhileIndexedFileRemainsAlive()
    {
        byte[] png = Png(256, 256, MagickColors.Red);
        ModFile file = new("Demaster/A", ModFormat.Demaster, "textures/world/dat/texl/texl_001_0.png", 1, () => png);
        WeakReference<PixelImage> previous = DecodeAndForget(file);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(previous.TryGetTarget(out _));
        DecodedModImage reloaded = ModImageDecoder.Decode(file);
        Assert.Equal(new Rgba32(255, 0, 0), reloaded.Image[0, 0]);
        GC.KeepAlive(file);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PixelImage> DecodeAndForget(ModFile file)
    {
        return new WeakReference<PixelImage>(ModImageDecoder.Decode(file).Image);
    }

    [Fact]
    public void TruncatedPngDecodeIsNormalizedForResourcePreparation()
    {
        byte[] png = Png(256, 256, MagickColors.Red);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => ModImageDecoder.Decode(png[..40], "truncated.png"));
        Assert.NotNull(error.InnerException);
    }

    [Fact]
    public void CachedFileDecodeFailurePreservesPackageProvenanceAndCause()
    {
        byte[] png = Png(256, 256, MagickColors.Red);
        ModFile file = new("Demaster/BrokenPack", ModFormat.Demaster, "textures/world/dat/texl/texl_001_0.png", 9, () => png[..40]);
        ModTextureLoadException error = Assert.Throws<ModTextureLoadException>(() => ModImageDecoder.Decode(file));
        Assert.IsAssignableFrom<IOException>(error);
        Assert.Equal(file.PackageId, error.PackageId);
        Assert.Equal(file.RelativePath, error.RelativePath);
        InvalidDataException decodedError = Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.NotNull(decodedError.InnerException);
        ModTextureLoadException retried = Assert.Throws<ModTextureLoadException>(() => ModImageDecoder.Decode(file));
        Assert.NotSame(error, retried);
        Assert.Equal(file.PackageId, retried.PackageId);
        Assert.Equal(file.RelativePath, retried.RelativePath);
    }

    [Fact]
    public void InspectClassifiesIndividualTargetsWithoutOpeningImages()
    {
        ModFile terrain = MetadataFile("Demaster/Mixed", "textures/world/dat/texl/texl_001_0.png");
        ModFile road = MetadataFile("Demaster/Roads", "textures/world/dat/wmset/wmset_029_0.dds");
        ModFile cloud = MetadataFile("Demaster/Mixed", "textures/world/dat/wmset/wmset_014_0.png");
        ModFile model = MetadataFile("Demaster/ModelsOnly", "textures/field.fs/field_hd/d000_0.png");
        ModCatalogSnapshot catalog = new(1, [], [terrain, road, cloud, model]);
        DemasterInspection result = DemasterTextureResolver.Inspect(new("classic", GameDataEdition.Classic, ["classic-world-texl", "classic-world-roads"]), catalog);
        Assert.Equal(2, result.UsablePackageIds.Count);
        Assert.Contains("Demaster/Mixed", result.UsablePackageIds);
        Assert.Contains("Demaster/Roads", result.UsablePackageIds);
        Assert.DoesNotContain("Demaster/ModelsOnly", result.UsablePackageIds);
        Assert.Equal(new[] { cloud.RelativePath, model.RelativePath }, result.Diagnostics.Select(diagnostic => diagnostic.RelativePath));
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("demaster-unsupported-target", diagnostic.Code));
    }

    [Fact]
    public void InspectRequiresExplicitNativeLayoutsAndCanonicalWorldNames()
    {
        ModCatalogSnapshot catalog = new(1, [], [MetadataFile("Demaster/Native", "textures/world/dat/wmset/wmset0_029_0.dds"), MetadataFile("Demaster/Native", "textures/world/dat/wmset/wmset_014_0.png"), MetadataFile("Demaster/Malformed", "textures/world/dat/texl/texl_1_0.png")]);
        DemasterInspection unavailable = DemasterTextureResolver.Inspect(new("future", GameDataEdition.Remastered, []), catalog);
        Assert.Empty(unavailable.UsablePackageIds);
        Assert.Equal(3, unavailable.Diagnostics.Count);
        DemasterInspection available = DemasterTextureResolver.Inspect(new("future", GameDataEdition.Remastered, ["demaster-world-wmset", "demaster-world-wmset0"]), catalog);
        Assert.Equal("Demaster/Native", Assert.Single(available.UsablePackageIds));
        Assert.Single(available.Diagnostics);
    }

    [Fact]
    public void TexlExportAddsOneAndRetainsPaletteFallbackOrder()
    {
        TextureBuildContext context = Context("world/texl.obj", "texl/0", "classic-world-texl", 3);
        Assert.Equal(new[] { "textures/world/dat/texl/texl_001_3.dds", "textures/world/dat/texl/texl_001_3.png", "textures/world/dat/texl/texl_001_0.dds", "textures/world/dat/texl/texl_001_0.png" }, DemasterTextureResolver.CandidatePaths(context));
        Assert.Equal(2, DemasterTextureResolver.CandidatePaths(Context("world/texl.obj", "texl/18", "classic-world-texl")).Count);
        Assert.EndsWith("texl_019_0.dds", DemasterTextureResolver.CandidatePaths(Context("world/texl.obj", "texl/18", "classic-world-texl"))[0]);
    }

    [Fact]
    public void ResolveUsesVirtualNamespaceWithoutReadingBaseOrInferringEditionFromPackageName()
    {
        byte[] png = Png(1024, 1024, new MagickColor("#12345678"));
        ModFile file = new("Demaster/HorizonPack-Remastered", ModFormat.Demaster, "textures/world/dat/texl/texl_001_0.png", 7, () => png);
        DemasterTextureResolver resolver = new(new ModCatalogSnapshot(1, [], [file]));
        TextureReplacement result = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("world/texl.obj", "texl/0", "classic-world-texl", 4)));
        Assert.Equal(1024, result.Image.Width);
        Assert.Equal(256, result.LogicalWidth);
        Assert.Equal(new Rgba32(0x12, 0x34, 0x56, 0x78), result.Image[0, 0]);
        Assert.Contains("HorizonPack-Remastered", result.Provenance);
    }

    [Fact]
    public void UnsupportedOrDifferentSourceLayoutCannotAccidentallyMatch()
    {
        Assert.Empty(DemasterTextureResolver.CandidatePaths(Context("world/texl.obj", "texl/0", "unknown-layout")));
        Assert.Empty(DemasterTextureResolver.CandidatePaths(Context("world/wmsetus.obj", "water", "classic-world-water")));
        Assert.Empty(DemasterTextureResolver.CandidatePaths(Context("battle/c0m001.dat", "0", "classic-model")));
        Assert.EndsWith("wmset_029_0.dds", DemasterTextureResolver.CandidatePaths(Context("world/wmsetus.obj", "roads", "classic-world-roads"))[0]);
        Assert.EndsWith("wmset0_029_0.dds", DemasterTextureResolver.CandidatePaths(Context("world/wmsetus.obj", "29", "demaster-world-wmset0"))[0]);
        Assert.EndsWith("wmset_014_0.dds", DemasterTextureResolver.CandidatePaths(Context("world/wmsetus.obj", "14", "demaster-world-wmset"))[0]);
    }

    [Fact]
    public void DdsWinsBeforePngAndMissingRequestedPaletteFallsBackToZero()
    {
        ModFile png = new("Demaster/A", ModFormat.Demaster, "textures/world/dat/texl/texl_001_0.png", 1, () => throw new InvalidOperationException("PNG should not be decoded."));
        TextureBuildContext context = new(new(new("world/texl.obj"), "texl/0", 8), new("memory", GameDataEdition.Classic, []), 1, "classic-world-texl", 256, 256, () => throw new InvalidOperationException());
        // Use an HD-sized DDS for a published 256x256 canvas.
        byte[] large = Dds(256, 256, 0x31545844, Enumerable.Repeat(new byte[] { 0, 248, 0, 0, 0, 0, 0, 0 }, 64 * 64).SelectMany(block => block).ToArray());
        ModFile preferred = new("Demaster/Z", ModFormat.Demaster, "textures/world/dat/texl/texl_001_0.dds", 2, () => large);
        DemasterTextureResolver resolver = new(new ModCatalogSnapshot(2, [], [png, preferred]));
        TextureReplacement result = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
        Assert.Equal(new Rgba32(255, 0, 0, 255), result.Image[0, 0]);
        Assert.Contains(".dds@2", result.Provenance);
    }

    [Fact]
    public void MismatchedReplacementAspectDoesNotStretchOverClassicUv()
    {
        ModFile file = new("Demaster/A", ModFormat.Demaster, "textures/world/dat/texl/texl_001_0.png", 1, () => Png(512, 256, MagickColors.Red));
        DemasterTextureResolver resolver = new(new ModCatalogSnapshot(1, [], [file]));
        ModTextureLoadException error = Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context("world/texl.obj", "texl/0", "classic-world-texl")));
        Assert.Equal(file.PackageId, error.PackageId);
        Assert.Equal(file.RelativePath, error.RelativePath);
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact]
    public void FamilyAddressesRetainDistinctPageRowAndAnimationMeanings()
    {
        Assert.Equal(new[] { "textures/field_bg/bc/bcgate_1/bcgate_1_0_2.dds", "textures/field_bg/bc/bcgate_1/bcgate_1_0_2.png", "textures/field_bg/bc/bcgate_1/bcgate_1_0.dds", "textures/field_bg/bc/bcgate_1/bcgate_1_0.png" }, DemasterTextureResolver.FieldBackgroundCandidates("bcgate_1", 16, 2));
        Assert.EndsWith("a0stg001_16_3.dds", DemasterTextureResolver.BattleSceneryCandidates(1, 16, 3)[0]);
        Assert.EndsWith("a0stg001_16_0.dds", DemasterTextureResolver.BattleSceneryCandidates(1, 16, 3)[2]);
        Assert.EndsWith("field_hd/d000_1.dds", DemasterTextureResolver.FieldModelCandidates(97, 1)[0]);
        Assert.EndsWith("field_hd_new/p000_0.dds", DemasterTextureResolver.FieldModelCandidates(3097, 0)[2]);
        Assert.EndsWith("d1c002_0.dds", DemasterTextureResolver.BattleCharacterCandidates(4199, 0)[0]);
    }

    [Theory]
    [InlineData(0x31545844U)]
    [InlineData(0x33545844U)]
    [InlineData(0x35545844U)]
    public void DecodeBc1Bc2Bc3(uint fourCc)
    {
        byte[] color = [0, 248, 0, 0, 0, 0, 0, 0];
        byte[] block = fourCc switch
        {
            0x33545844 => Enumerable.Repeat((byte)255, 8).Concat(color).ToArray(),
            0x35545844 => new byte[] { 255, 0, 0, 0, 0, 0, 0, 0 }.Concat(color).ToArray(),
            _ => color
        };
        DecodedModImage image = ModImageDecoder.Decode(Dds(4, 4, fourCc, block), "test.dds");
        Assert.Equal(new Rgba32(255, 0, 0, 255), image.Image[0, 0]);
    }

    [Fact]
    public void DecodeBc7AndRejectTruncatedBlock()
    {
        byte[] block = new byte[16];
        block[0] = 0x40;
        byte[] dds = Dds(4, 4, 0x30315844, block, dxgi: 98);
        Assert.Equal(default, ModImageDecoder.Decode(dds, "test.dds").Image[0, 0]);
        Assert.Throws<InvalidDataException>(() => ModImageDecoder.Decode(dds[..^1], "test.dds"));
    }

    [Fact]
    public void DecodeLegacyBgraHonorsRowPitchAndRejectsCubeMaps()
    {
        byte[] dds = Dds(1, 2, 0, [1, 2, 3, 4, 0, 0, 0, 0, 5, 6, 7, 8, 0, 0, 0, 0]);
        Write(8, 8);
        Write(20, 8);
        Write(80, 0x41);
        Write(88, 32);
        Write(92, 0xFF0000);
        Write(96, 0xFF00);
        Write(100, 0xFF);
        Write(104, 0xFF000000);
        DecodedModImage image = ModImageDecoder.Decode(dds, "test.dds");
        Assert.Equal(new Rgba32(3, 2, 1, 4), image.Image[0, 0]);
        Assert.Equal(new Rgba32(7, 6, 5, 8), image.Image[0, 1]);
        Write(112, 0x200);
        Assert.Throws<NotSupportedException>(() => ModImageDecoder.Decode(dds, "test.dds"));

        void Write(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(offset), value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecodeUncompressedChannelsAndPreserveMipLevels(bool bgra)
    {
        byte[] bytes = new byte[20];
        bytes[0] = 0x12;
        bytes[1] = 0x34;
        bytes[2] = 0x56;
        bytes[3] = 0x78;
        bytes[16] = 0xAB;
        byte[] dds = Dds(2, 2, 0x30315844, bytes, dxgi: bgra ? 87U : 28U, mips: 2);
        DecodedModImage decoded = ModImageDecoder.Decode(dds, "test.dds");
        Assert.Equal(bgra ? new Rgba32(0x56, 0x34, 0x12, 0x78) : new Rgba32(0x12, 0x34, 0x56, 0x78), decoded.Image[0, 0]);
        Assert.Equal(2, decoded.MipLevels.Count);
        Assert.Equal(1, decoded.MipLevels[1].Width);
        Assert.Equal(bgra ? (byte)0xAB : (byte)0, decoded.MipLevels[1][0, 0].B);
    }

    [Fact]
    public void InstalledHorizonPackWorldEntriesDecodeAndBindToClassicCanvases()
    {
        string? root = Environment.GetEnvironmentVariable("FF8_DEMASTER_PACKAGE");
        if (string.IsNullOrWhiteSpace(root))
        {
            Assert.Skip("Set FF8_DEMASTER_PACKAGE to the unpacked Demaster HorizonPack directory.");
            return;
        }
        List<ModFile> files = Directory.EnumerateFiles(Path.Combine(root, "textures", "world", "dat"), "*.png", SearchOption.AllDirectories).Select(path => new ModFile("Demaster/HorizonPack-Remastered", ModFormat.Demaster, Path.GetRelativePath(root, path), 1, () => File.ReadAllBytes(path))).ToList();
        DemasterTextureResolver resolver = new(new ModCatalogSnapshot(1, [], files));
        int terrainCount = 0;
        foreach (ModFile file in files.Where(file => Path.GetFileName(file.RelativePath).StartsWith("texl_", StringComparison.Ordinal)))
        {
            string filename = Path.GetFileNameWithoutExtension(file.RelativePath);
            int index = int.Parse(filename.Split('_')[1], System.Globalization.CultureInfo.InvariantCulture);
            TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("world/texl.obj", $"texl/{index - 1}", "classic-world-texl")));
            Assert.Equal(1024, replacement.Image.Width);
            Assert.Equal(1024, replacement.Image.Height);
            terrainCount++;
        }
        Assert.Equal(19, terrainCount);
        Assert.Equal(1024, Assert.IsType<TextureReplacement>(resolver.Resolve(Context("world/wmsetus.obj", "roads", "classic-world-roads"))).Image.Width);
        // Native Demaster cloud addressing is available to a future source-published page, with no invented Classic cloud renderer.
        Assert.Equal(1024, Assert.IsType<TextureReplacement>(resolver.Resolve(Context("world/wmsetus.obj", "14", "demaster-world-wmset"))).Image.Width);
    }

    private static TextureBuildContext Context(string owner, string subresource, string layout, int palette = 0)
    {
        return new(new(new(owner), subresource, palette), new("memory", GameDataEdition.Classic, []), 1, layout, 256, 256, () => throw new InvalidOperationException("Semantic lookup must not decode the base image."));
    }

    private static ModFile MetadataFile(string package, string path) => new(package, ModFormat.Demaster, path, 1, () => throw new InvalidOperationException("Inspection must leave image decoding lazy."));

    private static byte[] Png(int width, int height, IMagickColor<byte> color)
    {
        using MagickImage image = new(color, (uint)width, (uint)height);
        return image.ToByteArray(MagickFormat.Png);
    }

    private static byte[] Dds(int width, int height, uint fourCc, byte[] pixels, uint dxgi = 0, int mips = 1)
    {
        int headerSize = fourCc == 0x30315844 ? 148 : 128;
        byte[] bytes = new byte[headerSize + pixels.Length];
        Write(0, 0x20534444);
        Write(4, 124);
        Write(12, (uint)height);
        Write(16, (uint)width);
        Write(28, (uint)mips);
        Write(76, 32);
        Write(80, 4);
        Write(84, fourCc);
        if (headerSize == 148)
        {
            Write(128, dxgi);
            Write(132, 3);
            Write(140, 1);
        }
        pixels.CopyTo(bytes, headerSize);
        return bytes;

        void Write(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
