using System.Buffers;
using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7WorldCameraTransform(Matrix4x4 View, Matrix4x4 Rotation);

/// <summary>Original world camera transform and simulation-owned terrain occlusion probe.</summary>
public sealed class Ff7WorldCameraMath(Ff7WorldPresentationData data)
{
    private readonly Ff7WorldMath math = new(data);

    public Ff7WorldCameraTransform Create(Vector3 focus, int heading, int pitch, int distance, int groundHeight, int roll)
    {
        if (distance <= 0 || data.ArcAngles.Count != 512)
        {
            throw new ArgumentException("FFVII world camera requires bounded native resources and a positive distance.");
        }
        Vector3 position = Vector3.Transform(new Vector3(0, 0, distance), math.Rotation(pitch, 0, 0));
        int y = (int)position.Y;
        int z = (int)position.Z;
        if (z == 0)
        {
            throw new InvalidDataException("FFVII world camera inverse-angle divisor is zero.");
        }
        int ratio = checked(((y << 4) - groundHeight) << 8) / (z << 4);
        int angle = data.ArcAngles[Math.Clamp(ratio, 0, 511)] + 0x800;
        Matrix4x4 rotation = Ff7WorldMath.Quantize(math.Rotation(angle, heading, 0) * math.Rotation(0, 0, roll));
        Matrix4x4 view = Matrix4x4.CreateTranslation(-focus.X, -groundHeight, -focus.Z) * rotation * Matrix4x4.CreateTranslation(0, 0, distance);
        return new(view, rotation);
    }

    public static Vector3 Curve(Vector3 position, Matrix4x4 view, float logicalFocal, int threshold)
    {
        Vector3 camera = Vector3.Transform(position, view);
        int distance = ((int)MathF.Round(camera.Z, MidpointRounding.ToEven) >> 2) - threshold;
        if (distance > 0 && camera.Z != 0)
        {
            int screenX = (int)MathF.Round(camera.X * logicalFocal / camera.Z, MidpointRounding.ToEven);
            float bend = distance * (1f / 64) + Math.Abs(screenX) * (1f / 32);
            position.Y -= bend * bend;
        }
        return position;
    }

    public static bool Occluded(PcWorldMesh mesh, Vector3 origin, Ff7WorldCameraTransform camera, int focal, int viewMode, int curvatureThreshold = 5000, Func<int, Vector3, Vector3>? deformVertex = null)
    {
        Vector3[] projected = ArrayPool<Vector3>.Shared.Rent(mesh.Vertices.Count);
        try
        {
            for (int index = 0; index < mesh.Vertices.Count; index++)
            {
                Vector3 local = mesh.Vertices[index];
                Vector3 source = (deformVertex is null ? local : deformVertex(index, local)) + origin;
                float depth = Vector3.Transform(source, camera.View).Z;
                Vector3 position = Curve(source, camera.View, focal, curvatureThreshold);
                Vector3 point = Vector3.Transform(position, camera.View);
                projected[index] = new(160 + point.X * focal / point.Z, (viewMode == 0 ? 120 : 160) + point.Y * focal / point.Z, depth);
            }
            foreach (PcWorldTriangle triangle in mesh.Triangles)
            {
                if (TriangleOccludes(projected[triangle.A], projected[triangle.B], projected[triangle.C]))
                {
                    return true;
                }
            }
            return false;
        }
        finally
        {
            ArrayPool<Vector3>.Shared.Return(projected);
        }
    }

    public static bool TriangleOccludes(Vector3 a, Vector3 b, Vector3 c)
    {
        static float Round(float value) => MathF.Round(value, MidpointRounding.ToEven);
        static bool Within(float value, int maximum) => Round(value) >= 0 && Round(value) < maximum;
        if (!(Within(a.X, 320) || Within(b.X, 320) || Within(c.X, 320)) || !(Within(a.Y, 240) || Within(b.Y, 240) || Within(c.Y, 240)))
        {
            return false;
        }
        float determinant = a.X * b.Y + b.X * c.Y + c.X * a.Y - a.X * c.Y - b.X * a.Y - c.X * b.Y;
        if (!float.IsFinite(determinant) || Round(determinant) < 0)
        {
            return false;
        }
        float depth = c.Z;
        if (Round(a.Z - b.Z) < 0)
        {
            if (Round(b.Z - c.Z) >= 0)
            {
                depth = b.Z;
            }
        }
        else if (Round(a.Z - c.Z) >= 0)
        {
            depth = a.Z;
        }
        return Round(depth) <= 5500 && MathF.Min(a.X, MathF.Min(b.X, c.X)) < 160 && MathF.Max(a.X, MathF.Max(b.X, c.X)) > 160 && MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) < 160 && MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) > 160;
    }
}
