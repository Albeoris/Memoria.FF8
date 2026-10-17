using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    // Verified signed entries atB80D1C/B80D9C for angles0,32,...224.
    private static readonly int[] ShadowSine = [0, 2896, 4096, 2896, 0, -2896, -4096, -2896];
    private static readonly int[] ShadowCosine = [4096, 2896, 0, -2896, -4096, -2896, 0, 2896];

    private void DrawActorShadow(ActorVisual actor)
    {
        IReadOnlyList<byte>? radii = actor.GroundShadowRadii601To608;
        if (radii is not null && (radii.Count != 8 || actor.GroundShadowRadius601To608 is not null))
        {
            throw new NotSupportedException("Native ground-shadow shape requires exactly bytes601..608 and one current shape authority.");
        }
        if (radii is null ? actor.GroundShadowRadius601To608 is not byte uniformRadius || uniformRadius == 0 : radii.All(radius => radius == 0))
        {
            // All eight native fan edges coincide with the center for radius0.
            return;
        }
        if (field.NativeExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || actor.GroundShadowLevel609 is not byte level || actor.GroundShadowCentre is not Vector3 body)
        {
            throw new NotSupportedException("Native ground-shadow geometry requires authenticated tables, body XYZ and exact BYTE609 authority.");
        }
        Vector3 center = new(unchecked((short)(int)body.X), unchecked((short)(int)body.Y), unchecked((short)(int)body.Z));
        ProjectedPoint middle = ProjectShadowPoint(center);
        Span<ProjectedPoint> edge = stackalloc ProjectedPoint[8];
        for (int index = 0; index < edge.Length; index++)
        {
            byte radius = radii is null ? actor.GroundShadowRadius601To608!.Value : radii[index];
            // This software backend supports native draw mode2 subtraction.
            // 472F20 uses full radii for that capability; its half-radius
            // fallback for other backends is a separate unsupported contract.
            Vector3 vertex = new(unchecked((short)((int)center.X + radius * ShadowSine[index] / 512)), unchecked((short)((int)center.Y + radius * ShadowCosine[index] / 512)), center.Z);
            edge[index] = ProjectShadowPoint(vertex);
        }
        // 56C910 returns45E250's final projected vertex depth>>2.
        float orderingDepth = MathF.Truncate(edge[7].Depth / 4) * 4;
        if (orderingDepth >= 4095 * 4 || middle.Depth <= 1)
        {
            return;
        }
        for (int index = 0; index < edge.Length; index++)
        {
            DrawShadowTriangle(middle, edge[index], edge[(index + 1) & 7], orderingDepth, level);
        }
    }

    private ProjectedPoint ProjectShadowPoint(Vector3 point)
    {
        return ToViewport(camera.Project(point));
    }

    private void DrawShadowTriangle(ProjectedPoint a, ProjectedPoint b, ProjectedPoint c, float orderingDepth, byte level)
    {
        if (b.Depth <= 1 || c.Depth <= 1)
        {
            throw new NotSupportedException("Native shadow near-plane clipping requires the original projected fan contract.");
        }
        float area = Edge(a, b, c.X, c.Y);
        if (Math.Abs(area) < 0.001f)
        {
            return;
        }
        if (TriangleRenderer is not null)
        {
            TriangleMaterial material = GpuMaterial(null, TriangleBlend.Subtract, true) with { DepthComparison = TriangleDepthComparison.Less };
            TriangleRenderer.Draw(GpuVertex(a, Vector2.Zero, new Vector4(new Vector3(level / 255f), 1), orderingDepth, false), GpuVertex(b, Vector2.Zero, new Vector4(0, 0, 0, 1), orderingDepth, false), GpuVertex(c, Vector2.Zero, new Vector4(0, 0, 0, 1), orderingDepth, false), material);
            return;
        }
        int minX = Math.Max((int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), clipLeft);
        int maxX = Math.Min((int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), clipRight - 1);
        int minY = Math.Max((int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), clipTop);
        int maxY = Math.Min((int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), clipBottom - 1);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float wA = Edge(b, c, x + 0.5f, y + 0.5f) / area;
                float wB = Edge(c, a, x + 0.5f, y + 0.5f) / area;
                float wC = 1 - wA - wB;
                // Half-open radial edges prevent a fan seam from subtracting twice.
                if (wA < 0 || wB < 0 || wC <= 0)
                {
                    continue;
                }
                int index = y * OutputWidth + x;
                if (orderingDepth >= depth[index])
                {
                    continue;
                }
                byte shade = (byte)Math.Clamp((int)(level * wA), 0, 255);
                WritePixel(index, new Rgba32(shade, shade, shade, 255), MapBlendMode.Subtract);
                // Preserve this primitive's ordering depth for foreground MAP
                // blending, while subsequent nearer model fragments still win.
                depth[index] = orderingDepth;
            }
        }
    }
}
