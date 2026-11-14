using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

/// <summary>Projects original world shadows into the existing triangle submission contract.</summary>
public sealed class Ff7WorldFootShadowAdapter(PcTexture texture, Ff7WorldMath math)
{
    private readonly PixelImage image = Ff7FieldRenderer.DecodeTexture(texture);

    public static bool Eligible(Ff7WorldVisual entity, bool controlled, int heightSmoothVelocity)
    {
        bool ordinary = (entity.Model is >= 0 and <= 2 && entity.NativeAnimation is not (>= 2 and <= 5) || entity.Model is 4 or 19) && (!controlled || heightSmoothVelocity == 0) && (0x311B6F05u & (1u << (entity.Terrain & 31))) != 0;
        return (entity.Flags & 2) != 0 && (entity.Flags & 8) == 0 && entity.GroundHeight is not null && (ordinary || entity.Model is 3 or 11);
    }

    public static int Curvature(Vector3 position, Ff7WorldView view, int cameraGroundHeight, int threshold)
    {
        Vector3 camera = Vector3.Transform(new(position.X, cameraGroundHeight, position.Z), view.NativeView);
        if (camera.Z == 0)
        {
            throw new InvalidDataException("FFVII world curvature has a zero native projection divisor.");
        }
        float scale = view.Height / 240f;
        int projectedX = unchecked((short)(int)(view.Width / (2f * scale) + camera.X * (view.Focal / scale) / camera.Z));
        int lateral = Math.Abs(unchecked((short)(projectedX - (int)(view.Width / (2f * scale)))));
        int distance = ((int)camera.Z >> 2) - threshold;
        return distance > 0 ? unchecked(((distance * distance >> 12) + (lateral * lateral >> 10)) >> 2) : 0;
    }

    public int Draw(ITriangleRenderer renderer, Ff7WorldVisual entity, Vector3 position, int curvature, Ff7WorldView view, int cameraRoll, bool controlled, int heightSmoothVelocity)
    {
        if (!Eligible(entity, controlled, heightSmoothVelocity))
        {
            return 0;
        }
        int ground = entity.GroundHeight!.Value - curvature;
        int halfSize = 100 - (((int)position.Y - ground) >> 6);
        (short halfWidth, short halfLength) = entity.Model switch
        {
            4 or 19 => (unchecked((short)(halfSize * 3 >> 1)), unchecked((short)(halfSize * 3 >> 1))),
            3 => (unchecked((short)(halfSize * 2)), unchecked((short)(halfSize * 6))),
            11 => (unchecked((short)(halfSize * 6)), unchecked((short)(halfSize * 12))),
            _ => (unchecked((short)halfSize), unchecked((short)halfSize))
        };
        Matrix4x4 rotation = math.Rotation(0, 0x800 + entity.Direction + cameraRoll * 4, 0);
        Vector3 offset = entity.Model switch { 3 => new(-10, 0, 80), 11 => new(0, 0, -360), _ => Vector3.Zero };
        offset = Vector3.Transform(offset, math.Rotation(0, entity.Direction + cameraRoll * 4, 0));
        (int u, int v, int width, int height) = entity.Model switch { 3 => (24, 16, 14, 31), 11 => (0, 0, 23, 47), _ => (24, 0, 15, 15) };
        return DrawGroundQuad(renderer, image, view, new(position.X + offset.X, unchecked((short)ground), position.Z + offset.Z), halfWidth, halfLength, rotation, new(u, v, u + width, v + height), new(64 / 255f, 64 / 255f, 64 / 255f, 128 / 255f), TriangleBlend.Alpha);
    }

    public static int DrawGroundQuad(ITriangleRenderer renderer, PixelImage image, Ff7WorldView view, Vector3 center, short halfWidth, short halfLength, Matrix4x4 rotation, Vector4 atlas, Vector4 color, TriangleBlend blend)
    {
        Vector3[] corners = [new(-halfWidth, 0, -halfLength), new(halfWidth, 0, -halfLength), new(-halfWidth, 0, halfLength), new(halfWidth, 0, halfLength)];
        Vector3[] camera = corners.Select(corner => Vector3.Transform(Vector3.Transform(corner, rotation) + center, view.NativeView)).ToArray();
        if (camera.Any(point => MathF.Round(point.Z, MidpointRounding.ToEven) <= 0))
        {
            return 0;
        }
        int nearestInteger = Math.Min(Math.Min((int)camera[0].Z, (int)camera[1].Z), (int)camera[2].Z);
        int orderDepth = Math.Min(nearestInteger, (int)MathF.Round(camera[3].Z, MidpointRounding.ToEven)) >> 4;
        if (orderDepth is < 0 or >= 4096)
        {
            return 0;
        }
        float depth = 1 / camera.Min(point => point.Z);
        Vector2[] uv = [new(atlas.X, atlas.Y), new(atlas.Z, atlas.Y), new(atlas.X, atlas.W), new(atlas.Z, atlas.W)];
        TriangleVertex Vertex(int index)
        {
            Vector3 point = camera[index];
            float x = view.Width / 2f + view.OffsetX + point.X * view.Focal / point.Z;
            float y = view.CenterY + point.Y * view.Focal / point.Z;
            Vector2 screen = new(MathF.Round(x, MidpointRounding.ToEven), MathF.Round(y, MidpointRounding.ToEven));
            return new(screen, depth, 1, uv[index] / new Vector2(image.Width, image.Height), color);
        }
        TriangleMaterial material = new(image, blend, TriangleDepthComparison.GreaterEqual, false, Sampler: TriangleSampler.PointWrap)
        {
            DepthMapping = TriangleDepthMapping.Positive,
            DepthScale = 1,
            PreserveTargetAlpha = false
        };
        TriangleVertex a = Vertex(0);
        TriangleVertex b = Vertex(1);
        TriangleVertex c = Vertex(2);
        TriangleVertex d = Vertex(3);
        renderer.Draw(a, b, c, material);
        renderer.Draw(b, d, c, material);
        return 2;
    }
}
