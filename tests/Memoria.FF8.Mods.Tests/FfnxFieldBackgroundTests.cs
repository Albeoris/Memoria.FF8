using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using ImageMagick;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class FfnxFieldBackgroundTests
{
    private static readonly ResourceKey Owner = new("field/sample/sample.mim");
    private static readonly ResourceSourceDescriptor Source = new("native", GameDataEdition.Classic, ["classic-ffnx"]);

    [Fact]
    public void InspectionCachesCompleteMapPrefixAndUsesCandidateOverlayBeforeInstalledSource()
    {
        using PrefixMapSource source = new();
        ModFile dds = new("first", ModFormat.Junction, "mods/textures/field/mapdata/sample/sample.dds", 1, () => throw new IOException("Inspection must not decode replacement images."));
        ModFile png = new("second", ModFormat.Junction, "mods/textures/eng/field/mapdata/sample/sample.png", 1, () => throw new IOException("Inspection must not decode replacement images."));
        ModCatalogSnapshot catalog = new(1, [], [dds, png]);

        DemasterInspection original = FfnxTextureResolver.Inspect(Source, catalog, source);

        Assert.Equal(1, source.Reads);
        Assert.Equal(2, original.Diagnostics.Count);
        Assert.All(original.Diagnostics, diagnostic => Assert.Equal("ffnx-unsupported-native-target", diagnostic.Code));
        byte[] candidateMap = new MapFile([new MapTile()]).Write();
        using ResourceSourceView candidate = new(source, [new(new("field/sample/sample.map"), "map", 2, "candidate", candidateMap)]);

        DemasterInspection replaced = FfnxTextureResolver.Inspect(Source, catalog, candidate);

        Assert.Empty(replaced.Diagnostics);
        Assert.Equal(1, source.Reads);
        Assert.Equal(2, replaced.UsablePackageIds.Count);
    }

    [Fact]
    public void InspectionRejectsIncompleteDeclaredMapInsteadOfParsingPartialPrefix()
    {
        using PrefixMapSource source = new(incomplete: true);
        ModFile file = new("atlas", ModFormat.Junction, "mods/textures/field/mapdata/sample/sample.png", 1, () => throw new IOException("Inspection must not decode."));

        DemasterInspection inspection = FfnxTextureResolver.Inspect(Source, new(1, [], [file]), source);

        ModDiagnostic diagnostic = Assert.Single(inspection.Diagnostics);
        Assert.Equal("ffnx-unsupported-native-target", diagnostic.Code);
        Assert.Equal("atlas", diagnostic.PackageId);
        Assert.Contains("complete declared resource", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AtlasGeometryComesFromAllMapRecordsIncludingDuplicateSlotsAndPartialFinalRow()
    {
        MapTile[] tiles = Enumerable.Range(0, 17).Select(index => new MapTile { SourceX = (byte)(index % 16 * 16), SourceY = (byte)(index / 16 * 16) }).ToArray();
        IReadOnlyList<TextureSourceAddress> addresses = FieldBackgroundTileDecoder.AtlasAddresses(Owner, tiles);

        Assert.Equal(17, addresses.Count);
        Assert.All(addresses, address => Assert.Equal((32, 256, 16, 16), (address.CanvasWidth, address.CanvasHeight, address.Width, address.Height)));
        Assert.Equal((16, 0), (addresses[1].X, addresses[1].Y));
        Assert.Equal((0, 128), (addresses[16].X, addresses[16].Y));
        Assert.Empty(FieldBackgroundTileDecoder.AtlasAddresses(Owner, []));
    }

    [Fact]
    public void NativeIdentityUsesFirstRecordAcrossDestinationAnimationAndBlendDifferences()
    {
        MapTile[] tiles =
        [
            new() { SourceX = 0, SourceY = 0 },
            new() { SourceX = 0, SourceY = 0, X = 100, Y = 200, Parameter = 3, State = 4, PackedTexture = 0x20, BlendMode = MapBlendMode.Add },
            new() { SourceX = 0, PaletteId = 1 },
            new() { SourceX = 0, PackedTexture = 0x80 },
            new() { SourceX = 0, Unknown = 1, PaletteId = 3 },
            new() { SourceX = 0, Unknown = 1, PaletteId = 8 },
            new() { SourceX = 0, TextureId = 1 }
        ];
        IReadOnlyList<TextureSourceAddress> addresses = FieldBackgroundTileDecoder.AtlasAddresses(Owner, tiles);

        Assert.Equal(addresses[0], addresses[1]);
        Assert.Equal(addresses[4], addresses[5]);
        Assert.Equal(new[] { 0, 0, 32, 48, 64, 64, 96 }, addresses.Select(address => address.Y));
        Assert.All(addresses, address => Assert.Equal((16, 256), (address.CanvasWidth, address.CanvasHeight)));
    }

    [Theory]
    [InlineData(1, 0, 0, 0)]
    [InlineData(0, 15, 0, 0)]
    [InlineData(0, 0, 128, 1)]
    [InlineData(128, 0, 128, 0)]
    [InlineData(64, 0, 0, 1)]
    public void UnsupportedNativeSourceRejectsEntireAtlasOnlyWhenReplacementExists(byte sourceX, byte sourceY, byte packedTexture, byte upperTexture)
    {
        MapTile[] tiles = [new(), new() { SourceX = sourceX, SourceY = sourceY, PackedTexture = packedTexture, Unknown = upperTexture }];
        IReadOnlyList<TextureSourceAddress> addresses = FieldBackgroundTileDecoder.AtlasAddresses(Owner, tiles);
        FfnxTextureResolver resolver = new(new(1, [], [Atlas(16, 256, 1)]));
        FfnxTextureResolver original = new(new(1, [], []));

        foreach (TextureSourceAddress address in addresses)
        {
            ModTextureLoadException exception = Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context(0, address)));
            Assert.Equal("atlas", exception.PackageId);
            Assert.IsType<NotSupportedException>(exception.InnerException);
            Assert.Null(original.Resolve(Context(0, address)));
        }
    }

    [Theory]
    [InlineData(240, 0, 0)]
    [InlineData(112, 128, 0)]
    [InlineData(48, 0, 1)]
    public void AlignedTileAtFinalNativeDepthColumnRemainsSupported(byte sourceX, byte packedTexture, byte upperTexture)
    {
        Assert.True(FieldBackgroundTileDecoder.SupportsAtlas([new MapTile { SourceX = sourceX, SourceY = 240, PackedTexture = packedTexture, Unknown = upperTexture }]));
    }

    [Fact]
    public void PackedAtlasCropsNativeSlotsAndKeepsTileLogicalSize()
    {
        MapTile[] tiles = Enumerable.Range(0, 17).Select(index => new MapTile { SourceX = (byte)(index % 16 * 16), SourceY = (byte)(index / 16 * 16) }).ToArray();
        IReadOnlyList<TextureSourceAddress> addresses = FieldBackgroundTileDecoder.AtlasAddresses(Owner, tiles);
        FfnxTextureResolver resolver = new(new(1, [], [Atlas(32, 256, 2)]));

        TextureReplacement firstRow = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(1, addresses[1])));
        TextureReplacement finalRow = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(16, addresses[16])));

        Assert.Equal((16, 16, 32, 32), (firstRow.LogicalWidth, firstRow.LogicalHeight, firstRow.Image.Width, firstRow.Image.Height));
        Assert.Equal(new Rgba32(16, 0, 7, 128), firstRow.Image[0, 0]);
        Assert.Equal(new Rgba32(0, 128, 7, 128), finalRow.Image[0, 0]);
        Assert.Equal(new Rgba32(15, 143, 7, 128), finalRow.Image[31, 31]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(126, 0)]
    [InlineData(127, 128)]
    [InlineData(128, 128)]
    [InlineData(251, 128)]
    [InlineData(255, 128)]
    public void ExternalAtlasCoverageBecomesCanonicalPrimitiveSemitransparency(byte authoredAlpha, byte expectedAlpha)
    {
        FfnxTextureResolver resolver = new(new(1, [], [Atlas(16, 256, 2, alpha: authoredAlpha)]));
        TextureSourceAddress address = new(Owner, "background-atlas", 0, 0, 0, 16, 16, 16, 256);

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(0, address)));

        Assert.Equal(new Rgba32(15, 15, 7, expectedAlpha), replacement.Image[31, 31]);
        Assert.All(replacement.Image.Pixels, pixel => Assert.Equal(expectedAlpha, pixel.A));
    }

    [Theory]
    [InlineData(64, 256)]
    [InlineData(32, 257)]
    [InlineData(16, 128)]
    public void NonuniformOrNonintegralAtlasDoesNotTryLowerPngOrPackage(int width, int height)
    {
        ModFile winner = Atlas(width, height, 1, "preferred", ".dds");
        ModFile lower = new("lower", ModFormat.Junction, "mods/textures/field/mapdata/sample/sample.png", 2, () => throw new IOException("Lower candidates must not be read."));
        FfnxTextureResolver resolver = new(new(1, [], [winner, lower]));
        TextureSourceAddress address = new(Owner, "background-atlas", 0, 0, 0, 16, 16, 32, 256);

        ModTextureLoadException exception = Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context(0, address)));

        Assert.Equal("preferred", exception.PackageId);
        Assert.Equal(winner.RelativePath, exception.RelativePath);
    }

    [Fact]
    public void CorruptPreferredDdsPreservesOwnershipAndDoesNotRetryPng()
    {
        ModFile winner = new("preferred", ModFormat.Junction, "mods/textures/field/mapdata/sample/sample.dds", 1, () => [1, 2, 3]);
        ModFile lower = new("lower", ModFormat.Junction, "mods/textures/field/mapdata/sample/sample.png", 2, () => throw new IOException("Lower candidates must not be read."));
        FfnxTextureResolver resolver = new(new(1, [], [winner, lower]));
        TextureSourceAddress address = new(Owner, "background-atlas", 0, 0, 0, 16, 16, 16, 256);

        ModTextureLoadException exception = Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context(0, address)));

        Assert.Equal("preferred", exception.PackageId);
    }

    [Fact]
    public void AtlasRequiresNativeMetadataAndInspectionRecognizesOnlyExactMatchingFieldName()
    {
        ModFile accepted = Atlas(16, 256, 1);
        ModFile wrongName = new("wrong", ModFormat.Junction, "mods/textures/field/mapdata/sample/other.png", 1, () => throw new IOException("Inspection must not decode."));
        ModFile oldPage = new("page", ModFormat.Junction, "mods/textures/field/mapdata/sa/sample/sample_0.png", 1, () => throw new IOException("Inspection must not decode."));
        ModCatalogSnapshot catalog = new(1, [], [accepted, wrongName, oldPage]);
        DemasterInspection inspection = FfnxTextureResolver.Inspect(Source, catalog);

        Assert.Contains("atlas", inspection.UsablePackageIds);
        Assert.DoesNotContain("wrong", inspection.UsablePackageIds);
        Assert.DoesNotContain("page", inspection.UsablePackageIds);
        FfnxTextureResolver resolver = new(catalog);
        TextureBuildContext unmapped = new(new(Owner, "tile/0"), Source, 1, "classic-field-tile", 16, 16, () => throw new IOException("Unmapped resources must stay lazy."));
        Assert.Null(resolver.Resolve(unmapped));
    }

    private static TextureBuildContext Context(int index, TextureSourceAddress address) => new(new(Owner, $"tile/{index}"), Source, 1, "classic-field-tile", 16, 16, () => throw new IOException("Replacement must not decode original pixels.")) { NativeSheet = address };

    private sealed class PrefixMapSource(bool incomplete = false) : IResourceSource, IResourcePrefixSource
    {
        public int Reads { get; private set; }
        public ResourceSourceDescriptor Descriptor => Source;
        public ResourceContent? Find(ResourceKey key) => throw new IOException("Bounded MAP inspection must not retain ordinary nested source archives.");

        public ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes)
        {
            Assert.Equal(new ResourceKey("field/sample/sample.map"), key);
            Reads++;
            byte[] bytes = new MapFile([new MapTile(), new MapTile { SourceX = 1 }]).Write();
            return new ResourcePrefix(bytes, bytes.Length + (incomplete ? 1 : 0));
        }

        public void Dispose()
        {
        }
    }

    private static ModFile Atlas(int width, int height, int scale, string package = "atlas", string extension = ".png", byte alpha = 128)
    {
        byte[] pixels = new byte[width * height * scale * scale * 4];
        for (int y = 0; y < height * scale; y++)
        {
            for (int x = 0; x < width * scale; x++)
            {
                int position = (y * width * scale + x) * 4;
                pixels[position] = (byte)(x / scale);
                pixels[position + 1] = (byte)(y / scale);
                pixels[position + 2] = 7;
                pixels[position + 3] = alpha;
            }
        }
        using MagickImage image = new();
        image.ReadPixels(pixels, new PixelReadSettings((uint)(width * scale), (uint)(height * scale), StorageType.Char, PixelMapping.RGBA));
        byte[] content;
        if (extension == ".dds")
        {
            content = new byte[128 + pixels.Length];
            Write(0, 0x20534444);
            Write(4, 124);
            Write(12, (uint)(height * scale));
            Write(16, (uint)(width * scale));
            Write(76, 32);
            Write(80, 0x41);
            Write(88, 32);
            Write(92, 0xFF);
            Write(96, 0xFF00);
            Write(100, 0xFF0000);
            Write(104, 0xFF000000);
            pixels.CopyTo(content, 128);

            void Write(int position, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(position), value);
        }
        else
        {
            content = image.ToByteArray(MagickFormat.Png);
        }
        return new(package, ModFormat.Junction, "mods/textures/field/mapdata/sample/sample" + extension, 1, () => content);
    }
}
