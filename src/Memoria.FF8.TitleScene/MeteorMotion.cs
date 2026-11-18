using System.Numerics;

namespace Memoria.FF8.TitleScene;

/// <summary>Analytic presentation motion in screen-height units and the meteor's flight-space basis.</summary>
public static class MeteorMotion
{
    public const double Radius = 0.24;
    public const double TailMaximumLagSeconds = 0.25;
    public const int TailSampleCount = 9;
    public const int ImpulseSlotCount = 4096;
    public const double ImpulseSlotSeconds = 3.5;
    public const double ImpulsePeriodSeconds = ImpulseSlotCount * ImpulseSlotSeconds;
    private const double DegreesToRadians = Math.PI / 180;

    public static MeteorMotionSample Sample(double seconds)
    {
        MeteorMotionImpulse impulse = CurrentImpulse(seconds, out double age);
        double response = AngularResponse(age, impulse.AngularDurationSeconds);
        Vector2 offset = ScreenOffset(impulse, age, response);
        float spin = (float)(seconds % 450 * 0.8 * DegreesToRadians);
        float tiltY = (float)(impulse.PeakTiltRadians * response * impulse.TiltDirection.X);
        float tiltZ = (float)(impulse.PeakTiltRadians * response * impulse.TiltDirection.Y);
        Matrix4x4 rotation = Matrix4x4.CreateRotationX(spin) * Matrix4x4.CreateRotationY(tiltY) * Matrix4x4.CreateRotationZ(tiltZ);
        return new(offset, new(rotation.M11, rotation.M12, rotation.M13), new(rotation.M21, rotation.M22, rotation.M23), new(rotation.M31, rotation.M32, rotation.M33));
    }

    public static Vector2 SampleScreenOffset(double seconds)
    {
        MeteorMotionImpulse impulse = CurrentImpulse(seconds, out double age);
        return ScreenOffset(impulse, age, AngularResponse(age, impulse.AngularDurationSeconds));
    }

    /// <summary>Describes one isolated impulse. Slot indices extend in both time directions; event parameters repeat after 4096 slots.</summary>
    public static MeteorMotionImpulse DescribeImpulse(int slot)
    {
        uint seed = (uint)((slot % ImpulseSlotCount + ImpulseSlotCount) % ImpulseSlotCount);
        double direction = Hash(seed, 3) * Math.Tau;
        return new(slot * ImpulseSlotSeconds + 0.15 + 1.3 * Hash(seed, 0),
            0.4 + 0.4 * Hash(seed, 1), 1.6 + 0.4 * Hash(seed, 2),
            (0.15 + 0.15 * Hash(seed, 4)) * DegreesToRadians,
            new((float)Math.Cos(direction), (float)Math.Sin(direction)),
            (0.002 + 0.001 * Hash(seed, 5)) * Radius,
            (0.004 + 0.001 * Hash(seed, 6)) * Radius,
            (2 * Hash(seed, 7) - 1) * 0.001 * Radius);
    }

    private static MeteorMotionImpulse CurrentImpulse(double seconds, out double age)
    {
        if (!double.IsFinite(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        double time = seconds % ImpulsePeriodSeconds;
        if (time < 0)
        {
            time += ImpulsePeriodSeconds;
        }
        int slot = Math.Min((int)(time / ImpulseSlotSeconds), ImpulseSlotCount - 1);
        MeteorMotionImpulse impulse = DescribeImpulse(slot);
        if (time < impulse.StartSeconds)
        {
            impulse = DescribeImpulse(slot - 1);
        }
        age = time - impulse.StartSeconds;
        return impulse;
    }

    private static double AngularResponse(double age, double duration)
    {
        if (age <= 0 || age >= duration)
        {
            return 0;
        }
        double decayTime = 8 * age / duration;
        double fade = 1 - Smooth((age / duration - 0.75) / 0.25);
        return decayTime * Math.Exp(1 - decayTime) * fade;
    }

    private static Vector2 ScreenOffset(MeteorMotionImpulse impulse, double age, double angularResponse)
    {
        if (age <= 0 || age >= impulse.ExcursionDurationSeconds)
        {
            return Vector2.Zero;
        }
        double phase = age / impulse.ExcursionDurationSeconds;
        double u;
        if (phase < 0.25)
        {
            u = -impulse.ForwardDistance * Smooth(phase / 0.25);
        }
        else if (phase < 0.6)
        {
            u = -impulse.ForwardDistance + (impulse.ForwardDistance + impulse.BackwardDistance) * Smooth((phase - 0.25) / 0.35);
        }
        else
        {
            u = impulse.BackwardDistance * (1 - Smooth((phase - 0.6) / 0.4));
        }
        double v = impulse.TransverseDistance * angularResponse;
        const double determinant = 0.94 * 0.94 + 0.341 * 0.341;
        return new((float)((0.94 * u + 0.341 * v) / determinant), (float)((-0.341 * u + 0.94 * v) / determinant));
    }

    private static double Smooth(double value)
    {
        double t = Math.Clamp(value, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double Hash(uint seed, uint channel)
    {
        uint value = unchecked(seed * 747796405u + channel * 2891336453u + 277803737u);
        value = unchecked(((value >> (int)((value >> 28) + 4)) ^ value) * 277803737u);
        value = (value >> 22) ^ value;
        return value / (double)uint.MaxValue;
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
}

/// <summary>Object axes are the rows of an orthonormal rotation matrix expressed in flight space.</summary>
public readonly record struct MeteorMotionSample(Vector2 ScreenOffset, Vector3 AxisX, Vector3 AxisY, Vector3 AxisZ);

/// <summary>Immutable event parameters for inspecting the sparse angular impulse and its longer catch-up excursion.</summary>
public readonly record struct MeteorMotionImpulse(double StartSeconds, double AngularDurationSeconds, double ExcursionDurationSeconds, double PeakTiltRadians, Vector2 TiltDirection, double ForwardDistance, double BackwardDistance, double TransverseDistance);
