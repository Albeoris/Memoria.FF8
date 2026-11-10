using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public sealed record PcWorldFieldEntry(short X, short Y, ushort Triangle, ushort Field, byte Direction);

public sealed record PcWorldFieldTable(IReadOnlyList<PcWorldFieldEntry> Entries)
{
    public static PcWorldFieldTable Read(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0 || source.Length % 12 != 0)
        {
            throw new InvalidDataException("FF7 world field table requires complete twelve-byte rows.");
        }
        PcWorldFieldEntry[] entries = new PcWorldFieldEntry[source.Length / 12];
        for (int i = 0; i < entries.Length; i++)
        {
            ReadOnlySpan<byte> row = source.Slice(i * 12, 12);
            entries[i] = new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadUInt16LittleEndian(row[4..]), BinaryPrimitives.ReadUInt16LittleEndian(row[6..]), row[8]);
        }
        return new(Array.AsReadOnly(entries));
    }

    public PcWorldFieldEntry Select(ushort selector)
    {
        int index = (((selector >> 8) * 2 - 2) & 0x1FE) | (selector & 1);
        if (index >= Entries.Count)
        {
            throw new InvalidDataException("FF7 world field selector resolves outside the original table.");
        }
        return Entries[index];
    }
}
