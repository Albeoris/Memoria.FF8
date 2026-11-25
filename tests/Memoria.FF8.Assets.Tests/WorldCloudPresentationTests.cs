using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

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

    [Theory]
    [InlineData(-53, 373)]
    [InlineData(-352, 672)]
    public void ExpandedCloudStripKeepsNativeTilesAndCoversBothViewportEdges(float left, float right)
    {
        WorldSkyGradient gradient = new(960, default, default, new Rgba32(81, 123, 255));
        WorldMapLayer native = Assert.Single(WorldCloudPresentation.Create(123, gradient));
        WorldMapLayer expanded = Assert.Single(WorldCloudPresentation.Create(123, gradient, left: left, right: right));
        Assert.All(native.Triangles, triangle => Assert.Contains(triangle, expanded.Triangles));
        Assert.True(expanded.Triangles.Min(triangle => triangle.A.Position.X) <= left);
        Assert.True(expanded.Triangles.Max(triangle => triangle.C.Position.X) >= right);
        Assert.True(expanded.Triangles.Count >= native.Triangles.Count);
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

    [Fact]
    public void InstalledRemasteredCloudPublishesItsPageRegionToRuntimeAndCatalog()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(directory is not null && File.Exists(Path.Combine(directory, "main.zzz")), "Set MEMORIA_FF8_REMASTERED_DIR to the installed Remastered game directory.");
        using RemasteredGameDataSource source = new(directory!);
        ResourceService resources = new(source);
        CaptureTextureSources capture = new();
        resources.Prepare(capture).Commit();

        using WorldAssets world = new(directory!, resources: resources);
        _ = world.ReadNativeSectionTexture(38, 10);
        _ = world.ReadNativeSectionTexture(38, 11);
        TextureBuildContext clouds = Assert.Single(capture.Contexts, context => context.NativePage is not null);

        using GameAssets assets = new(directory!, resources: resources);
        IReadOnlyList<TextureBuildContext>? unit = assets.LoadAddressableCatalogTextureUnit(TextureTarget.FromKey(clouds.Key));
        Assert.NotNull(unit);
        TextureBuildContext catalogClouds = Assert.Single(unit, context => context.NativePage is not null);
        Assert.All(new[] { clouds, catalogClouds }, context =>
        {
            Assert.Equal(new TextureKey(new ResourceKey("world/wmsetus.obj"), "section/38/texture/10"), context.Key);
            Assert.Equal((256, 64), (context.LogicalWidth, context.LogicalHeight));
            TextureSourcePageAddress page = Assert.IsType<TextureSourcePageAddress>(context.NativePage);
            Assert.Equal("world-wmset", page.Namespace);
            Assert.Equal((14, 0, 64, 256, 64, 832, 224), (page.Page, page.X, page.Y, page.Width, page.Height, page.PaletteX, page.PaletteY));
        });
    }

    private sealed class CaptureTextureSources : ITextureReplacementResolver
    {
        public List<TextureBuildContext> Contexts { get; } = [];

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            Contexts.Add(context);
            return null;
        }
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
