using System.Numerics;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuPositiveDepthRenderingTests
{
    [Theory]
    [InlineData(100000f)]
    [InlineData(200000f)]
    public void Large_positive_depths_render_and_distinguish_half_unit_ordering(float nearDepth)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        gpu.Begin(16, 16, new Vector4(0, 0, 0, 1));
        Quad(nearDepth + 0.5f, new Vector4(1, 0, 0, 1));
        Quad(nearDepth, new Vector4(0, 1, 0, 1));
        Quad(nearDepth + 0.5f, new Vector4(0, 0, 1, 1));
        gpu.End();
        Assert.All(GpuTestDevice.Read(gpu.Target), pixel => Assert.Equal(Color.Lime, pixel));

        void Quad(float depth, Vector4 color)
        {
            TriangleMaterial material = new(null, DepthComparison: TriangleDepthComparison.Less)
            {
                DepthMapping = TriangleDepthMapping.Positive
            };
            TriangleVertex a = new(Vector2.Zero, depth, 1, Vector2.Zero, color);
            TriangleVertex b = new(new Vector2(16, 0), depth, 1, Vector2.UnitX, color);
            TriangleVertex c = new(new Vector2(0, 16), depth, 1, Vector2.UnitY, color);
            TriangleVertex d = new(new Vector2(16, 16), depth, 1, Vector2.One, color);
            gpu.Draw(a, b, c, material);
            gpu.Draw(b, d, c, material);
        }
    }
}
