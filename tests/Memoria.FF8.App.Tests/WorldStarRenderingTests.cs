using System.Numerics;
using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class WorldStarRenderingTests
{
    [Fact]
    public void OriginalSkyPointsHaveMatchingCpuAndGpuColorsAndAnimateWithoutDrawingStateChanges()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        WorldTerrainRenderer renderer = new(assets, 320, 224);
        FieldInfo counter = typeof(WorldTerrainRenderer).GetField("nativeMainLoopCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        byte[] software = (byte[])typeof(WorldTerrainRenderer).GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        MethodInfo draw = typeof(WorldTerrainRenderer).GetMethod("DrawWorldStars", BindingFlags.Instance | BindingFlags.NonPublic)!;
        WorldSkyGradient sky = new(1280, default, default, default);
        Color[]? first = null;
        foreach (uint clock in new uint[] { 0, 2 })
        {
            Array.Clear(software);
            for (int pixel = 3; pixel < software.Length; pixel += 4)
            {
                software[pixel] = 255;
            }
            counter.SetValue(renderer, (uint?)clock);
            object[] arguments = [0, sky, true, (byte)0, Matrix3x2.Identity, 0f, 320f];
            renderer.TriangleRenderer = null;
            draw.Invoke(renderer, arguments);
            Color[] expected = new Color[320 * 224];
            for (int pixel = 0; pixel < expected.Length; pixel++)
            {
                expected[pixel] = new Color(software[pixel * 4], software[pixel * 4 + 1], software[pixel * 4 + 2], software[pixel * 4 + 3]);
            }
            Assert.True(expected.Count(pixel => pixel != Color.Black) >= 20);
            renderer.TriangleRenderer = gpu;
            gpu.Begin(320, 224, new Vector4(0, 0, 0, 1), 0);
            draw.Invoke(renderer, arguments);
            gpu.End();
            Color[] actual = GpuTestDevice.Read(gpu.Target);
            Assert.Equal(expected, actual);
            Assert.Equal(clock, counter.GetValue(renderer));
            if (first is not null)
            {
                Assert.False(first.SequenceEqual(actual));
            }
            first = actual;
        }
    }
}
