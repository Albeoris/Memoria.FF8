using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuCustomFieldSceneTests
{
    [Fact]
    public void OpaqueCoreFractionalAndHolesShareTheActorsDepth()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        PixelImage texture = new(4, 1, [new(255, 0, 0, 255), new(255, 0, 0, 251), new(255, 0, 0, 162), new(0, 255, 0, 0)]);
        TriangleMaterial scene = new(texture, Sampler: TriangleSampler.PointClamp)
        {
            SceneCoverage = SceneAlphaCoverage.OpaqueCore,
            SceneSampler = new FieldSceneSampler(33071, 33071, 9728, 9728),
            DepthMapping = TriangleDepthMapping.Positive
        };
        renderer.Begin(32, 16, new Vector4(0, 0, 0, 1));
        Draw(scene, 0, 200, Vector4.One);
        Draw(new TriangleMaterial(null) { DepthMapping = TriangleDepthMapping.Positive }, 0, 300, new Vector4(0, 0, 1, 1));
        Draw(scene with { DepthWrite = false, Blend = TriangleBlend.Alpha, SceneCoverage = SceneAlphaCoverage.Fractional }, 0, 200, Vector4.One);
        Draw(new TriangleMaterial(null) { DepthMapping = TriangleDepthMapping.Positive }, 4, 250, new Vector4(0, 1, 0, 1));
        renderer.End();

        Color[] actual = GpuTestDevice.Read(renderer.Target);
        Assert.Equal(Color.Red, actual[2 * 32 + 4]);
        Assert.Equal(new Color(251, 0, 4), actual[2 * 32 + 12]);
        Assert.Equal(new Color(162, 0, 93), actual[2 * 32 + 20]);
        Assert.Equal(Color.Blue, actual[2 * 32 + 28]);
        Assert.Equal(Color.Red, actual[10 * 32 + 4]);
        Assert.Equal(Color.Lime, actual[10 * 32 + 12]);
        Assert.Equal(Color.Lime, actual[10 * 32 + 20]);
        Assert.Equal(Color.Lime, actual[10 * 32 + 28]);

        void Draw(TriangleMaterial material, int top, float z, Vector4 color)
        {
            TriangleVertex a = new(new(0, top), z, 1, new(0, 0.5f), color);
            TriangleVertex b = new(new(32, top), z, 1, new(1, 0.5f), color);
            TriangleVertex c = new(new(0, 16), z, 1, new(0, 0.5f), color);
            TriangleVertex d = new(new(32, 16), z, 1, new(1, 0.5f), color);
            renderer.Draw(a, b, c, material);
            renderer.Draw(b, d, c, material);
        }
    }

    [Fact]
    public void OrthographicCameraPreservesDepthAndClipsIntersectingNearPlane()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        FieldRenderer renderer = RenderOrthographicScene(gpu);
        Color[] pixels = GpuTestDevice.Read(gpu.Target);
        Assert.Equal(Color.Red, pixels[12 * 32 + 16]);
        Assert.Equal(Color.Blue, pixels[1 * 32 + 2]);
        Assert.True(renderer.TryScreenRay(new Vector2(16, 16), out Vector3 origin, out Vector3 direction));
        Assert.Equal(Vector3.Zero, origin);
        Assert.Equal(Vector3.UnitZ, direction);
    }

    [Fact]
    public void NearPlaneInterpolatesAttributesAndRetainsOrthographicDepthInPackets()
    {
        RecordingTriangles stream = new();
        RenderOrthographicScene(stream);
        Assert.Equal(3, stream.Materials.Count);
        Assert.All(stream.Materials, material => Assert.False(material.PerspectiveDepth));
        Assert.All(stream.Vertices, vertex => Assert.Equal(1, vertex.ReciprocalW));
        Assert.Equal(new[] { 2f, 5f, 6f }, stream.Vertices.Select(vertex => vertex.Depth).Distinct().Order().ToArray());
        Assert.All(stream.Vertices.Where(vertex => vertex.Depth == 2), vertex => Assert.Equal(2, vertex.WorldPosition.Z, 5));
    }

    [Fact]
    public void AuthoredCameraScrollAndPickingUseTheSamePrincipalPoint()
    {
        RecordingTriangles stream = new();
        FieldRenderer renderer = RenderOrthographicScene(stream);
        Vector2 baseline = new(5, 6);
        renderer.CustomSceneCamera = renderer.CustomSceneCamera! with { NativeScrollBaseline = baseline };
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundVisible: false, actorScrollOffset: new(15, 13), drawingJitterX: 1, drawingJitterY: 2);
        Vector2 projected = renderer.ProjectWorldPoint(new Vector3(0, 0, 5))!.Value;
        Assert.Equal(16 + 11 * 0.1f, projected.X, 5);
        Assert.Equal(16 + 9 * 0.1f * 240 / 224, projected.Y, 5);
        Assert.True(renderer.TryScreenRay(projected, out Vector3 origin, out Vector3 direction));
        Assert.Equal(Vector3.Zero, origin);
        Assert.Equal(Vector3.UnitZ, direction);
    }

    [Fact]
    public void UnlitReceiverUsesPrimaryShadowWithoutDiffuseOrAlphaChanges()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        PixelImage atlas = new(2, 1, [new(200, 100, 50, 162), new(200, 100, 50, 255)]);
        FieldLight sun = new() { Id = "sun", Name = "Sun", Type = FieldLight.SunlightType, Direction = -Vector3.UnitZ };
        FieldLightingScene lights = new() { FieldName = "room", Lights = [sun], PrimaryLightId = sun.Id, GroundShadow = new FieldGroundShadowSettings { Softness = 0, Opacity = 0.5f } };
        TriangleMaterial material = new(atlas, TriangleBlend.Alpha, Sampler: TriangleSampler.PointClamp)
        {
            SceneCoverage = SceneAlphaCoverage.Blend,
            SceneSampler = new FieldSceneSampler(33071, 33071, 9728, 9728),
            FieldLighting = FieldLightingPass.UnlitShadowReceiver
        };
        TriangleVertex Vertex(float x, float y, float z, float half) => new(new((x + half) * 16 / half, (y + half) * 16 / half), 0.5f, 1, new((x + half) / (half * 2), 0.5f), Vector4.One) { WorldPosition = new(x, y, z), WorldNormal = -Vector3.UnitZ };
        TriangleVertex a = Vertex(-8, -8, 0, 8);
        TriangleVertex b = Vertex(8, -8, 0, 8);
        TriangleVertex c = Vertex(-8, 8, 0, 8);
        TriangleVertex d = Vertex(8, 8, 0, 8);
        TriangleVertex sa = Vertex(-2, -2, 5, 2);
        TriangleVertex sb = Vertex(2, -2, 5, 2);
        TriangleVertex sc = Vertex(-2, 2, 5, 2);
        TriangleVertex sd = Vertex(2, 2, 5, 2);
        TriangleMaterial shadowMaterial = new(null) { SceneCoverage = SceneAlphaCoverage.Opaque };

        Color[] unshadowed = Render(false);
        Color[] shadowed = Render(true);
        Assert.Equal(new Color(127, 64, 32), unshadowed[16 * 32 + 14]);
        Assert.Equal(unshadowed[16 * 32 + 4], shadowed[16 * 32 + 4]);
        Assert.InRange((int)shadowed[16 * 32 + 14].R, 91, 94);
        Assert.Equal((byte)255, shadowed[16 * 32 + 14].A);

        Color[] Render(bool shadow)
        {
            renderer.Begin(32, 32, new Vector4(0, 0, 0, 1));
            renderer.SetFieldLighting(lights);
            if (shadow)
            {
                Assert.True(renderer.PrepareFieldShadow([new(sa, sb, sc, shadowMaterial), new(sb, sd, sc, shadowMaterial)], out string? reason, [a.WorldPosition, b.WorldPosition, c.WorldPosition, d.WorldPosition]), reason);
            }
            renderer.Draw(a, b, c, material);
            renderer.Draw(b, d, c, material);
            renderer.End();
            return GpuTestDevice.Read(renderer.Target);
        }
    }

    private static FieldRenderer RenderOrthographicScene(ITriangleRenderer backend)
    {
        EditableMesh Mesh(string name, Vector3[] positions, Vector4 color)
        {
            EditableVertex[] vertices = positions.Select(position => new EditableVertex(position, new Vector3(0, 0, -1), Vector2.Zero, Vector4.Zero, Vector4.Zero, color)).ToArray();
            return new EditableMesh(name, vertices, [0, 1, 2], -1, Matrix4x4.Identity);
        }

        EditableModel source = new("room", "scene", [],
        [
            Mesh("near", [new(-4, -4, 1), new(0, 4, 5), new(4, -4, 5)], new(1, 0, 0, 1)),
            Mesh("far", [new(-4, -4, 6), new(0, 4, 6), new(4, -4, 6)], new(0, 0, 1, 1))
        ], [], [], JsonSerializer.SerializeToElement(new { }));
        FieldSceneAsset scene = FieldSceneAsset.ImportGlb(ModelExchange.ExportGlb(source));
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 256);
        FieldRenderer renderer = new(new FieldAssets(0, "test", [], [], [], camera, new FieldWalkmesh([]), []), 32, 32)
        {
            TriangleRenderer = backend,
            CustomSceneInstances = [new(scene, Matrix4x4.CreateScale(1, -1, -1))],
            CustomSceneCamera = new(Matrix4x4.Identity, true, 4, NearPlane: 2)
        };
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundVisible: false);
        return renderer;
    }

    private sealed class RecordingTriangles : ITriangleRenderer
    {
        public List<TriangleMaterial> Materials { get; } = [];
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Materials.Add(material);
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void End()
        {
        }
    }
}
