using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record ExpandedBattleFrame(BattleCamera Camera, NativeBattleCameraPose Pose, NativeBattleCameraProjection Projection);

internal static class ExpandedBattleFraming
{
    private const int ViewportHeight = 240;
    private const short AuthoredCenterX = 160;
    private const short AuthoredCenterY = 108;
    private const int Margin = 8;
    private const int HorizontalRadius = AuthoredCenterX - Margin;

    internal static ExpandedBattleFrame Fit(BattleStageCamera authored, short roll, IReadOnlyList<Vector3> idleBodyVertices, int allyCount, int activeEnemyCount)
    {
        ArgumentNullException.ThrowIfNull(authored);
        if (allyCount is < 1 or > ExpandedBattleFormation.MaximumAllies)
        {
            throw new ArgumentOutOfRangeException(nameof(allyCount));
        }
        if (activeEnemyCount is < 1 or > ExpandedBattleFormation.MaximumEnemies)
        {
            throw new ArgumentOutOfRangeException(nameof(activeEnemyCount));
        }
        if (authored.FocalLength is < 1 or > short.MaxValue)
        {
            throw new InvalidDataException("The authored battle camera focal length must be a positive signed native WORD.");
        }

        NativeBattleCameraPoint eye = NativePoint(authored.NativeEye);
        NativeBattleCameraPoint target = NativePoint(authored.NativeTarget);
        BattleCamera camera = BattleCamera.FromNative(authored, roll);
        if (allyCount <= 3 && activeEnemyCount <= 4)
        {
            return Result(authored.FocalLength, AuthoredCenterY);
        }

        ArgumentNullException.ThrowIfNull(idleBodyVertices);
        // Match BattleRenderer.Begin and TryProject in the canonical native viewport.
        Matrix4x4 rotation = Matrix4x4.CreateRotationZ(-roll * (2 * MathF.PI / 4096));
        Vector3 rotatedEye = Vector3.Transform(camera.Eye, rotation);
        Vector3 rotatedTarget = Vector3.Transform(camera.Target, rotation);
        if (rotatedEye.X == rotatedTarget.X && rotatedEye.Z == rotatedTarget.Z)
        {
            throw new InvalidDataException("The authored battle camera has no valid horizontal viewing direction.");
        }
        Matrix4x4 view = rotation * Matrix4x4.CreateLookAt(rotatedEye, rotatedTarget, Vector3.UnitY);
        Vector3[] vertices = new Vector3[idleBodyVertices.Count];
        if (vertices.Length == 0)
        {
            throw new InvalidDataException("Battle framing requires at least one idle body vertex.");
        }
        for (int index = 0; index < vertices.Length; index++)
        {
            Vector3 vertex = Vector3.Transform(idleBodyVertices[index], view);
            if (!float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y) || !float.IsFinite(vertex.Z))
            {
                throw new InvalidDataException("Battle framing requires finite idle body vertices in camera space.");
            }
            if (vertex.Z >= -0.25f)
            {
                throw new InvalidDataException("An idle body vertex is on or behind the battle camera near plane.");
            }
            vertices[index] = vertex;
        }

        float safeBottom = (allyCount <= 4 ? 516f : 408f) / 672f * ViewportHeight;
        var authoredBounds = Measure(vertices, camera.FieldOfView, AuthoredCenterY);
        if (Fits(authoredBounds, safeBottom))
        {
            return Result(authored.FocalLength, AuthoredCenterY);
        }

        double radius = Math.Max(AuthoredCenterX - authoredBounds.Left, authoredBounds.Right - AuthoredCenterX);
        double height = (double)authoredBounds.Bottom - authoredBounds.Top;
        double scale = Math.Min(1, Math.Min(HorizontalRadius / radius, (safeBottom - Margin) / height));
        int upper = (int)Math.Floor(authored.FocalLength * scale);
        if (upper >= 1 && TryFit(upper, out short upperCenter))
        {
            return Result(upper, upperCenter);
        }

        // A smaller focal length expands the feasible interval for the vertical center.
        int low = 1;
        int high = upper - 1;
        int bestFocal = 0;
        short bestCenter = 0;
        while (low <= high)
        {
            int focal = low + (high - low) / 2;
            if (TryFit(focal, out short center))
            {
                bestFocal = focal;
                bestCenter = center;
                low = focal + 1;
            }
            else
            {
                high = focal - 1;
            }
        }
        if (bestFocal != 0)
        {
            return Result(bestFocal, bestCenter);
        }
        throw new InvalidDataException("The idle body bounds cannot fit within a positive native focal length and a renderable integer vertical center.");

        bool TryFit(int focal, out short center)
        {
            center = 0;
            float fov = FieldOfView(focal);
            var bounds = Measure(vertices, fov, 0);
            if (bounds.Left < Margin || bounds.Right > AuthoredCenterX + HorizontalRadius)
            {
                return false;
            }
            double lowest = Math.Max(1, Math.Ceiling(Margin - (double)bounds.Top));
            double highest = Math.Min(ViewportHeight - 1, Math.Floor(safeBottom - (double)bounds.Bottom));
            if (lowest > highest)
            {
                return false;
            }
            center = (short)Math.Clamp(AuthoredCenterY, lowest, highest);
            var fitted = Measure(vertices, fov, ViewportHeight * (center / (float)ViewportHeight));
            return Fits(fitted, safeBottom);
        }

        ExpandedBattleFrame Result(int focal, short center)
        {
            NativeBattleCameraPose pose = new(eye, target, checked((short)focal), roll);
            BattleCamera fitted = camera with { FieldOfView = FieldOfView(focal), VerticalCenter = center / (float)ViewportHeight };
            return new(fitted, pose, new(NativeBattlePoseMatrices.CameraView(pose), checked((ushort)focal), AuthoredCenterX, center));
        }
    }

    private static (float Left, float Top, float Right, float Bottom) Measure(Vector3[] vertices, float fieldOfView, float centerY)
    {
        float focal = ViewportHeight / (2 * MathF.Tan(fieldOfView / 2));
        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        float top = float.PositiveInfinity;
        float bottom = float.NegativeInfinity;
        foreach (Vector3 vertex in vertices)
        {
            float x = AuthoredCenterX - vertex.X * focal / vertex.Z;
            float y = centerY + vertex.Y * focal / vertex.Z;
            if (!float.IsFinite(x) || !float.IsFinite(y))
            {
                throw new InvalidDataException("An idle body vertex does not produce a finite battle camera projection.");
            }
            left = Math.Min(left, x);
            right = Math.Max(right, x);
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
        }
        return (left, top, right, bottom);
    }

    private static bool Fits((float Left, float Top, float Right, float Bottom) bounds, float safeBottom)
    {
        return bounds.Left >= Margin && bounds.Right <= AuthoredCenterX + HorizontalRadius && bounds.Top >= Margin && bounds.Bottom <= safeBottom;
    }

    private static float FieldOfView(int focal) => 2 * MathF.Atan(120f / focal);

    private static NativeBattleCameraPoint NativePoint(Vector3 point)
    {
        return new(NativeCoordinate(point.X), NativeCoordinate(point.Y), NativeCoordinate(point.Z));
    }

    private static short NativeCoordinate(float value)
    {
        if (!float.IsFinite(value) || value < short.MinValue || value > short.MaxValue || value != MathF.Truncate(value))
        {
            throw new InvalidDataException("The authored battle camera eye and target require signed native WORD coordinates.");
        }
        return (short)value;
    }
}
