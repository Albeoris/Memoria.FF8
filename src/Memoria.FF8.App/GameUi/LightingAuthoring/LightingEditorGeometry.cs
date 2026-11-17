using System.Numerics;

namespace Memoria.FF8.GameUi;

public enum LightingTransformTool
{
    Translate,
    Rotate,
    Orbit
}

public enum LightingAxis
{
    None,
    X,
    Y,
    Z
}

public readonly record struct LightingEditorRay(Vector3 Origin, Vector3 Direction);

public readonly record struct LightingGizmoSegment(Vector2 Start, Vector2 End, LightingAxis Axis, LightingTransformTool Tool);

public static class LightingEditorGeometry
{
    public static Vector3 AxisVector(LightingAxis axis)
    {
        return axis switch
        {
            LightingAxis.X => Vector3.UnitX,
            LightingAxis.Y => Vector3.UnitY,
            LightingAxis.Z => Vector3.UnitZ,
            _ => Vector3.UnitZ
        };
    }

    public static Vector3 Rotate(Vector3 value, LightingAxis axis, float degrees)
    {
        return Rotate(value, AxisVector(axis), degrees);
    }

    public static Vector3 Rotate(Vector3 value, Vector3 axis, float degrees)
    {
        return Vector3.Transform(value, Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis), degrees * MathF.PI / 180));
    }

    public static Vector3? IntersectPlane(LightingEditorRay? ray, Vector3 point, Vector3 normal)
    {
        if (ray is not { } value)
        {
            return null;
        }
        float denominator = Vector3.Dot(value.Direction, normal);
        if (MathF.Abs(denominator) < 0.00001f)
        {
            return null;
        }
        float distance = Vector3.Dot(point - value.Origin, normal) / denominator;
        return distance >= 0 && float.IsFinite(distance) ? value.Origin + value.Direction * distance : null;
    }

    public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        float t = lengthSquared < 0.0001f ? 0 : Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0, 1);
        return Vector2.Distance(point, start + segment * t);
    }

    public static float? AxisParameter(LightingEditorRay? ray, Vector3 origin, Vector3 axis)
    {
        if (ray is not { } value)
        {
            return null;
        }
        Vector3 direction = Vector3.Normalize(value.Direction);
        float alignment = Vector3.Dot(axis, direction);
        float denominator = 1 - alignment * alignment;
        if (denominator < 0.00001f)
        {
            return null;
        }
        Vector3 relative = value.Origin - origin;
        return (Vector3.Dot(axis, relative) - alignment * Vector3.Dot(direction, relative)) / denominator;
    }

    public static IEnumerable<(Vector3 Start, Vector3 End)> Ring(Vector3 center, Vector3 normal, float radius, int segments = 48)
    {
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(normal, MathF.Abs(normal.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitY));
        Vector3 bitangent = Vector3.Cross(normal, tangent);
        Vector3 previous = center + tangent * radius;
        for (int index = 1; index <= segments; index++)
        {
            float angle = index * MathF.Tau / segments;
            Vector3 next = center + (tangent * MathF.Cos(angle) + bitangent * MathF.Sin(angle)) * radius;
            yield return (previous, next);
            previous = next;
        }
    }
}
