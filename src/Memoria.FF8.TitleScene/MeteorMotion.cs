using System.Numerics;

namespace Memoria.FF8.TitleScene;

/// <summary>Analytic presentation motion in screen-height units and the meteor's flight-space basis.</summary>
public static class MeteorMotion
{
    public const double Radius = 0.24;
    public const double TailMaximumLagSeconds = 0.25;
    public const int TailSampleCount = 9;
    private const double DegreesToRadians = Math.PI / 180;

    public static MeteorMotionSample Sample(double seconds)
    {
        Vector2 offset = SampleScreenOffset(seconds);
        float spin = (float)(seconds % 180 * 2 * DegreesToRadians);
        float wobbleY = (float)(0.75 * DegreesToRadians * (0.7 * Sine(seconds, 0.61, 0.8) + 0.3 * Sine(seconds, 0.89, 2.1)));
        float wobbleZ = (float)(0.75 * DegreesToRadians * (0.65 * Sine(seconds, 0.73, 2.4) + 0.35 * Sine(seconds, 0.97, 0.3)));
        Matrix4x4 rotation = Matrix4x4.CreateRotationX(spin) * Matrix4x4.CreateRotationY(wobbleY) * Matrix4x4.CreateRotationZ(wobbleZ);
        return new(offset, new(rotation.M11, rotation.M12, rotation.M13), new(rotation.M21, rotation.M22, rotation.M23), new(rotation.M31, rotation.M32, rotation.M33));
    }

    public static Vector2 SampleScreenOffset(double seconds)
    {
        if (!double.IsFinite(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        double slowX = 0.018 * Radius * (0.6 * Sine(seconds, 0.43, 0.2) + 0.4 * Sine(seconds, 0.71, 1.9));
        double slowY = 0.018 * Radius * (0.55 * Sine(seconds, 0.53, 2.6) + 0.45 * Sine(seconds, 0.79, 0.7));
        double fastX = 0.002 * Radius * (0.65 * Sine(seconds, 4.3, 1.1) + 0.35 * Sine(seconds, 6.7, 2.9));
        double fastY = 0.002 * Radius * (0.6 * Sine(seconds, 5.1, 2.2) + 0.4 * Sine(seconds, 6.1, 0.4));
        return new((float)(slowX + fastX), (float)(slowY + fastY));
    }

    /// <summary>Writes the current head position followed by eight uniformly retarded positions, ending at a quarter-second lag.</summary>
    public static void FillTailScreenOffsets(double seconds, Span<Vector2> destination)
    {
        if (!double.IsFinite(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        if (destination.Length < TailSampleCount)
        {
            throw new ArgumentException("The meteor tail requires nine position samples.", nameof(destination));
        }
        for (int index = 0; index < TailSampleCount; index++)
        {
            destination[index] = SampleScreenOffset(seconds - TailMaximumLagSeconds * index / (TailSampleCount - 1));
        }
    }

    private static double Sine(double seconds, double frequency, double phase)
    {
        return Math.Sin(seconds % (1 / frequency) * frequency * Math.Tau + phase);
    }
}

/// <summary>Object axes are the rows of an orthonormal rotation matrix expressed in flight space.</summary>
public readonly record struct MeteorMotionSample(Vector2 ScreenOffset, Vector3 AxisX, Vector3 AxisY, Vector3 AxisZ);
