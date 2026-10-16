using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// Reads and writes the string-table layout: a 16-bit entry count, one 16-bit offset per entry
/// (zero for an absent entry), then the texts. Each text owns the whole span up to the next
/// addressed text, so the irregular padding and the packer garbage found between texts survive
/// a round trip inside <see cref="MngrpTextEntry.TrailingBytes"/>.
/// </summary>
internal static class MngrpStringTableCodec
{
    /// <summary>
    /// Parses a table from <paramref name="body"/>. The last text's span reaches the end of
    /// <paramref name="body"/>; with <paramref name="trimLastEntry"/> set its trailing zeros are
    /// dropped, because the caller recreates them as computed padding.
    /// </summary>
    public static MngrpStringTable Read(ReadOnlySpan<Byte> body, Encoding encoding, Boolean trimLastEntry)
    {
        UInt16 count = ReadUInt16(body, 0, "entry count");
        Int32 headerLength = 2 + count * 2;
        if (headerLength > body.Length)
        {
            throw new InvalidDataException($"A string table declares {count} entries but is only {body.Length} bytes long.");
        }

        Int32[] offsets = new Int32[count];
        List<Int32> presentIndexes = [];
        for (Int32 i = 0; i < count; i++)
        {
            offsets[i] = ReadUInt16(body, 2 + i * 2, "entry offset");
            if (offsets[i] == 0)
            {
                continue;
            }

            Int32 previous = presentIndexes.Count > 0 ? offsets[presentIndexes[^1]] : headerLength;
            if (offsets[i] < previous || offsets[i] >= body.Length)
            {
                throw new InvalidDataException($"String table entry {i} has offset {offsets[i]}, which is out of order or out of bounds.");
            }

            presentIndexes.Add(i);
        }

        MngrpStringTable table = new();
        table.LeadingBytes = presentIndexes.Count > 0
            ? body[headerLength..offsets[presentIndexes[0]]].ToArray()
            : MngrpFormat.TrimTrailingZeros(body[Math.Min(headerLength, body.Length)..]).ToArray();

        Int32 presentRead = 0;
        for (Int32 i = 0; i < count; i++)
        {
            if (offsets[i] == 0)
            {
                table.Entries.Add(new MngrpTextEntry());
                continue;
            }

            Boolean isLast = presentRead == presentIndexes.Count - 1;
            Int32 end = isLast ? body.Length : offsets[presentIndexes[presentRead + 1]];
            table.Entries.Add(MngrpTextSpanCodec.ReadEntry(body[offsets[i]..end], encoding, trimTrailingZeros: isLast && trimLastEntry));
            presentRead++;
        }

        return table;
    }

    public static void Write(MngrpStringTable table, Encoding encoding, MngrpByteWriter writer)
    {
        List<MngrpTextEntry> entries = table.Entries;
        writer.WriteUInt16(MngrpFormat.ToUInt16(entries.Count, "string table entry count"));

        Int32 position = 2 + entries.Count * 2 + table.LeadingBytes.Length;
        foreach (MngrpTextEntry entry in entries)
        {
            if (entry.Text is null)
            {
                writer.WriteUInt16(0);
                continue;
            }

            writer.WriteUInt16(MngrpFormat.ToUInt16(position, "string table entry offset"));
            position += MngrpTextSpanCodec.Measure(entry, encoding);
        }

        writer.WriteBytes(table.LeadingBytes);
        foreach (MngrpTextEntry entry in entries.Where(entry => entry.Text is not null))
        {
            MngrpTextSpanCodec.WriteEntry(entry, encoding, writer);
        }
    }

    private static UInt16 ReadUInt16(ReadOnlySpan<Byte> body, Int32 offset, String description)
    {
        if (offset + 2 > body.Length)
        {
            throw new InvalidDataException($"A string table ends in the middle of its {description}.");
        }

        return BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(offset, 2));
    }
}
