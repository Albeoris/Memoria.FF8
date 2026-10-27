using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco100ActorPacketTests
{
    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleCameraProjection Projection => new(null, 128, 0, 0);

    [Theory]
    [InlineData(1024, 512)]
    [InlineData(3072, -512)]
    public void Wave_uses_32768_division_after_flattening_and_preserves_XY(ushort angle, short expectedZ)
    {
        Assert.Equal(new NativeBattleVector(12, 34, expectedZ), NativeBattleChoco100ActorPackets.DeformVertex(new(12, 34, 1000), new(4096, 4096, 4096, 0, angle)));
    }

    [Fact]
    public void Signed_divisions_truncate_toward_zero_and_angle_wraps_before_lookup()
    {
        Assert.Equal(new NativeBattleVector(1, 2, -3), NativeBattleChoco100ActorPackets.DeformVertex(new(1, 2, -5), new(-7, 2048, 2048, 0, 1024)));
        Assert.Equal(new NativeBattleVector(1, 2, 512), NativeBattleChoco100ActorPackets.DeformVertex(new(1, 2, 0), new(4096, 4096, 0, ushort.MaxValue, 1026)));
    }

    [Fact]
    public void Radius_and_flatten_operands_are_signed_WORDs_before_multiplication()
    {
        Assert.Equal(new NativeBattleVector(1, 2, -512), NativeBattleChoco100ActorPackets.DeformVertex(new(1, 2, 0), new(4096, 0xF000, 0, 0, 1024)));
        Assert.Equal(new NativeBattleVector(1, 2, 2000), NativeBattleChoco100ActorPackets.DeformVertex(new(1, 2, 1000), new(4096, 0, 0xF000, 0, 0)));
    }

    [Fact]
    public void Native_work_records_and_plain_faces_preserve_unknown_padding_and_partial_UV_stores()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x20000, Enumerable.Repeat((byte)0xA5, 0xA000).ToArray());
        NativeBattleChoco100ActorDraw result = Emit(Mesh(), arena);
        Assert.Equal(72, result.Packets.AllocationBytes);
        Assert.Equal(new[] { 0, 32 }, result.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        NativeBattleGfGeometryAttempt triangle = result.Packets.Attempts[0];
        NativeBattleGfGeometryAttempt quad = result.Packets.Attempts[1];
        Assert.Equal(0x26102030u, triangle.Primitive!.Words[1]);
        Assert.Equal(0x2C102030u, quad.Primitive!.Words[1]);
        Assert.Equal(81, triangle.Primitive.Bucket);
        Assert.Equal(81, quad.Primitive.Bucket);
        Assert.Equal(new[] { 1024, 1024, 1024, 0 }, triangle.Primitive.VertexPayload4);
        Assert.Equal(new[] { 1024, 1024, 1024, 1024 }, quad.Primitive.VertexPayload4);
        Assert.All(triangle.WriteMask.Skip(28).Take(2), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.All(triangle.WriteMask.Skip(30).Take(2), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.All(quad.WriteMask.Skip(30).Take(2), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.All(quad.WriteMask.Skip(38).Take(2), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        for (int index = 0; index < 4; index++)
        {
            int offset = 0x20000 + index * 16;
            Assert.Throws<NotSupportedException>(() => result.Arena.Read(offset + 6, 2));
            Assert.Equal(1024u, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(offset + 12, 4)));
        }
        Assert.Equal((byte)0xA5, result.Arena.Read(0x20040, 1)[0]);
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(result.Arena))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Emit(Mesh(), result.Arena)), NativeBattleTestSnapshots.Json(Emit(Mesh(), restored)));
    }

    [Fact]
    public void Deformed_actor_does_not_apply_near_flags_or_the_normal_screen_rectangle()
    {
        NativeBattleRawMesh mesh = Mesh() with { Polygons = [Face(false)] };
        NativeBattleChoco100ActorDraw result = NativeBattleChoco100ActorPackets.Emit716DD0(mesh, [Identity], Identity with { X = 500, Z = -1 }, Projection, 0x123456, new(0, 0, 0, 0, 0), NativeBattleEffectArena.Clear(), 0x20000);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Packets.Attempts).Primitive!;
        Assert.Equal(17, primitive.Bucket);
        Assert.Equal(new[] { 0, 0, 0, 0 }, primitive.VertexPayload4);
        Assert.True(unchecked((short)primitive.Words[2]) > 2560);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nonpositive_NCLIP_emits_no_prefix_but_still_updates_vertex_work(bool reverse)
    {
        NativeBattleRawMesh mesh = Mesh() with { Polygons = [Face(false)] };
        if (reverse)
        {
            byte[] bytes = mesh.Polygons[0].Bytes.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 1);
            mesh = mesh with { Polygons = [new(false, bytes.ToImmutableArray())] };
        }
        else
        {
            mesh = mesh with { Vertices = mesh.Vertices.SetItem(2, new(new(128, 0, 0), 0)) };
        }
        NativeBattleChoco100ActorDraw result = Emit(mesh, NativeBattleEffectArena.Clear());
        Assert.Empty(result.Packets.Attempts);
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.Equal(1024u, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x2000C, 4)));
    }

    [Fact]
    public void Each_mesh_reuses_the_same_work_base_and_keeps_larger_previous_mesh_suffix()
    {
        NativeBattleChoco100ActorDraw first = Emit(Mesh(), NativeBattleEffectArena.Clear());
        NativeBattleRawMesh small = new([new(new(123, 4, 5), 0)], []);
        NativeBattleChoco100ActorDraw second = Emit(small, first.Arena);
        Assert.Equal((short)123, BinaryPrimitives.ReadInt16LittleEndian(second.Arena.Read(0x20000, 2)));
        Assert.Equal((short)64, BinaryPrimitives.ReadInt16LittleEndian(second.Arena.Read(0x20010, 2)));
        Assert.Throws<NotSupportedException>(() => second.Arena.Read(0x20016, 2));
        NativeBattleActionMatrix saturating = Identity with { X = 40000 };
        NativeBattleChoco100ActorDraw clamped = NativeBattleChoco100ActorPackets.Emit716DD0(small, [saturating], Identity with { Z = 1024 }, Projection, 0, new(0, 0, 0, 0, 0), second.Arena, 0x20000);
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(clamped.Arena.Read(0x20000, 2)));
    }

    [Fact]
    public void Actual_work_extent_is_bounded_without_allocating_an_alternate_vertex_bank()
    {
        NativeBattleRawMesh oversized = new(Enumerable.Repeat(new NativeBattleRawVertex(new(0, 0, 0), 0), 0xA000 / 16 + 1).ToImmutableArray(), []);
        Assert.Throws<NotSupportedException>(() => Emit(oversized, NativeBattleEffectArena.Clear()));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100ActorPackets.Emit716DD0(Mesh(), [Identity], Identity, Projection, 0, new(0, 0, 0, 0, 0), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Length - 0xA000 + 1));
    }

    private static NativeBattleChoco100ActorDraw Emit(NativeBattleRawMesh mesh, NativeBattleEffectArena arena) => NativeBattleChoco100ActorPackets.Emit716DD0(mesh, [Identity], Identity with { Z = 1024 }, Projection, 0xFF102030, new(0, 0, 0, 0, 0), arena, 0x20000);
    private static NativeBattleRawMesh Mesh() => new([new(new(0, 0, 0), 0), new(new(64, 0, 0), 0), new(new(0, 64, 0), 0), new(new(64, 64, 0), 0)], [Face(true), Face(false)]);
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
}
