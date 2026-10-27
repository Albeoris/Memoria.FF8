using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>Resolves nested table and text pointers within their complete parent group.</summary>
internal static class MngrpStringTableGroupCodec
{
    private sealed record OriginalLayout(Byte[] Body, Byte[] State);
    private static readonly ConditionalWeakTable<MngrpStringTableGroupSection, OriginalLayout> OriginalLayouts = new();

    public static MngrpStringTableGroupSection Read(Int32 slotIndex, ReadOnlySpan<Byte> body, Encoding encoding)
    {
        if (body.Length < 2)
        {
            throw new InvalidDataException("A string table group has no complete count word.");
        }
        UInt16 count = BinaryPrimitives.ReadUInt16LittleEndian(body);
        Int32 headerLength = 2 + count * 2;
        if (headerLength > body.Length)
        {
            throw new InvalidDataException($"A string table group declares {count} tables but is only {body.Length} bytes long.");
        }
        Int32[] offsets = new Int32[count];
        for (Int32 index = 0; index < count; index++)
        {
            offsets[index] = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(2 + index * 2, 2));
            if (offsets[index] != 0 && (offsets[index] < headerLength || offsets[index] >= body.Length))
            {
                throw new InvalidDataException($"Nested table {index} has offset {offsets[index]} outside its parent resource.");
            }
        }
        Int32[] physicalOffsets = offsets.Where(offset => offset != 0).Distinct().Order().ToArray();
        Int32 bodyLength = body.Length;
        Dictionary<Int32, Int32> ends = physicalOffsets.Select((offset, index) => (offset, end: index + 1 < physicalOffsets.Length ? physicalOffsets[index + 1] : bodyLength)).ToDictionary(item => item.offset, item => item.end);
        MngrpStringTableGroupSection section = new(slotIndex);
        section.LeadingBytes = physicalOffsets.Length > 0 ? body[headerLength..physicalOffsets[0]].ToArray() : MngrpFormat.TrimTrailingZeros(body[headerLength..]).ToArray();
        foreach (Int32 offset in offsets)
        {
            if (offset == 0)
            {
                section.Tables.Add(null);
                continue;
            }
            section.Tables.Add(MngrpStringTableCodec.Read(body[offset..], encoding, trimLastEntry: ends[offset] == body.Length, storageLength: ends[offset] - offset));
        }
        OriginalLayouts.Add(section, new OriginalLayout(body.ToArray(), State(section, encoding)));
        return section;
    }

    public static void Write(MngrpStringTableGroupSection section, Encoding encoding, MngrpByteWriter writer)
    {
        if (OriginalLayouts.TryGetValue(section, out OriginalLayout? original) && State(section, encoding).AsSpan().SequenceEqual(original.State))
        {
            writer.WriteBytes(original.Body);
            return;
        }
        List<MngrpStringTable?> tables = section.Tables;
        Byte[][] serializedTables = new Byte[tables.Count][];
        for (Int32 index = 0; index < tables.Count; index++)
        {
            if (tables[index] is not MngrpStringTable table)
            {
                serializedTables[index] = [];
                continue;
            }
            MngrpByteWriter tableWriter = new();
            // Rebuild all parent-relative text references when any table changes.
            MngrpStringTableCodec.Write(table, encoding, tableWriter, preserveLayout: false);
            serializedTables[index] = tableWriter.ToArray();
        }
        writer.WriteUInt16(MngrpFormat.ToUInt16(tables.Count, "nested table count"));
        Int32 position = 2 + tables.Count * 2 + section.LeadingBytes.Length;
        for (Int32 index = 0; index < tables.Count; index++)
        {
            if (tables[index] is null)
            {
                writer.WriteUInt16(0);
                continue;
            }
            writer.WriteUInt16(MngrpFormat.ToUInt16(position, "nested table offset"));
            position += serializedTables[index].Length;
        }
        writer.WriteBytes(section.LeadingBytes);
        foreach (Byte[] serializedTable in serializedTables)
        {
            writer.WriteBytes(serializedTable);
        }
    }

    private static Byte[] State(MngrpStringTableGroupSection section, Encoding encoding)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write(section.Tables.Count);
        writer.Write(section.LeadingBytes.Length);
        writer.Write(section.LeadingBytes);
        foreach (MngrpStringTable? table in section.Tables)
        {
            writer.Write(table is not null);
            if (table is not null)
            {
                Byte[] state = MngrpStringTableCodec.State(table, encoding);
                writer.Write(state.Length);
                writer.Write(state);
            }
        }
        return stream.ToArray();
    }
}
