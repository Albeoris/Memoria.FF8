using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldCloudPresentationTests
{
    [Theory]
    [InlineData(0, -256, 6)]
    [InlineData(1, -1, 4)]
    [InlineData(64, -64, 4)]
    [InlineData(123, -123, 4)]
    [InlineData(255, -255, 6)]
    [InlineData(256, -256, 6)]
    public void NativeYawLowByteControlsTheRepeating256PixelStrip(int yaw, int left, int triangles)
    {
        WorldSkyGradient gradient = new(960, default, default, new Rgba32(81, 123, 255));
        WorldMapLayer layer = Assert.Single(WorldCloudPresentation.Create(yaw, gradient));
        Assert.Equal(10, layer.TextureMember);
        Assert.True(layer.Additive);
        Assert.Equal(triangles, layer.Triangles.Count);
        Assert.Equal(new Vector2(left, 72), layer.Triangles[0].A.Position);
        Assert.Equal(new Vector2(left + 256, 136), layer.Triangles[1].C.Position);
        Assert.Equal(new Vector2(1.5f / 256, 1.5f / 64), layer.Triangles[0].A.Uv);
        Assert.Equal(new Vector4(0.5f, 0.5f, 0.5f, 1), layer.Triangles[0].A.Color);
        Assert.Equal(new Vector4(40 / 128f, 61 / 128f, 127 / 128f, 1), layer.Triangles[1].C.Color);
        Assert.Empty(WorldCloudPresentation.Create(yaw, gradient, 12));
    }

    [Fact]
    public void InstalledCloudTextureReachesTheBackgroundGpuAndCpuConsumers()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldTerrainRenderer renderer = new(assets, 320, 224);
        CaptureTriangles capture = new();
        renderer.TriangleRenderer = capture;
        MethodInfo draw = typeof(WorldTerrainRenderer).GetMethod("DrawWorldClouds", BindingFlags.Instance | BindingFlags.NonPublic)!;
        WorldSky sky = assets.Skies[0];
        WorldCamera camera = WorldCamera.Foot(123, 2048);
        draw.Invoke(renderer, [camera, Vector3.Zero, sky, (byte)0]);
        Assert.NotEmpty(capture.Materials);
        Assert.All(capture.Materials, material =>
        {
            Assert.Equal(TriangleBlend.Add, material.Blend);
            Assert.Equal(TriangleDepthComparison.Always, material.DepthComparison);
            Assert.False(material.DepthWrite);
            Assert.Equal(256, material.Texture!.Width);
            Assert.Equal(64, material.Texture.Height);
        });
        int calls = capture.Materials.Count;
        draw.Invoke(renderer, [camera, Vector3.Zero, sky, (byte)12]);
        Assert.Equal(calls, capture.Materials.Count);
        renderer.TriangleRenderer = null;
        draw.Invoke(renderer, [camera, Vector3.Zero, sky, (byte)0]);
        byte[] pixels = (byte[])typeof(WorldTerrainRenderer).GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        Assert.Contains(pixels, value => value != 0);
    }

    private sealed class CaptureTriangles : ITriangleRenderer
    {
        public List<TriangleMaterial> Materials { get; } = [];
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Materials.Add(material);
        }
        public void End()
        {
        }
    }
}
