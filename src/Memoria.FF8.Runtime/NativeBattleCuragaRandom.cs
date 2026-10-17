namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaRandomValue(NativeCrtRandomState Random, int Value);
public sealed record NativeBattleCuragaRandomVector(NativeCrtRandomState Random, NativeBattleVector Value);

/// <summary>881F20/882030/881FE0's original CRT draws, signed ranges and asymmetric negative angle normalization.</summary>
public static class NativeBattleCuragaRandom
{
    public static NativeBattleCuragaRandomValue Range881F20(NativeCrtRandomState random, int first, int second)
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

    public static NativeBattleCuragaRandomValue Angle882030(NativeCrtRandomState random, short center, short span)
    {
        int half = span / 2;
        NativeBattleCuragaRandomValue draw = Range881F20(random, -half, half);
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

    public static NativeBattleCuragaRandomValue ProductRange881F50(NativeCrtRandomState random, int first, int second)
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

    public static NativeBattleCuragaRandomVector Euler881FE0(NativeCrtRandomState random, NativeBattleVector center, NativeBattleVector span)
    {
        NativeBattleCuragaRandomValue x = Angle882030(random, center.X, span.X);
        NativeBattleCuragaRandomValue y = Angle882030(x.Random, center.Y, span.Y);
        NativeBattleCuragaRandomValue z = Angle882030(y.Random, center.Z, span.Z);
        return new(z.Random, new(unchecked((short)x.Value), unchecked((short)y.Value), unchecked((short)z.Value)));
    }
}
