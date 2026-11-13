using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7BattleArenaGeometryTests
{
    [Fact]
    public void Original_arena_geometry_preserves_primitive_materials_and_supports_independent_capture_and_suppression()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select the original game installation.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        SceneCameraPlacement camera = SceneBattleData.Read(formation).Cameras[0];
        Ff7BattleArenaResources resources = Ff7BattleArenaResources.Load(archive, formation.LocationId);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        Ff7BattleArenaGeometry geometry = renderer.CaptureArenaGeometry();
        Assert.Same(geometry, renderer.CaptureArenaGeometry());
        Assert.Equal(resources.Geometry.Sum(part => part.Polygon.Groups.Sum(group => group.PolygonCount)), geometry.Faces.Length);
        Assert.Equal(geometry.Faces.Length, geometry.Faces.Select(face => face.Id).Distinct().Count());
        Ff7ModelMaterialState material = new() { Cull = true };
        int faceIndex = 0;
        for (int partIndex = 0; partIndex < resources.Geometry.Count; partIndex++)
        {
            PcPolygonModel model = resources.Geometry[partIndex].Polygon;
            for (int groupIndex = 0; groupIndex < model.Groups.Count; groupIndex++)
            {
                PcModelGroup group = model.Groups[groupIndex];
                PcRenderState state = PcRenderState.Read(model.RenderStates[groupIndex].Span);
                material.Apply(state);
                for (int polygonIndex = group.PolygonStart; polygonIndex < group.PolygonStart + group.PolygonCount; polygonIndex++)
                {
                    Ff7BattleArenaFace face = geometry.Faces[faceIndex++];
                    PcModelPolygon polygon = model.Polygons[polygonIndex];
                    Assert.Equal(new(partIndex, groupIndex, polygonIndex), face.Id);
                    Assert.Equal(model.Vertices[group.VertexStart + polygon.A], face.A);
                    Assert.Equal(model.Vertices[group.VertexStart + polygon.B], face.B);
                    Assert.Equal(model.Vertices[group.VertexStart + polygon.C], face.C);
                    NativeBattleForeignRenderMaterial captured = Assert.IsType<NativeBattleForeignRenderMaterial>(face.Surface.Material);
                    Assert.Equal(material.Blend, captured.Material.Blend);
                    Assert.Equal(material.DepthWrite, captured.Material.DepthWrite);
                    Assert.Equal(material.DepthTest ? TriangleDepthComparison.GreaterEqual : TriangleDepthComparison.Always, captured.Material.DepthComparison);
                    Assert.Equal(material.Cull, captured.Cull);
                    Assert.Equal(material.Sampler, captured.Material.Sampler);
                    Assert.Equal(material.AlphaReference, captured.Material.MinimumAlpha);
                    Assert.Equal(group.TexturesUsed != 0, captured.Material.Texture is not null);
                    Assert.Equal(3, face.Surface.Uv.Length);
                    Assert.Equal(3, face.Surface.Colors.Length);
                    if (group.TexturesUsed != 0)
                    {
                        Assert.Equal(model.TextureCoordinates[group.TextureCoordinateStart + polygon.A], face.Surface.Uv[0]);
                        Assert.Equal(model.TextureCoordinates[group.TextureCoordinateStart + polygon.B], face.Surface.Uv[1]);
                        Assert.Equal(model.TextureCoordinates[group.TextureCoordinateStart + polygon.C], face.Surface.Uv[2]);
                    }
                    Vector4 first = Color(polygon.A);
                    Assert.Equal(first, face.Surface.Colors[0]);
                    Assert.Equal(material.Gouraud ? Color(polygon.B) : first, face.Surface.Colors[1]);
                    Assert.Equal(material.Gouraud ? Color(polygon.C) : first, face.Surface.Colors[2]);
                }
                Vector4 Color(int local)
                {
                    int vertex = group.VertexStart + local;
                    uint argb = vertex < model.VertexColors.Count ? model.VertexColors[vertex] : uint.MaxValue;
                    return new((argb >> 16 & 255) / 255f, (argb >> 8 & 255) / 255f, (argb & 255) / 255f, (argb >> 24) / 255f * state.VertexAlpha / 255f);
                }
            }
        }
        Capture stage = new();
        renderer.Render(stage, 320, 240, new(camera, []));
        Capture independent = new();
        renderer.RenderArena(independent, 320, 240, camera);
        Assert.NotEmpty(stage.Draws);
        Assert.Equal(stage.Draws, independent.Draws);
        Capture hidden = new();
        renderer.Render(hidden, 320, 240, new(camera, []) { SuppressArenaPrimitives = geometry.Faces.Select(face => face.Id).ToHashSet() });
        Assert.Empty(hidden.Draws);
        Capture restored = new();
        renderer.Render(restored, 320, 240, new(camera, []));
        Assert.Equal(stage.Draws, restored.Draws);
        Assert.Same(geometry, renderer.CaptureArenaGeometry());
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material)> Draws { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Assert.Equal(0, clearDepth);
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Draws.Add((a, b, c, material));
        }

        public void End()
        {
        }
    }
}
