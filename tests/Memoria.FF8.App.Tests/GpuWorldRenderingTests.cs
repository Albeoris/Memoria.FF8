using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuWorldRenderingTests
{
    private static readonly Vector3 Position = new(24576, 658, -29406);
    private static readonly Dictionary<int, int> Replacements = new() { [274] = 827, [275] = 828 };

    [Fact]
    public void NativeSkyGpuTextureRetainsCpuRowColorsWithoutWritingSoftwarePixels()
    {
        RecordingRenderer sink = new();
        WorldTerrainRenderer renderer = new(null!, 96, 64) { TriangleRenderer = sink };
        byte[] software = SoftwarePixels(renderer);
        Array.Fill(software, (byte)73);
        WorldSky sky = new(Vector3.Zero, new Rgba32(7, 48, 101), new Rgba32(43, 92, 151), new Rgba32(115, 178, 224));
        WorldSkyGradient gradient = new(960, sky.Top, sky.Center, sky.Bottom);
        typeof(WorldTerrainRenderer).GetMethod("DrawSky", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(renderer, [sky, gradient]);
        Assert.Equal(2, sink.Materials.Count);
        TriangleMaterial material = sink.Materials[0];
        Assert.Equal(TriangleDepthComparison.Always, material.DepthComparison);
        Assert.False(material.DepthWrite);
        Assert.True(material.DynamicTexture);
        PixelImage texture = Assert.IsType<PixelImage>(material.Texture);
        Assert.Equal(1, texture.Width);
        Assert.Equal(renderer.Height, texture.Height);
        for (int y = 0; y < renderer.Height; y++)
        {
            Assert.Equal(gradient.Sample((y + 0.5) * WorldSkyProjection.LogicalHeight / renderer.Height), texture.Pixels[y]);
        }
        Assert.All(software, value => Assert.Equal((byte)73, value));
    }

    [Fact]
    public void InstalledWorldSubmitsPerspectiveGeometryAndBypassesTheSoftwareFrame()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        RecordingRenderer sink = new();
        WorldTerrainRenderer renderer = new(assets, 160, 112) { TriangleRenderer = sink };
        byte[] software = SoftwarePixels(renderer);
        Array.Fill(software, (byte)73);
        float[] softwareDepth = (float[])typeof(WorldTerrainRenderer).GetField("depth", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        Array.Fill(softwareDepth, 73);
        WorldCamera camera = WorldCamera.Foot(2048, 2048);
        Assert.Empty(renderer.Render(Position, MathF.PI, 0, 0, camera, Replacements));
        Assert.Equal(1, sink.Begins);
        Assert.Equal(1, sink.Ends);
        Assert.Equal(0, sink.ClearDepth);
        Assert.True(renderer.DrawnTriangles > 100);
        Assert.Equal(renderer.DrawnTriangles + 2, sink.Materials.Count);
        Assert.All(software, value => Assert.Equal((byte)73, value));
        Assert.All(softwareDepth, value => Assert.Equal(73, value));
        Assert.All(sink.Materials.Skip(2), material =>
        {
            Assert.Equal(TriangleDepthComparison.Greater, material.DepthComparison);
            Assert.Equal(TriangleDepthMapping.Positive, material.DepthMapping);
            Assert.Equal(1, material.DepthScale);
            Assert.Equal(TriangleSampler.PointClamp, material.Sampler);
            Assert.Equal(1 / camera.FarDistance, material.MinimumReciprocalW);
            Assert.Equal(camera.FarDistance * 0.6f, material.FogStart);
            Assert.Equal(camera.FarDistance, material.FogEnd);
            Assert.Equal(material.Blend == TriangleBlend.Replace, material.DepthWrite);
        });
        Assert.All(sink.Vertices.Skip(6), vertex =>
        {
            Assert.True(vertex.ReciprocalW > 0);
            Assert.Equal(vertex.Depth, vertex.ReciprocalW);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledWorldGpuSamplesMatchSoftwareTerrainAndSky(bool nativeCamera)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        const int width = 160;
        const int height = 112;
        WorldTerrainRenderer renderer = new(assets, width, height);
        WorldCamera camera = nativeCamera ? WorldCamera.Foot(2048, 2048) : new WorldCamera(0, 120, 160, 10, MathF.PI / 3, 900);
        byte[] expected = renderer.Render(Position, MathF.PI, 0, 0, camera, Replacements).ToArray();
        int triangles = renderer.DrawnTriangles;
        renderer.TriangleRenderer = gpu;
        Assert.Empty(renderer.Render(Position, MathF.PI, 0, 0, camera, Replacements));
        Assert.Equal(triangles, renderer.DrawnTriangles);
        Color[] actual = GpuTestDevice.Read(gpu.Target);
        Assert.All(actual, pixel => Assert.Equal(255, pixel.A));
        int samples = 0;
        int matching = 0;
        for (int y = 2; y < height - 2; y += 3)
        {
            for (int x = 2; x < width - 2; x += 3)
            {
                int pixel = y * width + x;
                int offset = pixel * 4;
                int difference = Math.Max(Math.Abs(expected[offset] - actual[pixel].R), Math.Max(Math.Abs(expected[offset + 1] - actual[pixel].G), Math.Abs(expected[offset + 2] - actual[pixel].B)));
                samples++;
                if (difference <= 6)
                {
                    matching++;
                }
            }
        }
        Assert.True(matching >= samples * 0.95, $"Only {matching}/{samples} world RGB samples match within six channel levels (native camera: {nativeCamera}).");
        Assert.True(actual.Distinct().Count() > 32, "The GPU frame must contain textured terrain and models.");
    }

    private static byte[] SoftwarePixels(WorldTerrainRenderer renderer)
    {
        return (byte[])typeof(WorldTerrainRenderer).GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
    }

    private sealed class RecordingRenderer : ITriangleRenderer
    {
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        public float ClearDepth { get; private set; }
        public List<TriangleMaterial> Materials { get; } = [];
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Begins++;
            ClearDepth = clearDepth;
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Materials.Add(material);
            Vertices.AddRange([a, b, c]);
        }

        public void End()
        {
            Ends++;
        }
    }
}
