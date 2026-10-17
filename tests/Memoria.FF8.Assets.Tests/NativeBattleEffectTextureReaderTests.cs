using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffectTextureReaderTests
{
    [Fact]
    public void OriginalFireTimUploadsAndDecodesItsActualClutAndImage()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_GAME_DIR to the installed game directory.");
        using GameAssets assets = new(directory!);
        NativeBattleEffectInitializerAsset asset = assets.ReadNativeEffectCatalog()[1].InitializerAsset!;
        NativeBattleEffectHeapAllocation source = new(asset.Name, asset.Data);
        TimFile original = TimFile.Read(asset.Data.AsSpan());
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        int offset = 0;
        foreach (TimImage image in original.Images)
        {
            var queued = state.EnqueueTim505E30(source, offset);
            state = queued.State;
            offset = queued.NextOffset;
        }
        Assert.Equal(asset.Data.Length, offset);
        NativeBattleEffectTextureDrain drain = state.Drain505D20();
        Assert.Equal(2 * original.Images.Count, drain.Uploads.Length);
        foreach (TimImage image in original.Images)
        {
            Assert.NotNull(image.Clut);
            int depth = image.PixelMode == TimPixelMode.Indexed4 ? 0 : 1;
            ushort clut = (ushort)((image.Clut!.Y << 6) | (image.Clut.X / 16));
            int pixelsPerWord = depth == 0 ? 4 : 2;
            PixelImage expected = image.Decode();
            //A TIM can occupy several native pages. Each original sprite still selects one page with byte UVs.
            for (int row = 0; row < image.Height;)
            {
                int v = (image.Y + row) % 256;
                int height = Math.Min(image.Height - row, 256 - v);
                for (int column = 0; column < image.PixelWidth;)
                {
                    int wordX = image.X + column / pixelsPerWord;
                    int u = wordX % 64 * pixelsPerWord;
                    int width = Math.Min(image.PixelWidth - column, 256 - u);
                    ushort page = (ushort)((wordX / 64) | (((image.Y + row) / 256) << 4) | (depth << 7));
                    PixelImage decoded = NativeBattleEffectTextureReader.DecodeRegion(drain.State, page, clut, u, v, width, height);
                    for (int line = 0; line < height; line++)
                    {
                        Assert.Equal(expected.Pixels.AsSpan((row + line) * image.PixelWidth + column, width).ToArray(), decoded.Pixels.AsSpan(line * width, width).ToArray());
                    }
                    column += width;
                }
                row += height;
            }
        }
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void NativePixelDepthSelectsPackedWordsAndPalette(int depth, int width)
    {
        byte[] colors = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(2), 31);
        BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(4), 0x83E0);
        byte[] pixels = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(pixels, depth == 0 ? (ushort)0x0021 : depth == 1 ? (ushort)0x0201 : (ushort)31);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with
        {
            VramRegions = [new(0, colors.ToImmutableArray()), new(2 * 1024, pixels.ToImmutableArray())]
        };
        PixelImage result = NativeBattleEffectTextureReader.DecodeRegion(state, (ushort)(depth << 7), 0, 0, 1, width, 1);
        Assert.Equal(new Rgba32(255, 0, 0), result[0, 0]);
        if (depth < 2)
        {
            Assert.Equal(new Rgba32(0, 255, 0, 128), result[1, 0]);
        }
        if (depth == 0)
        {
            Assert.Equal(default, result[2, 0]);
            Assert.Equal(default, result[3, 0]);
        }
        Assert.Equal(default, NativeBattleEffectTextureReader.DecodeRegion(NativeBattleEffectTextureState.FromColdProcess(), 0, 0, 0, 0, 1, 1)[0, 0]);
    }

    [Theory]
    [InlineData(0, 0x1111, 0x2222)]
    [InlineData(1, 0x0101, 0x0202)]
    public void Stale_index_words_use_the_current_separate_palette_even_when_that_palette_overlaps_the_cached_word_rectangle(int depth, ushort oldWord, ushort newWord)
    {
        byte[] beforeBytes = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(beforeBytes, oldWord);
        BinaryPrimitives.WriteUInt16LittleEndian(beforeBytes.AsSpan(2), 31);
        NativeBattleEffectTextureState before = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, beforeBytes.ToImmutableArray())] };
        NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(depth * 32, new(0, 0, 8, 8), before);
        byte[] afterBytes = beforeBytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(afterBytes, newWord);
        BinaryPrimitives.WriteUInt16LittleEndian(afterBytes.AsSpan(2), 0x03E0);
        BinaryPrimitives.WriteUInt16LittleEndian(afterBytes.AsSpan(4), 0x7C00);
        NativeBattleEffectTextureState after = before with { VramRegions = [new(0, afterBytes.ToImmutableArray())] };
        PixelImage stale = NativeBattleEffectTextureReader.DecodePcRegion(after, (ushort)(depth << 7), 0, 0, 0, 1, 1, pixelWords: retained);
        PixelImage current = NativeBattleEffectTextureReader.DecodePcRegion(after, (ushort)(depth << 7), 0, 0, 0, 1, 1);
        Assert.Equal(new Rgba32(0, 255, 0, 127), stale[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 127), current[0, 0]);
    }

    [Fact]
    public void PC_palette_selection_ignores_CLUT_bit15_before_addressing_the_palette_row()
    {
        NativeBattleEffectTextureState texture = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, [1, 0]), new(2 * (256 * 1024 + 1), [31, 0])] };
        PixelImage ordinary = NativeBattleEffectTextureReader.DecodePcRegion(texture, 0, 0x4000, 0, 0, 1, 1);
        PixelImage highBit = NativeBattleEffectTextureReader.DecodePcRegion(texture, 0, 0xC000, 0, 0, 1, 1);
        Assert.Equal(new Rgba32(255, 0, 0, 127), highBit[0, 0]);
        Assert.Equal(ordinary.Pixels, highBit.Pixels);
    }

    [Fact]
    public void Stale_direct16_words_keep_their_original_color_without_rolling_back_current_VRAM()
    {
        NativeBattleEffectTextureState before = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, [31, 0])] };
        NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), before);
        NativeBattleEffectTextureState after = before with { VramRegions = [new(0, [0xE0, 3])] };
        PixelImage stale = NativeBattleEffectTextureReader.DecodePcRegion(after, 0x100, 0, 0, 0, 1, 1, directTextureFormat: NativeBattlePcDirectTextureFormat.Rgb555, pixelWords: retained);
        PixelImage current = NativeBattleEffectTextureReader.DecodePcRegion(after, 0x100, 0, 0, 0, 1, 1, directTextureFormat: NativeBattlePcDirectTextureFormat.Rgb555);
        Assert.Equal(new Rgba32(255, 0, 0, 255), stale[0, 0]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), current[0, 0]);
    }

    [Fact]
    public void OriginalFireSpriteResourcesUseTheirOwnDynamicFrameDirectories()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_GAME_DIR to the installed game directory.");
        byte[] executable = File.ReadAllBytes(Path.Combine(directory!, "FF8_EN.exe"));
        foreach (uint address in new uint[] { 0xDEDC2C, 0xDEDDD8, 0xDEDF84 })
        {
            NativeBattleModuleResource resource = NativeBattleEffectStaticResourcesReader.ReadSprite(executable, address);
            resource.Validate();
            Assert.Equal(address, resource.Address);
            Assert.Equal(0, resource.Kind);
            Assert.True(resource.Bytes.Length > 12);
        }
        ImmutableArray<byte> descriptor = NativeBattleEffectStaticResourcesReader.Read(executable, 0xDED5FC, 0x40);
        Assert.Equal(0x40, descriptor.Length);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectStaticResourcesReader.ReadSprite(new byte[100], 0xDEDC2C));
    }
}
