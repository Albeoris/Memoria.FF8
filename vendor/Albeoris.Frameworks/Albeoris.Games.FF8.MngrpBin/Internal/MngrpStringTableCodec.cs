using System.Runtime.CompilerServices;
using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>Reads logical string pointers independently of their physical storage order.</summary>
internal static class MngrpStringTableCodec
{
    private sealed record OriginalLayout(Byte[] Body, Byte[] State);
    private static readonly ConditionalWeakTable<MngrpStringTable, OriginalLayout> OriginalLayouts = new();

    public static MngrpStringTable Read(ReadOnlySpan<Byte> body, Encoding encoding, Boolean trimLastEntry, Int32? storageLength = null)
    {
        Int32 ownedLength = storageLength ?? body.Length;
        UInt16 count = ReadUInt16(body, 0, "entry count");
        Int32 headerLength = 2 + count * 2;
        if (ownedLength < headerLength || ownedLength > body.Length)
        {
            throw new InvalidDataException($"A string table declares {count} entries but has only {ownedLength} owned bytes.");
        }
        Int32[] offsets = new Int32[count];
        for (Int32 index = 0; index < count; index++)
        {
            offsets[index] = ReadUInt16(body, 2 + index * 2, "entry offset");
            if (offsets[index] != 0 && (offsets[index] < headerLength || offsets[index] >= body.Length))
            {
                throw new InvalidDataException($"String table entry {index} has offset {offsets[index]} outside its parent resource.");
            }
        }
        Int32[] physicalOffsets = offsets.Where(offset => offset != 0).Distinct().Order().ToArray();
        Dictionary<Int32, (MngrpText Text, Byte[] Trailing)> physical = [];
        for (Int32 index = 0; index < physicalOffsets.Length; index++)
        {
            Int32 start = physicalOffsets[index];
            Int32 terminator = body[start..].IndexOf((Byte)0);
            if (terminator < 0)
            {
                throw new InvalidDataException("A menu string has no terminator within its parent resource.");
            }
            Int32 textEnd = start + terminator;
            Int32 chunkEnd = index + 1 < physicalOffsets.Length ? physicalOffsets[index + 1] : ownedLength;
            // Storage chunks follow distinct physical starts. A native string can overlap
            // another start or refer beyond its table's storage into the parent group.
            Int32 trailingEnd = start < ownedLength ? Math.Min(chunkEnd, ownedLength) : textEnd + 1;
            ReadOnlySpan<Byte> trailing = trailingEnd > textEnd ? body[textEnd..trailingEnd] : [];
            if (trimLastEntry && index == physicalOffsets.Length - 1)
            {
                trailing = MngrpFormat.TrimTrailingZeros(trailing);
            }
            physical.Add(start, (MngrpTextSpanCodec.ReadText(body[start..textEnd], encoding), trailing.ToArray()));
        }
        MngrpStringTable table = new();
        Int32 first = physicalOffsets.Length == 0 ? ownedLength : Math.Min(physicalOffsets[0], ownedLength);
        table.LeadingBytes = physicalOffsets.Length == 0 ? MngrpFormat.TrimTrailingZeros(body[headerLength..ownedLength]).ToArray() : body[headerLength..first].ToArray();
        foreach (Int32 offset in offsets)
        {
            if (offset == 0)
            {
                table.Entries.Add(new MngrpTextEntry());
                continue;
            }
            var entry = physical[offset];
            table.Entries.Add(new MngrpTextEntry(new MngrpText(entry.Text.Value, entry.Text.EncodedValue)) { TrailingBytes = entry.Trailing.ToArray() });
        }
        OriginalLayouts.Add(table, new OriginalLayout(body[..ownedLength].ToArray(), State(table, encoding)));
        return table;
    }

    internal static void CopyLayout(MngrpStringTable source, MngrpStringTable destination)
    {
        if (OriginalLayouts.TryGetValue(source, out OriginalLayout? original))
        {
            OriginalLayouts.Add(destination, original);
        }
    }

    public static void Write(MngrpStringTable table, Encoding encoding, MngrpByteWriter writer, Boolean preserveLayout = true)
    {
        if (preserveLayout && OriginalLayouts.TryGetValue(table, out OriginalLayout? original) && State(table, encoding).AsSpan().SequenceEqual(original.State))
        {
            writer.WriteBytes(original.Body);
            return;
        }
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
            if (entry.TrailingBytes.Length == 0 || entry.TrailingBytes[0] != 0)
            {
                position++;
            }
        }
        writer.WriteBytes(table.LeadingBytes);
        foreach (MngrpTextEntry entry in entries.Where(entry => entry.Text is not null))
        {
            writer.WriteBytes(MngrpTextSpanCodec.GetBytes(entry.Text!, encoding).Span);
            if (entry.TrailingBytes.Length == 0 || entry.TrailingBytes[0] != 0)
            {
                writer.WriteZeros(1);
            }
            writer.WriteBytes(entry.TrailingBytes);
        }
    }

    internal static Byte[] State(MngrpStringTable table, Encoding encoding)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write(table.Entries.Count);
        writer.Write(table.LeadingBytes.Length);
        writer.Write(table.LeadingBytes);
        foreach (MngrpTextEntry entry in table.Entries)
        {
            writer.Write(entry.Text is not null);
            if (entry.Text is not null)
            {
                ReadOnlyMemory<Byte> bytes = MngrpTextSpanCodec.GetBytes(entry.Text, encoding);
                writer.Write(bytes.Length);
                writer.Write(bytes.Span);
            }
            writer.Write(entry.TrailingBytes.Length);
            writer.Write(entry.TrailingBytes);
        }
        return stream.ToArray();
    }

    private static UInt16 ReadUInt16(ReadOnlySpan<Byte> body, Int32 offset, String description)
    {
        if (offset + 2 > body.Length)
        {
            throw new InvalidDataException($"A string table ends in the middle of its {description}.");
        }
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(offset, 2));
    }
}
