using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7WaterProfileSixTests
{
    [Fact]
    public void NativeBlueCarryIncludesRejectedRepeatedAndLaterPartVerticesAndResetsPerCapture()
    {
        var fixture = Fixture();
        Ff7FieldWaterParameters parameters = new(1, 0, 0, 0, 4, 6);
        Ff7FieldWaterGeometry geometry = Capture(fixture, parameters, 600);
        Assert.Equal(new short[] { 30, 120, 90 }, geometry.Parts[0].BlueOffsets);
        Assert.Equal(new short[] { 150, 180, 210 }, geometry.Parts[1].BlueOffsets);
        Assert.True(geometry.Parts[0].ModifiedVertices[0]);
        Assert.False(geometry.Parts[0].ModifiedVertices[2]);
        Assert.Equal(JsonSerializer.Serialize(geometry), JsonSerializer.Serialize(Capture(fixture, parameters, 600)));
        Assert.Equal((short)0, parameters.Blue);
        Assert.Equal(unchecked((short)(32760 + 30)), Ff7FieldWaterGeometryMath.ProfileSixBlueOffset(32760, 0));
        Assert.Equal(unchecked((short)(32760 + 60)), Ff7FieldWaterGeometryMath.ProfileSixBlueOffset(32760, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ff7FieldWaterGeometryMath.ProfileSixBlueOffset(0, -1));
    }

    [Fact]
    public void FixedExclusivePlaneIgnoresFlagsAndLevelAndQuarteringPrecedesBoneAndActorTransforms()
    {
        var fixture = Fixture();
        Ff7FieldWaterParameters parameters = new(1, 0, 0, 0, 0, 6);
        Ff7FieldWaterGeometry geometry = Capture(fixture, parameters, 600);
        Assert.Equal(543, geometry.ColorRegion!.Height);
        Assert.True(geometry.ColorRegion.ProfileSix);
        Assert.Equal(new Vector3(1, 0.25f, 1), geometry.ColorRegion.LocalVertexScale);
        Assert.Equal(geometry.Parts[0].ModifiedVertices, Capture(fixture, parameters with { Level = -7, Flags = 4 }, 600).Parts[0].ModifiedVertices);
        Assert.False(Capture(fixture, parameters, 543).Parts[0].ModifiedVertices[0]);
        Assert.True(Capture(fixture, parameters, 543.25f).Parts[0].ModifiedVertices[0]);
        Matrix4x4 basis = Ff7NativeFieldPoseMath.ModelBasis(fixture.Descriptor);
        Matrix4x4 transform = Ff7NativeFieldPoseMath.Sample(fixture.Model, fixture.Animation, 0, basis.M11).Bones[0] * basis * Matrix4x4.CreateTranslation(0, 0, 600);
        Vector3 vertex = fixture.Model.Geometry[0].Polygon.Vertices[1];
        bool expected = Vector3.Transform(vertex * new Vector3(1, 0.25f, 1), transform).Z > 543;
        bool incorrectlyScaledWorld = (Vector3.Transform(vertex, transform) * new Vector3(1, 0.25f, 1)).Z > 543;
        Assert.NotEqual(expected, incorrectlyScaledWorld);
        Assert.Equal(expected, geometry.Parts[0].ModifiedVertices[1]);
    }

    [Fact]
    public void SourceAndTargetShadersUseTheirCapturedOrdinalsPreserveAlphaAndRestoreCold()
    {
        var fixture = Fixture();
        Ff7FieldWaterGeometry geometry = Capture(fixture, new(1, 0, 0, 0, 0, 6), 600);
        Ff7NativeWaterEffectState water = Water(geometry);
        string before = JsonSerializer.Serialize(water);
        Ff7ModelRenderOverrides source = Ff7Scene.CreateWaterModelEffects(water);
        var sourceShader = source.SourceVertexColor!;
        Assert.Equal(0x7330304EU, sourceShader(0, 0, 0x73303030));
        Assert.Equal(0x733030A8U, sourceShader(0, 1, 0x73303030));
        Assert.Equal(0x73303030U, sourceShader(0, 2, 0x73303030));
        Assert.Equal(0x733030C6U, sourceShader(1, 0, 0x73303030));
        Appearance appearance = new();
        Ff7ModelRenderOverrides target = Ff7Scene.CreateWaterAppearanceEffects(water, appearance, new(0, 0, 0, 0, 0, 0, 0, 0), fixture.Descriptor, fixture.Model, geometry.ColorRegion!);
        var targetShader = target.AppearanceVertexColor!;
        Assert.Equal(new Vector3(1, 0.25f, 1), appearance.Scale);
        Assert.Equal(0x7330304EU, targetShader(0, 0x73303030));
        Assert.Equal(0x73303030U, targetShader(1, 0x73303030));
        Assert.Equal(0x7330308AU, targetShader(2, 0x73303030));
        Ff7NativeWaterEffectState cold = JsonSerializer.Deserialize<Ff7NativeWaterEffectState>(before)!;
        Assert.Equal(sourceShader(0, 1, 0x73303030), Ff7Scene.CreateWaterModelEffects(cold).SourceVertexColor!(0, 1, 0x73303030));
        Ff7NativeWaterEffectState malformed = water with { DisplayedSample = water.DisplayedSample! with { Parts = [new(0, [true, false]) { BlueOffsets = [30] }] } };
        Assert.Throws<InvalidDataException>(malformed.Validate);
        Assert.Equal(before, JsonSerializer.Serialize(water));
        Ff7NativeWaterEffectSample originalSample = water.DisplayedSample!;
        Ff7NativeWaterEffectState legacy = water with { DisplayedSample = originalSample with { Parameters = originalSample.Parameters with { Blue = 5, EnvironmentProfile = 4 }, Parts = [new(0, [true])], ColorRegion = new(true, 0, false) } };
        Assert.Equal(0x73303035U, Ff7Scene.CreateWaterModelEffects(legacy).SourceVertexColor!(0, 0, 0x73303030));
        Assert.Equal(Vector3.One, new Ff7FieldWaterColorRegion(true, 0, false).LocalVertexScale);
    }

    private sealed class Appearance : IFf7ActorAppearance
    {
        public bool SupportsVertexColorBindings => true;
        public Vector3 Scale { get; private set; }

        public bool TrySampleVertices(Ff7ActorVisual pose, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, Vector3 modelLocalScale, out IReadOnlyList<Vector3> vertices)
        {
            Scale = modelLocalScale;
            vertices = [new(0, 0, 544), new(0, 0, 543), new(0, 0, 600)];
            return true;
        }

        public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
        {
            triangles = [];
            return false;
        }
    }

    private static Ff7NativeWaterEffectState Water(Ff7FieldWaterGeometry geometry)
    {
        return new() { RequestedPayload = [1], Initialized = true, OwnsColors = true, DisplayedSample = new(new(1, 0, 0, 0, 0, 6), geometry.Parts.ToArray(), 0, 0) { ColorRegion = geometry.ColorRegion } };
    }

    private static Ff7FieldWaterGeometry Capture((Ff7FieldModelResources Model, PcFieldModelDescriptor Descriptor, PcFieldAnimation Animation) fixture, Ff7FieldWaterParameters parameters, float height)
    {
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 10000), 0, 100);
        return Ff7FieldWaterGeometryMath.Capture(fixture.Model, fixture.Descriptor, fixture.Animation, 0, new(0, 0, height), 0, camera, Vector2.Zero, 50, 32000, parameters, false);
    }

    private static (Ff7FieldModelResources Model, PcFieldModelDescriptor Descriptor, PcFieldAnimation Animation) Fixture()
    {
        HrcHierarchy hierarchy = HrcHierarchy.Read(Encoding.ASCII.GetBytes(":HEADER_BLOCK 2\n:SKELETON water\n:BONES 1\nbody\nroot\n3000\n2 body other\n"));
        PcModelGroup group = new(0, 0, 0, 0, 3, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        PcPolygonModel polygon = new([Vector3.Zero, new(0, 200, 0), new(0, 400, 0)], [], [], [uint.MaxValue, uint.MaxValue, uint.MaxValue], [], [], [], [group, group with { VertexStart = 1, VertexCount = 1 }], ReadOnlyMemory<byte>.Empty);
        Ff7BoneGeometry first = new(0, "body.rsd", new("water", "body.p", [], new Dictionary<string, string>()), "body.p", polygon, []);
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x020001);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(60), 45);
        PcFieldAnimation animation = PcFieldAnimation.Read(bytes);
        Ff7BoneGeometry second = first with { DescriptorName = "other.rsd", PolygonName = "other.p", Polygon = polygon with { Groups = [group] } };
        Ff7FieldModelResources model = new("water.hrc", hierarchy, [first, second], new Dictionary<string, PcFieldAnimation> { ["water.a"] = animation });
        PcFieldModelDescriptor descriptor = new("water", 0, "water.hrc", "128", [new(0, Vector3.Zero), new(0, Vector3.Zero), new(0, Vector3.Zero)], uint.MaxValue, ["water.a"]);
        return (model, descriptor, animation);
    }
}
