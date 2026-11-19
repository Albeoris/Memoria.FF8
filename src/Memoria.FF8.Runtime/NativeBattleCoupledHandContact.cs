using System.Numerics;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCoupledHandContact
{
    public static Vector3 SolvePrimary(Vector3 preferred, Vector3 primaryShoulder, float primaryUpper, float primaryLower, Vector3 secondaryShoulder, float secondaryUpper, float secondaryLower, Vector3 sourceGap)
    {
        Vector3 first = primaryShoulder;
        Vector3 second = secondaryShoulder - sourceGap;
        float firstMin = MathF.Abs(primaryUpper - primaryLower);
        float firstMax = primaryUpper + primaryLower;
        float secondMin = MathF.Abs(secondaryUpper - secondaryLower);
        float secondMax = secondaryUpper + secondaryLower;
        if (!Finite(preferred) || !Finite(first) || !Finite(second) || !Finite(sourceGap) || !float.IsFinite(firstMax) || !float.IsFinite(secondMax) || primaryUpper <= 0 || primaryLower <= 0 || secondaryUpper <= 0 || secondaryLower <= 0)
        {
            throw new InvalidDataException("A coupled hand contact requires finite positions and positive finite arm lengths.");
        }
        bool Admitted(Vector3 point)
        {
            float a = Vector3.Distance(point, first);
            float b = Vector3.Distance(point, second);
            return Finite(point) && a >= firstMin && a <= firstMax && b >= secondMin && b <= secondMax;
        }
        if (Admitted(preferred))
        {
            return preferred;
        }
        List<Vector3> candidates = [];
        float[] firstRadii = [Interior(firstMin, true), Interior(firstMax, false)];
        float[] secondRadii = [Interior(secondMin, true), Interior(secondMax, false)];
        foreach (float radius in firstRadii)
        {
            Add(Project(first, radius));
        }
        foreach (float radius in secondRadii)
        {
            Add(Project(second, radius));
        }
        Vector3 delta = second - first;
        float distance = delta.Length();
        if (distance > 0)
        {
            Vector3 axis = delta / distance;
            foreach (float a in firstRadii)
            {
                foreach (float b in secondRadii)
                {
                    double along = ((double)a * a - (double)b * b + (double)distance * distance) / (2 * distance);
                    double squaredHeight = (double)a * a - along * along;
                    if (squaredHeight < 0)
                    {
                        continue;
                    }
                    Vector3 center = first + axis * (float)along;
                    Vector3 radial = preferred - center;
                    radial -= axis * Vector3.Dot(radial, axis);
                    if (radial.LengthSquared() == 0)
                    {
                        radial = Vector3.Cross(axis, MathF.Abs(axis.X) < MathF.Abs(axis.Y) ? Vector3.UnitX : Vector3.UnitY);
                    }
                    Add(center + Vector3.Normalize(radial) * (float)Math.Sqrt(squaredHeight));
                }
            }
        }
        if (candidates.Count == 0)
        {
            throw new NotSupportedException($"The coupled hand contact has no shared reachable grip: shoulder-gap separation {distance:R}, primary reach [{firstMin:R}, {firstMax:R}], secondary reach [{secondMin:R}, {secondMax:R}] native units.");
        }
        return candidates.MinBy(point => Vector3.DistanceSquared(preferred, point));

        Vector3 Project(Vector3 center, float radius)
        {
            Vector3 direction = preferred - center;
            return center + (direction.LengthSquared() == 0 ? Vector3.UnitX : Vector3.Normalize(direction)) * radius;
        }

        void Add(Vector3 candidate)
        {
            if (Admitted(candidate))
            {
                candidates.Add(candidate);
            }
        }
    }

    private static float Interior(float boundary, bool increase)
    {
        // Keep a representable interior point after the vector arithmetic, without changing the physical reach interval.
        for (int operation = 0; operation < 8; operation++)
        {
            boundary = increase ? MathF.BitIncrement(boundary) : MathF.BitDecrement(boundary);
        }
        return boundary;
    }

    private static bool Finite(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);
}
