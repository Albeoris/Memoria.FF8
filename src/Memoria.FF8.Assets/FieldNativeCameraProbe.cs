using System.Numerics;

namespace Memoria.FF8.Assets;

public readonly record struct FieldNativeCameraPoint(int X, int Y, int Z);
public readonly record struct FieldNativeCameraCenter(int X, int Y);
public sealed record FieldNativeCameraDiagnostics(FieldNativeCameraPoint Anchor, FieldNativeCameraPoint? Transformed = null, int? ProjectionDepth = null, FieldNativeCameraCenter? ProjectedEighth = null, FieldNativeCameraCenter? UnclampedCenter = null, FieldNativeCameraCenter? ProjectedEighthLow = null, FieldNativeCameraCenter? ProjectedEighthHigh = null, FieldNativeCameraPoint? TransformedLow = null, FieldNativeCameraPoint? TransformedHigh = null, int? ProjectionDepthLow = null, int? ProjectionDepthHigh = null);
public sealed record FieldNativeCameraResult(FieldNativeCameraCenter? Center, FieldNativeCameraDiagnostics? Diagnostics, string? UnsupportedReason)
{
    public bool Supported => Center.HasValue && UnsupportedReason is null;
}

/// <summary>Native selected CA normal-follow projection and INF clamp. Scene eligibility, draw offsets and mutation belong to the caller.</summary>
public static class FieldNativeCameraProbe
{
    public static FieldNativeCameraResult Resolve(FieldCamera camera, FieldInformation information, int fixedX, int fixedY, int fixedZ, int selectedCameraIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(information);
        FieldNativeCameraDiagnostics? diagnostics = null;

        FieldNativeCameraResult Unsupported(string reason)
        {
            return new FieldNativeCameraResult(null, diagnostics, reason);
        }

        if (selectedCameraIndex is < 0 or > 1)
        {
            return Unsupported("Native normal-follow projection requires selected CA slot0 or1.");
        }
        if (information.CameraBounds is null || information.CameraBounds.Count <= selectedCameraIndex || information.ScreenBounds is null || information.ScreenBounds.Count == 0)
        {
            return Unsupported("Native INF selected camera and screen0 bounds are required.");
        }
        if (!TryAxis(camera.AxisX, out FieldNativeCameraPoint axisX) || !TryAxis(camera.AxisY, out FieldNativeCameraPoint axisY) || !TryAxis(camera.AxisZ, out FieldNativeCameraPoint axisZ))
        {
            return Unsupported("Projection requires original signed-WORD camera axes divided by4096.");
        }
        if (!TryTranslation(camera.Translation, out FieldNativeCameraPoint translation))
        {
            return Unsupported("Camera translation must be an exactly recoverable native integer.");
        }
        if (!float.IsFinite(camera.FocalLength) || camera.FocalLength != MathF.Truncate(camera.FocalLength) || camera.FocalLength is < 1 or > ushort.MaxValue)
        {
            return Unsupported("Native projection requires a positive unsigned-WORD focal length.");
        }

        FieldNativeCameraPoint anchor = new(unchecked((short)(fixedX >> 12)), unchecked((short)(fixedY >> 12)), unchecked((short)((fixedZ >> 12) + information.CameraHeight)));
        diagnostics = new FieldNativeCameraDiagnostics(anchor);
        (int xLow, int xHigh) = TransformInterval(axisX, anchor, translation.X);
        (int yLow, int yHigh) = TransformInterval(axisY, anchor, translation.Y);
        (int zLow, int zHigh) = TransformInterval(axisZ, anchor, translation.Z);
        FieldNativeCameraPoint transformedLow = new(xLow, yLow, zLow);
        FieldNativeCameraPoint transformedHigh = new(xHigh, yHigh, zHigh);
        diagnostics = diagnostics with { Transformed = transformedLow == transformedHigh ? transformedLow : null, TransformedLow = transformedLow, TransformedHigh = transformedHigh };
        if (xLow < short.MinValue || xHigh > short.MaxValue || yLow < short.MinValue || yHigh > short.MaxValue || zLow < short.MinValue || zHigh > short.MaxValue)
        {
            return Unsupported("Native transformed signed-WORD saturation is outside this bounded projection contract.");
        }
        int focal = (ushort)camera.FocalLength;
        // 0x45FAA0 checks the raw signed depth as unsigned against65535.
        // Negative depth becomes0 (not a wrapped ushort), then uses the H/2 near clamp.
        // 0x475E60 retains the flags but still consumes the projected camera center.
        int depthLow = Math.Max(Math.Max(zLow, 0), focal >> 1);
        int depthHigh = Math.Max(Math.Max(zHigh, 0), focal >> 1);
        diagnostics = diagnostics with { ProjectionDepth = depthLow == depthHigh ? depthLow : null, ProjectionDepthLow = depthLow, ProjectionDepthHigh = depthHigh };
        if (depthLow == 0)
        {
            return Unsupported("Native projection has no positive denominator after the near clamp.");
        }
        int lowX = int.MaxValue;
        int highX = int.MinValue;
        int lowY = int.MaxValue;
        int highY = int.MinValue;
        // Signed coordinate/depth projection is monotone in each coordinate
        // and in positive depth (with direction depending on coordinate sign).
        // At most eight integer XYZ corners enclose every transform outcome.
        foreach (int x in Endpoints(xLow, xHigh))
        {
            foreach (int y in Endpoints(yLow, yHigh))
            {
                foreach (int z in Endpoints(zLow, zHigh))
                {
                    int depth = Math.Max(Math.Max(z, 0), focal >> 1);
                    (int projectedXLow, int projectedXHigh) = ProjectInterval(x, focal, depth);
                    (int projectedYLow, int projectedYHigh) = ProjectInterval(y, focal, depth);
                    lowX = Math.Min(lowX, projectedXLow);
                    highX = Math.Max(highX, projectedXHigh);
                    lowY = Math.Min(lowY, projectedYLow);
                    highY = Math.Max(highY, projectedYHigh);
                }
            }
        }
        FieldNativeCameraCenter projectedLow = new(lowX, lowY);
        FieldNativeCameraCenter projectedHigh = new(highX, highY);
        diagnostics = diagnostics with { ProjectedEighth = projectedLow == projectedHigh ? projectedLow : null, ProjectedEighthLow = projectedLow, ProjectedEighthHigh = projectedHigh };

        // 0x475E60 uses signed IDIV by8, after signed-WORD projection storage.
        FieldNativeCameraCenter centerLow = new(lowX / 8, lowY / 8);
        FieldNativeCameraCenter centerHigh = new(highX / 8, highY / 8);
        diagnostics = diagnostics with { UnclampedCenter = centerLow == centerHigh ? centerLow : null };
        FieldNativeCameraCenter clampedLow = Clamp(centerLow, information, selectedCameraIndex);
        FieldNativeCameraCenter clampedHigh = Clamp(centerHigh, information, selectedCameraIndex);
        if (clampedLow != clampedHigh)
        {
            return Unsupported("Native eighth-pixel rounding interval reaches different final camera centers.");
        }
        return new FieldNativeCameraResult(clampedLow, diagnostics, null);
    }

    private static FieldNativeCameraCenter Clamp(FieldNativeCameraCenter center, FieldInformation information, int selectedCameraIndex)
    {
        short centerX = (short)center.X;
        short centerY = (short)center.Y;
        FieldBounds screen = information.ScreenBounds[0];
        FieldBounds bounds = information.CameraBounds[selectedCameraIndex];
        int halfWidth = (screen.Right - screen.Left) / 2;
        int halfHeight = (screen.Bottom - screen.Top) / 2;
        // Preserve native cap-then-floor ordering and WORD assignment, even when bounds overlap.
        if (centerX > bounds.Right - halfWidth)
        {
            centerX = unchecked((short)(bounds.Right - halfWidth));
        }
        if (centerX < bounds.Left + halfWidth)
        {
            centerX = unchecked((short)(bounds.Left + halfWidth));
        }
        if (centerY > bounds.Bottom - halfHeight)
        {
            centerY = unchecked((short)(bounds.Bottom - halfHeight));
        }
        if (centerY < bounds.Top + halfHeight)
        {
            centerY = unchecked((short)(bounds.Top + halfHeight));
        }
        // IDIV is monotone. Each WORD clamp is monotone or constant on signed-WORD input:
        // an out-of-WORD comparison threshold makes its assignment apply to all or no inputs.
        return new FieldNativeCameraCenter(centerX, centerY);
    }

    private static (int Low, int High) TransformInterval(FieldNativeCameraPoint axis, FieldNativeCameraPoint point, int translation)
    {
        long numerator = (long)axis.X * point.X + (long)axis.Y * point.Y + (long)axis.Z * point.Z;
        // Original WORD products divided by4096 are exact binary rationals in binary64 and x87.
        double value = numerator / 4096.0;
        (int low, int high) = RoundInterval(Math.BitDecrement(value), Math.BitIncrement(value));
        return (low + translation, high + translation);
    }

    private static IEnumerable<int> Endpoints(int low, int high)
    {
        yield return low;
        if (high != low)
        {
            yield return high;
        }
    }

    private static (int Low, int High) ProjectInterval(int coordinate, int focal, int depth)
    {
        // 0x45FAA0 retains H/depth in x87 before multiplying the exact integer8*coordinate.
        double ratio = focal / (double)depth;
        double ratioLow = Math.BitDecrement(ratio);
        double ratioHigh = Math.BitIncrement(ratio);
        int multiplier = 8 * coordinate;
        double low = Math.BitDecrement(multiplier * (multiplier < 0 ? ratioHigh : ratioLow));
        double high = Math.BitIncrement(multiplier * (multiplier < 0 ? ratioLow : ratioHigh));
        const double magicAddError = 1.0 / 4096;
        // 0x45FAA0 saturates each projected eighth coordinate before WORD storage.
        return (Math.Clamp((int)Math.Round(low - magicAddError, MidpointRounding.ToEven), -8192, 8184), Math.Clamp((int)Math.Round(high + magicAddError, MidpointRounding.ToEven), -8192, 8184));
    }

    private static (int Low, int High) RoundInterval(double low, double high)
    {
        // Native addition of2^52+2^31 can round first to x87 extended precision, then binary64.
        // Preserve both possible integers instead of assigning a portable tie rule to native code.
        const double magicAddError = 1.0 / 4096;
        int lowInteger = (int)Math.Round(low - magicAddError, MidpointRounding.ToEven);
        int highInteger = (int)Math.Round(high + magicAddError, MidpointRounding.ToEven);
        return (lowInteger, highInteger);
    }

    private static bool TryAxis(Vector3 value, out FieldNativeCameraPoint axis)
    {
        axis = default;
        if (!Word(value.X * 4096) || !Word(value.Y * 4096) || !Word(value.Z * 4096))
        {
            return false;
        }
        axis = new FieldNativeCameraPoint((short)(value.X * 4096), (short)(value.Y * 4096), (short)(value.Z * 4096));
        return true;

        static bool Word(float component)
        {
            return float.IsFinite(component) && component == MathF.Truncate(component) && component is >= short.MinValue and <= short.MaxValue;
        }
    }

    private static bool TryTranslation(Vector3 value, out FieldNativeCameraPoint translation)
    {
        translation = default;
        if (!Integer(value.X) || !Integer(value.Y) || !Integer(value.Z))
        {
            return false;
        }
        translation = new FieldNativeCameraPoint((int)value.X, (int)value.Y, (int)value.Z);
        return true;

        static bool Integer(float component)
        {
            // Beyond2^24 the float-based asset record cannot retain every original DWORD exactly.
            return float.IsFinite(component) && component == MathF.Truncate(component) && component is >= -16777216 and <= 16777216;
        }
    }
}
