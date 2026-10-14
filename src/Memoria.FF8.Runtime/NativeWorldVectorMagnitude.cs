namespace Memoria.FF8.Runtime;

public static class NativeWorldVectorMagnitude
{
    public static int Truncate(int x, int y, int z)
    {
        if (x is < short.MinValue or > short.MaxValue || y is < short.MinValue or > short.MaxValue || z is < short.MinValue or > short.MaxValue)
        {
            throw new NotSupportedException("Native world movement magnitude requires the original signed-WORD vector.");
        }
        int squared = unchecked(x * x + y * y + z * z);
        if (squared < 0)
        {
            throw new NotSupportedException("Native world squared velocity exceeds the proved signed-DWORD square-root domain.");
        }
        // Native53FAC0 uses56BEC0 sqrt followed by55AD90 truncation.
        return (int)Math.Sqrt(squared);
    }
}
