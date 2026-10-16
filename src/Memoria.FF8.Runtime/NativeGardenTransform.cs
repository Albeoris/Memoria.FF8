namespace Memoria.FF8.Runtime;

/// <summary>Original56D100/56D130/56D160 constants and rounded short-vector transforms used by53D8A0/549320.</summary>
public static class NativeGardenTransform
{
    private static readonly double RadiansPerUnit = BitConverter.Int32BitsToSingle(0x3AC90FDB);
    private static readonly double UnitsPerRadian = BitConverter.Int32BitsToSingle(0x4422F983);

    public static (int X, int Z) Offset(short distance, short yaw)
    {
        return Rotate(0, distance, yaw);
    }

    public static (int X, int Z) Rotate(short x, short z, short yaw)
    {
        int sine = Trig(yaw, false);
        int cosine = Trig(yaw, true);
        return (checked((int)Math.Round(((long)cosine * x + (long)sine * z) / 4096d, MidpointRounding.ToEven)), checked((int)Math.Round((-(long)sine * x + (long)cosine * z) / 4096d, MidpointRounding.ToEven)));
    }

    public static (short X, short Z) CircleBoundary(short dx, short dz, short radius)
    {
        // 549320 zero-extends its X WORD into the DWORD scratch pair.
        // Thus the pitch numerator is0, irrespective of the height gate.
        short pitch = 0;
        short yaw = unchecked((short)StableRound(Math.Atan2(dx, dz) * UnitsPerRadian));
        int sine = Trig(yaw, false);
        int cosine = Trig(yaw, true);
        int pitchCosine = Trig(pitch, true);
        // 56CD50 constructs Rx*Ry*Rz, with Rz0 here. Preserve the
        // intermediate signed-WORD matrix rounding before transforming Z.
        short zz = unchecked((short)RoundedProduct(pitchCosine, cosine));
        return (unchecked((short)RoundedProduct(sine, radius)), unchecked((short)RoundedProduct(zz, radius)));
    }

    private static int Trig(short angle, bool cosine) => StableRound((cosine ? Math.Cos(angle * RadiansPerUnit) : Math.Sin(angle * RadiansPerUnit)) * 4096);

    private static int RoundedProduct(int first, int second) => checked((int)Math.Round((long)first * second / 4096d, MidpointRounding.ToEven));

    private static int StableRound(double value)
    {
        // Same bounded x87/libm rounding envelope as the current native
        // world projection certificate; refuse a nonunique integer result.
        int low = checked((int)Math.Round(Math.BitDecrement(value) - 1d / 4096, MidpointRounding.ToEven));
        int high = checked((int)Math.Round(Math.BitIncrement(value) + 1d / 4096, MidpointRounding.ToEven));
        if (low != high)
        {
            throw new NotSupportedException("Native Garden rotation rounding has no unique represented integer result.");
        }
        return low;
    }
}
