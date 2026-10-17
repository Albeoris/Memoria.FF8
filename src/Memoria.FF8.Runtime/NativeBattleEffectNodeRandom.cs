namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleEffectNodeRandomValue(NativeCrtRandomState Random, int Value);
internal sealed record NativeBattleEffectNodeRandomVector(NativeCrtRandomState Random, NativeBattleVector Value);

/// <summary>881F20/882030/881FE0's original CRT draws, signed ranges and asymmetric negative angle normalization.</summary>
internal static class NativeBattleEffectNodeRandom
{
    public static NativeBattleEffectNodeRandomValue Range881F20(NativeCrtRandomState random, int first, int second)
    {
        if (first == second)
        {
            return new(random, first);
        }
        int width = unchecked(second - first);
        random = random.Draw(out ushort sample);
        int remainder = sample % width;
        return new(random, width < 0 ? unchecked(first - remainder) : unchecked(first + remainder));
    }

    public static NativeBattleEffectNodeRandomValue Angle882030(NativeCrtRandomState random, short center, short span)
    {
        int half = span / 2;
        NativeBattleEffectNodeRandomValue draw = Range881F20(random, -half, half);
        int angle = unchecked(draw.Value + center);
        if (angle < 0)
        {
            //The native negative branch maps exact negative multiples of4096 to4096, unlike a modulo expression.
            angle = unchecked(angle + (int)(((uint)(4096 - angle) >> 12) << 12));
        }
        else if (angle >= 4096)
        {
            angle = unchecked(angle - (int)(((uint)angle >> 12) << 12));
        }
        return new(draw.Random, angle);
    }

    public static NativeBattleEffectNodeRandomValue ProductRange881F50(NativeCrtRandomState random, int first, int second)
    {
        if (first == second)
        {
            return new(random, first);
        }
        int width = unchecked(second - first);
        random = random.Draw(out ushort a);
        random = random.Draw(out ushort b);
        int remainder = unchecked(a * b) % width;
        return new(random, width < 0 ? unchecked(first - remainder) : unchecked(first + remainder));
    }

    public static NativeBattleEffectNodeRandomVector Euler881FE0(NativeCrtRandomState random, NativeBattleVector center, NativeBattleVector span)
    {
        NativeBattleEffectNodeRandomValue x = Angle882030(random, center.X, span.X);
        NativeBattleEffectNodeRandomValue y = Angle882030(x.Random, center.Y, span.Y);
        NativeBattleEffectNodeRandomValue z = Angle882030(y.Random, center.Z, span.Z);
        return new(z.Random, new(unchecked((short)x.Value), unchecked((short)y.Value), unchecked((short)z.Value)));
    }
}
