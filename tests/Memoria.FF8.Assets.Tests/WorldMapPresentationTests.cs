using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldMapPresentationTests
{
    [Theory]
    [InlineData(2, 1280, 448, 1280, 448)]
    [InlineData(2, 640, 896, 640, 896)]
    [InlineData(3, 1280, 448, 960, 448)]
    [InlineData(3, 640, 896, 640, 672)]
    public void ExpandedViewportsPreserveMapAspectAndAnchorItsNativeCanvas(byte mode, int width, int height, int right, int bottom)
    {
        Matrix3x2 transform = WorldMapPresentation.ViewportTransform(mode, width, height);
        Assert.Equal(new Vector2(right, bottom), Vector2.Transform(new(320, 224), transform));
        Assert.Equal(2, Vector2.TransformNormal(Vector2.UnitX, transform).Length());
        Assert.Equal(2, Vector2.TransformNormal(Vector2.UnitY, transform).Length());
        Matrix3x2 globe = WorldMapPresentation.ViewportTransform(1, width, height);
        Assert.Equal(Vector2.TransformNormal(Vector2.UnitX, globe).Length(), Vector2.TransformNormal(Vector2.UnitY, globe).Length());
    }

    [Theory]
    [InlineData(2, 186, 124, 314, 220, 1, true)]
    [InlineData(3, 32, 16, 288, 208, 0, false)]
    public void FlatMapsUseNativeScreenRectanglesAndDistinctPalettes(byte mode, int x, int y, int right, int bottom, int palette, bool transparent)
    {
        WorldMapLayer layer = Assert.Single(WorldMapPresentation.Create(mode, Vector2.Zero));
        Assert.Equal(11, layer.TextureMember);
        Assert.Equal(palette, layer.Palette);
        Assert.Equal(transparent, layer.HalfTransparent);
        Assert.Equal(new Vector2(x, y), layer.Triangles[0].A.Position);
        Assert.Equal(new Vector2(right, bottom), layer.Triangles[1].C.Position);
        Assert.Equal(new Vector2(0.5f / 256, 0.5f / 192), layer.Triangles[0].A.Uv);
        Assert.Empty(WorldMapPresentation.Create(0, Vector2.Zero));
        Assert.Empty(WorldMapPresentation.Markers(0, Vector2.Zero, 0, 0, 0, true));
    }

    [Theory]
    [InlineData(-131072, -98304, 0, 0)]
    [InlineData(0, 0, 64, 48)]
    [InlineData(131071, 98303, 127, 95)]
    public void MapCoordinatesUseTheOriginalWrapped2048UnitCells(int x, int z, int u, int v)
    {
        Assert.Equal(new Vector2(u, v), WorldMapPresentation.MapPosition(new Vector2(x, z)));
    }

    [Fact]
    public void InstalledGlobeUsesOriginalTopologyAndWrapsTextureAcrossWorldSeams()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldGlobeGeometry geometry = assets.ReadMapGlobeGeometry();
        Assert.Equal(40, geometry.Vertices.Count);
        Assert.Equal(37, geometry.Faces.Count);
        Assert.Equal(25, geometry.Faces.Count(face => face.Vertices.Count == 4));
        WorldMapLayer left = Assert.Single(WorldMapPresentation.Create(1, new Vector2(-131072, -98304), geometry));
        WorldMapLayer right = Assert.Single(WorldMapPresentation.Create(1, new Vector2(131071, 98303), geometry));
        Assert.Equal(62, left.Triangles.Count);
        Assert.True(left.Wrap);
        Assert.Equal(1, left.Palette);
        Assert.Equal(left.Triangles.Select(triangle => triangle.A.Position), right.Triangles.Select(triangle => triangle.A.Position));
        Assert.Contains(left.Triangles, triangle => triangle.A.Uv.X < 0 || triangle.A.Uv.Y < 0);
        Assert.Contains(right.Triangles, triangle => triangle.A.Uv.X > 1 || triangle.A.Uv.Y > 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void InstalledMapLayersReachTheActualGpuAndCpuConsumers(byte mode)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldTerrainRenderer renderer = new(assets, 320, 224);
        CaptureTriangles capture = new();
        renderer.TriangleRenderer = capture;
        WorldRenderFrame frame = new(Vector3.Zero, 0, 0, 0, new WorldCamera(0, 100, 100, 0, 1, 1000), new WorldSky(Vector3.Zero, default, default, default)) { MapDisplayMode = mode };
        MethodInfo draw = typeof(WorldTerrainRenderer).GetMethod("DrawWorldMapOverlay", BindingFlags.Instance | BindingFlags.NonPublic)!;
        draw.Invoke(renderer, [frame]);
        Assert.True(capture.Count >= 6);
        Assert.All(capture.Materials, material => Assert.Equal(TriangleDepthComparison.Always, material.DepthComparison));
        Assert.Contains(capture.Materials, material => material.Texture is { Width: > 0, Height: > 0 });
        renderer.TriangleRenderer = null;
        draw.Invoke(renderer, [frame]);
        byte[] pixels = (byte[])typeof(WorldTerrainRenderer).GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        Assert.Contains(pixels, value => value != 0);
        Assert.Equal(mode == 1 ? 66 : 6, renderer.DrawnTriangles / 2);
    }

    [Fact]
    public void CpuHalfTransparentMapDoesNotBlendItsSharedDiagonalTwice()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldTerrainRenderer renderer = new(assets, 8, 8);
        PixelImage texture = new(1, 1, [new Rgba32(200, 100, 60, 255)]);
        WorldMapVertex a = new(Vector2.Zero, Vector2.Zero, Vector4.One);
        WorldMapVertex b = a with { Position = new Vector2(8, 0) };
        WorldMapVertex c = a with { Position = new Vector2(0, 8) };
        WorldMapVertex d = a with { Position = new Vector2(8) };
        WorldMapLayer layer = new(11, 1, true, false, Array.Empty<WorldMapTriangle>());
        MethodInfo raster = typeof(WorldTerrainRenderer).GetMethod("RasterWorldMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        raster.Invoke(renderer, [a, b, c, texture, layer]);
        raster.Invoke(renderer, [c, b, d, texture, layer]);
        byte[] pixels = (byte[])typeof(WorldTerrainRenderer).GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        for (int pixel = 0; pixel < 64; pixel++)
        {
            Assert.Equal(new byte[] { 100, 50, 30 }, pixels.AsSpan(pixel * 4, 3).ToArray());
        }
    }

    private sealed class CaptureTriangles : ITriangleRenderer
    {
        public int Count { get; private set; }
        public List<TriangleMaterial> Materials { get; } = [];
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Count++;
            Materials.Add(material);
        }
        public void End()
        {
        }
    }
}
