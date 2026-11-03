using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldParticleRenderingTests
{
    private static NativeFieldParticleSprite Sprite => new(0, 0, 0, 0, 0, 400, 0, 4, 2, 0, 0, 16, 16, 4, 128, 128, 128, 0, false);

    [Theory]
    [InlineData(0, false, 0, 8, 4)]
    [InlineData(1024, false, 0, 4, 8)]
    [InlineData(0, true, 0, 16, 8)]
    [InlineData(0, false, 100, 4, 2)]
    public void BillboardsUseNativeEighthPixelExtentsRotationAndBiasedDepth(short angle, bool doubleSize, short depthOffset, int width, int height)
    {
        FieldRenderer renderer = new(Field(), 320, 224, nativeMovieViewport: true);
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, fieldParticles: [Sprite with { Angle = angle, DoubleSize = doubleSize, DepthOffset = depthOffset }], fieldParticleTexture: Texture(new Rgba32(40, 40, 40)));
        int[] changed = Enumerable.Range(0, 320 * 224).Where(index => pixels[index * 4] != 0).ToArray();
        Assert.NotEmpty(changed);
        Assert.Equal(width, changed.Max(index => index % 320) - changed.Min(index => index % 320) + 1);
        Assert.Equal(height, changed.Max(index => index / 320) - changed.Min(index => index / 320) + 1);
    }

    [Theory]
    [InlineData(0, 128, 70)]
    [InlineData(1, 128, 140)]
    [InlineData(2, 128, 60)]
    [InlineData(3, 128, 110)]
    [InlineData(4, 128, 40)]
    [InlineData(1, 255, 40)]
    [InlineData(1, 0, 100)]
    public void BlendModeAppliesOnlyToStpTexels(byte blend, byte alpha, byte expected)
    {
        FieldRenderer renderer = new(Field(true), 320, 224, nativeMovieViewport: true);
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, fieldParticles: [Sprite with { BlendMode = blend }], fieldParticleTexture: Texture(new Rgba32(40, 40, 40, alpha)));
        Assert.Equal(expected, pixels[(111 * 320 + 159) * 4]);
    }

    [Fact]
    public void ForegroundOccludesParticlesAndOrderingRangeRejectsDrawing()
    {
        FieldRenderer renderer = new(Field(true) with { Background = [Tile(50)] }, 320, 224, nativeMovieViewport: true);
        PixelImage texture = Texture(new Rgba32(40, 40, 40));
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        Assert.Equal(baseline[(111 * 320 + 159) * 4], renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, fieldParticles: [Sprite], fieldParticleTexture: texture)[(111 * 320 + 159) * 4]);
        renderer = new(Field(), 320, 224, nativeMovieViewport: true);
        Assert.All(renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, fieldParticles: [Sprite with { DepthOffset = -100 }], fieldParticleTexture: texture).Where((_, index) => index % 4 != 3), value => Assert.Equal(0, value));
    }

    [Fact]
    public void GpuUsesAffineBillboardsAndSeparateOpaqueAndStpPasses()
    {
        Recorder recorder = new();
        FieldRenderer renderer = new(Field(), 320, 224, nativeMovieViewport: true) { TriangleRenderer = recorder };
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, fieldParticles: [Sprite with { BlendMode = 2 }], fieldParticleTexture: Texture(new Rgba32(40, 40, 40, 128)));
        Assert.Equal(4, recorder.Materials.Count);
        Assert.All(recorder.Vertices, vertex =>
        {
            Assert.Equal(1, vertex.ReciprocalW);
            Assert.Equal(400, vertex.Depth);
        });
        Assert.All(recorder.Materials.Take(2), material =>
        {
            Assert.True(material.DepthWrite);
            Assert.Equal(TriangleBlend.Replace, material.Blend);
            Assert.Equal(0.75f, material.MinimumAlpha);
        });
        Assert.All(recorder.Materials.Skip(2), material =>
        {
            Assert.False(material.DepthWrite);
            Assert.Equal(TriangleBlend.Subtract, material.Blend);
            Assert.Equal(0.75f, material.MaximumAlpha);
        });
    }

    [Fact]
    public void RotatedQuadMatchesNativePacketGolden()
    {
        Recorder recorder = new();
        FieldRenderer renderer = new(Field(), 320, 224, nativeMovieViewport: true) { TriangleRenderer = recorder };
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, fieldParticles: [Sprite with { Angle = 512 }], fieldParticleTexture: Texture(new Rgba32(40, 40, 40)));
        Assert.Equal(new Vector2(160 - 11 / 8f, 112 - 34 / 8f), recorder.Vertices[0].Position);
        Assert.Equal(new Vector2(160 + 34 / 8f, 112 + 11 / 8f), recorder.Vertices[1].Position);
        Assert.Equal(new Vector2(160 - 34 / 8f, 112 - 11 / 8f), recorder.Vertices[2].Position);
        Assert.Equal(new Vector2(160 + 11 / 8f, 112 + 34 / 8f), recorder.Vertices[4].Position);
    }
    [Fact]
    public void EqualDepthParticlePoolUsesNativeHeadInsertionOrder()
    {
        FieldRenderer renderer = new(Field(), 320, 224, nativeMovieViewport: true);
        NativeFieldParticleSprite first = Sprite with { PoolIndex = 0, Red = 128, Green = 0, Blue = 0 };
        NativeFieldParticleSprite second = Sprite with { PoolIndex = 1, Red = 0, Green = 128, Blue = 0 };
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, fieldParticles: [first, second], fieldParticleTexture: Texture(new Rgba32(40, 40, 40)));
        int offset = (111 * 320 + 159) * 4;
        Assert.Equal((40, 0, 0), ((int)pixels[offset], (int)pixels[offset + 1], (int)pixels[offset + 2]));
    }
    private sealed class Recorder : ITriangleRenderer
    {
        public List<TriangleMaterial> Materials { get; } = [];
        public List<TriangleVertex> Vertices { get; } = [];
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }
        public void End()
        {
        }
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Materials.Add(material);
            Vertices.AddRange([a, b, c]);
        }
    }
    private static FieldAssets Field(bool background = false) => new(0, "particle-fixture", [], [], background ? [Tile(200)] : [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
    private static BackgroundTile Tile(int depth) => new(-16, -16, depth, 255, 0, 0, MapBlendMode.None, false, true, new PixelImage(16, 16, Enumerable.Repeat(new Rgba32(100, 100, 100), 256).ToArray()));
    private static PixelImage Texture(Rgba32 color) => new(256, 256, Enumerable.Repeat(color, 256 * 256).ToArray());
}
