using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePcLinearVramAddressTests
{
    [Fact]
    public void Indexed_PC_palette_pointer_continues_across_the_physical_row_and_receipts_track_consumed_words()
    {
        const ushort clut = 10 * 64 + 63;
        const int paletteOffset = 2 * (10 * 1024 + 1008);
        byte[] palette = new byte[512];
        palette[17 * 2] = 31;
        palette[254 * 2 + 1] = 0x7C;
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(2048, [17, 254]), new(paletteOffset, palette.ToImmutableArray())] };
        PixelImage image = NativeBattleEffectTextureReader.DecodePcRegion(state, 0x80, clut, 0, 1, 2, 1, out NativeBattlePcPaletteReceipt? receipt);
        Assert.Equal(new Rgba32(255, 0, 0, 127), image[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 127), image[1, 0]);
        Assert.True(receipt!.Matches(state));
        Assert.True(NativeBattleEffectTextureReader.HasSamePcRegionSource(state, state, 0x80, clut, 0, 1, 2, 1));
        NativeBattleEffectTextureState unused = state.PublishVramSpan(paletteOffset + 18 * 2, new byte[] { 0xE0, 3 });
        Assert.True(receipt.Matches(unused));
        NativeBattleEffectTextureState changed = unused.PublishVramSpan(paletteOffset + 17 * 2, new byte[] { 0xE0, 3 });
        Assert.False(receipt.Matches(changed));
        Assert.False(NativeBattleEffectTextureReader.HasSamePcRegionSource(state, changed, 0x80, clut, 0, 1, 2, 1));
    }

    [Fact]
    public void Direct_PC_texture_pointer_uses_linear_row_stride_and_the_actual_retained_trailing_owner()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(2 * 1024, [31, 0])] };
        PixelImage image = NativeBattleEffectTextureReader.DecodePcRegion(state, 0x10F, 0, 64, 0, 1, 1, directTextureFormat: NativeBattlePcDirectTextureFormat.Rgb555);
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[0, 0]);
        state = state with { VramReadTail = state.VramReadTail!.Value.SetItem(0, 31) };
        Assert.Equal(image[0, 0], NativeBattleEffectTextureReader.DecodePcRegion(state, 0x11F, 0, 64, 255, 1, 1, directTextureFormat: NativeBattlePcDirectTextureFormat.Rgb555)[0, 0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectTextureReader.DecodePcRegion(state with { VramReadTail = null }, 0x11F, 0, 64, 255, 1, 1, directTextureFormat: NativeBattlePcDirectTextureFormat.Rgb555));
        Assert.Throws<InvalidDataException>(() => NativeBattlePcTextureWindowPixels.ReadSourceWord(state, 1024 + NativeBattleEffectTextureState.PcPageReadTailLength / 2, 511));
    }

    [Fact]
    public void Original_page_011D_scalar_decode_matches_cached_capture_and_tracks_changed_tail_words()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        state = state with { VramReadTail = state.VramReadTail!.Value.SetItem(0, 31).SetItem(127, 0x7C) };
        NativeBattlePcTextureWindowPixels captured = NativeBattlePcTextureWindowPixels.Capture(64 + 29, new(0, 0, 256, 256), state);
        PixelImage image = NativeBattleEffectTextureReader.DecodePcRegion(state, 0x11D, 0, 0, 0, 256, 256, directTextureFormat: NativeBattlePcDirectTextureFormat.Rgb555);
        PixelImage cached = NativeBattleEffectTextureReader.DecodePcRegion(state, 0x11D, 0, 0, 0, 256, 256, directTextureFormat: NativeBattlePcDirectTextureFormat.Rgb555, pixelWords: captured);
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[192, 255]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), image[255, 255]);
        Assert.Equal(image.Pixels, cached.Pixels);
        Assert.True(NativeBattleEffectTextureReader.HasSamePcRegionSource(state, state, 0x11D, 0, 0, 0, 256, 256));
        NativeBattleEffectTextureState changed = state with { VramReadTail = state.VramReadTail!.Value.SetItem(0, 0xE0).SetItem(1, 3) };
        Assert.False(NativeBattleEffectTextureReader.HasSamePcRegionSource(state, changed, 0x11D, 0, 0, 0, 256, 256));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectTextureReader.HasSamePcRegionSource(state, state with { VramReadTail = null }, 0x11D, 0, 0, 0, 256, 256));
    }
}
