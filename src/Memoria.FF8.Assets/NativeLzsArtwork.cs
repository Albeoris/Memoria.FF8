using System.Buffers.Binary;
using Albeoris.Games.Core.NSCompression.LZS;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

/// <summary>Decodes native LZS image headers and opaque PSX BGR555 display pixels.</summary>
public static class NativeLzsArtwork
{
    public static PixelImage Decode(byte[] bytes) => DecodeCore(bytes, null, null);

    public static PixelImage Decode(byte[] bytes, int expectedWidth, int expectedHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedHeight, 1);
        return DecodeCore(bytes, expectedWidth, expectedHeight);
    }

    private static PixelImage DecodeCore(byte[] bytes, int? expectedWidth, int? expectedHeight)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != bytes.Length - 4)
        {
            throw new InvalidDataException("Native image resource lacks its bounded inner LZS stream.");
        }
        using MemoryStream input = new(bytes, 4, bytes.Length - 4, false);
        using LZSDecompressionStream decoder = new(input, bytes.Length - 4, 8L + PixelImage.MaximumPixelCount * 2L, true);
        try
        {
            byte[] header = new byte[8];
            decoder.ReadExactly(header);
            int width = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4));
            int height = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6));
            if (expectedWidth is not null && (width != expectedWidth || height != expectedHeight))
            {
                throw new NotSupportedException($"Native image resource differs from its required {expectedWidth} by {expectedHeight} canvas.");
            }
            if (width < 1 || width > PixelImage.MaximumDimension || height < 1 || height > PixelImage.MaximumDimension || (long)width * height > PixelImage.MaximumPixelCount)
            {
                throw new InvalidDataException("Native image dimensions exceed the supported pixel budget.");
            }
            PixelImage image = new(width, height);
            byte[] row = new byte[width * 2];
            for (int y = 0; y < height; y++)
            {
                decoder.ReadExactly(row);
                for (int x = 0; x < width; x++)
                {
                    ushort value = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(x * 2));
                    image[x, y] = Rgba32.FromPsxBgr555(value) with { A = 255 };
                }
            }
            if (input.Position != input.Length)
            {
                throw new InvalidDataException("Native image contains compressed bytes beyond its declared canvas.");
            }
            return image;
        }
        catch (Exception exception) when (exception is EndOfStreamException || exception.GetType() == typeof(Exception))
        {
            throw new InvalidDataException("Native image LZS stream is truncated.", exception);
        }
    }
}
