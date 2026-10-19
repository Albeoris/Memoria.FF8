using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSleep145Resources(ImmutableArray<byte> Bubble, NativeBattleModuleResource Particle, NativeBattleModuleResource Zzz, NativeBattleEffectHeapAllocation Camera, uint Sound);

public sealed record NativeBattleSleep145State([property: JsonRequired] ImmutableArray<byte> Zzz, [property: JsonRequired] ImmutableArray<byte> BubbleTrails, [property: JsonRequired] ImmutableArray<byte> MainTrails)
{
    public static NativeBattleSleep145State FromColdProcess() => new(new byte[40 * 12].ToImmutableArray(), new byte[3 * 6 * 3 * 12].ToImmutableArray(), new byte[3 * 3 * 12].ToImmutableArray());
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSleep145State? Sleep145 { get; init; }
}

public static class NativeBattleSleep145
{
    public static NativeBattleVector FaceNormal45E670(IReadOnlyList<NativeBattleVector> corners)
    {
        short ax = unchecked((short)(corners[2].X - corners[1].X));
        short ay = unchecked((short)(corners[2].Y - corners[1].Y));
        short az = unchecked((short)(corners[2].Z - corners[1].Z));
        short bx = unchecked((short)(corners[1].X - corners[0].X));
        short by = unchecked((short)(corners[1].Y - corners[0].Y));
        short bz = unchecked((short)(corners[1].Z - corners[0].Z));
        return new((short)Math.Clamp(unchecked(ay * bz - az * by), short.MinValue, short.MaxValue), (short)Math.Clamp(unchecked(az * bx - ax * bz), short.MinValue, short.MaxValue), (short)Math.Clamp(unchecked(ax * by - ay * bx), short.MinValue, short.MaxValue));
    }

    public static bool WriteTintFactors619B70(IReadOnlyList<NativeBattleVector> positions, short[] factors, NativeBattleVector center)
    {
        NativeBattleVector normal = FaceNormal45E670(positions);
        bool near = false;
        for (int corner = 0; corner < positions.Count; corner++)
        {
            NativeBattleVector offset = new(unchecked((short)(positions[corner].X - center.X)), unchecked((short)(positions[corner].Y - center.Y)), unchecked((short)(positions[corner].Z - center.Z)));
            int square = unchecked(offset.X * offset.X + offset.Y * offset.Y + offset.Z * offset.Z);
            int dot = corner < 3 ? NativePrecision53.RoundInteger(((double)offset.Z * normal.Z + (double)offset.Y * normal.Y + (double)offset.X * normal.X) / 4096d) : unchecked(offset.Y * normal.Y + offset.Z * normal.Z + offset.X * normal.X);
            int distance = unchecked(square + (dot >> 8));
            if (distance < 490000)
            {
                factors[corner] = unchecked((short)(4096 - unchecked(distance << 12) / 490000));
                near = true;
            }
        }
        return near;
    }

    public static int GroundFade(short height)
    {
        int clamped = Math.Max(-2000, (int)height);
        int shifted = unchecked(clamped << 12);
        int quotient = (int)(((long)shifted * unchecked((int)0xEF9DB22D)) >> 32) >> 7;
        quotient += (int)((uint)quotient >> 31);
        return unchecked(quotient << 12) >> 12;
    }
}
