using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeLzsArtworkTests
{
    [Fact]
    public void HeaderDimensionsAndDisplayColorsAreIndependentOfStartupCanvas()
    {
        PixelImage image = NativeLzsArtwork.Decode(Pack(4, 1, [0x001F, 0x03E0, 0x7C00, 0x8000]));
        Assert.Equal(4, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[0, 0]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), image[1, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), image[2, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), image[3, 0]);
        Assert.Throws<NotSupportedException>(() => NativeLzsArtwork.Decode(Pack(4, 1, [0, 0, 0, 0]), 5, 1));
        Assert.Throws<NotSupportedException>(() => NativeStartupArtwork.Decode(Pack(640, 407, [])));
    }

    [Fact]
    public void InvalidLengthsDimensionsAndTruncatedPixelsFailBeforePublication()
    {
        Assert.Throws<InvalidDataException>(() => NativeLzsArtwork.Decode([0, 0, 0]));
        Assert.Throws<InvalidDataException>(() => NativeLzsArtwork.Decode(Pack(0, 1, [])));
        Assert.Throws<InvalidDataException>(() => NativeLzsArtwork.Decode(Pack(ushort.MaxValue, ushort.MaxValue, [])));
        Assert.Throws<InvalidDataException>(() => NativeLzsArtwork.Decode(Pack(4, 1, [0])));
        byte[] trailing = [.. Pack(1, 1, [0]), 1];
        BinaryPrimitives.WriteUInt32LittleEndian(trailing, (uint)(trailing.Length - 4));
        Assert.Throws<InvalidDataException>(() => NativeLzsArtwork.Decode(trailing));
    }

    private static byte[] Pack(ushort width, ushort height, ushort[] pixels)
    {
        byte[] raw = new byte[8 + pixels.Length * 2];
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(4), width);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(6), height);
        for (int index = 0; index < pixels.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(8 + index * 2), pixels[index]);
        }
        using MemoryStream compressed = new();
        compressed.Write(new byte[4]);
        for (int offset = 0; offset < raw.Length; offset += 8)
        {
            compressed.WriteByte(255);
            compressed.Write(raw, offset, Math.Min(8, raw.Length - offset));
        }
        byte[] result = compressed.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)(result.Length - 4));
        return result;
    }
}
