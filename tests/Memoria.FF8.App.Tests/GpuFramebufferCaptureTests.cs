using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuFramebufferCaptureTests
{
    [Theory]
    [InlineData(32, 24, 0, 0, 32, 24, 16, 12, 0)]
    [InlineData(31, 23, 0, 0, 31, 23, 17, 13, 0)]
    [InlineData(31, 23, 3, 5, 27, 17, 16, 9, 0)]
    [InlineData(31, 23, 30, 22, 1, 1, 7, 5, 0)]
    [InlineData(31, 23, 1, 2, 29, 19, 13, 11, 4)]
    public void NativeSizedCaptureMatchesFullReadbackCentresByteForByte(int width, int height, int x, int y, int cropWidth, int cropHeight, int destinationWidth, int destinationHeight, int samples)
    {
        using GpuTestDevice device = new(samples);
        using GpuTriangleRenderer renderer = new(device.Device);
        renderer.Begin(width, height, new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        if (samples != 0)
        {
            Assert.SkipUnless(((RenderTarget2D)device.Device.GetRenderTargets()[0].RenderTarget).MultiSampleCount > 0, "The graphics device does not expose the requested multisampled source.");
        }
        DrawPattern(renderer, width, height);
        Assert.True(renderer.TryCaptureArgb(out uint[] full));
        RenderTargetBinding[] targets = device.Device.GetRenderTargets();
        Microsoft.Xna.Framework.Graphics.Viewport viewport = device.Device.Viewport;
        Rectangle scissor = new(1, 1, width - 2, height - 2);
        device.Device.ScissorRectangle = scissor;
        device.Device.BlendState = BlendState.NonPremultiplied;
        device.Device.DepthStencilState = DepthStencilState.DepthRead;
        device.Device.SamplerStates[0] = SamplerState.LinearWrap;
        IndexBuffer? indices = device.Device.Indices;
        Texture? texture = device.Device.Textures[0];

        Assert.True(renderer.TryCaptureArgb(width, height, new(x, y, cropWidth, cropHeight), destinationWidth, destinationHeight, out uint[] actual));
        Assert.Equal(Reference(full, width, new(x, y, cropWidth, cropHeight), destinationWidth, destinationHeight), actual);
        Assert.Same(targets[0].RenderTarget, device.Device.GetRenderTargets()[0].RenderTarget);
        Assert.Equal(viewport, device.Device.Viewport);
        Assert.Equal(scissor, device.Device.ScissorRectangle);
        Assert.Same(BlendState.NonPremultiplied, device.Device.BlendState);
        Assert.Same(DepthStencilState.DepthRead, device.Device.DepthStencilState);
        Assert.Same(SamplerState.LinearWrap, device.Device.SamplerStates[0]);
        Assert.Same(texture, device.Device.Textures[0]);
        Assert.Same(indices, device.Device.Indices);

        DrawSolid(renderer, width, height, new(0, 1, 0, 1));
        Assert.True(renderer.TryCaptureArgb(out uint[] next));
        Assert.All(next, pixel => Assert.Equal(0xFF00FF00u, pixel));
        renderer.End();
    }

    [Fact]
    public void NativeSurfaceResizeReplacesOnlyItsOwnedTargetAndKeepsEarlierOutputsIndependent()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        FieldInfo captureTarget = typeof(GpuTriangleRenderer).GetField("centreCaptureTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        renderer.Begin(16, 12, new Vector4(1, 0, 0, 1));
        Assert.True(renderer.TryCaptureArgb(16, 12, new(0, 0, 16, 12), 8, 6, out uint[] first));
        RenderTarget2D original = Assert.IsType<RenderTarget2D>(captureTarget.GetValue(renderer));
        Assert.True(renderer.TryCaptureArgb(16, 12, new(1, 1, 14, 10), 3, 5, out uint[] resized));
        Assert.True(original.IsDisposed);
        Assert.NotSame(first, resized);
        Assert.Equal(15, resized.Length);
        renderer.End();
        renderer.Begin(19, 13, new Vector4(0, 0, 1, 1));
        Assert.True(renderer.TryCaptureArgb(19, 13, new(2, 3, 17, 10), 5, 7, out uint[] next));
        Assert.All(first, pixel => Assert.Equal(0xFFFF0000u, pixel));
        Assert.All(resized, pixel => Assert.Equal(0xFFFF0000u, pixel));
        Assert.All(next, pixel => Assert.Equal(0xFF0000FFu, pixel));
        renderer.End();
    }

    [Fact]
    public void InvalidDimensionsAndDisposedResampleFailureRestoreTheActiveSource()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        renderer.Begin(16, 12, new Vector4(1, 0, 0, 1));
        Assert.True(renderer.TryCaptureArgb(16, 12, new(0, 0, 16, 12), 8, 6, out uint[] first));
        Assert.All(first, pixel => Assert.Equal(0xFFFF0000u, pixel));
        RenderTarget2D source = (RenderTarget2D)device.Device.GetRenderTargets()[0].RenderTarget;
        Assert.Throws<InvalidDataException>(() => renderer.TryCaptureArgb(15, 12, new(0, 0, 15, 12), 8, 6, out _));
        FieldInfo captureTarget = typeof(GpuTriangleRenderer).GetField("centreCaptureTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsType<RenderTarget2D>(captureTarget.GetValue(renderer)).Dispose();
        // This injected disposed-resource failure propagates from MonoGame; it is not a failed framebuffer lock.
        Assert.Throws<NullReferenceException>(() => renderer.TryCaptureArgb(16, 12, new(0, 0, 16, 12), 8, 6, out _));
        Assert.Same(source, device.Device.GetRenderTargets()[0].RenderTarget);
        DrawSolid(renderer, 16, 12, new(0, 0, 1, 1));
        Assert.True(renderer.TryCaptureArgb(out uint[] next));
        Assert.All(next, pixel => Assert.Equal(0xFF0000FFu, pixel));
        Assert.All(first, pixel => Assert.Equal(0xFFFF0000u, pixel));
        renderer.End();
    }

    private static uint[] Reference(uint[] source, int sourceWidth, PixelBounds crop, int width, int height)
    {
        uint[] result = new uint[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sourceX = crop.X + (int)((2L * x + 1) * crop.Width / (2L * width));
                int sourceY = crop.Y + (int)((2L * y + 1) * crop.Height / (2L * height));
                result[y * width + x] = source[sourceY * sourceWidth + sourceX];
            }
        }
        return result;
    }

    private static void DrawPattern(GpuTriangleRenderer renderer, int width, int height)
    {
        PixelImage image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = new(unchecked((byte)(x * 37 + y * 11)), unchecked((byte)(x * 17 + y * 53)), unchecked((byte)(x * 71 + y * 3)), unchecked((byte)(x * 13 + y * 29)));
            }
        }
        Quad(renderer, width, height, new(image, MinimumAlpha: 0) { PreserveTargetAlpha = false }, Vector4.One, 0.8f);
        TriangleVertex a = new(new(0.3f, 0.7f), 0.4f, 1, Vector2.Zero, new(0.17f, 0.66f, 0.43f, 0.37f));
        renderer.Draw(a, a with { Position = new(width - 0.2f, 4.4f) }, a with { Position = new(5.8f, height - 0.2f) }, new(null) { PreserveTargetAlpha = false });
    }

    private static void DrawSolid(GpuTriangleRenderer renderer, int width, int height, Vector4 color) => Quad(renderer, width, height, new(null) { PreserveTargetAlpha = false }, color, 0.1f);

    private static void Quad(GpuTriangleRenderer renderer, int width, int height, TriangleMaterial material, Vector4 color, float depth)
    {
        TriangleVertex a = new(Vector2.Zero, depth, 1, Vector2.Zero, color);
        TriangleVertex b = a with { Position = new(width, 0), Uv = Vector2.UnitX };
        TriangleVertex c = a with { Position = new(0, height), Uv = Vector2.UnitY };
        TriangleVertex d = a with { Position = new(width, height), Uv = Vector2.One };
        renderer.Draw(a, b, c, material);
        renderer.Draw(b, d, c, material);
    }
}
