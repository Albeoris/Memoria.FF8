using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldShadowPresentationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void NativeContactKindsProduceTwoGroundedTextureHalves(short kind)
    {
        NativeWorldShadowDraw draw = new(0, new Vector3(1000, 9999, 2000), 0, 40);
        WorldShadowGeometry geometry = WorldShadowPresentation.Create(draw, new NativeWorldContactShape(128, 64, 300, kind));
        Assert.Equal(12, geometry.TextureMember);
        Assert.Equal(4, geometry.Triangles.Count);
        WorldShadowVertex[] vertices = geometry.Triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).ToArray();
        Assert.All(vertices, vertex => Assert.Equal(40, vertex.Position.Y));
        Assert.Equal(1968, vertices.Min(vertex => vertex.Position.Z), 3);
        Assert.Equal(2032, vertices.Max(vertex => vertex.Position.Z), 3);
        Assert.Equal(kind == 2 ? 1000 : 936, vertices.Min(vertex => vertex.Position.X), 3);
        Assert.Equal(kind == 2 ? 1128 : 1064, vertices.Max(vertex => vertex.Position.X), 3);
        Assert.Equal(0.5f / 16, geometry.Triangles[0].A.Uv.Y);
        Assert.Equal(7.5f / 16, geometry.Triangles[0].C.Uv.Y);
        Assert.Equal(8.5f / 16, geometry.Triangles[2].A.Uv.Y);
        Assert.Equal(15.5f / 16, geometry.Triangles[2].C.Uv.Y);
        Assert.Equal(13, WorldShadowPresentation.Create(draw with { ShapeId = 73 }, new NativeWorldContactShape(128, 64, 300, kind)).TextureMember);
    }

    [Fact]
    public void NativeScaleAndYawAffectFootprintWithoutUsingAirborneActorHeight()
    {
        NativeWorldShadowDraw draw = new(0, new Vector3(1000, 9999, 2000), 1024, -50, 0.75f);
        WorldShadowGeometry geometry = WorldShadowPresentation.Create(draw, new NativeWorldContactShape(128, 64, 300, 1));
        WorldShadowVertex[] vertices = geometry.Triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).ToArray();
        Assert.Equal(976, vertices.Min(vertex => vertex.Position.X), 3);
        Assert.Equal(1024, vertices.Max(vertex => vertex.Position.X), 3);
        Assert.Equal(1952, vertices.Min(vertex => vertex.Position.Z), 3);
        Assert.Equal(2048, vertices.Max(vertex => vertex.Position.Z), 3);
        Assert.All(vertices, vertex => Assert.Equal(-50, vertex.Position.Y));
    }

    [Fact]
    public void OriginalShadowTextureReachesTheDepthAwareSubtractConsumer()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldTerrainRenderer renderer = new(assets, 320, 224);
        CaptureTriangles capture = new();
        renderer.TriangleRenderer = capture;
        Type type = typeof(WorldTerrainRenderer);
        type.GetField("view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, Matrix4x4.CreateLookAt(new Vector3(0, 10, 20), Vector3.Zero, Vector3.UnitY));
        type.GetField("focal", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, 100f);
        type.GetField("far", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, 1000f);
        NativeWorldShadowDraw[] shadows = [new(0, Vector3.Zero, 0, 0)];
        type.GetMethod("DrawWorldShadows", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(renderer, [shadows]);
        Assert.Equal(4, capture.Materials.Count);
        Assert.All(capture.Materials, material =>
        {
            Assert.Equal(TriangleBlend.Subtract, material.Blend);
            Assert.Equal(TriangleDepthComparison.GreaterEqual, material.DepthComparison);
            Assert.False(material.DepthWrite);
            Assert.Equal(0, material.FogEnd);
            Assert.Equal(16, material.Texture!.Width);
            Assert.Equal(16, material.Texture.Height);
        });
    }

    [Theory]
    [InlineData(TriangleBlend.Subtract, 1f, 80, 70, 60)]
    [InlineData(TriangleBlend.HalfAdd, 0f, 60, 65, 70)]
    [InlineData(TriangleBlend.Add, 0f, 120, 130, 140)]
    [InlineData(TriangleBlend.QuarterAdd, 0f, 105, 108, 110)]
    public void CpuNativeBlendsPreserveDepth(TriangleBlend blend, float groundDepth, byte red, byte green, byte blue)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldTerrainRenderer renderer = new(assets, 4, 4);
        Type type = typeof(WorldTerrainRenderer);
        byte[] pixels = (byte[])type.GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        Array.Fill(pixels, (byte)100);
        float[] depth = (float[])type.GetField("depth", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        Array.Fill(depth, groundDepth);
        type.GetField("far", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, 1000f);
        Type screenVertex = type.GetNestedType("ScreenVertex", BindingFlags.NonPublic)!;
        object Vertex(float x, float y)
        {
            return Activator.CreateInstance(screenVertex, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [x, y, 1f, Vector2.Zero, null], null)!;
        }
        PixelImage texture = new(1, 1, [new Rgba32(20, 30, 40, 128)]);
        type.GetMethod("Raster", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(renderer, [Vertex(0, 0), Vertex(4, 0), Vertex(0, 4), texture, true, 0, 3, blend]);
        Assert.Equal(new byte[] { red, green, blue }, pixels.AsSpan(0, 3).ToArray());
        Assert.Equal(new byte[] { 100, 100, 100 }, pixels.AsSpan(15 * 4, 3).ToArray());
        Assert.All(depth, value => Assert.Equal(groundDepth, value));
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
