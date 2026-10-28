using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffectTexturePaletteReuseTests
{
    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, -1)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void Optional_receipt_preserves_exact_used_words_and_default_decode_pixels(int depth, int alternateShift)
    {
        byte[] palette = new byte[depth == 0 ? 32 : 512];
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2), 0x801F);
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(4), 0x03E0);
        ImmutableArray<byte> words = depth == 0 ? [0x12, 0x21] : [1, 2];
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, palette.ToImmutableArray()), new(2048, words)] };
        ushort page = checked((ushort)(depth << 7));
        int width = depth == 0 ? 4 : 2;
        byte? shift = alternateShift < 0 ? null : checked((byte)alternateShift);
        PixelImage expected = NativeBattleEffectTextureReader.DecodePcRegion(state, page, 0, 0, 1, width, 1, shift);
        PixelImage actual = NativeBattleEffectTextureReader.DecodePcRegion(state, page, 0, 0, 1, width, 1, out NativeBattlePcPaletteReceipt? receipt, shift);
        Assert.Equal(expected.Pixels, actual.Pixels);
        Assert.NotNull(receipt);
        Assert.Equal(new NativeBattlePcPaletteEntry[] { new(1, 0x801F), new(2, 0x03E0) }, receipt.Entries);
        Assert.True(receipt.Matches(state));
        NativeBattleEffectTextureState unused = state.PublishVramSpan(0, new byte[] { 0, 0x7C });
        Assert.True(receipt.Matches(unused));
        NativeBattleEffectTextureState used = unused.PublishVramSpan(2, new byte[] { 31, 0 });
        Assert.False(receipt.Matches(used));
    }

    [Fact]
    public void Optional_receipt_never_reads_unused_out_of_row_CLUT_entries_and_used_invalid_entries_still_fail()
    {
        byte[] palette = new byte[32];
        palette[4] = 31;
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(2 * 1008, palette.ToImmutableArray()), new(2048, [2, 17])] };
        PixelImage image = NativeBattleEffectTextureReader.DecodePcRegion(state, 0x80, 63, 0, 1, 1, 1, out NativeBattlePcPaletteReceipt? receipt);
        Assert.Equal(new Rgba32(255, 0, 0, 127), image[0, 0]);
        Assert.NotNull(receipt);
        Assert.Equal(new NativeBattlePcPaletteEntry(2, 31), Assert.Single(receipt.Entries));
        Assert.True(receipt.Matches(state));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTextureReader.DecodePcRegion(state, 0x80, 63, 0, 1, 2, 1, out _));
    }

    [Theory]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565)]
    public void Direct_PC_decode_has_no_palette_receipt_and_keeps_its_required_format_guard(NativeBattlePcDirectTextureFormat format)
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, [0xE0, 3])] };
        PixelImage expected = NativeBattleEffectTextureReader.DecodePcRegion(state, 0x100, 0xFFFF, 0, 0, 1, 1, directTextureFormat: format);
        PixelImage actual = NativeBattleEffectTextureReader.DecodePcRegion(state, 0x100, 0xFFFF, 0, 0, 1, 1, out NativeBattlePcPaletteReceipt? receipt, directTextureFormat: format);
        Assert.Null(receipt);
        Assert.Equal(expected.Pixels, actual.Pixels);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectTextureReader.DecodePcRegion(state, 0x100, 0xFFFF, 0, 0, 1, 1, out _));
    }

    [Theory]
    [InlineData(0, false, -1)]
    [InlineData(1, false, -1)]
    [InlineData(0, true, -1)]
    [InlineData(1, true, -1)]
    [InlineData(0, true, 0)]
    [InlineData(1, true, 0)]
    [InlineData(0, true, 1)]
    [InlineData(1, true, 1)]
    public void Repeated_indices_match_scalar_expansion_for_wrapped_UVs_and_each_palette_mode(int depth, bool pc, int alternateShift)
    {
        byte[] bank = new byte[2 * 1024 * 512];
        int entries = depth == 0 ? 16 : 256;
        for (int index = 0; index < entries; index++)
        {
            ushort color = index == 0 ? (ushort)0 : index == 1 ? (ushort)0x8000 : unchecked((ushort)(index * 211));
            BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(index * 2), color);
        }
        int perWord = depth == 0 ? 4 : 2;
        for (int row = 1; row <= 2; row++)
        {
            for (int column = 0; column < 256 / perWord; column++)
            {
                int word = 0;
                for (int pixel = 0; pixel < perWord; pixel++)
                {
                    int index = (column * perWord + pixel + row * 3) % entries;
                    word |= index << (pixel * (depth == 0 ? 4 : 8));
                }
                BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(2 * (row * 1024 + column)), (ushort)word);
            }
        }
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, bank.ToImmutableArray())] };
        ushort page = (ushort)(depth << 7);
        PixelImage actual = pc ? NativeBattleEffectTextureReader.DecodePcRegion(state, page, 0, 251, 1, 256, 2, alternateShift < 0 ? null : (byte)alternateShift) : NativeBattleEffectTextureReader.DecodeRegion(state, page, 0, 251, 1, 256, 2);
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 256; column++)
            {
                int x = (251 + column) & 255;
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(bank.AsSpan(2 * ((1 + row) * 1024 + x / perWord)));
                int index = word >> (x % perWord * (depth == 0 ? 4 : 8)) & (entries - 1);
                ushort color = BinaryPrimitives.ReadUInt16LittleEndian(bank.AsSpan(index * 2));
                Rgba32 expected = Rgba32.FromPsxBgr555(color);
                if (pc)
                {
                    uint packed = alternateShift < 0 ? NativeBattlePcPalette.Decode467370(color) : NativeBattlePcPalette.Decode467470(color, (byte)alternateShift);
                    expected = new((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed, (byte)(packed >> 24));
                }
                Assert.Equal(expected, actual[column, row]);
            }
        }
    }

    [Fact]
    public void Only_consumed_CLUT_entries_are_validated_and_invalid_used_entries_still_fail()
    {
        byte[] palette = new byte[32];
        palette[4] = 31;
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(2 * 1008, palette.ToImmutableArray()), new(2 * 1024, [2, 17])] };
        PixelImage first = NativeBattleEffectTextureReader.DecodePcRegion(state, 0x80, 63, 0, 1, 1, 1);
        Assert.Equal(new Rgba32(255, 0, 0, 127), first[0, 0]);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTextureReader.DecodePcRegion(state, 0x80, 63, 0, 1, 2, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectTextureReader.DecodePcRegion(state, 0x80, 63, 0, 1, 1, 1, 2));
    }
}
