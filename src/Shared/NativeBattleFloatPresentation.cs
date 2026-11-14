namespace Memoria.Battles;

internal static class NativeBattleFloatPresentation
{
    //56D130's authenticated single-precision radians constant and biased-double conversion.
    internal static int Sine(int angle)
    {
        double biased = Math.Sin(angle * (double)0.0015339808f) * (double)4096f + 4503601774854144d;
        return unchecked((int)((uint)BitConverter.DoubleToInt64Bits(biased) + 0x80000000u));
    }

    internal static short Vertical50A5F0(byte phase)
    {
        int fraction = (Sine(phase << 8) + 4096) / 2;
        return unchecked((short)((-100 * fraction >> 12) - 200));
    }
}
