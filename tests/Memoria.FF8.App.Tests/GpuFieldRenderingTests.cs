using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuFieldRenderingTests
{
    [Theory]
    [InlineData(MapBlendMode.None, 320, 240)]
    [InlineData(MapBlendMode.Average, 320, 240)]
    [InlineData(MapBlendMode.Average, 641, 361)]
    [InlineData(MapBlendMode.Add, 320, 240)]
    [InlineData(MapBlendMode.Subtract, 320, 240)]
    [InlineData(MapBlendMode.QuarterAdd, 320, 240)]
    public void OriginalTileBlendPassesAndFadeMatchCpu(MapBlendMode blend, int width, int height)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        PixelImage ground = new(1, 1, [new Rgba32(81, 101, 121)]);
        PixelImage overlay = new(1, 1, [new Rgba32(23, 35, 41, 128)]);
        FieldAssets field = Field([
            new BackgroundTile(-8, -8, 100, 255, 0, 0, MapBlendMode.None, false, true, ground),
            new BackgroundTile(-8, -8, 10, 255, 0, 1, blend, true, true, overlay)
        ]);
        FieldRenderer cpu = new(field, width, height);
        FieldRenderer live = new(field, width, height) { TriangleRenderer = gpu };
        foreach (NativeFadeDraw fade in new NativeFadeDraw[] { new(NativeFadeDrawMode.None, 0, 0, 0), new(NativeFadeDrawMode.Add, 3, 5, 7), new(NativeFadeDrawMode.Subtract, 7, 5, 3), new(NativeFadeDrawMode.Black, 0, 0, 0) })
        {
            byte[] expected = cpu.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
            FieldColorComposite.Apply(expected, fade);
            live.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, fade: fade);
            Color[] actual = GpuTestDevice.Read(gpu.Target);
            AssertPixel(expected, actual, width / 2, height / 2, width, 2);
            AssertPixel(expected, actual, 0, 0, width, 1);
        }
    }

    [Theory]
    [InlineData(ModelTextureSampling.Nearest)]
    [InlineData(ModelTextureSampling.Bilinear)]
    public void ModelAndMovieMaskCompositionMatchCpuAndRefreshDecodedFrames(ModelTextureSampling sampling)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        PixelImage texture = new(2, 2, [new(200, 40, 80), new(60, 180, 100), new(30, 90, 220), new(220, 200, 50)]);
        FieldModel model = new("triangle", [new(-1, 0)], [new(new Vector3(0, -10, -10), 0), new(new Vector3(0, 10, -10), 0), new(new Vector3(0, 0, 10), 0)], [new([0, 2, 1], [Vector2.Zero, new Vector2(1, 1), new Vector2(2, 0)], 0, false)], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = Field([], [model]);
        FieldRenderer cpu = new(field, 320, 224, sampling, true);
        FieldRenderer live = new(field, 320, 224, sampling, true) { TriangleRenderer = gpu };
        ActorVisual actor = new(0, 0, 0, 0, 100, 192, 0, 0, true, PolygonColor: new Vector3(128));
        FieldMovieMask mask = new([new(new Vector3(-1000, -1000, 100), new Vector3(0, -1000, 100), new Vector3(0, 1000, 100)), new(new Vector3(-1000, -1000, 100), new Vector3(0, 1000, 100), new Vector3(-1000, 1000, 100))]);
        foreach (byte channel in new byte[] { 30, 90 })
        {
            byte[] movie = Enumerable.Range(0, 16).SelectMany(index => new byte[] { (byte)(channel + index), 50, 70, 255 }).ToArray();
            foreach (FieldMovieMask? selectedMask in new FieldMovieMask?[] { null, mask })
            {
                byte[] expected = cpu.RenderMovieFrame(movie, 4, 4, field.Camera, [actor], selectedMask).ToArray();
                live.RenderMovieFrame(movie, 4, 4, field.Camera, [actor], selectedMask);
                Color[] actual = GpuTestDevice.Read(gpu.Target);
                AssertPixel(expected, actual, 155, 112, 320, 2);
                AssertPixel(expected, actual, 164, 112, 320, 2);
                AssertPixel(expected, actual, 40, 40, 320, 2);
                AssertPixel(expected, actual, 280, 180, 320, 2);
            }
        }
    }

    private static FieldAssets Field(IReadOnlyList<BackgroundTile> tiles, IReadOnlyList<FieldModel>? models = null)
    {
        return new FieldAssets(0, "gpu-test", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), models ?? []);
    }

    private static void AssertPixel(byte[] expected, Color[] actual, int x, int y, int width, int tolerance)
    {
        int offset = (y * width + x) * 4;
        Color pixel = actual[y * width + x];
        Assert.InRange(Math.Abs(expected[offset] - pixel.R), 0, tolerance);
        Assert.InRange(Math.Abs(expected[offset + 1] - pixel.G), 0, tolerance);
        Assert.InRange(Math.Abs(expected[offset + 2] - pixel.B), 0, tolerance);
        Assert.InRange(Math.Abs(expected[offset + 3] - pixel.A), 0, tolerance);
    }
}
