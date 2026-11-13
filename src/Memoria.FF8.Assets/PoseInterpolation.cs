using System.Numerics;

namespace Memoria.FF8.Assets;

public static class PoseInterpolation
{
    public static Matrix4x4 Rotation(Matrix4x4 first, Matrix4x4 second, float fraction)
    {
        return Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(Quaternion.CreateFromRotationMatrix(first), Quaternion.CreateFromRotationMatrix(second), fraction));
    }
}
