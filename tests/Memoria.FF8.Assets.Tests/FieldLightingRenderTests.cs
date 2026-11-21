using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldLightingRenderTests
{
    [Fact]
    public void Spotlight_has_no_ambient_and_uses_actual_distance_direction_and_color()
    {
        FieldLight light = new() { Id = "lamp", Name = "Lamp", Position = new(0, 0, 10), Range = 20 };
        FieldLightingScene scene = new() { FieldName = "scene_a", Lights = [light] };
        Assert.Equal(Vector3.Zero, FieldLightingMath.Illumination(FieldLightingScene.Empty("scene_a"), Vector3.Zero, Vector3.UnitZ));
        Assert.Equal(new Vector3(0.25f), FieldLightingMath.Illumination(scene, Vector3.Zero, Vector3.UnitZ));
        Assert.Equal(0, FieldLightingMath.Influence(light with { Direction = Vector3.UnitZ }, Vector3.Zero));
        Assert.Equal(0, FieldLightingMath.Influence(light, new Vector3(0, 0, -10)));
        Assert.Equal(0, FieldLightingMath.Influence(light with { Enabled = false }, Vector3.Zero));
        Assert.Equal(Vector3.Zero, FieldLightingMath.Illumination(scene, Vector3.Zero, -Vector3.UnitZ));
    }

    [Fact]
    public void Sunlight_is_directional_and_adds_colored_fill_at_any_distance()
    {
        FieldLight sun = new() { Id = "sun", Name = "Sun", Type = FieldLight.SunlightType, Intensity = 0.25f, Color = Vector3.UnitX };
        FieldLight fill = sun with { Id = "fill", Direction = -Vector3.UnitX, Intensity = 0.5f, Color = Vector3.UnitZ };
        FieldLightingScene scene = new() { FieldName = "scene_a", Lights = [sun, fill], PrimaryLightId = sun.Id };
        Vector3 normal = Vector3.Normalize(new Vector3(1, 0, 1));
        Vector3 expected = new Vector3(0.25f, 0, 0.5f) * normal.Z;
        Vector3 actual = FieldLightingMath.Illumination(scene, Vector3.Zero, normal);
        Assert.InRange(Vector3.Distance(expected, actual), 0, 0.00001f);
        Assert.Equal(actual, FieldLightingMath.Illumination(scene, new Vector3(50000, -70000, 90000), normal));
        Assert.Equal(Vector3.Zero, FieldLightingMath.Illumination(scene, Vector3.Zero, -normal));
        Assert.Equal(Vector3.Zero, FieldLightingMath.Illumination(scene with { Lights = [sun with { Enabled = false }] }, Vector3.Zero, normal));
    }

    [Fact]
    public void Sunlight_shadow_projection_preserves_parallel_rays_and_includes_receiver_depth()
    {
        FieldLight sun = new() { Id = "sun", Name = "Sun", Type = FieldLight.SunlightType, Direction = Vector3.Normalize(new(0, 1, -1)) };
        Vector3 start = new(300, -60, 100);
        Vector3 receiver = start + sun.Direction * 300;
        TriangleVertex a = new() { WorldPosition = start };
        TriangleVertex b = new() { WorldPosition = start + Vector3.UnitX * 10 };
        TriangleVertex c = new() { WorldPosition = start + Vector3.UnitY * 10 };
        Matrix4x4 projection = FieldLightingMath.SunlightShadowViewProjection(sun, [new(a, b, c, default)], [receiver]);
        Vector4 near = Vector4.Transform(new Vector4(start, 1), projection);
        Vector4 far = Vector4.Transform(new Vector4(receiver, 1), projection);
        Assert.Equal(1, near.W);
        Assert.Equal(1, far.W);
        Assert.InRange(Vector2.Distance(new(near.X, near.Y), new(far.X, far.Y)), 0, 0.0001f);
        Assert.InRange(near.Z, 0, 0.1f);
        Assert.InRange(far.Z, 0.9f, 1);
    }

    [Fact]
    public void Crease_adjacency_does_not_weld_coincident_separate_parts()
    {
        Vector3[] positions = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.Zero, Vector3.UnitY, Vector3.UnitZ];
        FieldModel model = new("fixture", [new(-1, 0)], positions.Select(position => new ModelVertex(position, 0)).ToArray(), [new([0, 1, 2], new Vector2[3], 0, false), new([3, 4, 5], new Vector2[3], 0, false)], [], []);
        FieldModelNormals topology = new(model, positions);
        Vector3[][] normals = topology.Pose(positions);
        Assert.Equal(-Vector3.UnitZ, normals[0][0]);
        Assert.Equal(-Vector3.UnitX, normals[1][0]);
        Vector3[] authored = Enumerable.Repeat(Vector3.UnitY, positions.Length).ToArray();
        Assert.Equal(Vector3.UnitY, topology.Pose(positions, authored)[0][0]);
    }

    [Fact]
    public void Clockwise_native_field_faces_produce_outward_normals_and_counterclockwise_gltf()
    {
        Vector3[] positions = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
        PixelImage texture = new(1, 1, [new Rgba32(255, 255, 255, 255)]);
        FieldModel native = new("fixture", [], positions.Select(position => new ModelVertex(position, -1)).ToArray(), [new([0, 2, 1], new Vector2[3], 0, false)], [texture], []);
        Assert.Equal(Vector3.UnitZ, new FieldModelNormals(native, positions).Pose(positions)[0][0]);
        EditableModel exported = ModelExchange.FromField(native);
        EditableMesh mesh = Assert.Single(exported.Meshes);
        Vector3 a = mesh.Vertices[mesh.Indices[0]].Position;
        Vector3 b = mesh.Vertices[mesh.Indices[1]].Position;
        Vector3 c = mesh.Vertices[mesh.Indices[2]].Position;
        Vector3 exportedNormal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        Assert.InRange(Vector3.Distance(Vector3.UnitY, exportedNormal), 0, 0.00001f);
        Assert.All(mesh.Vertices, vertex => Assert.InRange(Vector3.Distance(exportedNormal, vertex.Normal), 0, 0.00001f));
        EditableModel withoutNormals = exported with { Meshes = [mesh with { Vertices = mesh.Vertices.Select(vertex => vertex with { Normal = Vector3.Zero }).ToArray() }] };
        FieldModel imported = ModelExchange.ToField(withoutNormals, native);
        Vector3[] importedPositions = imported.Vertices.Select(vertex => vertex.Position).ToArray();
        Vector3 fallback = new FieldModelNormals(imported, importedPositions).Pose(importedPositions, new Vector3[importedPositions.Length])[0][0];
        Assert.InRange(Vector3.Distance(Vector3.UnitZ, fallback), 0, 0.00001f);
        Vector3[] authored = Enumerable.Repeat(Vector3.UnitX, importedPositions.Length).ToArray();
        Assert.Equal(Vector3.UnitX, new FieldModelNormals(imported, importedPositions).Pose(importedPositions, authored)[0][0]);
    }

    [Fact]
    public void Imported_normals_use_inverse_transpose_and_native_axis_conversion()
    {
        Vector3 source = Vector3.Normalize(new Vector3(1, 1, 0));
        EditableMesh mesh = new("fixture", [new(Vector3.Zero, source, Vector2.Zero, Vector4.Zero, Vector4.Zero, Vector4.One)], [], 0, Matrix4x4.CreateScale(2, 1, 1));
        EditableModel model = new("fixture", "field", [], [mesh], [], [], default(JsonElement));
        Vector3 normal = Assert.Single(ModelExchange.PoseNativeNormals(model, -1, 0));
        Vector3 expected = Vector3.Normalize(new Vector3(0.5f, 0, 1));
        Assert.InRange(Vector3.Distance(normal, expected), 0, 0.00001f);
    }

    [Fact]
    public void Shadow_camera_is_perspective_at_actual_lamp_position()
    {
        FieldLight light = new() { Id = "lamp", Name = "Lamp", Position = new(0, 0, 10), Range = 30 };
        Matrix4x4 projection = FieldLightingMath.ShadowViewProjection(light);
        Vector4 near = Vector4.Transform(new Vector4(1, 0, 5, 1), projection);
        Vector4 far = Vector4.Transform(new Vector4(2, 0, 0, 1), projection);
        Assert.InRange(Math.Abs(near.X / near.W - far.X / far.W), 0, 0.00001f);
        Assert.True(near.W < far.W);
        Matrix4x4 moved = FieldLightingMath.ShadowViewProjection(light with { Position = new Vector3(3, 0, 10) });
        Assert.NotEqual(projection, moved);
    }

    [Fact]
    public void Native_lighting_remains_until_a_source_is_authored_and_returns_after_removing_all_sources()
    {
        FieldAssets field = FixtureField(true);
        RecordingLightingBackend backend = new();
        FieldRenderer renderer = new(field) { TriangleRenderer = backend, ActiveLighting = FieldLightingScene.Empty(field.Name) };
        ActorVisual actor = FixtureActor(0) with { GroundShadowEnabled = false };
        renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
        Assert.False(renderer.UsesAuthoredLighting);
        Assert.Null(backend.Scene);
        Assert.Empty(renderer.LightingShadowStates);

        FieldLight disabled = new() { Id = "lamp", Name = "Lamp", Enabled = false, Position = new(0, 0, 10), Range = 30 };
        FieldLightingScene authored = new() { FieldName = field.Name, Lights = [disabled], PrimaryLightId = disabled.Id };
        renderer.ActiveLighting = authored;
        renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
        Assert.True(renderer.UsesAuthoredLighting);
        Assert.Same(authored, backend.Scene);
        Assert.Equal(FieldActorShadowState.IntentionalNone, renderer.LightingShadowStates[actor.ObjectId]);

        foreach (FieldLightingScene scene in new[] { authored with { Lights = [], PrimaryLightId = null }, authored with { FieldName = "another_field" } })
        {
            renderer.ActiveLighting = scene;
            renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
            Assert.False(renderer.UsesAuthoredLighting);
            Assert.Null(backend.Scene);
            Assert.Empty(renderer.LightingShadowStates);
        }
    }

    [Fact]
    public void Intentional_shadow_suppression_precedes_missing_ground_or_backend_fallback()
    {
        FieldAssets field = FixtureField(false);
        RecordingLightingBackend backend = new();
        FieldRenderer renderer = new(field) { TriangleRenderer = backend };
        FieldLight light = new() { Id = "lamp", Name = "Lamp", Position = new(0, 0, 10), Range = 30 };
        ActorVisual actor = FixtureActor(0);
        foreach (FieldLightingScene scene in new[]
        {
            new FieldLightingScene { FieldName = field.Name, Lights = [light] },
            new FieldLightingScene { FieldName = field.Name, Lights = [light with { Enabled = false }], PrimaryLightId = light.Id },
            new FieldLightingScene { FieldName = field.Name, Lights = [light with { Intensity = 0 }], PrimaryLightId = light.Id },
            new FieldLightingScene { FieldName = field.Name, Lights = [light with { Range = 1 }], PrimaryLightId = light.Id },
            new FieldLightingScene { FieldName = field.Name, Lights = [light with { Position = new(0, 0, -10), Direction = Vector3.UnitZ }], PrimaryLightId = light.Id }
        })
        {
            renderer.ActiveLighting = scene;
            renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
            Assert.Equal(FieldActorShadowState.IntentionalNone, renderer.LightingShadowStates[actor.ObjectId]);
            Assert.Equal(0, backend.CasterCount);
        }
    }

    [Fact]
    public void Shared_ground_triangles_are_submitted_once_and_native_zero_level_suppresses_casting()
    {
        FieldAssets field = FixtureField(true);
        RecordingLightingBackend backend = new();
        FieldLight light = new() { Id = "lamp", Name = "Lamp", Position = new(0, 0, 10), Range = 30 };
        FieldRenderer renderer = new(field) { TriangleRenderer = backend, ActiveLighting = new FieldLightingScene { FieldName = field.Name, Lights = [light], PrimaryLightId = light.Id } };
        renderer.Render(new Dictionary<int, int>(), [FixtureActor(0), FixtureActor(1)], TimeSpan.Zero);
        Assert.Equal(2, backend.CasterCount);
        Assert.Equal(2, backend.GroundTriangles);
        Assert.All(renderer.LightingShadowStates.Values, value => Assert.Equal(FieldActorShadowState.Silhouette, value));
        renderer.Render(new Dictionary<int, int>(), [FixtureActor(0) with { GroundShadowLevel609 = 0 }], TimeSpan.Zero);
        Assert.Equal(FieldActorShadowState.IntentionalNone, renderer.LightingShadowStates[0]);
        Assert.Equal(0, backend.CasterCount);
        Assert.Equal(0, backend.GroundTriangles);
    }

    [Theory]
    [InlineData(FieldLight.SpotType, 5)]
    [InlineData(FieldLight.SunlightType, 5000)]
    public void Authored_walls_receive_without_walkmesh_and_coplanar_duplicates_draw_once(string lightType, float wallDistance)
    {
        FieldAssets field = FixtureField(false);
        RecordingLightingBackend backend = new();
        FieldLight light = new() { Id = "lamp", Name = "Lamp", Type = lightType, Position = new(0, -10, -1), Direction = Vector3.UnitY, Range = 30 };
        FieldShadowReceiver wall = new() { Id = "wall", Kind = "wall", Center = new(0, wallDistance, 1), Width = 10, Extent = 8 };
        FieldLightingScene scene = new() { FieldName = field.Name, Lights = [light], PrimaryLightId = light.Id, ShadowReceivers = [wall, wall with { Id = "duplicate" }] };
        FieldRenderer renderer = new(field) { TriangleRenderer = backend, ActiveLighting = scene };
        renderer.Render(new Dictionary<int, int>(), [FixtureActor(0)], TimeSpan.Zero);
        Assert.Equal(FieldActorShadowState.Silhouette, renderer.LightingShadowStates[0]);
        Assert.Equal(1, backend.CasterCount);
        Assert.Equal(2, backend.GroundTriangles);
        Assert.Empty(renderer.LightingNativeShadowReceivers);

        renderer.ActiveLighting = scene with { ShadowReceivers = [wall with { Center = new(25, wallDistance, 1) }] };
        renderer.Render(new Dictionary<int, int>(), [FixtureActor(0)], TimeSpan.Zero);
        Assert.Equal(FieldActorShadowState.IntentionalNone, renderer.LightingShadowStates[0]);
        Assert.Equal(0, backend.CasterCount);
        Assert.Equal(0, backend.GroundTriangles);
    }

    [Fact]
    public void Coplanar_authored_floor_replaces_overlap_without_losing_native_coverage()
    {
        FieldAssets field = FixtureField(true);
        RecordingLightingBackend backend = new();
        FieldLight light = new() { Id = "lamp", Name = "Lamp", Position = new(0, 0, 10), Range = 50 };
        FieldShadowReceiver floor = new() { Id = "floor", Kind = "floor", Width = 20, Extent = 10, HeadingDegrees = 32 };
        FieldLightingScene scene = new() { FieldName = field.Name, Lights = [light], PrimaryLightId = light.Id, ShadowReceivers = [floor] };
        FieldRenderer renderer = new(field) { TriangleRenderer = backend, ActiveLighting = scene };
        renderer.Render(new Dictionary<int, int>(), [FixtureActor(0)], TimeSpan.Zero);
        Assert.Equal(2, renderer.LightingNativeShadowReceivers.Count);
        Assert.InRange(backend.ReceiverArea, 1599.999f, 1600.001f);
    }

    [Fact]
    public void Editor_projection_and_ground_pick_use_native_camera_and_viewport()
    {
        FieldRenderer renderer = new(FixtureField(true), 960, 540);
        Vector3 ground = new(2, 3, 0);
        Vector2 projected = renderer.ProjectWorldPoint(ground)!.Value;
        Assert.True(renderer.TryPickGround(projected, out Vector3 picked));
        Assert.InRange(Vector3.Distance(ground, picked), 0, 0.001f);
    }

    private static ActorVisual FixtureActor(int id) => new(id, 0, 0, 0, 1, -64, 0, 0, true, PolygonColor: new Vector3(128), GroundShadowRadius601To608: 1, GroundShadowLevel609: 128, GroundShadowCentre: Vector3.Zero);

    private static FieldAssets FixtureField(bool ground)
    {
        PixelImage texture = new(1, 1, [new Rgba32(255, 255, 255, 255)]);
        FieldModel model = new("fixture", [new(-1, 0)], [new(new(0, -1, -1), 0), new(new(0, -1, 1), 0), new(new(0, 1, -1), 0)], [new([0, 1, 2], new Vector2[3], 0, false)], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldWalkmesh walkmesh = new(ground ? [new(new(-20, -20, 0), new(20, -20, 0), new(-20, 20, 0), 1, -1, -1), new(new(20, -20, 0), new(20, 20, 0), new(-20, 20, 0), 0, -1, -1)] : []);
        return new(0, "scene_a", [], [], [], new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new(0, 0, 100), Vector2.Zero, 100), walkmesh, [model]);
    }

    private sealed class RecordingLightingBackend : ITriangleRenderer, IFieldLightingRenderer
    {
        public bool FieldLightingSupported => true;
        public string? FieldLightingUnavailableReason => null;
        public int CasterCount { get; private set; }
        public int GroundTriangles { get; private set; }
        public float ReceiverArea { get; private set; }
        public FieldLightingScene? Scene { get; private set; }
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            CasterCount = GroundTriangles = 0;
            ReceiverArea = 0;
        }
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            if (material.FieldLighting == FieldLightingPass.GroundShadow)
            {
                GroundTriangles++;
                ReceiverArea += Vector3.Cross(b.WorldPosition - a.WorldPosition, c.WorldPosition - a.WorldPosition).Length() * 0.5f;
            }
        }
        public void End()
        {
        }
        public void SetFieldLighting(FieldLightingScene? scene)
        {
            Scene = scene;
        }
        public bool PrepareFieldShadow(IReadOnlyList<FieldShadowTriangle> casters, out string? reason, IReadOnlyList<Vector3>? receivers = null)
        {
            CasterCount = casters.Count;
            reason = null;
            return true;
        }
    }
}
