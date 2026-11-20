using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7DirectColorBackgroundTests
{
    [Theory]
    [InlineData(0x0000, 0, 0, 0, 0)]
    [InlineData(0xF800, 255, 0, 0, 255)]
    [InlineData(0x07E0, 0, 255, 0, 255)]
    [InlineData(0x001F, 0, 0, 255, 255)]
    [InlineData(0x0020, 0, 4, 0, 255)]
    [InlineData(0x0400, 0, 130, 0, 255)]
    [InlineData(0x0821, 8, 4, 8, 255)]
    [InlineData(0xFFFF, 255, 255, 255, 255)]
    public void CachedDirectPixelsUseRgb565WithoutPaletteOrStpInterpretation(int packed, int red, int green, int blue, int alpha)
    {
        byte[] pixels = new byte[256 * 256 * 2];
        // An offset texel also verifies that direct pages retain their two-byte pixel stride.
        BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan((2 * 256 + 3) * 2), (ushort)packed);
        PcFieldTexturePage page = new(26, 0, 2, pixels);
        PcFieldBackground background = new(320, 240, [], [], [page]);
        PcFieldPalettes palettes = new(0, 0, 0, []);
        PcFieldTile tile = new(0, 0, 0, 0, 3, 2, 0, 0, 16, 16, 0, 4095, 0, 0, 0, 0, 26, 0, 2, 0, 0, 0, ReadOnlyMemory<byte>.Empty);

        var image = Ff7FieldRenderer.DecodeTile(background, palettes, tile);

        Assert.Equal((byte)red, image[0, 0].R);
        Assert.Equal((byte)green, image[0, 0].G);
        Assert.Equal((byte)blue, image[0, 0].B);
        Assert.Equal((byte)alpha, image[0, 0].A);
        Assert.Equal((byte)0, image[1, 0].A);
    }
}
