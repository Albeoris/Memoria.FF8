using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

internal sealed class Ff7WorldEncounterTable
{
    private static readonly byte[][] TerrainSets =
    [
        [0, 9, 0, 17], [0, 0, 0, 17], [0, 9, 1, 17], [0, 20, 0, 17],
        [0, 9, 8, 17], [0, 0, 25, 17], [0, 9, 19, 17], [0, 0, 1, 17],
        [0, 0, 1, 17], [0, 9, 14, 17], [0, 9, 25, 17], [0, 10, 9, 17],
        [0, 9, 25, 17], [0, 0, 8, 11], [0, 0, 8, 0], [0, 0, 1, 0]
    ];
    private static readonly byte[] YuffieRates = [0, 0, 32, 0, 0, 64, 0, 64, 255, 0, 128, 0, 128, 0, 0, 128];
    private readonly byte[] data;

    public Ff7WorldEncounterTable(byte[] data)
    {
        if (data.Length != 0x8A0)
        {
            throw new InvalidDataException("FFVII world encounter table must contain sixteen regions of four native records.");
        }
        this.data = data;
    }

    public int Select(int terrainInfo, int partyCount, bool yuffieEligible, int previous, ref int danger, Func<byte> random, out bool preemptive)
    {
        int region = Math.Min(15, (terrainInfo >> 9) & 31);
        int terrain = terrainInfo & 31;
        int selector = Array.IndexOf(TerrainSets[region], (byte)(terrain == 16 ? 0 : terrain == 24 ? 8 : terrain));
        int record = 0xA0 + (region * 4 + Math.Max(0, selector)) * 32;
        int rate = data[record + 1];
        // Baseline equipment rates from the native party modifier builder.
        const int encounterModifier = 16;
        const int initiativeModifier = 16;
        danger = unchecked(danger + (rate == 0 ? 0x7FFF : (encounterModifier << 10) / rate));
        preemptive = false;
        if (random() >= danger >> 8 || (data[record] & 1) == 0)
        {
            return -1;
        }
        if (random() < YuffieRates[region] && terrain is 1 or 25 && yuffieEligible)
        {
            throw new NotSupportedException("FFVII world Yuffie encounters require their field-return contract.");
        }
        danger = 0;
        preemptive = random() < initiativeModifier;
        if (!preemptive)
        {
            int special = Weighted(record + 0x0E, 2, random() << 8);
            if (special < 0 && partyCount > 1)
            {
                special = Weighted(record + 0x12, 1, random() << 8);
            }
            if (special < 0)
            {
                special = Weighted(record + 0x14, 1, random() << 8);
            }
            if (special >= 0)
            {
                return special;
            }
        }
        int formation = Weighted(record + 2, 6, random() << 8);
        return formation == previous ? Weighted(record + 2, 6, random() << 8) : formation;
    }

    private int Weighted(int offset, int count, int roll)
    {
        int weight = 0;
        for (int index = 0; index < count; index++)
        {
            int entry = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2 * index));
            weight += entry;
            if (weight > roll)
            {
                return entry & 1023;
            }
        }
        return -1;
    }
}

internal sealed class Ff7WorldEncounterRandom
{
    private readonly byte[] table;
    public int Index { get; private set; }

    public Ff7WorldEncounterRandom(uint seed)
    {
        uint[] words = new uint[0x209];
        uint value = 0;
        for (int index = 0; index < 17; index++)
        {
            for (int bit = 0; bit < 32; bit++)
            {
                seed = unchecked(seed * 0x5D588B65 + 1);
                value = (value >> 1) | (seed & 0x80000000);
            }
            words[index] = value;
        }
        words[16] = (words[16] << 23) ^ (words[0] >> 9) ^ words[15];
        for (int index = 17; index < words.Length; index++)
        {
            words[index] = (words[index - 17] << 23) ^ (words[index - 16] >> 9) ^ words[index - 1];
        }
        table = words.Select(word => (byte)word).ToArray();
        for (int pass = 0; pass < 3; pass++)
        {
            Refresh();
        }
        Index = table.Length - 1;
    }

    public Ff7WorldEncounterRandom(byte[] table, int index)
    {
        if (table.Length != 0x209 || (uint)index >= table.Length)
        {
            throw new InvalidDataException("Invalid FFVII world random continuation.");
        }
        this.table = table.ToArray();
        Index = index;
    }

    public byte[] Capture() => table.ToArray();

    public byte Next()
    {
        if (++Index == table.Length)
        {
            Refresh();
            Index = 0;
        }
        return table[Index];
    }

    private void Refresh()
    {
        for (int index = 0; index < 32; index++)
        {
            table[index] ^= table[index + 0x1E9];
        }
        for (int index = 32; index < table.Length; index++)
        {
            table[index] ^= table[index - 32];
        }
    }
}
