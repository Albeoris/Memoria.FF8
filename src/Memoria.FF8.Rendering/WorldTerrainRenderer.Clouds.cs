using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class WorldTerrainRenderer
{
    private void DrawWorldClouds(WorldCamera camera, Vector3 position, WorldSky sky, byte processMode)
    {
        if (!camera.NativeProjection || processMode == 12)
        {
            return;
        }
        WorldSkyGradient gradient = WorldSkyProjection.Native(camera, position, sky);
        int nativeYaw = (int)MathF.Round(camera.Heading * 4096 / MathF.Tau);
        float scale = Height / 224f;
        float offset = (Width - 320 * scale) / 2;
        Matrix3x2 viewport = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offset, 0);
        DrawWorldStars(nativeYaw, gradient, sky.NativeStarsEnabled, processMode, viewport, -offset / scale, (Width - offset) / scale);
        foreach (WorldMapLayer layer in WorldCloudPresentation.Create(nativeYaw, gradient, processMode, -offset / scale, (Width - offset) / scale))
        {
            DrawWorldScreenLayer(layer, viewport);
        }
    }

    private void DrawWorldStars(int nativeYaw, WorldSkyGradient gradient, bool enabled, byte processMode, Matrix3x2 viewport, float left, float right)
    {
        if (!enabled)
        {
            return;
        }
        uint counter = nativeMainLoopCounter ?? throw new NotSupportedException("Native sky points require the retained main-loop counter.");
        foreach (WorldStarPoint star in WorldStarPresentation.Create(nativeYaw, gradient, counter, assets.ForestTables.Random, true, processMode, left, right))
        {
            Vector2 a = Vector2.Transform(star.Position, viewport);
            Vector2 d = Vector2.Transform(star.Position + Vector2.One, viewport);
            if (TriangleRenderer is not null)
            {
                Vector4 color = new(star.Color.R / 255f, star.Color.G / 255f, star.Color.B / 255f, 1);
                TriangleVertex first = new(a, 0, 1, Vector2.Zero, color);
                TriangleVertex second = new(new Vector2(d.X, a.Y), 0, 1, Vector2.Zero, color);
                TriangleVertex third = new(new Vector2(a.X, d.Y), 0, 1, Vector2.Zero, color);
                TriangleVertex fourth = new(d, 0, 1, Vector2.Zero, color);
                TriangleMaterial material = new(null, TriangleBlend.Replace, TriangleDepthComparison.Always, false);
                TriangleRenderer.Draw(first, second, third, material);
                TriangleRenderer.Draw(third, second, fourth, material);
                DrawnTriangles += 2;
            }
            else
            {
                for (int y = Math.Max(0, (int)MathF.Ceiling(a.Y - 0.5f)); y < Math.Min(Height, (int)MathF.Ceiling(d.Y - 0.5f)); y++)
                {
                    for (int x = Math.Max(0, (int)MathF.Ceiling(a.X - 0.5f)); x < Math.Min(Width, (int)MathF.Ceiling(d.X - 0.5f)); x++)
                    {
                        int index = (y * Width + x) * 4;
                        pixels[index] = star.Color.R;
                        pixels[index + 1] = star.Color.G;
                        pixels[index + 2] = star.Color.B;
                        pixels[index + 3] = 255;
                    }
                }
            }
        }
    }
}
