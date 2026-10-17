using System.Numerics;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuFrameFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbortedOrFailedFramesRetainTheLastCompletedSurfaceAndRestoreTheCaller(bool failDuringEnd)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        renderer.Begin(16, 12, new Vector4(1, 0, 0, 1));
        renderer.End();
        RenderTarget2D completed = renderer.Target;
        using RenderTarget2D outer = device.CreateTarget(24, 18);
        device.Device.SetRenderTarget(outer);
        renderer.Begin(32, 20, new Vector4(0, 1, 0, 1));
        Assert.Same(completed, renderer.Target);
        if (failDuringEnd)
        {
            TriangleVertex a = new(Vector2.Zero, 0.5f, 1, Vector2.Zero, Vector4.One);
            TriangleVertex b = a with { Position = new Vector2(32, 0) };
            TriangleVertex c = a with { Position = new Vector2(0, 20) };
            TriangleMaterial unsupported = new(null, (TriangleBlend)int.MaxValue);
            renderer.Draw(a, b, c, unsupported);
            Assert.Throws<NotSupportedException>(renderer.End);
        }
        else
        {
            renderer.Abort();
        }
        Assert.Same(outer, device.Device.GetRenderTargets()[0].RenderTarget);
        Assert.Equal(24, device.Device.Viewport.Width);
        Assert.Equal(18, device.Device.Viewport.Height);
        Assert.Same(completed, renderer.Target);
        Assert.Equal(16, renderer.Target.Width);
        Assert.Equal(12, renderer.Target.Height);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Red, pixel));
        device.Device.SetRenderTarget(outer);
        renderer.Begin(32, 20, new Vector4(0, 0, 1, 1));
        renderer.End();
        Assert.Same(outer, device.Device.GetRenderTargets()[0].RenderTarget);
        Assert.NotSame(completed, renderer.Target);
        Assert.Equal(32, renderer.Target.Width);
        Assert.Equal(20, renderer.Target.Height);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Blue, pixel));
    }
}
