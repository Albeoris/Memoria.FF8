using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>Original PC EXP, level, base-stat and base-HP/MP growth. The battle caller owns RNG and reward atomicity.</summary>
public sealed class Ff7NativeGrowth
{
    private readonly KernelGrowthTables tables;

    public Ff7NativeGrowth(KernelGrowthTables tables)
    {
        this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
    }

    public byte[] Apply(ReadOnlySpan<byte> nativeRecord, uint awardedExp, Ff7BattleRng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (nativeRecord.Length != 132 || nativeRecord[0] >= 9 || nativeRecord[1] is < 1 or > 99)
        {
            throw new InvalidDataException("Character growth requires a complete native character record and valid level.");
        }
        byte[] record = nativeRecord.ToArray();
        int character = record[0];
        int level = record[1];
        uint experience = Math.Min(unchecked(BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0x3C)) + awardedExp), 999999999u);
        int[] values = new int[8];
        for (int index = 0; index < 6; index++)
        {
            values[index] = Math.Max(1, (int)record[index + 2]);
        }
        values[6] = Math.Max(1, (int)BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(0x2E)));
        values[7] = Math.Max(1, (int)BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(0x32)));
        uint previousThreshold = Threshold(character, level - 1);
        uint nextThreshold = previousThreshold;
        while (level < 99)
        {
            int band = LevelBand(level);
            nextThreshold = Threshold(character, level);
            if (nextThreshold > experience)
            {
                break;
            }
            for (int statistic = 0; statistic < 6; statistic++)
            {
                KernelGrowthCurve curve = tables.Curve(character, statistic, band);
                int target = curve.Gradient * (level + 1) / 100 + curve.Intercept;
                int distribution = Distribution(target - values[statistic], rng);
                values[statistic] = Math.Min(100, values[statistic] + tables.RandomIncrement(statistic, distribution));
            }
            for (int statistic = 6; statistic < 8; statistic++)
            {
                KernelGrowthCurve curve = tables.Curve(character, statistic, band);
                int current = VitalityCurve(curve, level, statistic);
                int next = VitalityCurve(curve, level + 1, statistic);
                int distribution = Distribution(current * 100 / values[statistic] - 100, rng);
                values[statistic] = Math.Min(statistic == 6 ? 9999 : 999, values[statistic] + (next - current) * tables.RandomIncrement(statistic, distribution) / 100);
                values[statistic] = unchecked((ushort)values[statistic]);
            }
            level++;
            previousThreshold = nextThreshold;
        }
        record[1] = (byte)level;
        for (int statistic = 0; statistic < 6; statistic++)
        {
            record[statistic + 2] = unchecked((byte)values[statistic]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x2E), (ushort)values[6]);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0x32), (ushort)values[7]);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x3C), experience);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x80), level == 99 ? 0 : unchecked(nextThreshold - experience));
        record[0x21] = level == 99 ? (byte)61 : nextThreshold == previousThreshold ? (byte)0 : unchecked((byte)(unchecked((experience - previousThreshold) * 61) / (nextThreshold - previousThreshold)));
        return record;
    }

    public uint Threshold(int character, int level)
    {
        if ((uint)level > 98)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }
        KernelGrowthCurve curve = tables.Curve(character, 8, LevelBand(level));
        uint result = 0;
        for (int index = 1; index <= level; index++)
        {
            result += (uint)(curve.Gradient * index * index / 10);
        }
        return result;
    }

    private static int LevelBand(int level)
    {
        return level == 0 ? 0 : level <= 60 ? (level - 1) / 10 : (level - 61) / 20 + 6;
    }

    private static int VitalityCurve(KernelGrowthCurve curve, int level, int statistic)
    {
        return statistic == 6 ? curve.Gradient * level + curve.Intercept * 40 : curve.Gradient * level / 10 + curve.Intercept * 2;
    }

    private static int Distribution(int difference, Ff7BattleRng rng)
    {
        return Math.Clamp(difference + (rng.NextByte() & 7) + 1, 0, 11);
    }
}
