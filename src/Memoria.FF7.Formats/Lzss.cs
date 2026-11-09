using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public static class Lzss
{
    public const int DefaultMaximumOutputBytes = 64 * 1024 * 1024;

    public static byte[] DecodeLengthFramed(ReadOnlySpan<byte> source, int maximumOutputBytes = DefaultMaximumOutputBytes)
    {
        if (source.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(source) != source.Length - 4)
        {
            throw new InvalidDataException("FF7 LZSS compressed-length header does not match the payload.");
        }
        return DecodeRaw(source[4..], maximumOutputBytes);
    }

    public static byte[] DecodeRaw(ReadOnlySpan<byte> source, int maximumOutputBytes = DefaultMaximumOutputBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumOutputBytes);
        using MemoryStream output = new();
        byte[] window = new byte[4096];
        int cursor = 0xFEE;
        int input = 0;
        while (input < source.Length)
        {
            byte flags = source[input++];
            if (input == source.Length)
            {
                throw new InvalidDataException("FF7 LZSS flag byte has no token.");
            }
            for (int bit = 0; bit < 8 && input < source.Length; bit++)
            {
                if ((flags & (1 << bit)) != 0)
                {
                    Append(source[input++]);
                }
                else
                {
                    if (source.Length - input < 2)
                    {
                        throw new InvalidDataException("FF7 LZSS reference is truncated.");
                    }
                    int offset = source[input] | ((source[input + 1] & 0xF0) << 4);
                    int length = (source[input + 1] & 0x0F) + 3;
                    input += 2;
                    for (int i = 0; i < length; i++)
                    {
                        Append(window[(offset + i) & 0xFFF]);
                    }
                }
            }
        }
        return output.ToArray();

        void Append(byte value)
        {
            if (output.Length >= maximumOutputBytes)
            {
                throw new InvalidDataException($"FF7 LZSS output exceeds limit {maximumOutputBytes}.");
            }
            output.WriteByte(value);
            window[cursor] = value;
            cursor = (cursor + 1) & 0xFFF;
        }
    }
}
