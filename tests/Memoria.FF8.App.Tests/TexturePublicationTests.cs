using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class TexturePublicationTests
{
    [Fact]
    public void PreparedGpuImageDoesNotReplaceLivePixelsUntilPublicationAndNeedsNoLaterUpload()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        PixelImage original = new(1, 1, [new(255, 0, 0)]);
        PixelImage candidate = new(2, 2, Enumerable.Repeat(new Rgba32(0, 255, 0), 4).ToArray());
        Frame(renderer, original);
        Assert.All(GpuTestDevice.Read(renderer.Target), color => Assert.Equal(Color.Red, color));
        using TexturePublication prepared = renderer.PrepareTexturePublication([candidate]);
        Assert.True(renderer.ContainsTexture(original));
        Assert.False(renderer.ContainsTexture(candidate));
        Frame(renderer, original);
        Assert.Equal(0, renderer.FrameTextureUploads);
        Assert.All(GpuTestDevice.Read(renderer.Target), color => Assert.Equal(Color.Red, color));
        prepared.Commit();
        Frame(renderer, candidate);
        Assert.Equal(0, renderer.FrameTextureUploads);
        Assert.All(GpuTestDevice.Read(renderer.Target), color => Assert.Equal(Color.Lime, color));
    }

    [Fact]
    public void FailedBudgetAndDiscardedUploadPreservePreviousGpuStorageAndPixels()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device, 4, 16);
        PixelImage original = new(1, 1, [new(255, 0, 0)]);
        PixelImage candidate = new(1, 1, [new(0, 255, 0)]);
        Frame(renderer, original);
        Assert.Throws<InvalidDataException>(() => renderer.PrepareTexturePublication([new PixelImage(3, 3)]));
        using (renderer.PrepareTexturePublication([candidate]))
        {
            Assert.False(renderer.ContainsTexture(candidate));
        }
        Frame(renderer, original);
        Assert.True(renderer.ContainsTexture(original));
        Assert.False(renderer.ContainsTexture(candidate));
        Assert.Equal(0, renderer.FrameTextureUploads);
        Assert.All(GpuTestDevice.Read(renderer.Target), color => Assert.Equal(Color.Red, color));
    }

    private static void Frame(GpuTriangleRenderer renderer, PixelImage image)
    {
        renderer.Begin(8, 8, Vector4.One);
        TriangleVertex a = new(Vector2.Zero, 0.5f, 1, Vector2.Zero, Vector4.One);
        TriangleVertex b = a with { Position = new(8, 0), Uv = new(1, 0) };
        TriangleVertex c = a with { Position = new(0, 8), Uv = new(0, 1) };
        TriangleVertex d = a with { Position = new(8, 8), Uv = Vector2.One };
        TriangleMaterial material = new(image);
        renderer.Draw(a, b, c, material);
        renderer.Draw(b, d, c, material);
        renderer.End();
    }

    [Fact]
    public void RepeatedPublicationsStayWithinTheGpuCacheBudget()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device, 4, 16);
        PixelImage current = new(1, 1, [new(255, 0, 0)]);
        Frame(renderer, current);
        for (int revision = 0; revision < 24; revision++)
        {
            PixelImage next = new(1, 1, [new((byte)revision, 255, 0)]);
            using TexturePublication publication = renderer.PrepareTexturePublication([next], [current]);
            publication.Commit();
            Frame(renderer, next);
            Assert.Equal(0, renderer.FrameTextureUploads);
            current = next;
        }
        long bytes = (long)typeof(GpuTriangleRenderer).GetField("cachedTextureBytes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(renderer)!;
        Assert.InRange(bytes, 1, 16);
    }
}
