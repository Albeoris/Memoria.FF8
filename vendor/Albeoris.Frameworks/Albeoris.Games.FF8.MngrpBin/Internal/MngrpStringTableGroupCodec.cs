using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// Reads and writes the string-table-group layout: a 16-bit table count, one 16-bit offset per
/// nested table (zero for an absent table), then the nested tables. Each nested table owns the
/// whole span up to the next one, so the alignment padding and the packer garbage found between
/// tables survive a round trip inside the last entry of the preceding table.
/// </summary>
internal static class MngrpStringTableGroupCodec
{
    public static MngrpStringTableGroupSection Read(Int32 slotIndex, ReadOnlySpan<Byte> body, Encoding encoding)
    {
        UInt16 count = BinaryPrimitives.ReadUInt16LittleEndian(body);
        Int32 headerLength = 2 + count * 2;
        if (headerLength > body.Length)
        {
            throw new InvalidDataException($"A string table group declares {count} tables but is only {body.Length} bytes long.");
        }

        Int32[] offsets = new Int32[count];
        List<Int32> presentIndexes = [];
        for (Int32 i = 0; i < count; i++)
        {
            offsets[i] = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(2 + i * 2, 2));
            if (offsets[i] == 0)
            {
                continue;
            }

            Int32 previous = presentIndexes.Count > 0 ? offsets[presentIndexes[^1]] : headerLength;
            if (offsets[i] < previous || offsets[i] >= body.Length)
            {
                throw new InvalidDataException($"Nested table {i} has offset {offsets[i]}, which is out of order or out of bounds.");
            }

            presentIndexes.Add(i);
        }

        MngrpStringTableGroupSection section = new(slotIndex);
        section.LeadingBytes = presentIndexes.Count > 0
            ? body[headerLength..offsets[presentIndexes[0]]].ToArray()
            : MngrpFormat.TrimTrailingZeros(body[Math.Min(headerLength, body.Length)..]).ToArray();

        Int32 presentRead = 0;
        for (Int32 i = 0; i < count; i++)
        {
            if (offsets[i] == 0)
            {
                section.Tables.Add(null);
                continue;
            }

            Boolean isLast = presentRead == presentIndexes.Count - 1;
            Int32 end = isLast ? body.Length : offsets[presentIndexes[presentRead + 1]];
            section.Tables.Add(MngrpStringTableCodec.Read(body[offsets[i]..end], encoding, trimLastEntry: isLast));
            presentRead++;
        }

        return section;
    }

    public static void Write(MngrpStringTableGroupSection section, Encoding encoding, MngrpByteWriter writer)
    {
        List<MngrpStringTable?> tables = section.Tables;
        Byte[][] serializedTables = new Byte[tables.Count][];
        for (Int32 i = 0; i < tables.Count; i++)
        {
            if (tables[i] is not MngrpStringTable table)
            {
                serializedTables[i] = [];
                continue;
            }

            MngrpByteWriter tableWriter = new();
            MngrpStringTableCodec.Write(table, encoding, tableWriter);
            serializedTables[i] = tableWriter.ToArray();
        }

        writer.WriteUInt16(MngrpFormat.ToUInt16(tables.Count, "nested table count"));
        Int32 position = 2 + tables.Count * 2 + section.LeadingBytes.Length;
        for (Int32 i = 0; i < tables.Count; i++)
        {
            if (tables[i] is null)
            {
                writer.WriteUInt16(0);
                continue;
            }

            writer.WriteUInt16(MngrpFormat.ToUInt16(position, "nested table offset"));
            position += serializedTables[i].Length;
        }

        writer.WriteBytes(section.LeadingBytes);
        foreach (Byte[] serializedTable in serializedTables)
        {
            writer.WriteBytes(serializedTable);
        }
    }
}
