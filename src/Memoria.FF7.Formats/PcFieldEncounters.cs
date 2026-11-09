namespace Memoria.FF7.Formats;

public sealed record PcFieldEncounter(ushort Formation, byte Frequency);
public sealed record PcFieldEncounterTable(bool Enabled, byte Rate, IReadOnlyList<PcFieldEncounter> Standard, IReadOnlyList<PcFieldEncounter> Special)
{
    public static IReadOnlyList<PcFieldEncounterTable> Read(ReadOnlySpan<byte> data)
    {
        if (data.Length != 48)
        {
            throw new InvalidDataException("FFVII field encounters require two native 24-byte tables.");
        }
        BinaryCursor cursor = new(data);
        PcFieldEncounterTable[] tables = new PcFieldEncounterTable[2];
        for (int table = 0; table < tables.Length; table++)
        {
            bool enabled = (cursor.U8() & 1) != 0;
            byte rate = cursor.U8();
            PcFieldEncounter[] encounters = new PcFieldEncounter[10];
            for (int index = 0; index < encounters.Length; index++)
            {
                ushort encoded = cursor.U16();
                encounters[index] = new((ushort)(encoded & 1023), (byte)(encoded >> 10));
            }
            cursor.Skip(2);
            if (enabled && rate == 0)
            {
                throw new InvalidDataException("An enabled FFVII encounter table has zero native frequency divisor.");
            }
            tables[table] = new(enabled, rate, Array.AsReadOnly(encounters[..6]), Array.AsReadOnly(encounters[6..]));
        }
        return Array.AsReadOnly(tables);
    }
}
