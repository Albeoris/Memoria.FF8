using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class WorldTerrainRenderer
{
    private void DrawWorldShadows(IReadOnlyList<NativeWorldShadowDraw>? shadows, List<(WorldSegment Segment, Vector3 Offset)>? receivers)
    {
        if (shadows is null)
        {
            return;
        }
        foreach (NativeWorldShadowDraw shadow in shadows)
        {
            WorldShadowGeometry geometry = WorldShadowPresentation.Create(shadow, assets.ReadNativeContactShape(shadow.ShapeId));
            PixelImage texture = assets.ReadNativeSectionTexture(38, geometry.TextureMember);
            if (shadow.ShapeId == 1 && receivers is not null)
            {
                DrawTerrainShadow(geometry, texture, receivers);
                continue;
            }
            foreach (WorldShadowTriangle triangle in geometry.Triangles)
            {
                Vertex Convert(WorldShadowVertex vertex)
                {
                    return new(vertex.Position / 16, vertex.Uv);
                }
                Draw(Convert(triangle.A), Convert(triangle.B), Convert(triangle.C), texture, true, TriangleBlend.Subtract);
            }
        }
    }

    private void DrawTerrainShadow(WorldShadowGeometry geometry, PixelImage texture, List<(WorldSegment Segment, Vector3 Offset)> receivers)
    {
        // The allocation supplies the footprint, but its selected ground is only one
        // height sample. Clip the footprint to each opaque receiver in the X/Z plane.
        foreach (WorldShadowTriangle footprint in geometry.Triangles)
        {
            Vector2 a = Horizontal(footprint.A.Position / 16);
            Vector2 b = Horizontal(footprint.B.Position / 16);
            Vector2 c = Horizontal(footprint.C.Position / 16);
            Vector2 minimum = Vector2.Min(a, Vector2.Min(b, c));
            Vector2 maximum = Vector2.Max(a, Vector2.Max(b, c));
            foreach ((WorldSegment segment, Vector3 offset) in receivers)
            {
                foreach (WorldTriangle receiver in segment.Triangles)
                {
                    if (!receiver.Transparent)
                    {
                        DrawTerrainShadowTriangle(footprint, receiver, offset, a, b, c, minimum, maximum, texture);
                    }
                }
            }
        }
    }

    private void DrawTerrainShadowTriangle(WorldShadowTriangle footprint, WorldTriangle receiver, Vector3 offset, Vector2 a, Vector2 b, Vector2 c, Vector2 minimum, Vector2 maximum, PixelImage texture)
    {
        Vector2 p = Horizontal(receiver.A / 16 + offset);
        Vector2 q = Horizontal(receiver.B / 16 + offset);
        Vector2 r = Horizontal(receiver.C / 16 + offset);
        Vector2 receiverMinimum = Vector2.Min(p, Vector2.Min(q, r));
        Vector2 receiverMaximum = Vector2.Max(p, Vector2.Max(q, r));
        if (receiverMinimum.X >= maximum.X || receiverMaximum.X <= minimum.X || receiverMinimum.Y >= maximum.Y || receiverMaximum.Y <= minimum.Y)
        {
            return;
        }
        float area = Cross(q - p, r - p);
        if (area == 0)
        {
            return;
        }

        Span<Vector2> input = stackalloc Vector2[6];
        Span<Vector2> output = stackalloc Vector2[6];
        input[0] = a;
        input[1] = b;
        input[2] = c;
        int count = ClipShadow(input[..3], output, p, q, MathF.Sign(area));
        count = ClipShadow(output[..count], input, q, r, MathF.Sign(area));
        count = ClipShadow(input[..count], output, r, p, MathF.Sign(area));
        if (count < 3)
        {
            return;
        }

        Vector3 first = TerrainPoint(receiver.A, offset);
        Vector3 second = TerrainPoint(receiver.B, offset);
        Vector3 third = TerrainPoint(receiver.C, offset);
        float footprintArea = Cross(b - a, c - a);
        Vertex Project(Vector2 point)
        {
            Vector2 local = point - p;
            float u = Cross(local, r - p) / area;
            float v = Cross(q - p, local) / area;
            // Interpolate the already curved terrain plane and lift it slightly for depth precision.
            float height = first.Y + (second.Y - first.Y) * u + (third.Y - first.Y) * v + 1f / 256;
            local = point - a;
            u = Cross(local, c - a) / footprintArea;
            v = Cross(b - a, local) / footprintArea;
            Vector2 uv = footprint.A.Uv + (footprint.B.Uv - footprint.A.Uv) * u + (footprint.C.Uv - footprint.A.Uv) * v;
            return new(new Vector3(point.X, height, point.Y), uv);
        }

        Vertex origin = Project(output[0]);
        for (int index = 2; index < count; index++)
        {
            Draw(origin, Project(output[index - 1]), Project(output[index]), texture, true, TriangleBlend.Subtract);
        }
    }

    private static int ClipShadow(ReadOnlySpan<Vector2> input, Span<Vector2> output, Vector2 start, Vector2 end, float winding)
    {
        if (input.IsEmpty)
        {
            return 0;
        }
        int count = 0;
        Vector2 edge = end - start;
        Vector2 previous = input[^1];
        float previousSide = winding * Cross(edge, previous - start);
        foreach (Vector2 current in input)
        {
            float side = winding * Cross(edge, current - start);
            if ((previousSide < 0 && side > 0) || (previousSide > 0 && side < 0))
            {
                output[count++] = Vector2.Lerp(previous, current, previousSide / (previousSide - side));
            }
            if (side >= 0)
            {
                output[count++] = current;
            }
            previous = current;
            previousSide = side;
        }
        return count;
    }

    private static Vector2 Horizontal(Vector3 point)
    {
        return new(point.X, point.Z);
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.X * second.Y - first.Y * second.X;
    }
}
