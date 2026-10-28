using System.Buffers.Binary;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterContentUploadTests
{
    private static readonly ResourceSourceDescriptor Source = new("test", GameDataEdition.Remastered, ["demaster-content-upload"]);

    [Fact]
    public void CompleteUploadSharesDecodedPixelsAndMipsAcrossResolverGenerations()
    {
        TextureBuildContext context = Context();
        ModFile file = Dds(Target(context) + ".dds", 32, 16);
        DecodedModImage decoded = ModImageDecoder.Decode(file);
        TextureReplacement first = Assert.IsType<TextureReplacement>(Resolver(file).Resolve(context));
        TextureReplacement second = Assert.IsType<TextureReplacement>(Resolver(file).Resolve(Context()));
        Assert.Same(decoded.Image, first.Image);
        Assert.Same(first.Image, second.Image);
        Assert.Equal(decoded.MipLevels.Count, first.MipLevels!.Count);
        Assert.Equal(first.MipLevels.Count, second.MipLevels!.Count);
        for (int level = 0; level < decoded.MipLevels.Count; level++)
        {
            Assert.Same(decoded.MipLevels[level], first.MipLevels[level]);
            Assert.Same(first.MipLevels[level], second.MipLevels[level]);
        }
    }

    [Fact]
    public void NativeRectangleCropsEveryRepresentableMipAndPreservesLogicalCanvas()
    {
        TextureBuildContext context = Context(x: 2, y: 2, regionWidth: 4, regionHeight: 4, logicalWidth: 12, logicalHeight: 20);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Dds(Target(context) + ".dds", 32, 16)).Resolve(context));
        Assert.Equal((8, 8, 12, 20), (replacement.Image.Width, replacement.Image.Height, replacement.LogicalWidth, replacement.LogicalHeight));
        Assert.Equal(new Rgba32(4, 4, 0), replacement.Image[0, 0]);
        Assert.Equal(new[] { 8, 4, 2 }, replacement.MipLevels!.Select(mip => mip.Width));
        Assert.Equal(new Rgba32(1, 1, 2), replacement.MipLevels![2][0, 0]);
        Assert.Null(replacement.Animation);
    }

    [Fact]
    public void DdsWinsBeforePngAndCatalogOwnsTheWinningPackage()
    {
        TextureBuildContext context = Context();
        string stem = Target(context);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Png(stem + ".png", 32, 16), Dds(stem + ".dds", 32, 16)).Resolve(context));
        Assert.Contains(stem + ".dds@1", replacement.Provenance);
        replacement = Assert.IsType<TextureReplacement>(Resolver(Png(stem + ".png", 32, 16)).Resolve(context));
        Assert.Contains(stem + ".png@1", replacement.Provenance);
        ModFile preferredImage = Dds(stem + ".dds", 48, 24);
        ModFile preferred = new("Demaster/Preferred", ModFormat.Demaster, preferredImage.RelativePath, 1, preferredImage.ReadBytes);
        replacement = Assert.IsType<TextureReplacement>(Resolver(preferred, Dds(stem + ".dds", 32, 16)).Resolve(context));
        Assert.StartsWith("Demaster/Preferred/", replacement.Provenance);
        Assert.Equal(48, replacement.Image.Width);
    }

    [Fact]
    public void CorruptPreferredFileDoesNotFallThroughAndReportsOwnership()
    {
        TextureBuildContext context = Context();
        string stem = Target(context);
        ModFile corrupt = new("Demaster/Broken", ModFormat.Demaster, stem + ".dds", 1, () => [0]);
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(corrupt, Png(stem + ".png", 32, 16)).Resolve(context));
        Assert.Equal((corrupt.PackageId, corrupt.RelativePath), (failure.PackageId, failure.RelativePath));
    }

    [Theory]
    [InlineData(15, 8)]
    [InlineData(16, 16)]
    [InlineData(24, 12)]
    [InlineData(32, 8)]
    public void ArtworkRequiresEqualPositiveIntegralCanvasScale(int width, int height)
    {
        TextureBuildContext context = Context();
        Assert.Throws<ModTextureLoadException>(() => Resolver(Png(Target(context) + ".png", width, height)).Resolve(context));
    }

    [Fact]
    public void UnpublishedOrInvalidMetadataNeverReadsPixelsOrArtwork()
    {
        ModFile lazy = new("Demaster/Test", ModFormat.Demaster, "hashOutput/0000000000000000_HD.dds", 1, () => throw new InvalidOperationException("Artwork must stay lazy."));
        foreach ((int width, int height, int bpp, int x, int y, int regionWidth, int regionHeight) in new[] { (1024, 8, 4, 0, 0, 4, 4), (16, 8, 2, 0, 0, 4, 4), (16, 8, 4, -1, 0, 4, 4), (16, 8, 4, 0, 0, 17, 4), (16, 8, 4, 0, 7, 4, 2), (16, 8, 4, 0, 0, 0, 4) })
        {
            TextureSourceUpload upload = new(width, height, bpp, 0, () => throw new InvalidOperationException("Pixels must stay lazy."), x, y, regionWidth, regionHeight);
            Assert.Null(Resolver(lazy).Resolve(Context(upload: upload)));
        }
        Assert.Null(Resolver(lazy).Resolve(Context(published: false, upload: new(16, 8, 4, 0, () => throw new InvalidOperationException("Pixels must stay lazy.")))));
        Assert.Null(Resolver(lazy).Resolve(Context(upload: new(16, 8, 4, 0, () => new byte[511]))));
    }

    [Fact]
    public void DifferentUploadFramesUseTheirOwnContentAndRepeatedContextStaysRecognized()
    {
        TextureBuildContext first = Context(pixels: new byte[512]);
        TextureBuildContext second = Context(pixels: Enumerable.Repeat((byte)127, 512).ToArray());
        DemasterTextureResolver resolver = Resolver(Png(Target(first) + ".png", 32, 16), Png(Target(second) + ".png", 48, 24));
        Assert.Equal(32, resolver.Resolve(first)!.Image.Width);
        Assert.Equal(48, resolver.Resolve(second)!.Image.Width);
        Assert.Equal(32, resolver.Resolve(first)!.Image.Width);
    }

    [Fact]
    public void EmptyOrMalformedContentCatalogDoesNotReadNativePixels()
    {
        TextureBuildContext context = Context(upload: new(16, 8, 4, 0, () => throw new InvalidOperationException("No content targets are enabled.")));
        Assert.Null(Resolver().Resolve(context));
        ModFile malformed = new("Demaster/Test", ModFormat.Demaster, "hashOutput/not-a-fingerprint_HD.png", 1, () => throw new InvalidOperationException("Malformed target must stay lazy."));
        Assert.Null(Resolver(malformed).Resolve(context));
    }

    [Fact]
    public void InspectionRequiresCanonicalContentNamesAndPublishedLayoutWithoutOpeningFiles()
    {
        string[] valid = ["hashOutput/0123456789ABCDEF_HD.dds", "hashOutput/abcdef0123456789_HD.png"];
        string[] invalid = ["hashOutput/0123456789ABCDE_HD.dds", "hashOutput/0123456789ABCDEFG_HD.dds", "hashOutput/0123456789ABCDEG_HD.dds", "hashOutput/0123456789ABCDEF.dds", "hashOutput/0123456789ABCDEF_HD.dds.bak", "hashOutput/sub/0123456789ABCDEF_HD.dds"];
        ModCatalogSnapshot catalog = new(1, [], valid.Concat(invalid).Select(path => new ModFile("Demaster/Test", ModFormat.Demaster, path, 1, () => throw new InvalidOperationException("Inspection must stay lazy."))).ToArray());
        DemasterInspection inspection = DemasterTextureResolver.Inspect(Source, catalog);
        Assert.Single(inspection.UsablePackageIds);
        Assert.Equal(invalid.Order(), inspection.Diagnostics.Select(diagnostic => diagnostic.RelativePath).Order());
        Assert.Equal(valid.Length + invalid.Length, DemasterTextureResolver.Inspect(new("unpublished", GameDataEdition.Remastered, []), catalog).Diagnostics.Count);
    }

    [Fact]
    public void ConflictingLowHalfIsRejectedBeforeArtworkRead()
    {
        TextureBuildContext context = Context();
        TextureSourceUpload upload = context.NativeUpload!;
        Assert.True(DemasterTextureFingerprint.TryCalculate(upload.Pixels.Span, upload.Width, upload.Height, upload.BytesPerPixel, upload.NativeMode, out DemasterTextureFingerprint fingerprint));
        ModFile lazy = new("Demaster/Test", ModFormat.Demaster, Target(context) + ".dds", 1, () => throw new InvalidOperationException("Collision must precede decoding."));
        DemasterTextureResolver resolver = Resolver(lazy);
        Dictionary<ulong, ulong> recognized = (Dictionary<ulong, ulong>)typeof(DemasterTextureResolver).GetField("contentUploadHashes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resolver)!;
        recognized.Add(fingerprint.High64, fingerprint.Low64 ^ 1);
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(context));
        Assert.Contains("collid", failure.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(lazy.RelativePath, failure.RelativePath);
    }

    private static TextureBuildContext Context(int x = 0, int y = 0, int regionWidth = 16, int regionHeight = 8, int logicalWidth = 16, int logicalHeight = 8, bool published = true, byte[]? pixels = null, TextureSourceUpload? upload = null)
    {
        return new(new(new("test/native.bin"), "frame/0"), published ? Source : new("test", GameDataEdition.Remastered, []), 1, "test-content", logicalWidth, logicalHeight, () => throw new InvalidOperationException("Original image must stay lazy."))
        {
            NativeUpload = upload ?? new(16, 8, 4, 0, () => pixels ?? new byte[512], x, y, regionWidth, regionHeight)
        };
    }

    private static string Target(TextureBuildContext context)
    {
        TextureSourceUpload upload = context.NativeUpload!;
        Assert.True(DemasterTextureFingerprint.TryCalculate(upload.Pixels.Span, upload.Width, upload.Height, upload.BytesPerPixel, upload.NativeMode, out DemasterTextureFingerprint fingerprint));
        return $"hashOutput/{fingerprint.High64:X16}_HD";
    }

    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));

    private static ModFile Png(string path, int width, int height)
    {
        using MagickImage image = new(MagickColors.Blue, (uint)width, (uint)height);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/Test", ModFormat.Demaster, path, 1, () => bytes);
    }

    private static ModFile Dds(string path, int width, int height)
    {
        using MemoryStream stream = new();
        byte[] header = new byte[128];
        Write(0, 0x20534444);
        Write(4, 124);
        Write(12, (uint)height);
        Write(16, (uint)width);
        Write(28, (uint)(1 + (int)Math.Floor(Math.Log2(Math.Max(width, height)))));
        Write(76, 32);
        Write(80, 0x41);
        Write(88, 32);
        Write(92, 0xFF);
        Write(96, 0xFF00);
        Write(100, 0xFF0000);
        Write(104, 0xFF000000);
        stream.Write(header);
        for (int level = 0; ; level++, width = Math.Max(1, width / 2), height = Math.Max(1, height / 2))
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    stream.WriteByte((byte)x);
                    stream.WriteByte((byte)y);
                    stream.WriteByte((byte)level);
                    stream.WriteByte(255);
                }
            }
            if (width == 1 && height == 1)
            {
                break;
            }
        }
        byte[] bytes = stream.ToArray();
        return new("Demaster/Test", ModFormat.Demaster, path, 1, () => bytes);

        void Write(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(offset, 4), value);
        }
    }
}
