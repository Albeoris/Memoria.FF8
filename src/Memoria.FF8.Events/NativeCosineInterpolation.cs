namespace Memoria.FF8.Events;

public static class NativeCosineInterpolation
{
    public static int Interpolate(int start, int target, int duration, int elapsed)
    {
        int phase = duration == 0 ? 0 : (byte)((elapsed << 12) / duration / 32 + 128);
        // Truncation matches all 256 signed entries of the native 0xB80D9C table.
        int cosine = (int)(4096 * Math.Cos(phase * Math.Tau / 256));
        return start + (target - start) * (cosine + 4096) / 8192;
    }
}
