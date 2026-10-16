using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCureActorPacketTests
{
    private static readonly NativeBattleActionMatrix Bone = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(null, 128, 0, 0);

    [Fact]
    public void Texture_projection_keeps_the_original_page_CLUT_WORD_arithmetic_and_odd_extent_truncation()
    {
        NativeBattleCureTextureProjection texture = NativeBattleCureActorPackets.TextureProjection8D7050(512, 384, 320, 241, 128, 128);
        Assert.Equal(new(0xB8, 0x3C54, 96, 223, 56, 183, -96, 72), texture);
        NativeBattleCureTextureProjection odd = NativeBattleCureActorPackets.TextureProjection8D7050(513, 385, 320, 241, 129, 129);
        Assert.Equal(new(0xB8, 0x3C54, 96, 223, 56, 183, -94, 73), odd);
    }

    [Fact]
    public void Actor_and_overlay_packets_keep_separate_cursors_native_insertion_order_and_each_projection_depth_FIFO()
    {
        NativeBattleCureActorPacketBatch batch = Emit(Mesh());
        Assert.Equal(72, batch.ProcessAllocationBytes);
        Assert.Equal(92, batch.EffectAllocationBytes);
        Assert.Equal(new[] { NativeBattleCurePacketBank.EffectPrimary, NativeBattleCurePacketBank.Process, NativeBattleCurePacketBank.EffectPrimary, NativeBattleCurePacketBank.Process }, batch.Primitives.Select(primitive => primitive.Bank));
        Assert.Equal(new[] { 0, 0, 40, 32 }, batch.Primitives.Select(primitive => primitive.ByteOffset));
        NativeBattleGeometryPrimitive triangle = batch.Primitives[1].Primitive;
        Assert.Equal(80, triangle.Bucket);
        Assert.Equal(new uint[] { 0x7000000, 0x26102030, Screen(0, 0), 0x12340201, Screen(64, 0), 0x02560605, Screen(0, 64), 0x0908 }, triangle.Words);
        Assert.Equal(new[] { 1024, 1024, 1024, 0 }, triangle.VertexPayload4);
        NativeBattleGeometryPrimitive overlay = batch.Primitives[0].Primitive;
        Assert.Equal(80, overlay.Bucket);
        Assert.Equal(new uint[] { 0x9000000, 0x36808080, Screen(0, 0), 0x3C548000, 0x36808080, Screen(64, 0), 0x00B88020, 0x36808080, Screen(0, 64), 0xA000 }, overlay.Words);
        Assert.Equal(new[] { 2048, 2048, 2048, 0 }, overlay.VertexPayload4);
        Assert.Equal(81, batch.Primitives[3].Primitive.Bucket);
        Assert.Equal(0x2C102030u, batch.Primitives[3].Primitive.Words[1]);
        Assert.Equal(0x3E808080u, batch.Primitives[2].Primitive.Words[1]);
        Assert.Equal(13, batch.Primitives[2].Primitive.Words.Length);
        Assert.Equal(new[] { 2048, 2048, 2048, 2048 }, batch.Primitives[2].Primitive.VertexPayload4);
        string json = JsonSerializer.Serialize(batch);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleCureActorPacketBatch>(json)));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    public void Retained_overlay_cull_owner_is_read_only_when_a_projected_backface_could_emit(ushort cull, int overlays)
    {
        NativeBattleActionMatrix reversed = Bone with { Rotation = [-4096, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 64, Z = 2048 };
        NativeBattleRawMesh mesh = Mesh() with { Polygons = [Face(false)] };
        NativeBattleCureActorPacketBatch batch = Emit(mesh, reversed, cull);
        Assert.Equal(overlays, batch.Primitives.Count(primitive => primitive.Bank == NativeBattleCurePacketBank.EffectPrimary));
        Assert.Single(batch.Primitives.Where(primitive => primitive.Bank == NativeBattleCurePacketBank.Process));
        Assert.Throws<NotSupportedException>(() => Emit(mesh, reversed, null));
    }

    [Fact]
    public void Native_eighth_pixel_projection_is_not_rescaled_to_force_the_overlay_into_its_texture_window()
    {
        NativeBattleCureActorPacketBatch batch = NativeBattleCureActorPackets.Emit8D7430([Mesh()], [Bone], Bone with { Z = 1024 }, Bone with { Z = 2048 }, Projection with { OffsetX = 160, OffsetY = 108 }, uint.MaxValue, 0x102030, 0x808080, NativeBattleCureActorPackets.TextureProjection8D7050(512, 384, 320, 241, 128, 128), null, null, 17);
        Assert.Equal(0, batch.EffectAllocationBytes);
        Assert.Equal(2, batch.Primitives.Length);
        Assert.All(batch.Primitives, primitive => Assert.Equal(NativeBattleCurePacketBank.Process, primitive.Bank));
        Assert.Equal(Screen(1280, 864), batch.Primitives[0].Primitive.Words[2]);
    }

    [Fact]
    public void Bone_MVMVA_clamps_the_intermediate_WORD_and_actor_faces_do_not_apply_an_unrelated_near_flag_rejection()
    {
        NativeBattleVector clamped = NativeBattleCureActorPackets.TransformBone4607E0(new(new(1, -1, 1), 0), [Bone with { X = 40000, Y = -40000, Z = 32767 }]);
        Assert.Equal(new(short.MaxValue, short.MinValue, short.MaxValue), clamped);
        NativeBattleRawMesh mesh = Mesh() with { Polygons = [Face(false)] };
        NativeBattleCureActorPacketBatch near = NativeBattleCureActorPackets.Emit8D7430([mesh], [Bone], Bone with { Z = -1 }, Bone with { Z = 2048 }, Projection, 1, 0x102030, 0x808080, NativeBattleCureActorPackets.TextureProjection8D7050(512, 384, 320, 241, 320, 240), 0, null, 17);
        NativeBattleGeometryPrimitive body = Assert.Single(near.Primitives.Where(primitive => primitive.Bank == NativeBattleCurePacketBank.Process)).Primitive;
        Assert.Equal(17, body.Bucket);
        Assert.Equal(new[] { 0, 0, 0, 0 }, body.VertexPayload4);
        NativeBattleCureActorPacketBatch hidden = NativeBattleCureActorPackets.Emit8D7430([mesh], [Bone], Bone with { Z = 1024 }, Bone with { Z = 2048 }, Projection, 0, 0, 0, NativeBattleCureActorPackets.TextureProjection8D7050(512, 384, 320, 241, 320, 240), 0, null, 17);
        Assert.Empty(hidden.Primitives);
        Assert.Throws<InvalidDataException>(() => NativeBattleCureActorPackets.TransformBone4607E0(new(new(0, 0, 0), 1), [Bone]));
    }

    private static NativeBattleCureActorPacketBatch Emit(NativeBattleRawMesh mesh, NativeBattleActionMatrix? overlay = null, ushort? cull = 0)
    {
        return NativeBattleCureActorPackets.Emit8D7430([mesh], [Bone], Bone with { Z = 1024 }, overlay ?? Bone with { Z = 2048 }, Projection, uint.MaxValue, 0x102030, 0x808080, NativeBattleCureActorPackets.TextureProjection8D7050(512, 384, 320, 241, 320, 240), cull, null, 17);
    }

    private static NativeBattleRawMesh Mesh()
    {
        //Quad appears first deliberately;8D7430 still visits the plain triangle group first and ignores colored groups.
        return new([new(new(0, 0, 0), 0), new(new(64, 0, 0), 0), new(new(0, 64, 0), 0), new(new(64, 64, 0), 0)], [Face(true), new(false, new byte[20].ToImmutableArray()), Face(false)]);
    }

    private static NativeBattleRawPolygon Face(bool quad)
    {
        byte[] bytes = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), quad ? (ushort)3 : (ushort)0x0908);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), quad ? 0x00560605u : 0x02560605u);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 0x0908);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 0x0B0A);
        }
        return new(quad, bytes.ToImmutableArray());
    }

    private static uint Screen(short x, short y) => unchecked((ushort)x) | unchecked((uint)y << 16);
}
