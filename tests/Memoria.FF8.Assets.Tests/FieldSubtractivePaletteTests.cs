using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldSubtractivePaletteTests
{
    [Theory]
    [InlineData(0x0000, 0, 0)]
    [InlineData(0x8000, 0, 0)]
    [InlineData(0x0007, 0, 0)]
    [InlineData(0x0008, 0, 17)]
    [InlineData(0x0010, 0, 34)]
    [InlineData(0x001F, 0, 51)]
    [InlineData(0x03E0, 0, 51)]
    [InlineData(0x7C00, 0, 51)]
    [InlineData(0xCA52, 0, 102)]
    [InlineData(0x7FFF, 0, 187)]
    [InlineData(0xFFFF, 0, 187)]
    [InlineData(0x000F, 1, 0)]
    [InlineData(0x0010, 1, 17)]
    [InlineData(0xCA52, 1, 51)]
    [InlineData(0xFFFF, 1, 85)]
    public void AlphaOnlyPaletteUsesOriginalFiveBitChannelsAndFourBitAlpha(ushort word, byte shift, byte alpha)
    {
        Assert.Equal(new Rgba32(0, 0, 0, alpha), FieldBackgroundTileDecoder.SubtractiveColor(word, shift));
    }

    [Fact]
    public void AlphaOnlyPaletteIgnoresStpAndRejectsUnprovedShift()
    {
        for (int word = 0; word < 0x8000; word++)
        {
            Assert.Equal(FieldBackgroundTileDecoder.SubtractiveColor((ushort)word, 0), FieldBackgroundTileDecoder.SubtractiveColor((ushort)(word | 0x8000), 0));
        }
        Assert.Throws<NotSupportedException>(() => FieldBackgroundTileDecoder.SubtractiveColor(0xFFFF, 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndexedSourcePreservesTexelOrderPageDepthAndFullClutAddress(bool eightBit)
    {
        ushort[] words = new ushort[4096];
        words[8 * 256 + 16 + 3] = 0xCA52;
        words[8 * 256 + 16 + 12] = 0xFFFF;
        byte[] atlas = new byte[MimFile.AtlasStride * MimFile.AtlasHeight];
        MapTile tile = new() { PackedTexture = eightBit ? (byte)0xD9 : (byte)0x59, Unknown = 2, PackedPalette = 0x3E01, SourceX = 32, SourceY = 16 };
        int start = 9 * 128 + 16 * MimFile.AtlasStride + (eightBit ? 32 : 16);
        atlas[start] = eightBit ? (byte)3 : (byte)0xC3;
        if (eightBit)
        {
            atlas[start + 1] = 12;
        }
        MimFile mim = new(new byte[MimFile.HeaderSize], words, atlas);
        FieldBackgroundIndexedSource source = Assert.IsType<FieldBackgroundIndexedSource>(FieldBackgroundTileDecoder.IndexedSource(mim, tile));
        Assert.Equal(eightBit ? (ushort)0x99 : (ushort)0x19, source.TexturePage);
        Assert.Equal(256, source.Indices.Length);
        Assert.Equal((byte)3, source.Indices[0]);
        Assert.Equal((byte)12, source.Indices[1]);
        Assert.Equal((byte)0, source.Indices[16]);
        Assert.Same(words, source.PaletteWords);
        Assert.Equal((ushort)0xCA52, source.ReadColor(0, tile.PackedPalette));
        Assert.Equal((ushort)0xFFFF, source.ReadColor(1, tile.PackedPalette));
        Assert.Equal((ushort)0, source.ReadColor(0, 0x3E00));
        Assert.Equal(source.ReadColor(0, 0x3E00), source.ReadColor(0, 0xBE00));
        Assert.Equal((ushort)0xCA52, source.ReadColor(0, 0xBE01));
        Assert.Throws<InvalidDataException>(() => source.ReadColor(0, 0x0000));
        Assert.Throws<InvalidDataException>(() => source.ReadColor(0, 0x3BFF));
        Assert.Throws<InvalidDataException>(() => source.ReadColor(0, 0x3E10));
    }

    [Fact]
    public void FourBitSourceAtOddUStartsWithTheHighNibbleAndReadsTheFinalByte()
    {
        byte[] atlas = new byte[MimFile.AtlasStride * MimFile.AtlasHeight];
        MapTile tile = new() { PackedTexture = 0x59, PackedPalette = 0x3E00, SourceX = 33, SourceY = 16 };
        int start = 9 * 128 + 16 * MimFile.AtlasStride + 16;
        atlas[start] = 0xC3;
        atlas[start + 1] = 0xA7;
        atlas[start + 8] = 0xF5;
        MimFile mim = new(new byte[MimFile.HeaderSize], new ushort[4096], atlas);
        FieldBackgroundIndexedSource source = Assert.IsType<FieldBackgroundIndexedSource>(FieldBackgroundTileDecoder.IndexedSource(mim, tile));
        Assert.Equal((byte)12, source.Indices[0]);
        Assert.Equal((byte)7, source.Indices[1]);
        Assert.Equal((byte)10, source.Indices[2]);
        Assert.Equal((byte)5, source.Indices[15]);
        Assert.Equal((byte)0, source.Indices[16]);
    }

    [Theory]
    [InlineData(0x00, 1)]
    [InlineData(0x80, 1)]
    public void DirectOrUnsupportedTextureDepthHasNoIndexedSource(byte packed, byte unknown)
    {
        MimFile mim = new(new byte[MimFile.HeaderSize], new ushort[4096], []);
        Assert.Null(FieldBackgroundTileDecoder.IndexedSource(mim, new MapTile { PackedTexture = packed, Unknown = unknown }));
    }

    [Fact]
    public void PageRetainsItsFirstPreparedAlphaPaletteAcrossVisibilityChangesAndKeepsOtherPagesIndependent()
    {
        ushort[] words = PaletteWords();
        BackgroundTile gray = Mask(0, 1, 24, 0x3E00, words);
        BackgroundTile white = Mask(0, 2, 24, 0x3E80, words);
        BackgroundTile otherPage = Mask(16, 3, 25, 0x3E00, words);
        FieldRenderer renderer = new(Field([Backdrop(0), Backdrop(16), gray, white, otherPage]));
        byte[] first = renderer.Render(new Dictionary<int, int> { [2] = 0 }, [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(first));
        byte[] second = renderer.Render(new Dictionary<int, int> { [1] = 0, [3] = 0 }, [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(second));
        Assert.Equal(new byte[] { 96, 60, 30, 255 }, Sample(second, 177));
    }

    [Fact]
    public void ColdPreparationUsesLastVisiblePrepassPaletteInReverseMapOrder()
    {
        ushort[] words = PaletteWords();
        BackgroundTile gray = Mask(0, 1, 24, 0x3E00, words);
        BackgroundTile white = Mask(16, 2, 24, 0x3E80, words);
        FieldAssets field = Field([Backdrop(0), Backdrop(16), gray, white]);
        byte[] pixels = new FieldRenderer(field).Render(new Dictionary<int, int> { [1] = 0, [2] = 0 }, [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 96, 60, 30, 255 }, Sample(pixels));
        Assert.Equal(new byte[] { 96, 60, 30, 255 }, Sample(pixels, 177));
    }

    [Fact]
    public void UnrelatedTexturePublicationPreservesTheCapturedFieldPagePalette()
    {
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        BackgroundTile backdrop = Backdrop(0);
        AssetHandle<TextureValue> backgroundHandle = resources.GetTexture(new(new(new("field/background"), "tile/0"), source.Descriptor, 1, "classic-field-tile", 16, 16, () => backdrop.Image));
        TextureKey actorKey = new(new("field/model"), "texture/0");
        resources.GetTexture(new(actorKey, source.Descriptor, 1, "classic-field-model", 16, 16, () => Solid(new Rgba32(10, 20, 30))));
        ushort[] words = PaletteWords();
        FieldRenderer renderer = new(Field([backdrop with { Texture = backgroundHandle }, Mask(0, 1, 24, 0x3E00, words), Mask(0, 2, 24, 0x3E80, words)]));
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(renderer.Render(new Dictionary<int, int> { [2] = 0 }, [], TimeSpan.Zero)));
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(renderer.Render(new Dictionary<int, int> { [1] = 0 }, [], TimeSpan.Zero)));
        long generation = backgroundHandle.Generation;
        resources.GetTexture(new(actorKey, source.Descriptor, 2, "classic-field-model", 16, 16, () => Solid(new Rgba32(40, 50, 60))));
        Assert.True(backgroundHandle.Generation > generation);
        Assert.Same(backdrop.Image, backgroundHandle.Value.Image);
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(renderer.Render(new Dictionary<int, int> { [1] = 0 }, [], TimeSpan.Zero)));
    }

    [Fact]
    public void BackgroundImagePublicationRefreshesOnlyTheAffectedPageAlphaPalette()
    {
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        ushort[] words = PaletteWords();
        BackgroundTile backdrop = Backdrop(0) with { IndexedSource = new(24, new byte[256], words) };
        TextureKey backgroundKey = new(new("field/background"), "tile/0");
        AssetHandle<TextureValue> backgroundHandle = resources.GetTexture(new(backgroundKey, source.Descriptor, 1, "classic-field-tile", 16, 16, () => backdrop.Image));
        FieldAssets field = Field([backdrop with { Texture = backgroundHandle }, Backdrop(16), Mask(0, 1, 24, 0x3E00, words), Mask(0, 2, 24, 0x3E80, words), Mask(16, 3, 25, 0x3E00, words), Mask(16, 4, 25, 0x3E80, words)]);
        FieldRenderer renderer = new(field);
        byte[] first = renderer.Render(new Dictionary<int, int> { [2] = 0, [4] = 0 }, [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(first));
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(first, 177));
        Dictionary<int, int> grayVisible = new() { [1] = 0, [3] = 0 };
        byte[] retained = renderer.Render(grayVisible, [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(retained));
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(retained, 177));
        resources.GetTexture(new(backgroundKey, source.Descriptor, 2, "classic-field-tile", 16, 16, () => Solid(new Rgba32(160, 100, 50))));
        Assert.NotSame(backdrop.Image, backgroundHandle.Value.Image);
        byte[] refreshed = renderer.Render(grayVisible, [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 96, 60, 30, 255 }, Sample(refreshed));
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, Sample(refreshed, 177));
    }

    [Theory]
    [InlineData(128)]
    [InlineData(255)]
    public void TextureAlphaReplacesShadeAlphaAndCoversTheEntireSubtractivePrimitive(byte sourceAlpha)
    {
        BackgroundTile mask = Mask(0, 1, 24, 0x3E00, PaletteWords()) with { Image = Solid(new Rgba32(148, 148, 148, sourceAlpha)) };
        FieldRenderer renderer = new(Field([Backdrop(0), mask]));
        byte[] pixels = renderer.Render(new Dictionary<int, int> { [1] = 0 }, [], TimeSpan.Zero, backgroundModuleShades: new Dictionary<int, Vector3> { [1] = Vector3.Zero });
        Assert.Equal(new byte[] { 96, 60, 30, 255 }, Sample(pixels));
    }

    [Fact]
    public void FieldPaletteShiftChangesAlphaQuantization()
    {
        FieldAssets field = Field([Backdrop(0), Mask(0, 1, 24, 0x3E80, PaletteWords())]) with { Id = 285 };
        byte[] pixels = new FieldRenderer(field).Render(new Dictionary<int, int> { [1] = 0 }, [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 107, 67, 33, 255 }, Sample(pixels));
    }

    private static ushort[] PaletteWords()
    {
        ushort[] words = new ushort[4096];
        words[8 * 256 + 15] = 0xCA52;
        words[10 * 256 + 15] = 0xFFFF;
        return words;
    }

    private static BackgroundTile Mask(int x, int module, ushort page, ushort clut, ushort[] words)
    {
        return new(x, 0, 10, module, 0, 0, MapBlendMode.Subtract, true, true, Solid(new Rgba32(148, 148, 148, 128))) { PackedPalette = clut, IndexedSource = new(page, Enumerable.Repeat((byte)15, 256).ToArray(), words) };
    }

    private static BackgroundTile Backdrop(int x)
    {
        return new(x, 0, 40, 255, 0, 0, MapBlendMode.None, false, true, Solid(new Rgba32(160, 100, 50)));
    }

    private static FieldAssets Field(BackgroundTile[] tiles)
    {
        return new(0, "indexed-alpha", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
    }

    private static PixelImage Solid(Rgba32 color)
    {
        return new(16, 16, Enumerable.Repeat(color, 256).ToArray());
    }

    private static byte[] Sample(byte[] pixels, int x = 161)
    {
        return pixels.AsSpan((121 * 320 + x) * 4, 4).ToArray();
    }
}
