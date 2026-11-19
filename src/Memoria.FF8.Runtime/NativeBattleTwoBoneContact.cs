using System.Numerics;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTwoBoneContactSolution(Vector3 Elbow, Vector3 Wrist);

public static class NativeBattleTwoBoneContact
{
    public static NativeBattleTwoBoneContactSolution Solve(Vector3 shoulder, Vector3 elbow, Vector3 wrist, Vector3 desiredWrist, Vector3 bendAxis, float? upperLength = null, float? lowerLength = null)
    {
        if (!Finite(shoulder) || !Finite(elbow) || !Finite(wrist) || !Finite(desiredWrist) || !Finite(bendAxis))
        {
            throw new InvalidDataException("A two-link contact requires finite positions and a finite bend pole.");
        }
        float upper = upperLength ?? Vector3.Distance(shoulder, elbow);
        float lower = lowerLength ?? Vector3.Distance(elbow, wrist);
        float distance = Vector3.Distance(shoulder, desiredWrist);
        float minimum = MathF.Abs(upper - lower);
        float maximum = upper + lower;
        if (!float.IsFinite(upper) || !float.IsFinite(lower) || !float.IsFinite(distance))
        {
            throw new InvalidDataException("A two-link contact requires representable finite limb lengths and target distance.");
        }
        if (upper <= 0 || lower <= 0)
        {
            throw new InvalidDataException("A two-link contact requires two nonzero recipient limb lengths.");
        }
        if (distance < minimum || distance > maximum)
        {
            throw new NotSupportedException($"The secondary hand contact is unreachable: distance {distance:R}, recipient reach [{minimum:R}, {maximum:R}] native units.");
        }
        if (distance == 0)
        {
            return new(elbow, desiredWrist);
        }
        Vector3 axis = (desiredWrist - shoulder) / distance;
        Vector3 pole = elbow - shoulder;
        pole -= axis * Vector3.Dot(pole, axis);
        if (pole.LengthSquared() < 0.00000001f)
        {
            pole = bendAxis - axis * Vector3.Dot(bendAxis, axis);
        }
        if (pole.LengthSquared() < 0.00000001f)
        {
            throw new InvalidDataException("A collinear two-link contact requires a nonparallel calibrated bend pole.");
        }
        pole = Vector3.Normalize(pole);
        float along = (upper * upper + distance * distance - lower * lower) / (2 * distance);
        float height = MathF.Sqrt(MathF.Max(0, upper * upper - along * along));
        return new(shoulder + axis * along + pole * height, desiredWrist);
    }

    private static bool Finite(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);
}
