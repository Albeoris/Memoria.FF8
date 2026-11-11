using System.Numerics;

namespace Memoria.FF8.Rendering;

/// <summary>Interpolates display RGB without drifting outside the supplied channel endpoints.</summary>
public static class FieldColorInterpolation
{
    public static Vector3 Interpolate(Vector3 before, Vector3 after, float fraction)
    {
        if (!Valid(before))
        {
            throw new ArgumentOutOfRangeException(nameof(before), "Field colors require finite RGB channels between 0 and 255.");
        }
        if (!Valid(after))
        {
            throw new ArgumentOutOfRangeException(nameof(after), "Field colors require finite RGB channels between 0 and 255.");
        }
        if (!float.IsFinite(fraction) || fraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        if (fraction == 0)
        {
            return before;
        }
        if (fraction == 1)
        {
            return after;
        }
        // Weighted floating-point interpolation can overshoot even equal byte endpoints.
        return Vector3.Clamp(Vector3.Lerp(before, after, fraction), Vector3.Min(before, after), Vector3.Max(before, after));
    }

    private static bool Valid(Vector3 color) => float.IsFinite(color.X) && color.X is >= 0 and <= 255 && float.IsFinite(color.Y) && color.Y is >= 0 and <= 255 && float.IsFinite(color.Z) && color.Z is >= 0 and <= 255;
}
