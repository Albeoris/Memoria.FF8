namespace Memoria.FF8.Runtime;

/// <summary>Applies an authored elevation to a fresh scene pose without changing native camera tracks.</summary>
public static class EncounterCameraPresentation
{
    public static NativeBattleCameraPose Elevate(NativeBattleCameraPose pose, double elevationDegrees)
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (!double.IsFinite(elevationDegrees) || elevationDegrees is < -45 or > 45)
        {
            throw new InvalidDataException("Encounter camera elevation must be finite and between -45 and 45 degrees.");
        }
        if (elevationDegrees == 0)
        {
            return pose;
        }
        double x = pose.Eye.X - pose.Target.X;
        double y = pose.Eye.Y - pose.Target.Y;
        double z = pose.Eye.Z - pose.Target.Z;
        double horizontal = Math.Sqrt(x * x + z * z);
        if (horizontal == 0)
        {
            throw new InvalidDataException("An elevated encounter camera requires a nonvertical native scene pose.");
        }
        double radius = Math.Sqrt(horizontal * horizontal + y * y);
        double pitch = Math.Clamp(Math.Atan2(-y, horizontal) + elevationDegrees * Math.PI / 180, -85 * Math.PI / 180, 85 * Math.PI / 180);
        double scale = radius * Math.Cos(pitch) / horizontal;
        NativeBattleCameraPoint eye = new(Coordinate(pose.Target.X + x * scale), Coordinate(pose.Target.Y - radius * Math.Sin(pitch)), Coordinate(pose.Target.Z + z * scale));
        return pose with { Eye = eye };
    }

    private static short Coordinate(double value)
    {
        double rounded = Math.Round(value, MidpointRounding.ToEven);
        if (rounded < short.MinValue || rounded > short.MaxValue)
        {
            throw new InvalidDataException("The authored encounter camera exceeds the supported scene coordinate range.");
        }
        return (short)rounded;
    }
}
