using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCureActorAssemblyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Relocated_enemy_geometry_preserves_material_bytes_without_applying_the_original_row_map_again(bool cura)
    {
        var fixture = Fixture();
        byte[] face = Mesh(11).Polygons[0].Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 230 << 6);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(14), 13);
        face[7] = 145;
        NativeBattleRawMesh mesh = Mesh(11) with { Polygons = [new(false, face.ToImmutableArray())] };
        NativeBattleGraphicsResource resource = fixture.Resource with { Geometry = [mesh], LinkedGeometry = [], GeometryRelocated507550 = true };
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 20000, 4096);
        NativeBattleCameraProjection projection = new(camera, 216, 20, 15);
        NativeBattleCureActorPacketBatch batch = cura
            ? NativeBattleCureActorAssembly.Emit88BDA0(fixture.Model, resource, camera, projection, new(0, 0, 0), 0, 0)
            : NativeBattleCureActorAssembly.Emit8D7110(fixture.Model, resource, camera, projection, new(0, 0, 0), 0, 0);
        NativeBattleGeometryPrimitive body = Assert.Single(batch.Primitives.Where(item => item.Bank == NativeBattleCurePacketBank.Process)).Primitive;
        Assert.Equal((ushort)(230 << 6), (ushort)(body.Words[3] >> 16));
        Assert.Equal((ushort)13, (ushort)(body.Words[5] >> 16));
        Assert.Equal((byte)145, (byte)(body.Words[7] >> 8));
        Assert.Equal(face, mesh.Polygons[0].Bytes.ToArray());
        Assert.Throws<NotSupportedException>(() => Emit(fixture.Model, resource with { GeometryRelocated507550 = false }));
    }

    [Fact]
    public void Relocated_body_keeps_separately_owned_linked_geometry_material_relocation()
    {
        var fixture = Fixture();
        byte[] face = Mesh(11).Polygons[0].Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 230 << 6);
        NativeBattleRawMesh bodyMesh = Mesh(11) with { Polygons = [new(false, face.ToImmutableArray())] };
        NativeBattleGraphicsResource resource = fixture.Resource with { Geometry = [bodyMesh], LinkedGeometry = [Mesh(33)], LinkedGeometryUsesBodySkeleton = false, GeometryRelocated507550 = true };
        NativeBattleCureAllocatedPrimitive[] body = Emit(fixture.Model, resource).Primitives.Where(item => item.Bank == NativeBattleCurePacketBank.Process).ToArray();
        Assert.Equal(2, body.Length);
        Assert.Equal((ushort)(230 << 6), (ushort)(body[0].Primitive.Words[3] >> 16));
        Assert.Equal((ushort)(226 << 6), (ushort)(body[1].Primitive.Words[3] >> 16));
    }

    [Fact]
    public void Shared_weapon_replaces_body_directory_slot_one_and_keeps_its_distinct_material_binding()
    {
        var fixture = Fixture();
        NativeBattleGraphicsResource resource = fixture.Resource with { Geometry = [Mesh(11), Mesh(99)], LinkedGeometry = [Mesh(33)], LinkedGeometryUsesBodySkeleton = true };
        NativeBattleCureActorPacketBatch batch = Emit(fixture.Model, resource);
        Assert.Equal(64, batch.ProcessAllocationBytes);
        NativeBattleCureAllocatedPrimitive[] body = batch.Primitives.Where(primitive => primitive.Bank == NativeBattleCurePacketBank.Process).ToArray();
        Assert.Equal(2, body.Length);
        Assert.Equal((byte)11, (byte)body[0].Primitive.Words[3]);
        Assert.Equal((byte)33, (byte)body[1].Primitive.Words[3]);
        Assert.Equal((ushort)(224 << 6), (ushort)(body[0].Primitive.Words[3] >> 16));
        Assert.Equal((ushort)(226 << 6), (ushort)(body[1].Primitive.Words[3] >> 16));
        Assert.Equal((ushort)10, (ushort)(body[0].Primitive.Words[5] >> 16));
        Assert.Equal((ushort)11, (ushort)(body[1].Primitive.Words[5] >> 16));
        Assert.Equal(new[] { 0, 32 }, body.Select(primitive => primitive.ByteOffset).ToArray());
    }

    [Fact]
    public void Separate_linked_geometry_uses_the_body_pose_and_same_object_mask_even_with_the_ordinary_linked_hide_bit()
    {
        var fixture = Fixture();
        NativeBattleGraphicsModel model = fixture.Model with { Flags0 = 0x206, SequenceState = fixture.Model.SequenceState! with { MeshMask124 = 1 } };
        NativeBattleGraphicsResource resource = fixture.Resource with { Geometry = [Mesh(11), Mesh(99)], LinkedGeometry = [Mesh(33), Mesh(77)], LinkedGeometryUsesBodySkeleton = false, LinkedPoseSource = null };
        NativeBattleCureActorPacketBatch batch = Emit(model, resource);
        NativeBattleCureAllocatedPrimitive[] body = batch.Primitives.Where(primitive => primitive.Bank == NativeBattleCurePacketBank.Process).ToArray();
        Assert.Equal(2, body.Length);
        Assert.Equal(new byte[] { 11, 33 }, body.Select(primitive => (byte)primitive.Primitive.Words[3]).ToArray());
        Assert.Equal(body[0].Primitive.VertexPayload4.ToArray(), body[1].Primitive.VertexPayload4.ToArray());
    }

    [Fact]
    public void Cura_uses_the_proved_shared_actor_assembly_with_its_own_overlay_color_and_texture_projection()
    {
        var fixture = Fixture();
        NativeBattleGraphicsResource resource = fixture.Resource with { Geometry = [Mesh(11)], LinkedGeometry = [] };
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 20000, 4096);
        NativeBattleCureActorPacketBatch cure = NativeBattleCureActorAssembly.Emit8D7110(fixture.Model, resource, camera, new(camera, 216, 20, 15), new(0, 0, 0), 0, 0);
        NativeBattleCureActorPacketBatch cura = NativeBattleCureActorAssembly.Emit88BDA0(fixture.Model, resource, camera, new(camera, 216, 20, 15), new(0, 0, 0), 0, 0);
        Assert.Equal(cure.ProcessAllocationBytes, cura.ProcessAllocationBytes);
        Assert.Equal(cure.Primitives.Where(item => item.Bank == NativeBattleCurePacketBank.Process).Select(item => item.Primitive.Words.ToArray()), cura.Primitives.Where(item => item.Bank == NativeBattleCurePacketBank.Process).Select(item => item.Primitive.Words.ToArray()));
        NativeBattleGeometryPrimitive overlay = Assert.Single(cura.Primitives.Where(item => item.Bank == NativeBattleCurePacketBank.EffectPrimary)).Primitive;
        NativeBattleCureTextureProjection texture = NativeBattleCureActorPackets.TextureProjection8D7050(640, 448, 320, 244, 128, 64);
        Assert.Equal(0x404040u, overlay.Words[1] & 0xFFFFFF);
        Assert.Equal(texture.Clut, unchecked((ushort)(overlay.Words[3] >> 16)));
        Assert.Equal(texture.TexturePage, unchecked((ushort)(overlay.Words[6] >> 16)));
    }

    private static NativeBattleCureActorPacketBatch Emit(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource)
    {
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 20000, 4096);
        return NativeBattleCureActorAssembly.Emit8D7110(model, resource, camera, new(camera, 216, 0, 0), new(0, 0, 1024), 0, null);
    }

    private static NativeBattleRawMesh Mesh(byte u)
    {
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        face[6] = 17;
        face[8] = u;
        face[12] = 22;
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 224 << 6);
        return new([new(new(0, 0, 0), 0), new(new(64, 0, 0), 0), new(new(0, 64, 0), 0)], [new(false, face.ToImmutableArray())]);
    }

    private static (NativeBattleGraphicsModel Model, NativeBattleGraphicsResource Resource) Fixture()
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        //One frame with zero three-bit root translations, uncompressed root, and a zero six-bit rotation field.
        byte[] animations = [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0];
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
        byte?[] body = new byte?[16];
        byte?[] linked = new byte?[16];
        body[0] = 0;
        linked[0] = 2;
        NativeBattleGraphicsModel model = new(0, 0)
        {
            Color40 = 0x808080,
            GeometryWorldMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1024, 4096),
            SequenceState = new() { Pose = 0, PoseFrame114 = 1 },
            BodyTextureSlots = new(body.ToImmutableArray()),
            LinkedTextureSlots = new(linked.ToImmutableArray())
        };
        return (model, new([0, 0]) { PoseSource = source });
    }
}
