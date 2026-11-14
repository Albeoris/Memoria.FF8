using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7WaterAppearanceRendererTests
{
    [Fact]
    public void TargetVertexMasksRemainBoundToCommittedPoseIdentitiesWhilePresentationMoves()
    {
        (Ff7FieldResources field, Ff7FieldModelResources original) = Fixture();
        Appearance appearance = new();
        Ff7ActorVisual pose = new(0, 0, 0, 0, 0, 0, 0, 0);
        bool[] mask = Ff7FieldRenderer.CaptureAppearanceVertexColorMask(appearance, pose, field.Models.Models[0], original, new(true, 0, false));
        Assert.Equal(new[] { true, false, true }, mask);
        Assert.Equal(1, appearance.SampleCalls);
        Ff7ModelRenderOverrides effects = new(_ => true, null, false)
        {
            SourceVertexColor = (_, _, _) => throw new InvalidOperationException("Native P indices must not select target mesh colors."),
            AppearanceVertexColor = (vertex, color) => mask[vertex] ? Ff7NativeColorOffset.Shade(color, -64, -32, 0) : color
        };
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = original }, new(50, 32000), appearance);
        Capture before = Draw(renderer, pose with { ModelEffects = effects });
        Capture moving = Draw(renderer, pose with { AnimationFrame16 = 8, ModelEffects = effects });
        Assert.Equal(3, moving.Vertices.Count);
        Assert.Equal(new Vector4(191 / 255f, 223 / 255f, 1, 1), moving.Vertices[0].Color);
        Assert.Equal(moving.Vertices[0].Color, moving.Vertices[1].Color);
        Assert.Equal(Vector4.One, moving.Vertices[2].Color);
        Assert.Equal(before.Vertices.Select(vertex => vertex.Color), moving.Vertices.Select(vertex => vertex.Color));
        Assert.NotEqual(before.Vertices[0].Position, moving.Vertices[0].Position);
        Assert.Equal(1, appearance.SampleCalls);
        bool[] coldMask = JsonSerializer.Deserialize<bool[]>(JsonSerializer.Serialize(mask))!;
        Ff7ModelRenderOverrides coldEffects = effects with { AppearanceVertexColor = (vertex, color) => coldMask[vertex] ? Ff7NativeColorOffset.Shade(color, -64, -32, 0) : color };
        Capture cold = Draw(renderer, pose with { AnimationFrame16 = 8, ModelEffects = coldEffects });
        Assert.Equal(moving.Vertices, cold.Vertices);
        Assert.Equal(1, appearance.SampleCalls);
    }

    [Fact]
    public void IndexedEffectsRequireExplicitAppearanceCapabilitySamplingAndStableIds()
    {
        (Ff7FieldResources field, Ff7FieldModelResources original) = Fixture();
        Appearance appearance = new();
        Ff7ActorVisual pose = new(0, 0, 0, 0, 0, 0, 0, 0);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = original }, new(50, 32000), appearance);
        Ff7ModelRenderOverrides unbound = new(_ => true, null, false, false) { SourceVertexColor = (_, _, color) => color };
        Assert.Throws<NotSupportedException>(() => Draw(renderer, pose with { ModelEffects = unbound }));
        Ff7ModelRenderOverrides bound = unbound with { AppearanceVertexColor = (_, color) => color };
        appearance.SupportsVertexColorBindings = false;
        Assert.Throws<NotSupportedException>(() => Draw(renderer, pose with { ModelEffects = bound }));
        appearance.SupportsVertexColorBindings = true;
        appearance.EmitIdentities = false;
        Assert.Throws<InvalidDataException>(() => Draw(renderer, pose with { ModelEffects = bound }));
        appearance.SampleSucceeds = false;
        Assert.Throws<NotSupportedException>(() => Ff7FieldRenderer.CaptureAppearanceVertexColorMask(appearance, pose, field.Models.Models[0], original, new(true, 0, false)));
        Assert.Throws<InvalidDataException>(() => Ff7FieldRenderer.CaptureAppearanceVertexColorMask(appearance, pose with { AnimationFrame16 = 8 }, field.Models.Models[0], original, new(true, 0, false)));
        Assert.Throws<InvalidDataException>(() => Ff7FieldRenderer.CaptureAppearanceVertexColorMask([new(float.NaN, 0, 0)], new(true, 0, false)));
        Assert.Throws<InvalidDataException>(() => Ff7FieldRenderer.CaptureAppearanceVertexColorMask([Vector3.Zero], new(true, double.NaN, false)));
        Assert.Equal(new[] { false }, Ff7FieldRenderer.CaptureAppearanceVertexColorMask([Vector3.Zero], new(false, 0, false)));
    }

    [Fact]
    public void SplashStateFollowsTheLastSubmittedMappedMaterialAfterDifferentNativeState()
    {
        (Ff7FieldResources field, Ff7FieldModelResources original) = Fixture();
        PcTexture texture = new(1, 1, 4, [], new byte[] { 255, 255, 255, 255 }, new byte[236]);
        field = field with
        {
            Models = field.Models with { Models = [field.Models.Models[0], field.Models.Models[0] with { Hrc = "mapped.hrc" }] },
            WaterSplashTextures = [texture, texture, texture, texture]
        };
        Appearance appearance = new()
        {
            TargetModel = 1,
            Material = new(null, TriangleBlend.Replace, TriangleDepthComparison.LessEqual, true, 0.25f, TriangleSampler.LinearClamp)
        };
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = original, [1] = original }, new(50, 32000), appearance);
        Capture capture = new();
        renderer.Render(capture, 320, 240, new(0, Vector2.Zero, [new(0, 0, 0, 0, 0, 0, 0, 0), new(1, 1, 0, 0, 0, 0, 0, 0)], new ushort[256]) { WaterSplashes = [new(0, new(160, 120), 0.1f, 12)] });
        Assert.Equal(4, capture.Materials.Count);
        Assert.False(capture.Materials[0].DepthWrite);
        Assert.Equal(TriangleSampler.PointWrap, capture.Materials[0].Sampler);
        Assert.True(capture.Materials[1].DepthWrite);
        Assert.Equal(TriangleSampler.LinearClamp, capture.Materials[1].Sampler);
        Assert.All(capture.Materials.TakeLast(2), material =>
        {
            Assert.Equal(TriangleDepthComparison.LessEqual, material.DepthComparison);
            Assert.True(material.DepthWrite);
            Assert.Equal(0.25f, material.MinimumAlpha);
            Assert.Equal(TriangleSampler.PointClamp, material.Sampler);
        });
    }

    private static (Ff7FieldResources Field, Ff7FieldModelResources Original) Fixture()
    {
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 1000), 0, 100);
        PcFieldModelDescriptor descriptor = new("native", 0, "native.hrc", "128", [new(0, Vector3.Zero), new(0, Vector3.Zero), new(0, Vector3.Zero)], uint.MaxValue, ["pose.a"]);
        PcFieldTriggers triggers = new("appearance", 0, 0, -160, -120, 160, 120, [], [], [], []);
        Ff7FieldResources field = new("appearance", null!, null!, new(0, 0, [descriptor]), [camera], [], triggers, new(0, 0, 1, [[0x7FFF]]), new(320, 240, [0], [], []));
        HrcHierarchy hierarchy = HrcHierarchy.Read(Encoding.ASCII.GetBytes(":HEADER_BLOCK 2\n:SKELETON native\n:BONES 0\nbody\nroot\n0\n1 body\n"));
        byte[] state = new byte[100];
        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(12), 0x18004);
        PcPolygonModel polygon = new([Vector3.Zero, new(0, 10, 0), new(10, 0, 0)], [], [], [uint.MaxValue, uint.MaxValue, uint.MaxValue], [], [new(0, 0, 1, 2, 0, 0, 0, ReadOnlyMemory<byte>.Empty)], [state], [new(0, 0, 1, 0, 3, 0, 0, 0, ReadOnlyMemory<byte>.Empty)], ReadOnlyMemory<byte>.Empty);
        Ff7BoneGeometry part = new(0, "body.rsd", new("native", "body.p", [], new Dictionary<string, string>()), "body.p", polygon, []);
        byte[] clip = new byte[60];
        BinaryPrimitives.WriteUInt32LittleEndian(clip, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(clip.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(clip.AsSpan(12), 0x020001);
        Ff7FieldModelResources original = new("native.hrc", hierarchy, [part], new Dictionary<string, PcFieldAnimation> { ["pose.a"] = PcFieldAnimation.Read(clip) });
        return (field, original);
    }

    private static Capture Draw(Ff7FieldRenderer renderer, Ff7ActorVisual actor)
    {
        Capture capture = new();
        renderer.Render(capture, 320, 240, new(0, Vector2.Zero, [actor], new ushort[256]));
        return capture;
    }

    private sealed class Appearance : IFf7ActorAppearance
    {
        public bool SupportsPartVisibility => true;
        public bool SupportsVertexColorBindings { get; set; } = true;
        public bool SampleSucceeds { get; set; } = true;
        public bool EmitIdentities { get; set; } = true;
        public int SampleCalls { get; private set; }
        public int TargetModel { get; init; }
        public TriangleMaterial Material { get; init; } = new(null);

        public bool TrySampleVertices(Ff7ActorVisual pose, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Vector3> worldVertices)
        {
            SampleCalls++;
            worldVertices = Positions(pose);
            return SampleSucceeds;
        }

        public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
        {
            triangles = [];
            if (actor.ModelIndex != TargetModel)
            {
                return false;
            }
            Vector3[] positions = Positions(actor);
            Ff7WorldVertex Vertex(int index)
            {
                return new(positions[index], Vector2.Zero, Vector4.One) { AppearanceVertexIndex = EmitIdentities ? index : -1 };
            }
            triangles = [new(Vertex(2), Vertex(0), Vertex(1), Material)];
            return true;
        }

        private static Vector3[] Positions(Ff7ActorVisual pose)
        {
            float movement = pose.AnimationFrame16 / 16f * -30;
            return [new(-10, 0, 10 + movement), new(10, 0, -10 + movement), new(0, 10, 20 + movement)];
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];
        public List<TriangleMaterial> Materials { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.AddRange([a, b, c]);
            Materials.Add(material);
        }

        public void End()
        {
        }
    }
}
