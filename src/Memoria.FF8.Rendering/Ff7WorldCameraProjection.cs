using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldView(Matrix4x4 NativeView, Matrix4x4 NativeRotation, float Focal, int Width, int Height, float CenterY)
{
    public Vector3 ToCamera(Vector3 nativePosition)
    {
        Vector3 camera = Vector3.Transform(nativePosition, NativeView);
        return new(camera.X, -camera.Y + (Height / 2f - CenterY) * camera.Z / Focal, camera.Z);
    }
}

/// <summary>Builds the original world camera transform from immutable simulation-owned parameters.</summary>
public sealed class Ff7WorldCameraProjection(Ff7WorldPresentationData data)
{
    public Ff7WorldView Create(Vector3 focus, int heading, int pitch, int distance, int groundHeight, int roll, int focal, int width, int height, int viewMode)
    {
        if (distance <= 0 || focal <= 0 || width <= 0 || height <= 0 || data.ArcAngles.Count != 512 || viewMode is < 0 or > 3)
        {
            throw new ArgumentException("FFVII world camera requires bounded native resources and positive projection dimensions.");
        }
        Matrix4x4 tilt = Rotation(pitch, 0, 0);
        Vector3 position = Vector3.Transform(new Vector3(0, 0, distance), tilt);
        int y = (int)position.Y;
        int z = (int)position.Z;
        if (z == 0)
        {
            throw new InvalidDataException("FFVII world camera inverse-angle divisor is zero.");
        }
        int ratio = checked(((y << 4) - groundHeight) << 8) / (z << 4);
        int angle = data.ArcAngles[Math.Clamp(ratio, 0, 511)] + 0x800;
        Matrix4x4 rotation = Rotation(angle, heading, 0) * Rotation(0, 0, roll);
        Matrix4x4 view = Matrix4x4.CreateTranslation(-focus.X, -groundHeight, -focus.Z) * rotation * Matrix4x4.CreateTranslation(0, 0, distance);
        float scale = height / 240f;
        return new(view, rotation, focal * scale, width, height, (viewMode == 0 ? 120 : 160) * scale);
    }

    public Matrix4x4 Rotation(int x, int y, int z)
    {
        float Radians(int angle) => (float)((float)(angle * data.DegreesPerAngleUnit) * data.RadiansPerDegree);
        return Matrix4x4.CreateRotationX(Radians(x)) * Matrix4x4.CreateRotationY(Radians(y)) * Matrix4x4.CreateRotationZ(Radians(z));
    }
}
