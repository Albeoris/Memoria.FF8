using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7MenuPortraitTests
{
    [Fact]
    public void NativeUvRegionUsesSelectedPaletteAndKeepsWhiteAndAlphaWithinThePortrait()
    {
        byte[] header = Header();
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0xD0), 1);
        byte[] pixels = new byte[128 * 128];
        pixels[0] = 1;
        pixels[83 + 95 * 128] = 2;
        PcTexture texture = new(128, 128, 1, [new uint[] { 0xFF000000, 0xFF00FF00, 0 }, new uint[] { 0xFFFFFFFF, 0x7F123456, 0xFFABCDEF }], pixels, header);
        Ff7MenuPortrait image = Ff7MenuPortrait.Decode(texture);
        Assert.Equal((84, 96), (image.Width, image.Height));
        Assert.Equal(0x7F123456u, image.Pixels.Span[0]);
        Assert.Equal(0xFFFFFFFFu, image.Pixels.Span[1]);
        Assert.Equal(0xFFABCDEFu, image.Pixels.Span[^1]);
        Assert.Equal(0xFFFFFFFFu, image.Pixels.Span[84]);
    }

    [Fact]
    public void UnsupportedColorKeysAndMalformedPaletteBoundsFailInsteadOfChangingTheImage()
    {
        PcTexture texture = new(128, 128, 1, [new uint[] { 0xFFFFFFFF }], new byte[128 * 128], Header());
        byte[] keyed = Header();
        BinaryPrimitives.WriteUInt32LittleEndian(keyed.AsSpan(8), 1);
        Assert.Throws<NotSupportedException>(() => Ff7MenuPortrait.Decode(texture with { Header = keyed }));
        byte[] perPalette = Header();
        BinaryPrimitives.WriteUInt32LittleEndian(perPalette.AsSpan(0xBC), 1);
        Assert.Throws<NotSupportedException>(() => Ff7MenuPortrait.Decode(texture with { Header = perPalette }));
        byte[] wrongPalette = Header();
        BinaryPrimitives.WriteUInt32LittleEndian(wrongPalette.AsSpan(0xD0), 1);
        Assert.Throws<InvalidDataException>(() => Ff7MenuPortrait.Decode(texture with { Header = wrongPalette }));
        Assert.Throws<InvalidDataException>(() => Ff7MenuPortrait.Decode(texture with { Width = 127 }));
    }

    [Fact]
    public void OptionalInstalledNineOriginalPortraitsUseTheNativeMenuRectangle()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF7_GAME_DIR for the bounded original portrait check.");
        Ff7MenuResources menu = Ff7MenuResources.LoadPortraits(Ff7Installation.Discover(directory!));
        Assert.Equal(9, menu.Textures.Count);
        for (int id = 0; id < 9; id++)
        {
            PcTexture original = menu.GetPortrait(id);
            Ff7MenuPortrait image = Ff7MenuPortrait.Decode(original);
            Assert.Equal((84, 96), (image.Width, image.Height));
            uint first = original.Palettes[(int)original.RenderInfo.PaletteIndex][original.Pixels.Span[0]];
            uint last = original.Palettes[(int)original.RenderInfo.PaletteIndex][original.Pixels.Span[95 * original.Width + 83]];
            Assert.Equal(first, image.Pixels.Span[0]);
            Assert.Equal(last, image.Pixels.Span[^1]);
        }
    }

    private static byte[] Header()
    {
        byte[] header = new byte[0xEC];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x4C), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x50), 8);
        return header;
    }
}
