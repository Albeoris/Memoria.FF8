using System.Numerics;
using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldContactSparkAssetsTests
{
    [Fact]
    public void OriginalContactSparkRecordUsesItsAnimatedSpriteAndFixedLifetime()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldForestEffect effect = assets.ForestTables.GetEffect(21);
        Assert.Equal((21, 5120, 12, 4, 3, 64), ((int)effect.Type, (int)effect.Shrink, (int)effect.Lifetime, (int)effect.Frames, (int)effect.FrameDuration, (int)effect.HalfSize));
        Assert.Equal((864, 160, 912, 225, 32, 32, 4, 1), ((int)effect.TextureX, (int)effect.TextureY, (int)effect.PaletteX, (int)effect.PaletteY, (int)effect.Width, (int)effect.Height, (int)effect.DrawMode, (int)effect.BlendMode));
        for (int frame = 0; frame < 4; frame++)
        {
            var image = assets.GetForestTexture(21, frame);
            Assert.Equal((32, 32), (image.Width, image.Height));
            Assert.Contains(image.Pixels, pixel => pixel.A != 0);
        }
    }

    [Fact]
    public void NativeVerticalSparkPlaneReachesTheAdditiveTriangleConsumer()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldTerrainRenderer renderer = new(assets, 320, 224);
        CaptureTriangles capture = new();
        renderer.TriangleRenderer = capture;
        Type type = typeof(WorldTerrainRenderer);
        type.GetField("view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, Matrix4x4.CreateLookAt(new Vector3(0, 0, 20), Vector3.Zero, Vector3.UnitY));
        type.GetField("focal", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, 100f);
        type.GetField("far", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, 1000f);
        NativeWorldForestState forest = NativeWorldForestState.Create(0, 0) with { Particles = [new(0, 21, 0, 0, 0, 0, 0, 4096, 0, 12, true) { Motion = new(0, 0, 0, 0) }] };
        type.GetMethod("DrawForest", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(renderer, [forest, Vector3.Zero, null]);
        Assert.Equal(2, capture.Materials.Count);
        Assert.All(capture.Materials, material =>
        {
            Assert.Equal(TriangleBlend.Add, material.Blend);
            Assert.Equal((32, 32), (material.Texture!.Width, material.Texture.Height));
        });
        Assert.True(capture.Vertices.Max(vertex => vertex.Position.Y) > capture.Vertices.Min(vertex => vertex.Position.Y));
    }

    private sealed class CaptureTriangles : ITriangleRenderer
    {
        public List<TriangleMaterial> Materials { get; } = [];
        public List<TriangleVertex> Vertices { get; } = [];
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Materials.Add(material);
            Vertices.AddRange([a, b, c]);
        }
        public void End()
        {
        }
    }
}
