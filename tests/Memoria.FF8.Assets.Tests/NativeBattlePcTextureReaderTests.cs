using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePcTextureReaderTests
{
    [Theory]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 255)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 250)]
    public void Selected_direct_format_expands_RGB_and_keeps_both_black_words_opaque(NativeBattlePcDirectTextureFormat format, byte maximumGreen)
    {
        byte[] pixels = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(pixels, 0x7FFF);
        BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(2), 0xFFFF);
        BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(6), 0x8000);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, pixels.ToImmutableArray())] };
        PixelImage image = NativeBattleEffectTextureReader.DecodePcRegion(state, 0x100, 0xFFFF, 0, 0, 4, 1, directTextureFormat: format);
        Assert.Equal(new Rgba32(255, maximumGreen, 255, 255), image[0, 0]);
        Assert.Equal(image[0, 0], image[1, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), image[2, 0]);
        Assert.Equal(image[2, 0], image[3, 0]);
    }

    [Fact]
    public void Indexed_PC_palette_ignores_STP_and_uses_native_floor_expansion_and_alpha127()
    {
        byte[] colors = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(2), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(4), 0x8010);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, colors.ToImmutableArray()), new(2048, ImmutableArray.Create((byte)0x21, (byte)0))] };
        PixelImage image = NativeBattleEffectTextureReader.DecodePcRegion(state, 0, 0, 0, 1, 4, 1);
        Assert.Equal(new Rgba32(131, 0, 0, 127), image[0, 0]);
        Assert.Equal(image[0, 0], image[1, 0]);
        Assert.Equal(new Rgba32(0, 0, 1, 0), image[2, 0]);
        Assert.Equal(image[2, 0], image[3, 0]);
    }

    [Fact]
    public void Direct_PC_texture_requires_the_selected_native_device_format()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectTextureReader.DecodePcRegion(NativeBattleEffectTextureState.FromColdProcess(), 0x100, 0, 0, 0, 1, 1));
    }

    [Fact]
    public void Alternate_PC_palette_preserves_native_black_RGB_and_alpha_shift()
    {
        byte[] colors = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(2), 0xC210);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, colors.ToImmutableArray()), new(2048, ImmutableArray.Create((byte)0x11, (byte)0x11))] };
        Assert.Equal(new Rgba32(0, 0, 0, 96), NativeBattleEffectTextureReader.DecodePcRegion(state, 0, 0, 0, 1, 1, 1, 0)[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 48), NativeBattleEffectTextureReader.DecodePcRegion(state, 0, 0, 0, 1, 1, 1, 1)[0, 0]);
    }
}
