using System.Numerics;

namespace Memoria.FF7.Resources;

public sealed class Ff7WorldMath(Ff7WorldPresentationData data)
{
    public Vector3 TransformRotation(Vector3 value, int x, int y, int z)
    {
        Matrix4x4 matrix = Rotation(x, y, z);
        return new((float)((double)value.X * matrix.M11 + (double)value.Y * matrix.M21 + (double)value.Z * matrix.M31), (float)((double)value.X * matrix.M12 + (double)value.Y * matrix.M22 + (double)value.Z * matrix.M32), (float)((double)value.X * matrix.M13 + (double)value.Y * matrix.M23 + (double)value.Z * matrix.M33));
    }

    public Matrix4x4 Rotation(int x, int y, int z)
    {
        float Radians(int angle) => (float)((float)(angle * data.DegreesPerAngleUnit) * data.RadiansPerDegree);
        return Quantize(Matrix4x4.CreateRotationX(Radians(x)) * Matrix4x4.CreateRotationY(Radians(y)) * Matrix4x4.CreateRotationZ(Radians(z)));
    }

    public static Matrix4x4 Quantize(Matrix4x4 value)
    {
        static float Fixed(float component) => unchecked((short)(int)(component * 4096)) / 4096f;
        return new(Fixed(value.M11), Fixed(value.M12), Fixed(value.M13), 0, Fixed(value.M21), Fixed(value.M22), Fixed(value.M23), 0, Fixed(value.M31), Fixed(value.M32), Fixed(value.M33), 0, 0, 0, 0, 1);
    }
}
