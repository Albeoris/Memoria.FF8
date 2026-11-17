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

    public byte[] InitializeJoin(ReadOnlySpan<byte> nativeRecord, IReadOnlyList<Ff7CharacterState> characters, ushort available, Ff7BattleRng random, byte? savedLevel = null)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(random);
        if (nativeRecord.Length != 132 || nativeRecord[0] >= 9 || nativeRecord[1] is < 1 or > 99 || characters.Any(character => character is null || character.Id is < 0 or > 8 || character.NativeRecord is not { Length: 132 }) || characters.Select(character => character.Id).Distinct().Count() != characters.Count)
        {
            throw new InvalidDataException("Native join growth requires complete character records and a valid joining persona.");
        }
        int persona = nativeRecord[0];
        sbyte modifier = tables.JoinModifier(persona);
        int targetLevel;
        bool reset = (modifier & 1) != 0;
        if (reset)
        {
            targetLevel = savedLevel ?? throw new NotSupportedException("Native join growth requires its saved level input for this modifier.");
        }
        else
        {
            Ff7CharacterState[] physicalRecords = characters.OrderBy(character => character.Id).ToArray();
            int levels = 0;
            int count = 0;
            for (int logical = 0; logical < 9; logical++)
            {
                if (logical == persona || (available & 1 << logical) == 0)
                {
                    continue;
                }
                Ff7CharacterState? selected = physicalRecords.FirstOrDefault(character => character.NativeRecord[0] == logical);
                if (selected is not null)
                {
                    levels += selected.NativeRecord[1];
                    count++;
                }
            }
            targetLevel = (count == 0 ? nativeRecord[1] : levels / count) + modifier / 2;
        }
        targetLevel = Math.Max(1, targetLevel);
        byte[] prepared = nativeRecord.ToArray();
        if (reset)
        {
            prepared[1] = 1;
            prepared.AsSpan(2, 6).Clear();
            prepared.AsSpan(0x2E, 2).Clear();
            prepared.AsSpan(0x32, 2).Clear();
            prepared.AsSpan(0x3C, 4).Clear();
        }
        return Apply(prepared, JoinThreshold(persona, targetLevel - 1), random);
    }

    private uint JoinThreshold(int character, int level)
    {
        int gradient = tables.JoinExperienceGradient(character, LevelBand(level));
        uint result = 0;
        for (int index = 1; index <= level; index++)
        {
            result = unchecked(result + (uint)(gradient * index * index / 10));
        }
        return result;
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
