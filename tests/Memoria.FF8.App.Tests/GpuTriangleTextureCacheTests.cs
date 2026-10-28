using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuTriangleTextureCacheTests
{
    [Theory]
    [InlineData(2, 1024)]
    [InlineData(8, 8)]
    public void IntermittentImagesSurviveEmptyFramesAndHitsPromoteBeforeEntryOrByteEviction(int entries, long bytes)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device, entries, bytes);
        PixelImage first = Image(255, 0, 0);
        PixelImage second = Image(0, 255, 0);
        PixelImage third = Image(0, 0, 255);

        Frame(renderer, new TriangleMaterial(first), new(second));
        Assert.Equal(2, renderer.FrameTextureUploads);
        Frame(renderer, new TriangleMaterial(first));
        Assert.Equal(0, renderer.FrameTextureUploads);
        Frame(renderer);
        Frame(renderer, new TriangleMaterial(third));
        Assert.Equal(1, renderer.FrameTextureUploads);
        Frame(renderer, new TriangleMaterial(first));
        Assert.Equal(0, renderer.FrameTextureUploads);
        Frame(renderer, new TriangleMaterial(second));
        Assert.Equal(1, renderer.FrameTextureUploads);
    }

    [Fact]
    public void OversizedImageRendersBetweenCachedBatchesWithoutRetentionOrEvictingTheCachedImage()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device, 2, 4);
        PixelImage cached = Image(255, 0, 0);
        PixelImage oversized = new(2, 1, [new Rgba32(0, 0, 255, 255), new Rgba32(0, 0, 255, 255)]);

        Frame(renderer, new TriangleMaterial(cached), new(oversized), new(cached));
        Assert.Equal(2, renderer.FrameTextureUploads);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Red, pixel));
        Frame(renderer, new TriangleMaterial(oversized));
        Assert.Equal(1, renderer.FrameTextureUploads);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Blue, pixel));
        Frame(renderer, new TriangleMaterial(oversized), new(null));
        Assert.Equal(1, renderer.FrameTextureUploads);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.White, pixel));
        Frame(renderer, new TriangleMaterial(cached));
        Assert.Equal(0, renderer.FrameTextureUploads);
    }

    [Fact]
    public void AbortingAnOversizedMaterialReleasesOnlyItsBindingsAndRestoresTheCaller()
    {
        using GpuTestDevice device = new();
        using RenderTarget2D outer = device.CreateTarget(12, 10);
        device.Device.SetRenderTarget(outer);
        using GpuTriangleRenderer renderer = new(device.Device, 2, 4);
        PixelImage oversized = new(2, 1, [new Rgba32(255, 0, 0, 255), new Rgba32(255, 0, 0, 255)]);
        renderer.Begin(16, 16, Vector4.One);

        // Exercise interruption after material allocation, before batch submission.
        typeof(GpuTriangleRenderer).GetMethod("ApplyMaterial", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(renderer, [new TriangleMaterial(oversized)]);
        Texture2D texture = Assert.IsType<Texture2D>(typeof(GpuTriangleRenderer).GetField("transientTexture", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer));
        device.Device.Textures[0] = texture;
        Assert.False(texture.IsDisposed);
        renderer.Abort();
        Assert.True(texture.IsDisposed);
        Assert.Null(device.Device.Textures[0]);
        Assert.Same(outer, device.Device.GetRenderTargets()[0].RenderTarget);
        renderer.Abort();
        Assert.True(texture.IsDisposed);

        Frame(renderer, new TriangleMaterial(null));
        Assert.Same(outer, device.Device.GetRenderTargets()[0].RenderTarget);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.White, pixel));
    }

    [Fact]
    public void DynamicImageRefreshesOncePerFrameAcrossBatchesAndExplicitInvalidationReleasesItsEntry()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device, 2, 8);
        PixelImage image = Image(255, 0, 0);
        Frame(renderer, new TriangleMaterial(image));
        image.Pixels[0] = new(0, 255, 0, 255);
        TriangleMaterial dynamicMaterial = new(image) { DynamicTexture = true };
        Frame(renderer, dynamicMaterial, dynamicMaterial with { AlphaMultiplier = 0.5f }, dynamicMaterial);
        Assert.Equal(1, renderer.FrameTextureUploads);
        Frame(renderer, dynamicMaterial);
        Assert.Equal(1, renderer.FrameTextureUploads);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Lime, pixel));

        renderer.InvalidateTexture(image);
        Frame(renderer, new TriangleMaterial(image));
        Assert.Equal(1, renderer.FrameTextureUploads);
        Frame(renderer, new TriangleMaterial(image));
        Assert.Equal(0, renderer.FrameTextureUploads);
    }

    private static PixelImage Image(byte red, byte green, byte blue) => new(1, 1, [new Rgba32(red, green, blue, 255)]);

    private static void Frame(GpuTriangleRenderer renderer, params TriangleMaterial[] materials)
    {
        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        TriangleVertex a = new(Vector2.Zero, 0.5f, 1, Vector2.Zero, Vector4.One);
        TriangleVertex b = new(new(32, 0), 0.5f, 1, Vector2.Zero, Vector4.One);
        TriangleVertex c = new(new(0, 32), 0.5f, 1, Vector2.Zero, Vector4.One);
        foreach (TriangleMaterial material in materials)
        {
            renderer.Draw(a, b, c, material);
        }
        renderer.End();
    }
}
