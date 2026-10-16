namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcDirectTextureTests
{
    [Theory]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0x0000, 0x0000, 0xFF000000u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0x8000, 0x0000, 0xFF000000u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0x001F, 0x7C00, 0xFFFF0000u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0x03E0, 0x03E0, 0xFF00FF00u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0x7C00, 0x001F, 0xFF0000FFu)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0x4210, 0x4210, 0xFF838383u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0xFFFF, 0x7FFF, 0xFFFFFFFFu)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0x0000, 0x0000, 0xFF000000u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0x8000, 0x0000, 0xFF000000u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0x001F, 0xF800, 0xFFFF0000u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0x03E0, 0x07C0, 0xFF00FA00u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0x7C00, 0x001F, 0xFF0000FFu)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0x4210, 0x8410, 0xFF838183u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0xFFFF, 0xFFDF, 0xFFFFFAFFu)]
    public void Native_direct_conversion_swaps_red_blue_strips_high_bit_and_keeps_green_low_bit_zero(NativeBattlePcDirectTextureFormat format, ushort vram, ushort expectedDeviceWord, uint expectedArgb)
    {
        Assert.Equal(expectedDeviceWord, NativeBattlePcDirectTexture.Convert4675C0(vram, format));
        Assert.Equal(expectedArgb, NativeBattlePcDirectTexture.DecodeArgb(vram, format));
        Assert.Equal(expectedArgb, NativeBattlePcDirectTexture.Expand418DEC(expectedDeviceWord, format));
    }

    [Theory]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0x0421, 0xFF080808u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0x8000, 0xFF000000u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 0x7FFF, 0xFFFFFFFFu)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0x0821, 0xFF080408u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0x0400, 0xFF008100u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0x8000, 0xFF830000u)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 0xFFFF, 0xFFFFFFFFu)]
    public void Device_format_expansion_floors_each_channel_and_always_uses_alpha255(NativeBattlePcDirectTextureFormat format, ushort devicePixel, uint expected)
    {
        Assert.Equal(expected, NativeBattlePcDirectTexture.Expand418DEC(devicePixel, format));
    }

    [Theory]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565)]
    public void Single_word_conversion_matches_native_paired_DWORD_loop_for_every_WORD_and_ignores_source_STP(NativeBattlePcDirectTextureFormat format)
    {
        for (uint low = 0; low <= ushort.MaxValue; low++)
        {
            ushort high = unchecked((ushort)((low << 7) | (low >> 9)));
            uint pair = low | (uint)high << 16;
            uint native;
            if (format == NativeBattlePcDirectTextureFormat.Rgb555)
            {
                native = (pair >> 10 & 0x001F001F) | ((pair & 0x001F001F) << 10) | (pair & 0x03E003E0);
            }
            else
            {
                // Literal4676A2..4676CB pair operations, including its asymmetric first mask and two SHLs.
                uint first = ((pair & 0xFFFF001F) << 10) | (pair & 0x03E003E0);
                native = first << 1 | (pair >> 10 & 0x001F001F);
            }
            ushort converted = NativeBattlePcDirectTexture.Convert4675C0((ushort)low, format);
            uint actual = converted | (uint)NativeBattlePcDirectTexture.Convert4675C0(high, format) << 16;
            Assert.Equal(native, actual);
            Assert.Equal(converted, NativeBattlePcDirectTexture.Convert4675C0((ushort)(low ^ 0x8000), format));
            Assert.Equal(0, converted & (format == NativeBattlePcDirectTextureFormat.Rgb555 ? 0x8000 : 0x20));
        }
    }

    [Fact]
    public void Unselected_or_unknown_device_formats_remain_an_explicit_unsupported_contract()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattlePcDirectTexture.Convert4675C0(0x7FFF, default));
        Assert.Throws<NotSupportedException>(() => NativeBattlePcDirectTexture.Expand418DEC(0x7FFF, (NativeBattlePcDirectTextureFormat)3));
        Assert.Throws<NotSupportedException>(() => NativeBattlePcDirectTexture.DecodeArgb(0, default));
        Assert.NotEqual(NativeBattlePcPalette.Decode467370(0), NativeBattlePcDirectTexture.DecodeArgb(0, NativeBattlePcDirectTextureFormat.Rgb555));
        Assert.NotEqual(NativeBattlePcPalette.Decode467370(0x8000), NativeBattlePcDirectTexture.DecodeArgb(0x8000, NativeBattlePcDirectTextureFormat.Rgb565));
    }
}
