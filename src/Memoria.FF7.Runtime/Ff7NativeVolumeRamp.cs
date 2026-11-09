namespace Memoria.FF7.Runtime;

public static class Ff7NativeVolumeRamp
{
    public static (int Volume, int AccumulatedMilliseconds) Advance(int current, int target, int ratePerSecond, int accumulatedMilliseconds, int elapsedMilliseconds)
    {
        int milliseconds = unchecked(accumulatedMilliseconds + elapsedMilliseconds);
        int step = unchecked(milliseconds * ratePerSecond) / 1000;
        int volume = unchecked(current + step);
        if (step < 0 && volume < target || step > 0 && volume > target)
        {
            volume = target;
        }
        return (volume, step == 0 ? milliseconds : 0);
    }
}
