namespace Memoria.FF7.Formats;

public readonly record struct KernelGrowthCurve(byte Gradient, sbyte Intercept);

/// <summary>The original battle-kernel character selectors, level-band curves and random growth distributions.</summary>
public sealed class KernelGrowthTables
{
    private readonly byte[] data;

    private KernelGrowthTables(byte[] data)
    {
        this.data = data;
    }

    public static KernelGrowthTables Read(ReadOnlySpan<byte> section)
    {
        if (section.Length != 3988)
        {
            throw new InvalidDataException("PC character growth requires the original battle-kernel section layout.");
        }
        for (int character = 0; character < 9; character++)
        {
            for (int statistic = 0; statistic < 9; statistic++)
            {
                if (section[character * 56 + statistic] >= 192)
                {
                    throw new InvalidDataException("A character growth selector lies outside the original curve table.");
                }
            }
        }
        return new(section.ToArray());
    }

    public KernelGrowthCurve Curve(int character, int statistic, int band)
    {
        if ((uint)character >= 9 || (uint)statistic >= 9 || (uint)band >= 8)
        {
            throw new ArgumentOutOfRangeException(nameof(character), "Growth requires a native character, statistic and level band.");
        }
        int offset = 0x21C + data[character * 56 + statistic] * 16 + band * 2;
        return new(data[offset], unchecked((sbyte)data[offset + 1]));
    }

    public int RandomIncrement(int statistic, int index)
    {
        if ((uint)statistic >= 8 || (uint)index >= 12)
        {
            throw new ArgumentOutOfRangeException(nameof(statistic));
        }
        int table = statistic < 6 ? 0x1F8 : statistic == 6 ? 0x204 : 0x210;
        return data[table + index];
    }
}
