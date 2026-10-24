using System.Numerics;
using System.Runtime.CompilerServices;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class WorldTerrainRenderer
{
    private readonly ConditionalWeakTable<WorldSegment, TerrainGeometry> terrainGeometry = new();

    private sealed record TerrainRun(WorldTriangle TextureSource, int Start, int Count, Vector3 Minimum, Vector3 Maximum);

    private sealed class TerrainGeometry
    {
        public readonly List<TerrainRun> Opaque = [];
        public readonly List<WorldTriangle> Transparent = [];
        public readonly WorldTerrainMesh Mesh;
        public Vector3 Minimum = new(float.PositiveInfinity);
        public Vector3 Maximum = new(float.NegativeInfinity);

        public TerrainGeometry(WorldSegment segment)
        {
            List<WorldTerrainVertex> vertices = [];
            WorldTriangle? source = null;
            int runStart = 0;
            Vector3 runMinimum = new(float.PositiveInfinity);
            Vector3 runMaximum = new(float.NegativeInfinity);
            foreach (WorldTriangle triangle in segment.Triangles)
            {
                Minimum = Vector3.Min(Minimum, Vector3.Min(triangle.A, Vector3.Min(triangle.B, triangle.C)) / 16);
                Maximum = Vector3.Max(Maximum, Vector3.Max(triangle.A, Vector3.Max(triangle.B, triangle.C)) / 16);
                if (triangle.Transparent)
                {
                    Transparent.Add(triangle);
                    continue;
                }
                if (source is not null && !SameTexture(source, triangle))
                {
                    Opaque.Add(new TerrainRun(source, runStart, vertices.Count - runStart, runMinimum, runMaximum));
                    runStart = vertices.Count;
                    runMinimum = new Vector3(float.PositiveInfinity);
                    runMaximum = new Vector3(float.NegativeInfinity);
                }
                source = triangle;
                runMinimum = Vector3.Min(runMinimum, Vector3.Min(triangle.A, Vector3.Min(triangle.B, triangle.C)) / 16);
                runMaximum = Vector3.Max(runMaximum, Vector3.Max(triangle.A, Vector3.Max(triangle.B, triangle.C)) / 16);
                // Road polygons also carry the water bit, but use the full-height road atlas.
                Vector2 scale = triangle.Water && !triangle.Road ? new Vector2(1, 2) : Vector2.One;
                vertices.Add(new WorldTerrainVertex(triangle.A / 16, triangle.UvA * scale));
                vertices.Add(new WorldTerrainVertex(triangle.B / 16, triangle.UvB * scale));
                vertices.Add(new WorldTerrainVertex(triangle.C / 16, triangle.UvC * scale));
            }
            if (source is not null)
            {
                Opaque.Add(new TerrainRun(source, runStart, vertices.Count - runStart, runMinimum, runMaximum));
            }
            Mesh = new WorldTerrainMesh(vertices.ToArray());
        }

        private static bool SameTexture(WorldTriangle first, WorldTriangle second)
        {
            if (first.Road || second.Road)
            {
                return first.Road && second.Road;
            }
            if (first.Water || second.Water)
            {
                return first.Water && second.Water;
            }
            return first.TexturePage == second.TexturePage && first.Palette == second.Palette;
        }
    }

    private void DrawRetainedTerrain(IWorldTerrainRenderer hardware, WorldSegment segment, Vector3 offset, List<(WorldTriangle Triangle, Vector3 Offset)> transparent)
    {
        TerrainGeometry geometry = terrainGeometry.GetValue(segment, static source => new TerrainGeometry(source));
        if (!TerrainVisible(geometry.Minimum + offset, geometry.Maximum + offset))
        {
            return;
        }
        WorldTerrainView camera = new(view, offset, focal, nativeTerrain);
        foreach (TerrainRun run in geometry.Opaque)
        {
            if (!TerrainVisible(run.Minimum + offset, run.Maximum + offset))
            {
                continue;
            }
            // Resolve current resource handles each frame; geometry survives texture replacement.
            TriangleMaterial material = TerrainMaterial(assets.GetTexture(run.TextureSource, terrainReplacementMask), false);
            hardware.DrawTerrain(geometry.Mesh, camera, material, run.Start, run.Count);
            DrawnTriangles += run.Count / 3;
        }
        foreach (WorldTriangle triangle in geometry.Transparent)
        {
            transparent.Add((triangle, offset));
        }
    }

    private bool TerrainVisible(Vector3 minimum, Vector3 maximum)
    {
        if (minimum.X > maximum.X)
        {
            return false;
        }
        if (nativeTerrain)
        {
            // Curvature depends on the uncurved camera depth. Bound the integer expression
            // over the entire segment before testing its curved box against the frustum.
            float depthMinimum = float.PositiveInfinity;
            float depthMaximum = float.NegativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = BoxCorner(minimum, maximum, corner);
                float depth = -Vector3.Transform(point, view).Z;
                depthMinimum = MathF.Min(depthMinimum, depth);
                depthMaximum = MathF.Max(depthMaximum, depth);
            }
            int last = (Math.Clamp((int)MathF.Round(depthMaximum * 16), 0, 65535) >> 2) - 5216;
            if (last >= 0)
            {
                int first = Math.Max(0, (Math.Clamp((int)MathF.Round(depthMinimum * 16), 0, 65535) >> 2) - 5216);
                int nativeMinimum = -(int)(maximum.Y * 16);
                int nativeMaximum = -(int)(minimum.Y * 16);
                int a = (4096 - first) * nativeMinimum;
                int b = (4096 - first) * nativeMaximum;
                int c = (4096 - last) * nativeMinimum;
                int d = (4096 - last) * nativeMaximum;
                int heightMinimum = (first >> 6) * (first >> 6) + (Math.Min(Math.Min(a, b), Math.Min(c, d)) >> 12);
                int heightMaximum = (last >> 6) * (last >> 6) + (Math.Max(Math.Max(a, b), Math.Max(c, d)) >> 12);
                float curvedMinimum = heightMinimum < short.MinValue || heightMaximum > short.MaxValue ? -short.MaxValue / 16f : -heightMaximum / 16f;
                float curvedMaximum = heightMinimum < short.MinValue || heightMaximum > short.MaxValue ? -short.MinValue / 16f : -heightMinimum / 16f;
                minimum.Y = MathF.Min(minimum.Y, curvedMinimum);
                maximum.Y = MathF.Max(maximum.Y, curvedMaximum);
            }
        }
        int outside = 63;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 camera = Vector3.Transform(BoxCorner(minimum, maximum, corner), view);
            float z = -camera.Z;
            float x = camera.X * focal;
            float y = camera.Y * focal;
            int planes = (z < 1 ? 1 : 0) | (z > far ? 2 : 0) | (x < -Width / 2f * z ? 4 : 0) | (x > Width / 2f * z ? 8 : 0) | (y < -Height / 2f * z ? 16 : 0) | (y > Height / 2f * z ? 32 : 0);
            outside &= planes;
        }
        return outside == 0;
    }

    private static Vector3 BoxCorner(Vector3 minimum, Vector3 maximum, int corner)
    {
        return new Vector3((corner & 1) == 0 ? minimum.X : maximum.X, (corner & 2) == 0 ? minimum.Y : maximum.Y, (corner & 4) == 0 ? minimum.Z : maximum.Z);
    }
}
