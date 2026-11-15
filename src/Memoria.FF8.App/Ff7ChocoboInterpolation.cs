using System.Numerics;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal static class Ff7ChocoboInterpolation
{
    internal static Matrix4x4 View(float[] previous, Matrix4x4 current, float fraction)
    {
        Matrix4x4 before = new(previous[0], previous[1], previous[2], previous[3], previous[4], previous[5], previous[6], previous[7], previous[8], previous[9], previous[10], previous[11], previous[12], previous[13], previous[14], previous[15]);
        return Transform(before, current, fraction);
    }

    internal static Matrix4x4 Transform(Matrix4x4 previous, Matrix4x4 current, float fraction)
    {
        if (fraction <= 0)
        {
            return previous;
        }
        if (fraction >= 1)
        {
            return current;
        }
        Quaternion first = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(previous));
        Quaternion second = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(current));
        Matrix4x4 result = Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(first, second, fraction));
        result.Translation = Vector3.Lerp(previous.Translation, current.Translation, fraction);
        return result;
    }

    internal static Vector3 Position(Ff7ChocoboPoint previous, Vector3 current, float fraction)
    {
        return Vector3.Lerp(new(previous.X, previous.Y, previous.Z), current, fraction);
    }

    internal static Matrix4x4 RacerRotation(short yaw)
    {
        byte native = unchecked((byte)(256 - (yaw >> 4)));
        return Matrix4x4.CreateRotationZ(-MathF.PI) * Matrix4x4.CreateRotationY(-native * MathF.Tau / 250);
    }

    internal static float Frame(int previous, int current, int count, float fraction, bool loop)
    {
        if (fraction >= 1)
        {
            return current;
        }
        float end = loop && current < previous ? current + count : current;
        return (previous + (end - previous) * fraction) % count;
    }
}
