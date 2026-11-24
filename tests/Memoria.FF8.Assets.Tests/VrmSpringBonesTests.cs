using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class VrmSpringBonesTests
{
    [Fact]
    public void ReadsAndWritesStandardSpringBoneMetadata()
    {
        using JsonDocument document = ParseExtension("""
            {
              "specVersion": "1.0",
              "colliders": [
                { "node": 0, "shape": { "sphere": {} } },
                { "node": 1, "shape": { "capsule": { "offset": [1, 2, 3], "radius": 0.25, "tail": [1, 4, 3] } } }
              ],
              "colliderGroups": [{ "name": "body", "colliders": [0, 1] }],
              "springs": [{
                "name": "hair",
                "center": 0,
                "colliderGroups": [0],
                "joints": [
                  { "node": 1, "hitRadius": 0.25, "stiffness": 2, "gravityPower": 0.5, "gravityDir": [1, 0, 0], "dragForce": 0.75 },
                  { "node": 3 }
                ]
              }]
            }
            """);
        int[] parents = [-1, 0, 1, 2];
        VrmSpringBones input = Assert.IsType<VrmSpringBones>(VrmSpringBones.Read(document.RootElement, parents));
        Assert.Equal(new VrmSpringCollider(0, Vector3.Zero, 0), input.Colliders[0]);
        Assert.Equal(new VrmSpringCollider(1, new Vector3(1, 2, 3), 0.25f, new Vector3(1, 4, 3)), input.Colliders[1]);
        VrmSpringColliderGroup group = Assert.Single(input.ColliderGroups);
        Assert.Equal("body", group.Name);
        Assert.Equal([0, 1], group.Colliders);
        VrmSpring spring = Assert.Single(input.Springs);
        Assert.Equal("hair", spring.Name);
        Assert.Equal(0, spring.Center);
        Assert.Equal([0], spring.ColliderGroups);
        Assert.Equal(new VrmSpringJoint(1, 0.25f, 2, 0.5f, Vector3.UnitX, 0.75f), spring.Joints[0]);
        Assert.Equal(new VrmSpringJoint(3, 0, 1, 0, -Vector3.UnitY, 0.5f), spring.Joints[1]);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("extensions");
            input.Write(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        using JsonDocument exported = JsonDocument.Parse(stream.ToArray());
        VrmSpringBones roundTrip = Assert.IsType<VrmSpringBones>(VrmSpringBones.Read(exported.RootElement, parents));
        Assert.Equal(input.Colliders.ToArray(), roundTrip.Colliders.ToArray());
        Assert.Equal(group.Name, roundTrip.ColliderGroups[0].Name);
        Assert.Equal(group.Colliders.ToArray(), roundTrip.ColliderGroups[0].Colliders.ToArray());
        Assert.Equal(spring.Name, roundTrip.Springs[0].Name);
        Assert.Equal(spring.Center, roundTrip.Springs[0].Center);
        Assert.Equal(spring.ColliderGroups.ToArray(), roundTrip.Springs[0].ColliderGroups.ToArray());
        Assert.Equal(spring.Joints.ToArray(), roundTrip.Springs[0].Joints.ToArray());
    }

    [Theory]
    [InlineData("""{ "specVersion": "2.0" }""")]
    [InlineData("""{ "specVersion": "1.0", "springs": [{ "joints": [{ "node": 5 }] }] }""")]
    [InlineData("""{ "specVersion": "1.0", "springs": [{ "joints": [{ "node": 2 }, { "node": 1 }] }] }""")]
    [InlineData("""{ "specVersion": "1.0", "springs": [{ "joints": [{ "node": 0 }, { "node": 2 }] }, { "joints": [{ "node": 1 }] }] }""")]
    [InlineData("""{ "specVersion": "1.0", "springs": [{ "joints": [{ "node": 2 }], "center": 3 }] }""")]
    [InlineData("""{ "specVersion": "1.0", "springs": [{ "joints": [{ "node": 0 }] }, { "joints": [{ "node": 2 }], "center": 1 }] }""")]
    [InlineData("""{ "specVersion": "1.0", "springs": [{ "joints": [{ "node": 0, "dragForce": 1.1 }] }] }""")]
    [InlineData("""{ "specVersion": "1.0", "springs": [{ "joints": [{ "node": 0, "gravityDir": [0, 1e100, 0] }] }] }""")]
    [InlineData("""{ "specVersion": "1.0", "colliders": [{ "node": 0, "shape": { "sphere": {}, "capsule": {} } }] }""")]
    [InlineData("""{ "specVersion": "1.0", "colliderGroups": [{ "colliders": [0] }] }""")]
    public void RejectsInvalidSpringBoneMetadata(string extension)
    {
        using JsonDocument document = ParseExtension(extension);
        Assert.Throws<InvalidDataException>(() => VrmSpringBones.Read(document.RootElement, [-1, 0, 1, 1]));
    }

    [Fact]
    public void RejectsCyclicNodeHierarchy()
    {
        using JsonDocument document = ParseExtension("""{ "specVersion": "1.0", "springs": [{ "joints": [{ "node": 0 }, { "node": 1 }] }] }""");
        Assert.Throws<InvalidDataException>(() => VrmSpringBones.Read(document.RootElement, [1, 0]));
    }

    [Fact]
    public void ReadsAnOptionalExtension()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "extensions": { "other_extension": {} } }""");
        Assert.Null(VrmSpringBones.Read(document.RootElement, []));
    }

    private static JsonDocument ParseExtension(string extension)
    {
        return JsonDocument.Parse($$""" { "extensions": { "VRMC_springBone": {{extension}} } } """);
    }
}
