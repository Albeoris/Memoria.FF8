namespace Memoria.FF8.Runtime;

/// <summary>Original B693D8 FADD/FSTP integer conversion under the proved PC x87 control word027F.</summary>
public static class NativePrecision53
{
    public static int RoundInteger(double value)
    {
        // 2^52 +2^31, authenticated original DOUBLE bits4330000080000000.
        // Decode the stored low DWORD, preserving native unsigned wrap.
        const double magic = 4503601774854144.0;
        double stored = value + magic;
        return unchecked((int)BitConverter.DoubleToInt64Bits(stored) + int.MinValue);
    }
}
