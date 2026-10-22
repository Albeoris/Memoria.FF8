using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class WorldTerrainRenderer
{
    private PixelImage? gpuSkyTexture;
    private WorldSky gpuSky;
    private WorldSkyGradient? gpuSkyGradient;

    private void DrawGpuSky(WorldSky sky, WorldSkyGradient? nativeGradient)
    {
        ITriangleRenderer renderer = TriangleRenderer!;
        if (gpuSkyTexture is null || gpuSky != sky || gpuSkyGradient != nativeGradient)
        {
            gpuSkyTexture ??= new PixelImage(1, Height);
            Rgba32[] colors = gpuSkyTexture.Pixels;
            for (int y = 0; y < Height; y++)
            {
                float fraction = y / (float)Math.Max(1, Height - 1);
                Rgba32 first = fraction < 0.5f ? sky.Top : sky.Center;
                Rgba32 second = fraction < 0.5f ? sky.Center : sky.Bottom;
                float blend = fraction < 0.5f ? fraction * 2 : fraction * 2 - 1;
                colors[y] = nativeGradient is WorldSkyGradient gradient ? gradient.Sample((y + 0.5) * WorldSkyProjection.LogicalHeight / Height) : new Rgba32((byte)MathF.Round(first.R + (second.R - first.R) * blend), (byte)MathF.Round(first.G + (second.G - first.G) * blend), (byte)MathF.Round(first.B + (second.B - first.B) * blend), 255);
            }
            gpuSky = sky;
            gpuSkyGradient = nativeGradient;
        }
        TriangleMaterial material = new(gpuSkyTexture, TriangleBlend.Replace, TriangleDepthComparison.Always, false, Sampler: TriangleSampler.PointClamp)
        {
            DynamicTexture = true
        };
        TriangleVertex a = new(Vector2.Zero, 0, 1, Vector2.Zero, Vector4.One);
        TriangleVertex b = new(new Vector2(Width, 0), 0, 1, Vector2.UnitX, Vector4.One);
        TriangleVertex c = new(new Vector2(0, Height), 0, 1, Vector2.UnitY, Vector4.One);
        TriangleVertex d = new(new Vector2(Width, Height), 0, 1, Vector2.One, Vector4.One);
        renderer.Draw(a, b, c, material);
        renderer.Draw(c, b, d, material);
    }

    private void DrawGpuTriangle(ScreenVertex a, ScreenVertex b, ScreenVertex c, PixelImage texture, bool transparent)
    {
        static TriangleVertex Convert(ScreenVertex vertex)
        {
            return new TriangleVertex(new Vector2(vertex.X, vertex.Y), vertex.ReciprocalZ, vertex.ReciprocalZ, vertex.UvOverZ / vertex.ReciprocalZ, vertex.ColorOverZ / vertex.ReciprocalZ ?? Vector4.One);
        }
        TriangleMaterial material = TerrainMaterial(texture, transparent);
        TriangleRenderer!.Draw(Convert(a), Convert(b), Convert(c), material);
    }

    private TriangleMaterial TerrainMaterial(PixelImage texture, bool transparent)
    {
        return new TriangleMaterial(texture, transparent ? TriangleBlend.Alpha : TriangleBlend.Replace, TriangleDepthComparison.Greater, !transparent, Sampler: TriangleSampler.PointClamp)
        {
            AlphaMultiplier = transparent ? 0.5f : 1,
            DepthMapping = TriangleDepthMapping.Positive,
            DepthScale = 1,
            MinimumReciprocalW = 1 / far,
            FogColor = new Vector3(fog.R, fog.G, fog.B) / 255,
            FogStart = far * 0.6f,
            FogEnd = far
        };
    }
}
