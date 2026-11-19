namespace Memoria.FF8;

internal static class CommittedPresentationTime
{
    public static bool ShouldInterpolate(int presentationFps, double callbacksPerSecond)
    {
        return presentationFps > callbacksPerSecond;
    }

    public static float Fraction(double remainderSeconds, double callbacksPerSecond)
    {
        return (float)Math.Clamp(remainderSeconds * callbacksPerSecond, 0, 1);
    }
}
