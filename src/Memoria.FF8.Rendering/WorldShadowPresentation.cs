using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>Original stored shape/yaw with canonical raw anchor and selected ground elevation.</summary>
public readonly record struct NativeWorldShadowDraw(byte ShapeId, Vector3 Anchor, short NativeYaw, float GroundHeight, float Scale = 1);
public readonly record struct WorldShadowVertex(Vector3 Position, Vector2 Uv);
public readonly record struct WorldShadowTriangle(WorldShadowVertex A, WorldShadowVertex B, WorldShadowVertex C);
public sealed record WorldShadowGeometry(int TextureMember, IReadOnlyList<WorldShadowTriangle> Triangles);

public static class WorldShadowPresentation
{
    public static WorldShadowGeometry Create(NativeWorldShadowDraw draw, NativeWorldContactShape shape)
    {
        if (!float.IsFinite(draw.GroundHeight) || !float.IsFinite(draw.Scale) || draw.Scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(draw));
        }
        int length = shape.Length;
        int width = shape.Width;
        Vector3[] corners = shape.Kind switch
        {
            1 or 4 => [new(-(length >> 1), 0, width >> 1), new(length >> 1, 0, width >> 1), new(length >> 1, 0, -(width >> 1)), new(-(length >> 1), 0, -(width >> 1))],
            2 => [new(-length, 0, width >> 1), new(0, 0, width >> 1), new(0, 0, -(width >> 1)), new(-length, 0, -(width >> 1))],
            3 => [new(-(length >> 2), 0, -(width >> 1)), new(-(length >> 1), 0, width >> 1), new(length >> 1, 0, width >> 1), new(length >> 2, 0, -(width >> 1))],
            _ => throw new NotSupportedException($"Unsupported native world shadow contact kind {shape.Kind}.")
        };
        NativeWorldPathAngles angles = WorldStaticModelTransform.AllocationAngles(draw.ShapeId, 0, draw.NativeYaw, 0);
        Matrix4x4 rotation = Matrix4x4.CreateRotationY(angles.Yaw * MathF.Tau / 4096);
        Vector3 World(Vector3 point)
        {
            Vector3 native = Vector3.Transform(point * draw.Scale, rotation);
            return new(draw.Anchor.X + native.X, draw.GroundHeight, draw.Anchor.Z - native.Z);
        }
        Vector3 a = World(corners[0]);
        Vector3 b = World(corners[1]);
        Vector3 c = World(corners[2]);
        Vector3 d = World(corners[3]);
        Vector3 ab = (a + b) / 2;
        Vector3 cd = (c + d) / 2;
        List<WorldShadowTriangle> triangles = [];
        void Quad(Vector3 topLeft, Vector3 topRight, Vector3 bottomLeft, Vector3 bottomRight, int textureY)
        {
            WorldShadowVertex tl = new(topLeft, new Vector2(0.5f / 16, (textureY + 0.5f) / 16));
            WorldShadowVertex tr = new(topRight, new Vector2(15.5f / 16, (textureY + 0.5f) / 16));
            WorldShadowVertex bl = new(bottomLeft, new Vector2(0.5f / 16, (textureY + 7.5f) / 16));
            WorldShadowVertex br = new(bottomRight, new Vector2(15.5f / 16, (textureY + 7.5f) / 16));
            triangles.Add(new(tl, tr, bl));
            triangles.Add(new(bl, tr, br));
        }
        Quad(b, c, ab, cd, 0);
        Quad(ab, cd, a, d, 8);
        int entity = NativeTrainWorldObject.ClassifyShape(draw.ShapeId);
        return new(entity is >= 32 and <= 40 or 132 ? 13 : 12, triangles.AsReadOnly());
    }
}
