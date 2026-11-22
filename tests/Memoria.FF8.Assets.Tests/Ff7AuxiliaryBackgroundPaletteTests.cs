using Memoria.FF7.Formats;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7AuxiliaryBackgroundPaletteTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 255)]
    [InlineData(0, 195, 0, 0, 255)]
    [InlineData(1, 0, 255, 255, 128)]
    [InlineData(1, 195, 255, 255, 128)]
    public void RenderingAuxiliaryRowsUseTheirNativeRgbaIncludingIndexZero(int auxiliary, int index, int red, int green, int alpha)
    {
        byte[] pixels = Enumerable.Repeat((byte)index, 256 * 256).ToArray();
        PcFieldBackground background = new(320, 240, [1], [], [new(0, 0, 1, pixels)]);
        PcFieldPalettes palettes = new(0, 0, 256, [new ushort[256]]);
        PcFieldTile tile = Tile(checked((short)(palettes.Palettes.Count + auxiliary)));
        foreach (bool cached in new[] { false, true })
        {
            var image = Ff7FieldRenderer.DecodeTile(background, palettes, tile, cached);
            Assert.Equal((byte)red, image[0, 0].R);
            Assert.Equal((byte)green, image[0, 0].G);
            Assert.Equal((byte)red, image[0, 0].B);
            Assert.Equal((byte)alpha, image[0, 0].A);
            Assert.Equal(image[0, 0], image[15, 15]);
        }
        Assert.All(palettes.Palettes[0], color => Assert.Equal((ushort)0, color));
    }

    [Fact]
    public void AuxiliaryRowsFollowTheDeclaredRowCountAndPreserveResourceColors()
    {
        byte[] pixels = new byte[256 * 256];
        pixels[0] = 1;
        ushort[] first = new ushort[256];
        ushort[] second = new ushort[256];
        second[1] = 31;
        PcFieldPalettes palettes = new(0, 0, 256, [first, second]);
        PcFieldBackground background = new(320, 240, [0, 0], [], [new(0, 0, 1, pixels)]);
        var resource = Ff7FieldRenderer.DecodeTile(background, palettes, Tile(1));
        Assert.Equal((byte)255, resource[0, 0].R);
        Assert.Equal((byte)0, resource[0, 0].G);
        Assert.Equal((byte)255, resource[0, 0].A);
        Assert.Equal((byte)255, Ff7FieldRenderer.DecodeTile(background, palettes, Tile(2))[0, 0].A);
        Assert.Equal((byte)128, Ff7FieldRenderer.DecodeTile(background, palettes, Tile(3))[0, 0].A);
        Assert.Throws<InvalidDataException>(() => Ff7FieldRenderer.DecodeTile(background, palettes, Tile(4)));
        Assert.Throws<InvalidDataException>(() => Ff7FieldRenderer.DecodeTile(background, palettes, Tile(-1)));
    }

    private static PcFieldTile Tile(short palette)
    {
        return new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, palette, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
    }
}
