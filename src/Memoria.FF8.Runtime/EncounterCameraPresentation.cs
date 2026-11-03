namespace Memoria.FF8.Runtime;

/// <summary>Applies an authored elevation to a fresh scene pose without changing native camera tracks.</summary>
public static class EncounterCameraPresentation
{
    public static NativeBattleCameraPose FrameTargets(NativeBattleCameraPose pose, IReadOnlyList<NativeBattleVector> points, ushort focalLength, short offsetX = 160, short offsetY = 108)
    {
        if (points.Count == 0)
        {
            return pose;
        }
        if (focalLength == 0 || offsetX <= 1 || offsetX >= 319 || offsetY <= 1 || offsetY >= 215)
        {
            throw new InvalidDataException("Encounter target framing requires a positive lens and a principal point inside the battle viewport.");
        }
        double dx = pose.Eye.X - pose.Target.X;
        double dy = pose.Eye.Y - pose.Target.Y;
        double dz = pose.Eye.Z - pose.Target.Z;
        double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (length == 0)
        {
            throw new InvalidDataException("Encounter target framing requires a camera direction.");
        }
        NativeBattleCameraPose result = pose;
        double distance = 0;
        for (int pass = 0; pass < 16; pass++)
        {
            NativeBattleActionMatrix view = NativeBattlePoseMatrices.CameraView(result);
            double required = 0;
            foreach (NativeBattleVector point in points)
            {
                double Row(int row) => (view.Rotation[row * 3] * (double)point.X + view.Rotation[row * 3 + 1] * (double)point.Y + view.Rotation[row * 3 + 2] * (double)point.Z) / 4096;
                double x = Row(0) + view.X;
                double y = Row(1) + view.Y;
                double z = Row(2) + view.Z;
                // Keep the projected bounds inside the last complete raster pixel.
                double horizontal = x < 0 ? offsetX - 1 : 319 - offsetX;
                double vertical = y < 0 ? offsetY - 1 : 215 - offsetY;
                required = Math.Max(required, Math.Max(focalLength * Math.Abs(x) / horizontal - z, focalLength * Math.Abs(y) / vertical - z));
                required = Math.Max(required, focalLength / 2d - z);
            }
            if (required <= 0)
            {
                return result;
            }
            distance += Math.Ceiling(required) + 1;
            result = pose with { Eye = new(Coordinate(pose.Eye.X + dx / length * distance), Coordinate(pose.Eye.Y + dy / length * distance), Coordinate(pose.Eye.Z + dz / length * distance)) };
        }
        throw new InvalidDataException("Encounter target framing did not converge within the native coordinate domain.");
    }

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
