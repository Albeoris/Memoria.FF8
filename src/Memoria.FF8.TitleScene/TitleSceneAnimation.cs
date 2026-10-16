namespace Memoria.FF8.TitleScene;

/// <summary>Shared presentation clock for the light and the continuously advected fabric.</summary>
public static class TitleSceneAnimation
{
    public const double LightPeriodSeconds = 8;
    public const double MotionSpeed = 0.5;
    public const double VortexRadiansPerSecond = 0.025 * MotionSpeed;
    public const double VortexLogTravelPerSecond = 0.0225 * MotionSpeed;

    public static double MotionSeconds(double seconds)
    {
        return seconds * MotionSpeed;
    }

    public static float LightIntensity(double seconds)
    {
        return 1f + 0.22f * (float)Math.Sin(seconds % LightPeriodSeconds * Math.Tau / LightPeriodSeconds);
    }

    public static float VortexRotation(double seconds)
    {
        return (float)(seconds * VortexRadiansPerSecond % Math.Tau);
    }

    public static float VortexTravel(double seconds)
    {
        return (float)(seconds * VortexLogTravelPerSecond);
    }
}
