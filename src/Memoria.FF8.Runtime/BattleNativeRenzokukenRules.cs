using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeRenzokukenTimeline(ImmutableArray<short> InputThresholds, short TotalPoseFrames);

public static class NativeBattleRenzokukenRules
{
    /// <summary>4BA4C0 receives the AB scan's signed BYTE segment lengths, including its final tail.</summary>
    public static BattleNativeRenzokukenTimeline Timeline(IReadOnlyList<byte> segments)
    {
        if (segments.Count is < 1 or > 17)
        {
            throw new InvalidDataException("Native Renzokuken timing requires its AB contact segments and one final tail.");
        }
        List<short> thresholds = [];
        int cumulative = 0;
        for (int index = 0; index < segments.Count; index++)
        {
            int length = unchecked((sbyte)segments[index]);
            if (index == 0)
            {
                length += 2;
            }
            if (index == segments.Count - 1 && length > 10)
            {
                length = 10;
            }
            cumulative += length;
            if (index < segments.Count - 1)
            {
                thresholds.Add(unchecked((short)(4 * cumulative - 27)));
            }
        }
        return new(thresholds.ToImmutableArray(), unchecked((short)cumulative));
    }

    public static bool HasFinisher(byte crisis, byte roll)
    {
        return crisis != 0 && 15300 * crisis / 255 >= roll;
    }

    public static byte Finisher(byte weaponFlags, byte roll)
    {
        for (int finisher = roll % 4; finisher >= 0; finisher--)
        {
            if ((weaponFlags & (1 << finisher)) != 0)
            {
                return (byte)finisher;
            }
        }
        throw new InvalidDataException("Native Renzokuken weapon has no enabled finisher at or below its random selection.");
    }
}
