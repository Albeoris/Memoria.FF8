using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Rendering;

public sealed partial class WorldTerrainRenderer
{
    private void DrawWorldMapOverlay(WorldRenderFrame? frame)
    {
        if (frame is null || frame.MapDisplayMode == 0)
        {
            return;
        }
        Vector2 nativePosition = new(frame.Position.X, frame.Position.Z);
        IReadOnlyList<WorldMapLayer> layers = WorldMapPresentation.Create(frame.MapDisplayMode, nativePosition, frame.MapDisplayMode == 1 ? assets.ReadMapGlobeGeometry() : null);
        bool bodyMarker = frame.MapProcessMode is not (1 or 2 or 3 or 4);
        Matrix3x2 viewport = WorldMapPresentation.ViewportTransform(frame.MapDisplayMode, Width, Height);
        // The native ordering table draws the map before its two directional markers.
        foreach (WorldMapLayer layer in layers.Concat(WorldMapPresentation.Markers(frame.MapDisplayMode, nativePosition, frame.Heading, frame.Camera.Heading, frame.MapFrame, bodyMarker)))
        {
            DrawWorldScreenLayer(layer, viewport);
        }
    }

    private void DrawWorldScreenLayer(WorldMapLayer layer, Matrix3x2 viewport)
    {
        PixelImage texture = assets.ReadNativeSectionTexture(38, layer.TextureMember, layer.Palette);
        Matrix3x2 transform = viewport;
        foreach (WorldMapTriangle triangle in layer.Triangles)
        {
            WorldMapVertex a = triangle.A with { Position = Vector2.Transform(triangle.A.Position, transform) };
            WorldMapVertex b = triangle.B with { Position = Vector2.Transform(triangle.B.Position, transform) };
            WorldMapVertex c = triangle.C with { Position = Vector2.Transform(triangle.C.Position, transform) };
            if (TriangleRenderer is not null)
            {
                TriangleVertex Convert(WorldMapVertex vertex)
                {
                    return new(vertex.Position, 0, 1, vertex.Uv, vertex.Color);
                }
                TriangleMaterial material = new(texture, layer.Additive ? TriangleBlend.Add : layer.HalfTransparent ? TriangleBlend.HalfAdd : TriangleBlend.Replace, TriangleDepthComparison.Always, false, Sampler: layer.Wrap ? TriangleSampler.PointWrap : TriangleSampler.PointClamp)
                {
                    AlphaMultiplier = 1
                };
                TriangleRenderer.Draw(Convert(a), Convert(b), Convert(c), material);
            }
            else
            {
                RasterWorldMap(a, b, c, texture, layer);
            }
            DrawnTriangles++;
        }
    }

    private void RasterWorldMap(WorldMapVertex a, WorldMapVertex b, WorldMapVertex c, PixelImage texture, WorldMapLayer layer)
    {
        float area = Edge(a.Position.X, a.Position.Y, b.Position.X, b.Position.Y, c.Position.X, c.Position.Y);
        if (MathF.Abs(area) < 0.001f)
        {
            return;
        }
        int left = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Position.X, MathF.Min(b.Position.X, c.Position.X))));
        int right = Math.Min(Width - 1, (int)MathF.Ceiling(MathF.Max(a.Position.X, MathF.Max(b.Position.X, c.Position.X))));
        int top = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Position.Y, MathF.Min(b.Position.Y, c.Position.Y))));
        int bottom = Math.Min(Height - 1, (int)MathF.Ceiling(MathF.Max(a.Position.Y, MathF.Max(b.Position.Y, c.Position.Y))));
        bool IncludesEdge(Vector2 start, Vector2 end)
        {
            Vector2 delta = (end - start) * (area < 0 ? 1 : -1);
            return delta.Y < 0 || delta.Y == 0 && delta.X > 0;
        }
        bool includeA = IncludesEdge(b.Position, c.Position);
        bool includeB = IncludesEdge(c.Position, a.Position);
        bool includeC = IncludesEdge(a.Position, b.Position);
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float wa = Edge(b.Position.X, b.Position.Y, c.Position.X, c.Position.Y, x + 0.5f, y + 0.5f) / area;
                float wb = Edge(c.Position.X, c.Position.Y, a.Position.X, a.Position.Y, x + 0.5f, y + 0.5f) / area;
                float wc = 1 - wa - wb;
                if (wa < 0 || wb < 0 || wc < 0 || wa == 0 && !includeA || wb == 0 && !includeB || wc == 0 && !includeC)
                {
                    continue;
                }
                Vector2 uv = a.Uv * wa + b.Uv * wb + c.Uv * wc;
                if (layer.Wrap)
                {
                    uv = new(uv.X - MathF.Floor(uv.X), uv.Y - MathF.Floor(uv.Y));
                }
                int u = Math.Clamp((int)(uv.X * texture.Width), 0, texture.Width - 1);
                int v = Math.Clamp((int)(uv.Y * texture.Height), 0, texture.Height - 1);
                Rgba32 texel = texture.Pixels[v * texture.Width + u];
                if (texel.A == 0)
                {
                    continue;
                }
                Vector4 tint = a.Color * wa + b.Color * wb + c.Color * wc;
                int offset = (y * Width + x) * 4;
                float opacity = layer.HalfTransparent && !layer.Additive ? 0.5f : 1;
                byte Channel(int channel, byte source, float multiplier)
                {
                    float value = Math.Clamp(source * multiplier, 0, 255);
                    return layer.Additive ? (byte)Math.Clamp(pixels[offset + channel] + value, 0, 255) : Blend(pixels[offset + channel], value, opacity);
                }
                pixels[offset] = Channel(0, texel.R, tint.X);
                pixels[offset + 1] = Channel(1, texel.G, tint.Y);
                pixels[offset + 2] = Channel(2, texel.B, tint.Z);
            }
        }
    }
}
