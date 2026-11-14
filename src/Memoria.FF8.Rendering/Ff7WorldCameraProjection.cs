using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldView(Matrix4x4 NativeView, Matrix4x4 NativeRotation, float Focal, int Width, int Height, float CenterY)
{
    public float OffsetX { get; init; }

    public Vector3 ToCamera(Vector3 nativePosition)
    {
        Vector3 camera = Vector3.Transform(nativePosition, NativeView);
        return new(camera.X + OffsetX * camera.Z / Focal, -camera.Y + (Height / 2f - CenterY) * camera.Z / Focal, camera.Z);
    }
}

/// <summary>Builds the original world camera transform from immutable simulation-owned parameters.</summary>
public sealed class Ff7WorldCameraProjection(Ff7WorldPresentationData data)
{
    private readonly Ff7WorldMath math = new(data);
    private readonly Ff7WorldCameraMath camera = new(data);
    public Ff7WorldView Create(Vector3 focus, int heading, int pitch, int distance, int groundHeight, int roll, int focal, int width, int height, int viewMode, int shakeX = 0, int shakeY = 0)
    {
        if (distance <= 0 || focal <= 0 || width <= 0 || height <= 0 || data.ArcAngles.Count != 512 || viewMode is < 0 or > 3)
        {
            throw new ArgumentException("FFVII world camera requires bounded native resources and positive projection dimensions.");
        }
        Ff7WorldCameraTransform transform = camera.Create(focus, heading, pitch, distance, groundHeight, roll);
        float scale = height / 240f;
        return new(transform.View, transform.Rotation, focal * scale, width, height, ((viewMode == 0 ? 120 : 160) + shakeY) * scale) { OffsetX = shakeX * scale };
    }

    public Matrix4x4 Rotation(int x, int y, int z)
    {
        return math.Rotation(x, y, z);
    }
}
