namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcPaletteTests
{
    [Theory]
    [InlineData(0x0000, 0, 0x00000000u)]
    [InlineData(0x8000, 0, 0x00000000u)]
    [InlineData(0x0000, 1, 0x00000000u)]
    [InlineData(0x8000, 1, 0x00000000u)]
    [InlineData(0x001F, 0, 0x3E000000u)]
    [InlineData(0x03E0, 1, 0x1F000000u)]
    [InlineData(0x7C00, 0, 0x3E000000u)]
    [InlineData(0x7FFF, 0, 0xBA000000u)]
    [InlineData(0xFFFF, 1, 0x5D000000u)]
    [InlineData(0xC210, 0, 0x60000000u)]
    public void OriginalAlphaPaletteProfileMapsRgbSumToBlackRgbAndRetainedShiftedAlpha(ushort word, byte shift, uint expected)
    {
        Assert.Equal(expected, NativeBattlePcPalette.Decode467470(word, shift));
    }

    [Fact]
    public void AlternatePaletteIgnoresStpAndRequiresItsObservedShiftProducer()
    {
        for (int color = 0; color < 0x8000; color++)
        {
            uint normal = NativeBattlePcPalette.Decode467470((ushort)color, 0);
            uint reduced = NativeBattlePcPalette.Decode467470((ushort)color, 1);
            Assert.Equal(normal, NativeBattlePcPalette.Decode467470((ushort)(color | 0x8000), 0));
            Assert.Equal(reduced, NativeBattlePcPalette.Decode467470((ushort)(color | 0x8000), 1));
            Assert.Equal(0u, normal & 0xFFFFFF);
            Assert.Equal(reduced >> 24, (normal >> 24) / 2);
        }
        Assert.Throws<NotSupportedException>(() => NativeBattlePcPalette.Decode467470(0x7FFF, 2));
        Assert.Equal((byte?)0, NativeBattlePrimitiveColorState.FromColdProcess().AlternatePaletteShift);
        Assert.Null(new NativeBattlePrimitiveColorState(false).AlternatePaletteShift);
    }

    [Theory]
    [InlineData(0x0000, 0x00000001u)]
    [InlineData(0x8000, 0x7F000008u)]
    [InlineData(0x001F, 0x7FFF0000u)]
    [InlineData(0x03E0, 0x7F00FF00u)]
    [InlineData(0x7C00, 0x7F0000FFu)]
    [InlineData(0x1084, 0x7F202020u)]
    [InlineData(0x4210, 0x7F838383u)]
    public void OriginalIndexedPaletteUsesFloor255Over31AndItsNativeBlackEntries(ushort word, uint expected)
    {
        Assert.Equal(expected, NativeBattlePcPalette.Decode467370(word));
    }

    [Fact]
    public void OriginalPcClutIgnoresStpForEveryNonzeroRgbEntry()
    {
        for (int color = 1; color < 0x8000; color++)
        {
            uint decoded = NativeBattlePcPalette.Decode467370((ushort)color);
            Assert.Equal(decoded, NativeBattlePcPalette.Decode467370((ushort)(color | 0x8000)));
            Assert.Equal(127u, decoded >> 24);
        }
    }
}
