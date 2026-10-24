using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class WorldRoadUvScaleTests
{
    private const int Size = 96;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(0x20, 256)]
    [InlineData(0x40, 128)]
    [InlineData(0x60, 256)]
    public void Retained_mesh_uses_the_selected_atlas_height(int flags, int expectedAtlasHeight)
    {
        WorldTerrainRenderer renderer = Renderer();
        RecordingRenderer recorder = new();
        WorldSegment segment = new(0, 0, 0, [Triangle(flags)]);
        Invoke(renderer, "DrawRetainedTerrain", recorder, segment, Vector3.Zero, new List<(WorldTriangle Triangle, Vector3 Offset)>());
        WorldTerrainMesh mesh = Assert.IsType<WorldTerrainMesh>(recorder.Mesh);
        Assert.Equal(3, mesh.Vertices.Length);
        Assert.Equal(expectedAtlasHeight, recorder.Material.Texture!.Height);
        foreach (WorldTerrainVertex vertex in mesh.Vertices.Span)
        {
            Assert.Equal(new Vector2(32.5f / 256, 96.5f / expectedAtlasHeight), vertex.Uv);
        }
    }

    [Theory]
    [InlineData(0x20, 256)]
    [InlineData(0x40, 128)]
    [InlineData(0x60, 256)]
    public void Streamed_terrain_submits_uvs_for_the_selected_atlas(int flags, int expectedAtlasHeight)
    {
        WorldTerrainRenderer renderer = Renderer();
        RecordingRenderer recorder = new();
        renderer.TriangleRenderer = recorder;
        Invoke(renderer, "DrawTerrain", Triangle(flags), Vector3.Zero, false);
        Assert.Equal(3, recorder.Vertices.Count);
        Assert.Equal(expectedAtlasHeight, recorder.Material.Texture!.Height);
        foreach (TriangleVertex vertex in recorder.Vertices)
        {
            Assert.Equal(32.5f / 256, vertex.Uv.X, precision: 6);
            Assert.Equal(96.5f / expectedAtlasHeight, vertex.Uv.Y, precision: 6);
        }
    }

    [Theory]
    [InlineData(0x20)]
    [InlineData(0x40)]
    [InlineData(0x60)]
    public void Software_terrain_samples_the_native_row_instead_of_a_doubled_road_row(int flags)
    {
        WorldTerrainRenderer renderer = Renderer();
        Invoke(renderer, "DrawTerrain", Triangle(flags), Vector3.Zero, false);
        IEnumerable queued = (IEnumerable)Field(renderer, "rasterTriangles");
        object triangle = Assert.Single(queued.Cast<object>());
        Invoke(renderer, "Raster", Property(triangle, "A"), Property(triangle, "B"), Property(triangle, "C"), Property(triangle, "Texture"), false, 0, Size - 1, null);
        byte[] pixels = (byte[])Field(renderer, "pixels");
        int covered = 0;
        for (int index = 0; index < pixels.Length; index += 4)
        {
            Assert.Equal((byte)0, pixels[index]);
            Assert.Equal((byte)0, pixels[index + 2]);
            if (pixels[index + 1] == 255)
            {
                covered++;
            }
        }
        Assert.True(covered > 100, "The triangle must sample the marked native atlas row across a meaningful area.");
    }

    private static WorldTerrainRenderer Renderer()
    {
        // Populate only the external texture boundary; terrain decoding and GPU devices are unnecessary here.
        WorldAssets assets = (WorldAssets)RuntimeHelpers.GetUninitializedObject(typeof(WorldAssets));
        Set(assets, "roadsPrepared", true);
        Set(assets, "waterPrepared", true);
        Set(assets, "roads", Atlas(256));
        Set(assets, "water", Atlas(128));
        WorldTerrainRenderer renderer = new(assets, Size, Size);
        Set(renderer, "view", Matrix4x4.Identity);
        Set(renderer, "focal", 64f);
        Set(renderer, "far", 100f);
        return renderer;
    }

    private static PixelImage Atlas(int height)
    {
        Rgba32[] pixels = Enumerable.Repeat(new Rgba32(255, 0, 0, 255), 256 * height).ToArray();
        Array.Fill(pixels, new Rgba32(0, 255, 0, 255), 96 * 256, 256);
        return new(256, height, pixels);
    }

    private static WorldTriangle Triangle(int flags)
    {
        Vector2 uv = new(32.5f / 256, 96.5f / 256);
        return new(new(-80, -64, -160), new(80, -64, -160), new(0, 80, -160), uv, uv, uv, 0, 0, 0, (byte)flags, 0);
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);
    private static object Field(object target, string name) => target.GetType().GetField(name, PrivateInstance)!.GetValue(target)!;
    private static object? Property(object target, string name) => target.GetType().GetProperty(name)!.GetValue(target);
    private static void Invoke(object target, string method, params object?[] arguments) => target.GetType().GetMethod(method, PrivateInstance)!.Invoke(target, arguments);

    private sealed class RecordingRenderer : ITriangleRenderer, IWorldTerrainRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];
        public WorldTerrainMesh? Mesh { get; private set; }
        public TriangleMaterial Material { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.AddRange([a, b, c]);
            Material = material;
        }

        public void End()
        {
        }

        public void DrawTerrain(WorldTerrainMesh mesh, in WorldTerrainView view, in TriangleMaterial material, int startVertex = 0, int vertexCount = -1)
        {
            Mesh = mesh;
            Material = material;
            Assert.Equal(0, startVertex);
            Assert.Equal(3, vertexCount);
        }
    }
}
