using System.Numerics;

namespace Memoria.FF8.Assets;

public readonly record struct FieldNativeCameraPoint(int X, int Y, int Z);
public readonly record struct FieldNativeCameraCenter(int X, int Y);
public sealed record FieldNativeCameraDiagnostics(FieldNativeCameraPoint Anchor, FieldNativeCameraPoint? Transformed = null, int? ProjectionDepth = null, FieldNativeCameraCenter? ProjectedEighth = null, FieldNativeCameraCenter? UnclampedCenter = null);
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
        FieldNativeCameraPoint transformed = new(Transform(axisX, anchor, translation.X), Transform(axisY, anchor, translation.Y), Transform(axisZ, anchor, translation.Z));
        diagnostics = diagnostics with { Transformed = transformed };
        int focal = (ushort)camera.FocalLength;
        // 0x45FAA0 checks the raw signed depth as unsigned against65535.
        // Negative depth becomes0 (not a wrapped ushort), then uses the H/2 near clamp.
        // 0x475E60 retains the flags but still consumes the projected camera center.
        int depth = Math.Max(Math.Clamp(transformed.Z, 0, ushort.MaxValue), focal >> 1);
        diagnostics = diagnostics with { ProjectionDepth = depth };
        if (depth == 0)
        {
            return Unsupported("Native projection has no positive denominator after the near clamp.");
        }
        // 0x45FBA3..0x45FC3B saturates diagnostic IR words only. Projection
        // consumes the raw ESI/EBP transform results, not those saturated words.
        double ratio = focal / (double)depth;
        FieldNativeCameraCenter projected = new(Project(transformed.X, ratio), Project(transformed.Y, ratio));
        diagnostics = diagnostics with { ProjectedEighth = projected };

        // 0x475E60 uses signed IDIV by8, after signed-WORD projection storage.
        FieldNativeCameraCenter center = new(projected.X / 8, projected.Y / 8);
        diagnostics = diagnostics with { UnclampedCenter = center };
        return new FieldNativeCameraResult(Clamp(center, information, selectedCameraIndex), diagnostics, null);
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

    private static int Transform(FieldNativeCameraPoint axis, FieldNativeCameraPoint point, int translation)
    {
        long numerator = (long)axis.X * point.X + (long)axis.Y * point.Y + (long)axis.Z * point.Z;
        // Every WORD product and partial sum divided by4096 is exact at
        // native precision53. 45FB19..45FB98 rounds before DWORD translation.
        return unchecked(RoundNative(numerator / 4096.0) + translation);
    }

    private static int Project(int coordinate, double ratio)
    {
        // Preserve 45FCB3/45FD0C LEA DWORD wrapping and the separately rounded
        // FDIV, FMUL, FADD and binary64 store at native precision53.
        double value = unchecked(8 * coordinate) * ratio;
        return Math.Clamp(RoundNative(value), -8192, 8184);
    }

    private static int RoundNative(double value)
    {
        // Original B693D8 magic double under the PC nearest-even precision53
        // control word027F. Decode the stored low DWORD exactly as45FAA0 does;
        // converting to int first would lose native wrap outside signed range.
        const double magic = 4503601774854144.0; // 2^52 + 2^31.
        double stored = value + magic;
        return unchecked((int)BitConverter.DoubleToInt64Bits(stored) + int.MinValue);
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
