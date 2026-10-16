using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private void PrepareDrawPointParticles(NativeDrawPointParticles? particles, NativeDrawPointParticleTables? tables, ushort? displayMask, List<(float Depth, Action Draw)> translucent)
    {
        if (particles is null)
        {
            return;
        }
        particles.Validate();
        foreach (NativeDrawPointSpark spark in particles.Drawings)
        {
            if (spark.RenderStatus == 1 && displayMask is null)
            {
                throw new NotSupportedException("Native draw-point drawing requires the current physical display word.");
            }
            if (spark.RenderStatus != 2 && (spark.RenderStatus != 1 || displayMask != 1))
            {
                continue;
            }
            if (tables is null)
            {
                throw new NotSupportedException("Native draw-point drawing requires its current display word and authenticated trail colors.");
            }
            for (int age = 0; age < 5; age++)
            {
                int current = (spark.RenderCounter - age) & 7;
                int previous = (current - 1) & 7;
                ProjectedPoint a = ToViewport(camera.Project(spark.FirstEdge[current].Vector));
                ProjectedPoint b = ToViewport(camera.Project(spark.SecondEdge[current].Vector));
                ProjectedPoint c = ToViewport(camera.Project(spark.FirstEdge[previous].Vector));
                ProjectedPoint d = ToViewport(camera.Project(spark.SecondEdge[previous].Vector));
                float orderingDepth = MathF.Truncate((age == 0 ? c.Depth : d.Depth) / 4) * 4;
                if (orderingDepth >= 4096 * 4 || a.Depth <= 1 || b.Depth <= 1 || c.Depth <= 1 || d.Depth <= 1)
                {
                    continue;
                }
                Vector3 head = age == 0 ? new Vector3(128) : tables.Color(particles.Palette, age);
                Vector3 tail = tables.Color(particles.Palette, age + 1);
                translucent.Add((orderingDepth, () =>
                {
                    //474D90 emits five untextured Gouraud quads, draw mode1.
                    DrawSparkTriangle(a, b, c, head, head, tail, orderingDepth);
                    DrawSparkTriangle(b, d, c, head, tail, tail, orderingDepth);
                }));
            }
        }
    }

    private void DrawSparkTriangle(ProjectedPoint a, ProjectedPoint b, ProjectedPoint c, Vector3 colorA, Vector3 colorB, Vector3 colorC, float orderingDepth)
    {
        float area = Edge(a, b, c.X, c.Y);
        if (Math.Abs(area) < 0.001f)
        {
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
                float wC = Edge(a, b, x + 0.5f, y + 0.5f) / area;
                if (wA < 0 || wB < 0 || wC < 0)
                {
                    continue;
                }
                if ((wA == 0 && !OwnsRasterEdge(b, c, area)) || (wB == 0 && !OwnsRasterEdge(c, a, area)) || (wC == 0 && !OwnsRasterEdge(a, b, area)))
                {
                    continue;
                }
                int index = y * OutputWidth + x;
                if (orderingDepth >= depth[index])
                {
                    continue;
                }
                Vector3 color = Vector3.Clamp(colorA * wA + colorB * wB + colorC * wC, Vector3.Zero, new Vector3(255));
                WritePixel(index, new Rgba32((byte)color.X, (byte)color.Y, (byte)color.Z, 255), MapBlendMode.Add);
            }
        }
    }
}
