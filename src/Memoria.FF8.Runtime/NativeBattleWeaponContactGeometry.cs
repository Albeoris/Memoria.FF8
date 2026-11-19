using System.Numerics;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWeaponContactGeometry
{
    public static bool IntersectsHandEnvelope(Vector3 minimum, Vector3 maximum, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 center = (minimum + maximum) / 2;
        Vector3 extent = (maximum - minimum) / 2;
        if (!Finite(minimum) || !Finite(maximum) || !Finite(a) || !Finite(b) || !Finite(c) || extent.X < 0 || extent.Y < 0 || extent.Z < 0)
        {
            throw new InvalidDataException("Weapon contact requires a finite authored hand envelope and finite source triangle.");
        }
        a -= center;
        b -= center;
        c -= center;
        Vector3 first = b - a;
        Vector3 second = c - b;
        Vector3 third = a - c;
        if (Separated(Vector3.UnitX) || Separated(Vector3.UnitY) || Separated(Vector3.UnitZ) || Separated(Vector3.Cross(first, second)))
        {
            return false;
        }
        return !EdgeSeparated(first) && !EdgeSeparated(second) && !EdgeSeparated(third);

        bool EdgeSeparated(Vector3 edge)
        {
            return Separated(Vector3.Cross(edge, Vector3.UnitX)) || Separated(Vector3.Cross(edge, Vector3.UnitY)) || Separated(Vector3.Cross(edge, Vector3.UnitZ));
        }

        bool Separated(Vector3 axis)
        {
            float x = Vector3.Dot(a, axis);
            float y = Vector3.Dot(b, axis);
            float z = Vector3.Dot(c, axis);
            float radius = extent.X * MathF.Abs(axis.X) + extent.Y * MathF.Abs(axis.Y) + extent.Z * MathF.Abs(axis.Z);
            return MathF.Min(x, MathF.Min(y, z)) > radius || MathF.Max(x, MathF.Max(y, z)) < -radius;
        }
    }

    private static bool Finite(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);
}
