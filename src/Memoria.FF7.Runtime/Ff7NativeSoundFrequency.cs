namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeSoundDescription(int SampleRate, bool Streamed, bool Loop = false);

public static class Ff7NativeSoundFrequency
{
    public static int Frequency(int sourceRate, int pitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceRate);
        ArgumentOutOfRangeException.ThrowIfLessThan(pitch, -128);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pitch, 127);
        double change = pitch < 0 ? pitch / 128d * (sourceRate / 2d) : pitch / 127d * sourceRate;
        return checked(sourceRate + (int)change);
    }

    public static bool Accepted(int frequency)
    {
        return frequency is >= 100 and <= 100000;
    }

    public static uint LoopDelayMilliseconds(uint loopStartByteOffset, int sourceRate)
    {
        uint divisor = unchecked((uint)sourceRate << 4) / 1000;
        if (divisor == 0)
        {
            throw new NotSupportedException("FFVII loop timer has an undefined native source-rate divisor.");
        }
        return unchecked(loopStartByteOffset << 3) / divisor;
    }
}
