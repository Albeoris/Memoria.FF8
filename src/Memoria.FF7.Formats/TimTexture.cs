using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public sealed record TimTexture(int Width, int Height, int BitsPerPixel, IReadOnlyList<IReadOnlyList<ushort>> Palettes, ReadOnlyMemory<byte> Pixels, ushort ImageX, ushort ImageY)
{
    public uint DeclaredImageSize { get; init; }
    public int GetPixel(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        int rowBytes = (Width * BitsPerPixel + 7) / 8;
        int offset = y * rowBytes;
        return BitsPerPixel switch
        {
            4 => (Pixels.Span[offset + x / 2] >> (4 * (x & 1))) & 15,
            8 => Pixels.Span[offset + x],
            16 => BinaryPrimitives.ReadUInt16LittleEndian(Pixels.Span[(offset + x * 2)..]),
            _ => throw new NotSupportedException("Direct 24-bit TIM pixels require RGB byte access.")
        };
    }

    public static TimTexture Read(ReadOnlySpan<byte> source, int maximumPixels = 16 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumPixels);
        BinaryCursor cursor = new(source);
        if (cursor.U32() != 0x10)
        {
            throw new InvalidDataException("Invalid TIM signature.");
        }
        uint flags = cursor.U32();
        if ((flags & ~11u) != 0)
        {
            throw new InvalidDataException("Unsupported TIM flags.");
        }
        int depth = (int)(flags & 3);
        int bits = depth switch { 0 => 4, 1 => 8, 2 => 16, _ => 24 };
        List<IReadOnlyList<ushort>> palettes = new();
        if ((flags & 8) != 0)
        {
            uint size = cursor.U32();
            cursor.Skip(4);
            ushort paletteWidth = cursor.U16();
            ushort paletteHeight = cursor.U16();
            long entries = (long)paletteWidth * paletteHeight;
            int colors = depth == 0 ? 16 : 256;
            if (size != 12 + entries * 2 || entries > cursor.Remaining / 2 || entries % colors != 0)
            {
                throw new InvalidDataException("TIM palette block dimensions are invalid.");
            }
            for (int palette = 0; palette < entries / colors; palette++)
            {
                ushort[] values = new ushort[colors];
                for (int i = 0; i < colors; i++)
                {
                    values[i] = cursor.U16();
                }
                palettes.Add(Array.AsReadOnly(values));
            }
        }
        uint imageSize = cursor.U32();
        ushort x = cursor.U16();
        ushort y = cursor.U16();
        ushort widthWords = cursor.U16();
        ushort height = cursor.U16();
        int width = widthWords * 16 / bits;
        long requiredBytes = widthWords * 2L * height;
        if (width == 0 || height == 0 || (long)width * height > maximumPixels || requiredBytes > cursor.Remaining)
        {
            throw new InvalidDataException("TIM image block dimensions exceed the input or configured limit.");
        }
        byte[] pixels = cursor.Take((int)requiredBytes).ToArray();
        if (cursor.Remaining != 0)
        {
            throw new InvalidDataException("TIM contains trailing data.");
        }
        return new TimTexture(width, height, bits, palettes.AsReadOnly(), pixels, x, y) { DeclaredImageSize = imageSize };
    }
}
