using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

/// <summary>Projects original ordinary-foot shadows into the existing triangle submission contract.</summary>
public sealed class Ff7WorldFootShadowAdapter(PcTexture texture, Ff7WorldMath math)
{
    private readonly PixelImage image = Ff7FieldRenderer.DecodeTexture(texture);

    public static bool Eligible(Ff7WorldVisual entity, bool controlled, int heightSmoothVelocity)
    {
        return entity.Model is >= 0 and <= 2 && (entity.Flags & 2) != 0 && (entity.Flags & 8) == 0 && entity.GroundHeight is not null && entity.NativeAnimation is not (>= 2 and <= 5) && (!controlled || heightSmoothVelocity == 0) && (0x311B6F05u & (1u << (entity.Terrain & 31))) != 0;
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
        short halfSize = unchecked((short)(100 - (((int)position.Y - ground) >> 6)));
        Matrix4x4 rotation = math.Rotation(0, 0x800 + entity.Direction + cameraRoll * 4, 0);
        Vector3[] corners = [new(-halfSize, 0, -halfSize), new(halfSize, 0, -halfSize), new(-halfSize, 0, halfSize), new(halfSize, 0, halfSize)];
        Vector3[] camera = corners.Select(corner => Vector3.Transform(Vector3.Transform(corner, rotation) + new Vector3(position.X, unchecked((short)ground), position.Z), view.NativeView)).ToArray();
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
        Vector4 color = new(64 / 255f, 64 / 255f, 64 / 255f, 128 / 255f);
        Vector2[] uv = [new(24, 0), new(39, 0), new(24, 15), new(39, 15)];
        TriangleVertex Vertex(int index)
        {
            Vector3 point = camera[index];
            float x = view.Width / 2f + point.X * view.Focal / point.Z;
            float y = view.CenterY + point.Y * view.Focal / point.Z;
            Vector2 screen = new(MathF.Round(x, MidpointRounding.ToEven), MathF.Round(y, MidpointRounding.ToEven));
            return new(screen, depth, 1, uv[index] / new Vector2(texture.Width, texture.Height), color);
        }
        TriangleMaterial material = new(image, TriangleBlend.Alpha, TriangleDepthComparison.GreaterEqual, false, Sampler: TriangleSampler.PointWrap)
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
