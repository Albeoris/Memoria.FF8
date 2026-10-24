using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuModelMultisampleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void TextureCentroidDoesNotMoveDepthOcclusionOrFog(int samples)
    {
        using GpuTestDevice device = new(samples);
        using GpuTriangleRenderer gpu = new(device.Device);
        PixelImage texture = new(8, 1, Enumerable.Repeat(new Rgba32(200, 120, 80, 255), 8).ToArray());
        Color[] Render(bool centroid, bool reverse)
        {
            gpu.Begin(64, 64, new Vector4(0, 0, 1, 1));
            TriangleMaterial model = new(texture, DepthComparison: TriangleDepthComparison.Less, Sampler: TriangleSampler.CutoutLinearClamp)
            {
                PerspectiveDepth = true,
                DepthMapping = TriangleDepthMapping.Positive,
                CentroidTextureSampling = centroid,
                FogStart = 128,
                FogEnd = 260,
                FogColor = Vector3.One
            };
            void Occluder()
            {
                TriangleMaterial material = new(null) { DepthMapping = TriangleDepthMapping.Positive };
                TriangleVertex Vertex(float x, float y) => new(new Vector2(x, y), 190, 1, Vector2.Zero, new Vector4(0, 1, 0, 1));
                gpu.Draw(Vertex(0, 0), Vertex(64, 0), Vertex(0, 64), material);
                gpu.Draw(Vertex(64, 0), Vertex(64, 64), Vertex(0, 64), material);
            }
            if (!reverse)
            {
                Occluder();
            }
            Face(gpu, model, false);
            Face(gpu, model, true);
            if (reverse)
            {
                Occluder();
            }
            gpu.End();
            return GpuTestDevice.Read(gpu.Target);
        }
        foreach (bool reverse in new[] { false, true })
        {
            Color[] expected = Render(false, reverse);
            Assert.Contains(expected, pixel => pixel == Color.Lime);
            Assert.Contains(expected, pixel => pixel.R > 100 && pixel.G > 0 && pixel.B > 0);
            Assert.Equal(expected, Render(true, reverse));
        }
    }

    [Theory]
    [InlineData(0, TriangleSampler.CutoutLinearClamp, false)]
    [InlineData(2, TriangleSampler.CutoutLinearClamp, false)]
    [InlineData(4, TriangleSampler.CutoutLinearClamp, false)]
    [InlineData(8, TriangleSampler.CutoutLinearClamp, false)]
    [InlineData(8, TriangleSampler.CutoutLinearClamp, true)]
    [InlineData(8, TriangleSampler.PointClamp, false)]
    [InlineData(8, TriangleSampler.PointClamp, true)]
    public void MirroredAtlasFacesDoNotSampleOutsideTheirCoveredSurface(int samples, TriangleSampler sampler, bool transparentNeighbor)
    {
        using GpuTestDevice device = new(samples);
        using GpuTriangleRenderer gpu = new(device.Device);
        Rgba32 skin = new(200, 120, 80, 255);
        PixelImage texture = new(8, 1, Enumerable.Range(0, 8).Select(index => index == 0 ? new Rgba32(0, 0, 0, transparentNeighbor ? (byte)0 : (byte)255) : skin).ToArray());
        TriangleMaterial material = new(texture, DepthComparison: TriangleDepthComparison.Less, Sampler: sampler) { PerspectiveDepth = true, DepthMapping = TriangleDepthMapping.Positive, CentroidTextureSampling = true };
        foreach (bool reverse in new[] { false, true })
        {
            gpu.Begin(64, 64, new Vector4(0, 0, 1, 1));
            foreach (bool right in reverse ? new[] { true, false } : new[] { false, true })
            {
                Face(gpu, material, right);
            }
            gpu.End();
            if (samples > 0)
            {
                Assert.True(gpu.Target.MultiSampleCount > 0, "MSAA must be active for this regression check.");
            }
            Color[] pixels = GpuTestDevice.Read(gpu.Target);
            for (int y = 4; y < 60; y++)
            {
                for (int x = 26; x < 39; x++)
                {
                    Color pixel = pixels[y * 64 + x];
                    Assert.True(Math.Abs(pixel.R - skin.R) <= 1 && Math.Abs(pixel.G - skin.G) <= 1 && Math.Abs(pixel.B - skin.B) <= 1, $"MSAA {samples}, {sampler}, transparent {transparentNeighbor}, ({x},{y}): {pixel}");
                }
            }
            if (samples == 0 || samples >= 4)
            {
                Assert.Equal(samples > 0, pixels.Any(pixel => pixel.R > 0 && pixel.R < skin.R));
            }
            Assert.Equal(Color.Blue, pixels[0]);
        }
    }

    private static void Face(GpuTriangleRenderer gpu, TriangleMaterial material, bool right)
    {
        float outer = right ? 61.25f : 2.25f;
        TriangleVertex Vertex(float x, float y, float u) => new(new Vector2(x, y), 256, 1f / (128 + x * 2), new Vector2(u / 8, 0), Vector4.One);
        TriangleVertex a = Vertex(outer, 2.25f, 3);
        TriangleVertex b = Vertex(30.25f, 2.25f, 1);
        TriangleVertex c = Vertex(outer, 61.25f, 3);
        TriangleVertex d = Vertex(34.25f, 61.25f, 1);
        gpu.Draw(a, b, c, material);
        gpu.Draw(b, d, c, material);
    }
}
