using System.Buffers.Binary;
using Albeoris.Games.Core.NSCompression.LZS;

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
        LzsDecoderKernel kernel = new();
        int input = 0;
        while (input < source.Length)
        {
            int required = kernel.RequiredInputBytes;
            if (source.Length - input < required)
            {
                throw new InvalidDataException("FF7 LZSS reference is truncated.");
            }
            kernel.AcceptInput(source.Slice(input, required));
            input += required;
            while (kernel.TryReadOutput(out byte value))
            {
                if (output.Length >= maximumOutputBytes)
                {
                    throw new InvalidDataException($"FF7 LZSS output exceeds limit {maximumOutputBytes}.");
                }
                output.WriteByte(value);
            }
        }
        if (kernel.FlagHasNoToken)
        {
            throw new InvalidDataException("FF7 LZSS flag byte has no token.");
        }
        return output.ToArray();
    }
}
