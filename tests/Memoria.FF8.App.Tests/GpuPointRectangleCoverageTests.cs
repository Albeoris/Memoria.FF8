using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuPointRectangleCoverageTests
{
    private const int Size = 64;
    private static readonly Color Background = new(11, 19, 31, 255);

    [Theory]
    [InlineData(-0.2f, 1.25f, 1.375f, false)]
    [InlineData(2.375f, 3.125f, 2.625f, false)]
    [InlineData(1.25f, 2.375f, 5.5625f, false)]
    [InlineData(-0.2f, 1.25f, 1.375f, true)]
    [InlineData(2.375f, 3.125f, 2.625f, true)]
    [InlineData(1.25f, 2.375f, 5.5625f, true)]
    public void Adjacent_point_rectangles_preserve_pixel_ownership_with_multisampling(float x, float y, float scale, bool blended)
    {
        Vector2 origin = new(x, y);
        PixelImage opaque = Pattern(false);
        PixelImage cutout = Pattern(true);
        Color[] singleSample = Render(0, origin, scale, opaque, cutout, blended);
        Color[] multisample = Render(8, origin, scale, opaque, cutout, blended);
        Assert.Equal(singleSample, multisample);
        Assert.Contains(singleSample, pixel => pixel == Background);
        Assert.Contains(singleSample, pixel => pixel != Background);
        if (!blended)
        {
            Assert.Equal(Expected(origin, scale, opaque, cutout), singleSample);
        }
    }

    private static Color[] Render(int samples, Vector2 origin, float scale, PixelImage opaque, PixelImage cutout, bool blended)
    {
        using GpuTestDevice device = new(samples);
        using GpuTriangleRenderer renderer = new(device.Device);
        renderer.Begin(Size, Size, new Vector4(Background.R / 255f, Background.G / 255f, Background.B / 255f, 1));
        DrawTiles(renderer, origin, scale, opaque, 0.75f, TriangleBlend.Replace);
        DrawTiles(renderer, origin, scale, cutout, 0.5f, TriangleBlend.Replace);
        if (blended)
        {
            DrawTiles(renderer, origin, scale, cutout, 0.25f, TriangleBlend.HalfAdd);
        }
        renderer.End();
        if (samples > 0)
        {
            Assert.SkipUnless(renderer.Target.MultiSampleCount > 0, "The graphics device must support multisampling to check point-rectangle coverage.");
        }
        return GpuTestDevice.Read(renderer.Target);
    }

    private static void DrawTiles(GpuTriangleRenderer renderer, Vector2 origin, float scale, PixelImage image, float depth, TriangleBlend blend)
    {
        TriangleMaterial material = new(image, blend, TriangleDepthComparison.LessEqual, blend == TriangleBlend.Replace, Sampler: TriangleSampler.PointClamp) { PreserveTargetAlpha = false };
        float extent = 4 * scale;
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                Vector2 tileOrigin = origin + new Vector2(column * extent, row * extent);
                Vector2 uvStart = new(column / 2f, row / 2f);
                TrianglePrimitives.Quad(renderer, tileOrigin, new Vector2(extent), depth, Vector4.One, material, uvStart + new Vector2(0.5f), uvStart, pointSampleRectangle: true);
            }
        }
    }

    private static PixelImage Pattern(bool cutout)
    {
        PixelImage image = new(8, 8);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                byte alpha = cutout && (x + 2 * y) % 3 == 0 ? (byte)0 : (byte)255;
                byte red = (byte)(32 + x * 24);
                byte green = (byte)(24 + y * 28);
                byte blue = (byte)(cutout ? 224 - x * 16 : 16 + (x + y) * 12);
                image.Pixels[y * image.Width + x] = new Rgba32(red, green, blue, alpha);
            }
        }
        return image;
    }

    private static Color[] Expected(Vector2 origin, float scale, PixelImage opaque, PixelImage cutout)
    {
        Color[] expected = new Color[Size * Size];
        float extent = 4 * scale;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color color = Background;
                Vector2 local = new Vector2(x + 0.5f, y + 0.5f) - origin;
                if (local.X >= 0 && local.Y >= 0 && local.X < 2 * extent && local.Y < 2 * extent)
                {
                    int column = (int)(local.X / extent);
                    int row = (int)(local.Y / extent);
                    int sourceX = column * 4 + Math.Clamp((int)MathF.Floor((local.X - column * extent) / scale), 0, 3);
                    int sourceY = row * 4 + Math.Clamp((int)MathF.Floor((local.Y - row * extent) / scale), 0, 3);
                    int index = sourceY * opaque.Width + sourceX;
                    Rgba32 texel = cutout.Pixels[index].A != 0 ? cutout.Pixels[index] : opaque.Pixels[index];
                    color = new Color(texel.R, texel.G, texel.B, texel.A);
                }
                expected[y * Size + x] = color;
            }
        }
        return expected;
    }
}
