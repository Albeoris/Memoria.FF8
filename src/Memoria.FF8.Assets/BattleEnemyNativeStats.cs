using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Assets;

public sealed record BattleEnemyNativeProjection(int Level, int MaximumHp, int Strength, int Vitality, int Magic, int Spirit, int Speed, int Evasion, ImmutableArray<short> ElementDefenses);

/// <summary>Original48BBD0 HP/elements and48C3F0/48C1C0 six enemy statistic rows.</summary>
public sealed record BattleEnemyNativeStats(ImmutableArray<byte> HpCoefficients, ImmutableArray<byte> StatCoefficients, ImmutableArray<short> ElementDefenses)
{
    public static BattleEnemyNativeStats ReadMonster(byte[] originalDat)
    {
        if (originalDat.Length < 32)
        {
            throw new InvalidDataException("Enemy native statistics require the original DAT directory.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(originalDat);
        if (count is not (2 or 11))
        {
            throw new NotSupportedException("Enemy statistics require the original two/eleven-section monster directory.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(originalDat.AsSpan(count == 11 ? 28 : 4, 4));
        if (information < 0 || information > originalDat.Length - 360)
        {
            throw new InvalidDataException("Enemy statistics exceed the original information section.");
        }
        ImmutableArray<short>.Builder elements = ImmutableArray.CreateBuilder<short>(8);
        for (int index = 0; index < 8; index++)
        {
            elements.Add((short)(10 * originalDat[information + 352 + index]));
        }
        return new(originalDat.AsSpan(information + 24, 4).ToArray().ToImmutableArray(), originalDat.AsSpan(information + 28, 24).ToArray().ToImmutableArray(), elements.MoveToImmutable());
    }

    public BattleEnemyNativeProjection Project(int level)
    {
        if (level is < 0 or > 100 || HpCoefficients.IsDefault || HpCoefficients.Length != 4 || StatCoefficients.IsDefault || StatCoefficients.Length != 24 || ElementDefenses.IsDefault || ElementDefenses.Length != 8)
        {
            throw new InvalidDataException("Native enemy projection requires its complete original coefficients and level0..100.");
        }
        int maximumHp = level * (HpCoefficients[0] + 100 * HpCoefficients[2]) + 10 * (HpCoefficients[1] + 100 * HpCoefficients[3]) + level * level * HpCoefficients[0] / 20;
        int[] stats = new int[6];
        for (int stat = 0; stat < stats.Length; stat++)
        {
            ReadOnlySpan<byte> row = StatCoefficients.AsSpan(stat * 4, 4);
            if (row[1] == 0 || row[3] == 0)
            {
                throw new NotSupportedException("Original enemy stat divisor is zero; no inferred divisor is supported.");
            }
            int value;
            if (stat is 0 or 2)
            {
                value = (row[2] + level * row[0] / 10 + level / row[1] - ((level * level / row[3]) >> 1)) / 4;
            }
            else
            {
                value = row[2] + level * row[0] + level / row[1] - level / row[3];
            }
            //495930 caps only the upper bound;48C1C0 writes its low BYTE.
            stats[stat] = unchecked((byte)Math.Min(255, value));
        }
        return new(level, maximumHp, stats[0], stats[1], stats[2], stats[3], stats[4], stats[5], ElementDefenses);
    }

    public static int LevelRandomByteCount(byte sceneLevel)
    {
        return sceneLevel <= 100 || sceneLevel == 254 ? 0 : sceneLevel is 251 or 253 ? 2 : 1;
    }

    public static int ResolveLevel(byte sceneLevel, ReadOnlySpan<byte> presentPartyLevels, byte originalBattleRoll, byte? secondBattleRoll = null)
    {
        if (sceneLevel <= 100)
        {
            return sceneLevel;
        }
        if (LevelRandomByteCount(sceneLevel) == 2 && secondBattleRoll is null)
        {
            throw new InvalidDataException("Native enemy level code requires its second original battle random byte.");
        }
        if (presentPartyLevels.Length is < 1 or > 3)
        {
            throw new InvalidDataException("Native enemy level requires the actual one-to-three present battle party levels.");
        }
        int sum = 0;
        foreach (byte level in presentPartyLevels)
        {
            if (level is < 1 or > 100)
            {
                throw new InvalidDataException("Native present battle party levels must be1..100.");
            }
            sum += level;
        }
        int average = sum / presentPartyLevels.Length;
        if (sceneLevel == 254)
        {
            return average;
        }
        // Original48BBD0 dispatches48BFA0/48C020/48C0A0/48C140; preserve their byte consumption.
        int varied = Math.Clamp(average + ((originalBattleRoll & 1) != 0 ? average / 5 : average / -5), 1, 100);
        if (sceneLevel <= 200)
        {
            return Math.Min(sceneLevel - 100, varied);
        }
        if (sceneLevel <= 250)
        {
            return Math.Min(100, varied + sceneLevel - 200);
        }
        return sceneLevel switch
        {
            251 => Math.Clamp(average + ((originalBattleRoll & 1) != 0 ? secondBattleRoll!.Value & 3 : -(secondBattleRoll!.Value & 3)), 1, 65),
            252 => originalBattleRoll % 100 + 1,
            253 => Math.Max(1, secondBattleRoll!.Value % varied),
            255 => varied,
            _ => throw new InvalidDataException("Invalid native level dispatch.")
        };
    }
}
