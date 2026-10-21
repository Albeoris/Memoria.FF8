using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class WorldWidescreenTests
{
    private const int NativeWidth = 320;
    private const int Height = 224;
    private static readonly Vector3 Position = new(24576, 658, -29406);
    private static readonly Dictionary<int, int> Replacements = new() { [274] = 827, [275] = 828 };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledWorldSoftwarePreservesTheCenterAndAddsTerrainAtWideAspects(bool nativeCamera)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldCamera camera = Camera(nativeCamera);
        Color[] expected = Software(assets, NativeWidth, camera);
        foreach (int width in new[] { 426, 640, 1024 })
        {
            Color[] actual = Software(assets, width, camera);
            AssertCenter(expected, actual, width);
            AssertSideTerrain(actual, width);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledWorldGpuPreservesTheCenterAndAddsTerrainAtWideAspects(bool nativeCamera)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        WorldCamera camera = Camera(nativeCamera);
        Color[] expected = Hardware(assets, NativeWidth, camera, gpu);
        foreach (int width in new[] { 426, 640, 1024 })
        {
            Color[] actual = Hardware(assets, width, camera, gpu);
            AssertCenter(expected, actual, width);
            AssertSideTerrain(actual, width);
        }
    }

    private static WorldCamera Camera(bool nativeCamera)
    {
        return nativeCamera ? WorldCamera.Foot(2048, 2048) : new WorldCamera(0, 120, 160, 10, MathF.PI / 3, 900);
    }

    private static Color[] Software(WorldAssets assets, int width, WorldCamera camera)
    {
        WorldTerrainRenderer renderer = new(assets, width, Height);
        byte[] pixels = renderer.Render(Position, MathF.PI, 0, 0, camera, Replacements);
        Color[] result = new Color[width * Height];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new Color(pixels[i * 4], pixels[i * 4 + 1], pixels[i * 4 + 2], pixels[i * 4 + 3]);
        }
        return result;
    }

    private static Color[] Hardware(WorldAssets assets, int width, WorldCamera camera, GpuTriangleRenderer gpu)
    {
        WorldTerrainRenderer renderer = new(assets, width, Height) { TriangleRenderer = gpu };
        Assert.Empty(renderer.Render(Position, MathF.PI, 0, 0, camera, Replacements));
        return GpuTestDevice.Read(gpu.Target);
    }

    private static void AssertCenter(Color[] expected, Color[] actual, int width)
    {
        int offset = (width - NativeWidth) / 2;
        int different = 0;
        int maximumDifference = 0;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < NativeWidth; x++)
            {
                Color first = expected[y * NativeWidth + x];
                Color second = actual[y * width + offset + x];
                Assert.Equal(first.A, second.A);
                int difference = Math.Max(Math.Abs(first.R - second.R), Math.Max(Math.Abs(first.G - second.G), Math.Abs(first.B - second.B)));
                maximumDifference = Math.Max(maximumDifference, difference);
                if (difference > 1)
                {
                    different++;
                }
            }
        }
        Assert.True(different <= expected.Length / 1000, $"The {width}x{Height} center differs at {different}/{expected.Length} pixels by more than one channel level; maximum difference {maximumDifference}.");
    }

    private static void AssertSideTerrain(Color[] pixels, int width)
    {
        int sideWidth = (width - NativeWidth) / 2;
        foreach (int start in new[] { 0, width - sideWidth })
        {
            HashSet<Color> colors = [];
            int textured = 0;
            for (int y = Height / 2; y < Height; y++)
            {
                Color sky = pixels[y * width];
                for (int x = start; x < start + sideWidth; x++)
                {
                    Color pixel = pixels[y * width + x];
                    Assert.Equal(255, pixel.A);
                    colors.Add(pixel);
                    if (pixel != sky)
                    {
                        textured++;
                    }
                }
            }
            Assert.True(colors.Count > 32 && textured > sideWidth, $"The {width}x{Height} side at x={start} must contain textured terrain, found {colors.Count} colors and {textured} horizontally varying pixels.");
        }
    }
}
