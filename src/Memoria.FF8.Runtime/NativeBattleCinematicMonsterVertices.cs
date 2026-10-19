using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Word-width wobble coordinates and signed distance-color arithmetic used by cinematic monster models.</summary>
public static class NativeBattleCinematicMonsterVertices
{
    public static ImmutableArray<short> WobbleTable(short amplitude, short phase, short step)
    {
        short[] table = new short[768];
        int angle = phase;
        for (int index = 0; index < 256; index++)
        {
            table[index] = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * amplitude) >> 12));
            table[512 + index] = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * amplitude) >> 12));
            angle = unchecked(angle + step);
        }
        return table.ToImmutableArray();
    }

    public static NativeBattleVector Wobble(NativeBattleVector transformed, ImmutableArray<short> table)
    {
        if (table.IsDefault || table.Length != 768)
        {
            throw new InvalidDataException("Native cinematic wobble requires both physical sine and cosine WORD tables.");
        }
        int index = (unchecked((ushort)transformed.Y) >> 2) & 255;
        return new(unchecked((short)(transformed.X + table[index])), transformed.Y, unchecked((short)(transformed.Z + table[512 + index])));
    }

    public static ushort Distance(NativeBattleFullMacVertex transformed, NativeBattleVector origin)
    {
        int dx = unchecked(transformed.X - origin.X);
        int dy = unchecked(transformed.Y - origin.Y);
        int dz = unchecked(transformed.Z - origin.Z);
        int planar = SquareRoot(unchecked(dx * dx + dy * dy));
        return unchecked((ushort)SquareRoot(unchecked(dz * dz + planar * planar)));
    }

    public static ImmutableArray<uint> Palette(ReadOnlySpan<ushort> source)
    {
        if (source.Length != 256)
        {
            throw new InvalidDataException("Native cinematic actor glow requires all 256 source CLUT colors.");
        }
        uint[] ramp = new uint[256];
        for (int index = 0; index < ramp.Length; index++)
        {
            short packed = unchecked((short)source[index]);
            ramp[index] = unchecked((byte)(packed << 3)) | (uint)((packed >> 2) & 248) << 8 | (uint)((packed >> 7) & 248) << 16;
        }
        return ramp.ToImmutableArray();
    }

    public static uint Glow(ushort storedDistance, short radius, short intensity, short phase, uint actorColor, NativeBattleVector reference, ImmutableArray<uint> ramp)
    {
        if (ramp.IsDefault || ramp.Length != 256)
        {
            throw new InvalidDataException("Native cinematic actor glow requires its retained 256-entry RGB ramp.");
        }
        int distance = unchecked((short)storedDistance);
        if (distance >= radius)
        {
            return 0;
        }
        if (radius == 0)
        {
            throw new InvalidDataException("Native cinematic actor glow reached its original zero divisor.");
        }
        int index = unchecked((distance << 8) / radius + phase) & 255;
        int ratio = unchecked(distance << 12) / radius;
        int factor = unchecked((4096 - ratio) * intensity);
        factor = unchecked(factor < 0 ? factor + 255 : factor) >> 8;
        uint color = ramp[index];
        return Channel(0, reference.X) | Channel(1, reference.Y) << 8 | Channel(2, reference.Z) << 16;

        uint Channel(int lane, short offset)
        {
            int baseline = Math.Clamp(unchecked(offset + (byte)(actorColor >> (lane * 8))), 0, 255);
            int scaled = unchecked((byte)(color >> (lane * 8)) * factor);
            scaled = unchecked(scaled < 0 ? scaled + 4095 : scaled) >> 12;
            return unchecked((byte)Math.Min(255, unchecked(scaled + baseline)));
        }
    }

    private static int SquareRoot(int value) => unchecked((int)(long)Math.Sqrt(value));
}
