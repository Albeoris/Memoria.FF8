using System.IO.Compression;

namespace Memoria.FF7.Formats;

public sealed record KernelSection(ushort Type, ReadOnlyMemory<byte> Data);
public static class KernelArchive
{
    public static IReadOnlyList<KernelSection> Read(ReadOnlySpan<byte> source, int maximumSections = 64, int maximumDecodedBytes = 4 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSections);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDecodedBytes);
        BinaryCursor cursor = new(source);
        List<KernelSection> sections = new();
        int total = 0;
        while (cursor.Remaining > 0)
        {
            if (cursor.Remaining == 2 && source[^2..].SequenceEqual(new byte[2]))
            {
                cursor.Skip(2);
                break;
            }
            if (sections.Count >= maximumSections)
            {
                throw new InvalidDataException("FF7 kernel exceeds its section limit.");
            }
            ushort compressed = cursor.U16();
            ushort decoded = cursor.U16();
            ushort type = cursor.U16();
            if (decoded > maximumDecodedBytes - total)
            {
                throw new InvalidDataException("FF7 kernel exceeds its decoded byte limit.");
            }
            using MemoryStream input = new(cursor.Take(compressed).ToArray(), false);
            using GZipStream gzip = new(input, CompressionMode.Decompress);
            byte[] data = new byte[decoded];
            try
            {
                gzip.ReadExactly(data);
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("FF7 kernel section decompressed shorter than declared.", exception);
            }
            if (gzip.ReadByte() != -1)
            {
                throw new InvalidDataException("FF7 kernel section decompressed longer than declared.");
            }
            total += decoded;
            sections.Add(new KernelSection(type, data));
        }
        return sections.AsReadOnly();
    }
}
