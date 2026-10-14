namespace Memoria.FF8.Runtime;

public static class NativeWorldSalaryMovement
{
    public static uint AcceptedVectorUnits(int x, int y, int z)
    {
        if (x is < short.MinValue or > short.MaxValue || y is < short.MinValue or > short.MaxValue || z is < short.MinValue or > short.MaxValue)
        {
            throw new NotSupportedException("Native world salary requires the actual accepted signed-WORD velocity vector.");
        }
        int squared = unchecked(x * x + y * y + z * z);
        if (squared < 0)
        {
            throw new NotSupportedException("Native world salary squared velocity overflows its supported signed-DWORD square-root domain.");
        }
        // Native53FAC0:56BEC0 sqrt,55AD90 truncation, then integer division8.
        return (uint)((int)Math.Sqrt(squared) / 8);
    }
}
