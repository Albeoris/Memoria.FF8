using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeActorShadowTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(0)]
    [InlineData(-8)]
    public void NearCameraShadowRendersWithoutMutatingItsInputs(float cameraDepth)
    {
        (FieldAssets field, ActorVisual actor) = NearCameraFixture(cameraDepth);
        FieldRenderer renderer = new(field);
        Dictionary<int, int> state = new() { [3] = 7 };
        ActorVisual original = actor;
        byte[] radii = actor.GroundShadowRadii601To608!.ToArray();
        byte[] baseline = renderer.Render(state, [], TimeSpan.Zero).ToArray();
        byte[] actual = renderer.Render(state, [actor], TimeSpan.Zero).ToArray();

        int shadowPixels = 0;
        for (int index = 0; index < actual.Length; index += 4)
        {
            if (actual[index] < baseline[index])
            {
                shadowPixels++;
            }
            Assert.InRange(baseline[index] - actual[index], 0, 96);
            Assert.Equal(baseline[index] - actual[index], baseline[index + 1] - actual[index + 1]);
            Assert.Equal(baseline[index + 3], actual[index + 3]);
        }
        Assert.True(shadowPixels > 1000, $"Shadow pixels: {shadowPixels}");
        Assert.Equal(original, actor);
        Assert.Equal(radii, actor.GroundShadowRadii601To608);
        Assert.Equal(new KeyValuePair<int, int>(3, 7), Assert.Single(state));
        Assert.Equal(actual, renderer.Render(state, [actor], TimeSpan.Zero));
        Assert.Equal(baseline, renderer.Render(state, [], TimeSpan.Zero));
    }

    [Theory]
    [InlineData(8, 4)]
    [InlineData(0, 4)]
    [InlineData(-8, 4)]
    [InlineData(0, 255)]
    public void NearCameraShadowSubmitsEightFiniteSaturatedTriangles(float cameraDepth, byte radius)
    {
        (FieldAssets field, ActorVisual actor) = NearCameraFixture(cameraDepth, radius);
        ShadowRecorder recorder = new();
        FieldRenderer renderer = new(field) { TriangleRenderer = recorder };
        renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);

        Assert.Equal(8, recorder.Triangles.Count);
        Assert.All(recorder.Triangles, triangle =>
        {
            Assert.Equal(TriangleBlend.Subtract, triangle.Material.Blend);
            Assert.Equal(TriangleDepthComparison.LessEqual, triangle.Material.DepthComparison);
            Assert.False(triangle.Material.DepthWrite);
            Assert.Equal(TriangleDepthMapping.Positive, triangle.Material.DepthMapping);
            Assert.Equal(Math.Max(0, cameraDepth), triangle.A.Depth);
            foreach (TriangleVertex vertex in new[] { triangle.A, triangle.B, triangle.C })
            {
                Assert.True(float.IsFinite(vertex.Position.X));
                Assert.True(float.IsFinite(vertex.Position.Y));
                Assert.InRange(vertex.Position.X, 160 - 1024, 160 + 1023);
                Assert.InRange(vertex.Position.Y, 120 - 1024 * 240f / 224 - 0.001f, 120 + 1023 * 240f / 224 + 0.001f);
                Assert.InRange(vertex.Depth, 0, 65535);
                Assert.Equal(1, vertex.ReciprocalW);
                float nativeX = (vertex.Position.X - 160) * 8;
                float nativeY = (vertex.Position.Y - 120) * 224f / 240 * 8;
                Assert.InRange(Math.Abs(nativeX - MathF.Round(nativeX)), 0, 0.01f);
                Assert.InRange(Math.Abs(nativeY - MathF.Round(nativeY)), 0, 0.01f);
            }
        });
        Assert.Contains(recorder.Triangles, triangle => triangle.B.Depth == 0 || triangle.C.Depth == 0);
        if (radius == 4)
        {
            Assert.Equal(96, recorder.Triangles.Min(triangle => triangle.B.Position.X));
            Assert.Equal(224, recorder.Triangles.Max(triangle => triangle.B.Position.X));
        }
        else
        {
            Assert.Contains(recorder.Triangles, triangle => triangle.B.Position.X == 160 - 1024);
            Assert.Contains(recorder.Triangles, triangle => triangle.B.Position.X == 160 + 1023);
        }
    }

    [Theory]
    [InlineData(16379, true)]
    [InlineData(16380, false)]
    [InlineData(70000, false)]
    public void ShadowSkipsOnlyAtFinalEdgeOrderingDepthCutoff(float cameraDepth, bool submitted)
    {
        (FieldAssets field, ActorVisual actor) = NearCameraFixture(cameraDepth);
        field = WithCamera(field, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, cameraDepth), Vector2.Zero, 16384));
        ShadowRecorder recorder = new();
        FieldRenderer renderer = new(field) { TriangleRenderer = recorder };
        renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
        Assert.Equal(submitted ? 8 : 0, recorder.Triangles.Count);
    }

    [Fact]
    public void ShadowRetainsSaturatedVertexDepthWhenItsFinalEdgeIsNear()
    {
        (FieldAssets field, ActorVisual actor) = NearCameraFixture(8, 255);
        field = WithCamera(field, field.Camera with { AxisY = Vector3.UnitY, AxisZ = new Vector3(40, 0, 1) });
        ShadowRecorder recorder = new();
        FieldRenderer renderer = new(field) { TriangleRenderer = recorder };
        renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);

        Assert.Equal(8, recorder.Triangles.Count);
        Assert.Contains(recorder.Triangles, triangle => triangle.B.Depth == 65535);
        Assert.Contains(recorder.Triangles, triangle => triangle.B.Depth == 0);
        Assert.Equal(0, recorder.Triangles[^1].B.Depth);
        Assert.All(recorder.Triangles, triangle => Assert.InRange(triangle.B.Depth, 0, 65535));
    }

    [Fact]
    public void ShadowOrderingCutoffUsesFinalEdgeRatherThanCenterDepth()
    {
        (FieldAssets field, ActorVisual actor) = NearCameraFixture(16390);
        field = WithCamera(field, field.Camera with { FocalLength = 16384 });
        ShadowRecorder recorder = new();
        FieldRenderer renderer = new(field) { TriangleRenderer = recorder };
        renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
        Assert.Equal(8, recorder.Triangles.Count);
        Assert.All(recorder.Triangles, triangle => Assert.True(triangle.A.Depth >= 16380));
        Assert.True(recorder.Triangles[^1].B.Depth < 16380);
    }

    [Fact]
    public void ShadowAppliesCameraPanAfterNativeScreenSaturation()
    {
        (FieldAssets field, ActorVisual actor) = NearCameraFixture(0, 255);
        ShadowRecorder baseline = new();
        new FieldRenderer(field) { TriangleRenderer = baseline }.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
        Vector2 pan = new(50.25f, -17.5f);
        field = WithCamera(field, field.Camera with { Pan = pan });
        ShadowRecorder shifted = new();
        new FieldRenderer(field) { TriangleRenderer = shifted }.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);

        Assert.Equal(8, baseline.Triangles.Count);
        Assert.Equal(8, shifted.Triangles.Count);
        Vector2 viewportPan = new(pan.X, pan.Y * 240f / 224);
        for (int index = 0; index < baseline.Triangles.Count; index++)
        {
            Vector2 expected = baseline.Triangles[index].B.Position + viewportPan;
            Vector2 actual = shifted.Triangles[index].B.Position;
            Assert.InRange(Vector2.Distance(expected, actual), 0, 0.001f);
            Assert.Equal(baseline.Triangles[index].B.Depth, shifted.Triangles[index].B.Depth);
        }
        Assert.Contains(shifted.Triangles, triangle => triangle.B.Position.X == 160 + 1023 + pan.X);
    }

    private static (FieldAssets Field, ActorVisual Actor) NearCameraFixture(float cameraDepth, byte radius = 4)
    {
        (FieldAssets field, ActorVisual actor) = RaisedModelFixture();
        field = WithCamera(field, field.Camera with { Translation = new Vector3(0, 0, cameraDepth) });
        actor = actor with { ModelId = 1, GroundShadowRadii601To608 = Enumerable.Repeat(radius, 8).ToArray(), GroundShadowLevel609 = 96, GroundShadowCentre = Vector3.Zero };
        return (field, actor);
    }

    private static FieldAssets WithCamera(FieldAssets field, FieldCamera camera)
    {
        return field with { Camera = camera, Cameras = [camera] };
    }

    private sealed class ShadowRecorder : ITriangleRenderer
    {
        public List<(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material)> Triangles { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Triangles.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            if (material.Blend == TriangleBlend.Subtract)
            {
                Triangles.Add((a, b, c, material));
            }
        }

        public void End()
        {
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SlopedGroundShadowPreservesRaisedModelRegardlessOfActorSubmissionOrder(bool reverse, bool movie)
    {
        (FieldAssets field, ActorVisual actor) = RaisedModelFixture();
        FieldRenderer renderer = new(field, modelTextureSampling: ModelTextureSampling.Nearest);
        byte[] moviePixels = Enumerable.Range(0, 320 * 240).SelectMany(_ => new byte[] { 200, 180, 160, 255 }).ToArray();
        byte[] Render(ActorVisual[] actors)
        {
            return movie ? renderer.RenderMovieFrame(moviePixels, 320, 240, field.Camera, actors, null).ToArray() : renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero).ToArray();
        }
        byte[] baseline = Render([actor]);
        ActorVisual shadow = actor with { ModelId = 1, GroundShadowRadius601To608 = 8, GroundShadowLevel609 = 96, GroundShadowCentre = Vector3.Zero };
        byte[] actual = Render(reverse ? [shadow, actor] : [actor, shadow]);
        int modelPixels = 0;
        int shadowPixels = 0;
        for (int index = 0; index < baseline.Length; index += 4)
        {
            if (baseline[index + 2] == 220)
            {
                modelPixels++;
                Assert.Equal(baseline.AsSpan(index, 4).ToArray(), actual.AsSpan(index, 4).ToArray());
            }
            else if (actual[index] < baseline[index])
            {
                shadowPixels++;
            }
        }
        Assert.True(modelPixels > 500, $"Model pixels: {modelPixels}");
        Assert.True(shadowPixels > 500, $"Shadow pixels: {shadowPixels}");
    }

    internal static (FieldAssets Field, ActorVisual Actor) RaisedModelFixture()
    {
        PixelImage ground = new(1, 1, [new Rgba32(200, 180, 160, 255)]);
        PixelImage texture = new(1, 1, [new Rgba32(40, 100, 220, 255)]);
        ModelVertex[] vertices = [new(new Vector3(8, -16, -16), 0), new(new Vector3(8, -16, 16), 0), new(new Vector3(8, 16, -16), 0), new(new Vector3(8, 16, 16), 0)];
        FieldModel model = new("raised-quad", [new(-1, 0)], vertices, [new([0, 1, 2, 3], [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, true)], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        BackgroundTile[] tiles = Enumerable.Range(0, 300).Select(index => new BackgroundTile((index % 20) * 16 - 160, (index / 20) * 16 - 120, 1000, 255, 0, 0, MapBlendMode.None, false, true, ground)).ToArray();
        FieldCamera camera = new(Vector3.UnitX, new Vector3(0, 0.8f, 0.6f), new Vector3(0, -0.6f, 0.8f), new Vector3(0, 0, 256), Vector2.Zero, 256);
        FieldAssets field = new(100, "sloped-shadow", [], [], tiles, camera, new FieldWalkmesh([]), [model, new("shadow-only", [], [], [], [], [])]) { NativeExecutableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash };
        return (field, new ActorVisual(0, 0, 0, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128)));
    }

    [Fact]
    public void DegenerateNativeShadowPreservesTexturedAvatarAndEntireFramebuffer()
    {
        PixelImage texture = new(1, 1, [new Rgba32(100, 140, 200, 255)]);
        FieldModel model = new("triangle", [new ModelBone(-1, 0)], [new ModelVertex(new Vector3(0, -16, -16), 0), new ModelVertex(new Vector3(0, 16, -16), 0), new ModelVertex(new Vector3(0, 0, 16), 0)], [new ModelFace([0, 2, 1], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [texture], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = new(100, "shadow-fixture", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256), new FieldWalkmesh([]), [model]);
        FieldRenderer renderer = new(field, modelTextureSampling: ModelTextureSampling.Nearest);
        ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0, true, PolygonColor: new Vector3(128));
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        Assert.Contains((byte)200, baseline);
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadius601To608 = 0 }], TimeSpan.Zero));
        Assert.NotEqual(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadius601To608 = 0, PolygonColor = new Vector3(64, 128, 192) }], TimeSpan.Zero));
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadius601To608 = 1 }], TimeSpan.Zero));
    }

    [Fact]
    public void NativeSubtractiveFanUsesBodyCenterGradientAndHiddenEligibility()
    {
        PixelImage ground = new(1, 1, [new Rgba32(200, 180, 160, 255)]);
        FieldModel emptyModel = new("shadow-only", [], [], [], [], []);
        BackgroundTile[] tiles = Enumerable.Range(0, 300).Select(index => new BackgroundTile((index % 20) * 16 - 160, (index / 20) * 16 - 120, 1000, 255, 0, 0, MapBlendMode.None, false, true, ground)).ToArray();
        FieldAssets field = new(100, "shadow-fan-fixture", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256), new FieldWalkmesh([]), [emptyModel]) { NativeExecutableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash };
        FieldRenderer renderer = new(field);
        ActorVisual actor = new(0, 0, 500, 700, 0, 0, 0, 0, true, GroundShadowRadius601To608: 4, GroundShadowLevel609: 96, GroundShadowCentre: Vector3.Zero);
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] shadow = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        int center = (120 * 320 + 160) * 4;
        int halfway = (120 * 320 + 176) * 4;
        int outside = (120 * 320 + 200) * 4;
        Assert.InRange(shadow[center], (byte)104, (byte)108);
        Assert.True(shadow[halfway] > shadow[center] && shadow[halfway] < baseline[halfway]);
        Assert.Equal(baseline[outside], shadow[outside]);
        Assert.Equal(Enumerable.Range(0, baseline.Length / 4).Where(pixel => baseline[pixel * 4] != shadow[pixel * 4]).ToArray(), Enumerable.Range(0, baseline.Length / 4).Where(pixel => baseline[pixel * 4 + 1] != shadow[pixel * 4 + 1]).ToArray());
        Assert.All(Enumerable.Range(0, shadow.Length / 4), pixel => Assert.True(baseline[pixel * 4] - shadow[pixel * 4] <= 96));
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { Visible = false }], TimeSpan.Zero));
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { ModelId = -1 }], TimeSpan.Zero));
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowLevel609 = 0 }], TimeSpan.Zero));
        // Fourteen native vertical pixels become fifteen PC viewport pixels.
        Assert.Equal(shadow[center], renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, actorScrollOffset: new Vector2(14, 14))[((135 * 320) + 174) * 4]);
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowLevel609 = null }], TimeSpan.Zero));
        FieldAssets foreground = field with { Background = tiles.Select(tile => tile with { Z = 1 }).ToArray() };
        Assert.Equal(baseline, new FieldRenderer(foreground).Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero));
    }

    [Fact]
    public void NativeEightByteFormChangesTheCorrespondingWorldFanEdge()
    {
        PixelImage ground = new(1, 1, [new Rgba32(200, 180, 160, 255)]);
        FieldModel emptyModel = new("shadow-only", [], [], [], [], []);
        BackgroundTile[] tiles = Enumerable.Range(0, 300).Select(index => new BackgroundTile((index % 20) * 16 - 160, (index / 20) * 16 - 120, 1000, 255, 0, 0, MapBlendMode.None, false, true, ground)).ToArray();
        FieldAssets field = new(100, "shadow-form-fixture", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256), new FieldWalkmesh([]), [emptyModel]) { NativeExecutableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash };
        FieldRenderer renderer = new(field);
        ActorVisual actor = new(0, 0, 500, 700, 0, 0, 0, 0, true, GroundShadowLevel609: 96, GroundShadowCentre: Vector3.Zero, GroundShadowRadii601To608: [4, 4, 4, 4, 4, 8, 4, 4]);
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] form = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        byte[] uniform = renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadii601To608 = null, GroundShadowRadius601To608 = 4 }], TimeSpan.Zero).ToArray();
        int extendedEdge = ((90 * 320) + 130) * 4;
        int oppositeEdge = ((150 * 320) + 190) * 4;
        Assert.Equal(baseline[extendedEdge], uniform[extendedEdge]);
        Assert.True(form[extendedEdge] < baseline[extendedEdge]);
        Assert.Equal(baseline[oppositeEdge], form[oppositeEdge]);
        Assert.Equal(form, renderer.Render(new Dictionary<int, int>(), [actor with { Direction = 128 }], TimeSpan.Zero));
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadii601To608 = new byte[8] }], TimeSpan.Zero));
        Assert.Equal(uniform, renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadii601To608 = [4, 4, 4, 4, 4, 4, 4, 4] }], TimeSpan.Zero));
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadii601To608 = [4, 4] }], TimeSpan.Zero));
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadius601To608 = 4 }], TimeSpan.Zero));
        Assert.All(Enumerable.Range(0, form.Length / 4), pixel => Assert.True(baseline[pixel * 4] - form[pixel * 4] <= 96));
    }
}
