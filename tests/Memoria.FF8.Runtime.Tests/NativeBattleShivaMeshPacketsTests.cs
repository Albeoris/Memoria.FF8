using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaMeshPacketsTests
{
    [Theory]
    [InlineData(36, 0x2E7E7E7Eu)]
    [InlineData(2, 0x2E707070u)]
    public void Prefix_UVs_and_suffix_indices_advance_independently_with_the_actual_shade_divisor(int window, uint secondShade)
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleShivaMeshPacketResult result = NativeBattleShivaMeshPackets.Emit5C1B50(mesh, default, Matrix(), Projection(), Context(), 2, window, 0x100, 0, 0);
        Assert.Equal(2, result.Visited);
        Assert.Equal(0x268 + 2 * 24, result.PolygonCursorOffset);
        Assert.Equal(80, result.Batch.AllocationBytes);
        NativeBattleGeometryPrimitive first = result.Batch.Attempts[0].Primitive!;
        NativeBattleGeometryPrimitive second = result.Batch.Attempts[1].Primitive!;
        Assert.Equal(0x2E909090u, first.Words[1]);
        Assert.Equal(secondShade, second.Words[1]);
        Assert.Equal(0x11110000u, first.Words[3]);
        Assert.Equal(0x11110001u, second.Words[3]);
        Assert.Equal(0x33334444u, first.Words[7]);
        Assert.Equal(0x3333u, first.Words[9]);
        Assert.Equal(NativeBattleModuleProjection.Project45FE10(Matrix(), new(-100, -100, 0), Projection()).Screen, first.Words[2]);
        Assert.Equal(NativeBattleModuleProjection.Project45FE10(Matrix(), new(-50, -50, 0), Projection()).Screen, second.Words[2]);
        Assert.Equal(new[] { 1000, 1000, 1000, 1000 }, first.VertexPayload4.ToArray());
    }

    [Fact]
    public void Rejected_faces_still_consume_their_UV_record_and_shade_but_reuse_the_packet_cursor()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        byte[] bytes = mesh.Bytes.ToArray();
        bytes.AsSpan(0x268 + 34 * 24 + 4, 8).Clear();
        NativeBattleShivaMeshPacketResult result = NativeBattleShivaMeshPackets.Emit5C1B50(mesh with { Bytes = bytes.ToImmutableArray() }, default, Matrix(), Projection(), Context(), 2, 36, 0x100, 0, 0);
        Assert.Null(result.Batch.Attempts[0].Primitive);
        Assert.Equal(0, result.Batch.Attempts[0].ByteOffsetRelative);
        Assert.Equal(0, result.Batch.Attempts[1].ByteOffsetRelative);
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Visited);
        Assert.Equal(0x2E7E7E7Eu, result.Batch.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(0x11110001u, result.Batch.Attempts[1].Primitive!.Words[3]);
    }

    [Fact]
    public void Explicit_vertex_override_uses_the_selected_bytes_and_does_not_gate_on_vector_W()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        byte[] selected = mesh.Bytes.AsSpan().Slice(8, 74 * 8).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(selected.AsSpan(4 * 8), -25);
        NativeBattleShivaMeshPacketResult result = NativeBattleShivaMeshPackets.Emit5C1B50(mesh, selected.ToImmutableArray(), Matrix(), Projection(), Context(0x2030), 1, 1, 0x100, 0, 0);
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal(NativeBattleModuleProjection.Project45FE10(Matrix(), new(-25, -50, 0), Projection()).Screen, result.Batch.Attempts[0].Primitive!.Words[2]);
        Assert.Throws<NotSupportedException>(() => NativeBattleShivaMeshPackets.Emit5C1B50(mesh, default, Matrix(), Projection(), Context(0x2030), 1, 1, 0x100, 0, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Sparse_selected_vertex_reads_do_not_consume_unknown_W_or_an_unreached_fourth_corner(int count)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 74 * 8);
        for (int corner = 0; corner < 3; corner++)
        {
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, (short)(corner * 10));
            arena = arena.Write(32 + corner * 8, xyz);
        }
        List<int> reads = [];
        NativeBattleShivaMeshPacketResult result = NativeBattleShivaMeshPackets.Emit5C1B50(Mesh(), default, Matrix(), Projection(), Context(0x2030), count == 0 ? 0 : 1, count, 0x100, 0, 0, readSelectedVertex: offset =>
        {
            reads.Add(offset);
            byte[] bytes = arena.Read(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        });
        Assert.Equal(count, result.Visited);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(count == 0 ? Array.Empty<int>() : new[] { 32, 40, 48 }, reads);
        Assert.Throws<NotSupportedException>(() => arena.Read(38, 2));
        Assert.Throws<NotSupportedException>(() => arena.Read(56, 6));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Near_or_zero_area_rejection_keeps_only_prefix_stores_without_projecting_the_fourth_corner(bool near)
    {
        byte[] face = Face();
        int reads = 0;
        List<ushort> depths = [];
        NativeBattleGfGeometryAttempt result = NativeBattleShivaMeshPackets.EmitFace5C1BB0(face, index =>
        {
            reads++;
            Assert.True(index < 3);
            return new(Screen((short)(index * 10), (short)(index * 10)), (ushort)(1000 + index), near ? 0x20000u : 0u);
        }, Context(), 0x100, observeDepth: depths.Add);
        Assert.Null(result.Primitive);
        Assert.Equal(3, reads);
        Assert.Equal(new ushort[] { 1000, 1001, 1002 }, depths);
        Assert.Equal(0x09000000u, Word(result, 0));
        Assert.Equal(0x2E909090u, Word(result, 1));
        Assert.Equal(0x11110000u, Word(result, 3));
        Assert.Equal(0x3333u, Word(result, 9));
        for (int word = 0; word < 10; word++)
        {
            NativeBattleGfPacketByteState expected = word is 0 or 1 or 3 or 5 or 7 or 9 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched;
            Assert.All(result.WriteMask.AsSpan().Slice(word * 4, 4).ToArray(), actual => Assert.Equal(expected, actual));
        }
    }

    [Fact]
    public void Negative_area_obeys_flag10_and_fourth_projection_flags_do_not_reject_an_admitted_face()
    {
        NativeBattleModuleProjectedVertex[] corners = [new(Screen(100, 100), 1000, 0), new(Screen(100, 200), 1000, 0), new(Screen(200, 100), 1000, 0), new(Screen(200, 200), 1000, 0x60000)];
        int fourth = 0;
        NativeBattleModuleProjectedVertex Read(int index)
        {
            if (index == 3)
            {
                fourth++;
            }
            return corners[index];
        }
        Assert.Null(NativeBattleShivaMeshPackets.EmitFace5C1BB0(Face(), Read, Context(0), 0x100).Primitive);
        Assert.Equal(0, fourth);
        NativeBattleGfGeometryAttempt admitted = NativeBattleShivaMeshPackets.EmitFace5C1BB0(Face(), Read, Context(0x10), 0x100);
        Assert.NotNull(admitted.Primitive);
        Assert.Equal(1, fourth);
        Assert.Equal(40, admitted.Data.Length);
    }

    [Fact]
    public void Entirely_offscreen_faces_retain_screen_stores_and_do_not_advance_the_allocation()
    {
        NativeBattleModuleProjectedVertex[] corners = [new(Screen(-100, 100), 1000, 0), new(Screen(-100, 200), 1000, 0), new(Screen(-200, 100), 1000, 0), new(Screen(-200, 200), 1000, 0)];
        NativeBattleGfGeometryAttempt result = NativeBattleShivaMeshPackets.EmitFace5C1BB0(Face(), index => corners[index], Context(), 0x100);
        Assert.Null(result.Primitive);
        Assert.All(result.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.Equal(corners[3].Screen, Word(result, 8));
    }

    [Fact]
    public void Depth_cue_and_bucket_use_the_explicit_current_context_and_ZSF4_without_an_extra_bias()
    {
        NativeBattleModuleProjectedVertex[] corners = [new(Screen(100, 100), 1000, 0), new(Screen(200, 100), 2000, 0), new(Screen(100, 200), 3000, 0), new(Screen(200, 200), 4000, 0)];
        NativeBattleGfGeometryContext context = Context(0xF0) with { FarColor = 0x00112233, DepthCue = 2048 };
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattleGfGeometryAttempt result = NativeBattleShivaMeshPackets.EmitFace5C1BB0(Face(), index => corners[index], context, 0x200, colors);
        Assert.Equal(329, result.Primitive!.Bucket);
        Assert.Equal(colors.Interpolate(0x2E909090, 2048, 0x00112233), result.Primitive.Words[1]);
        Assert.Equal(new[] { 1000, 2000, 3000, 4000 }, result.Primitive.VertexPayload4.ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleShivaMeshPackets.EmitFace5C1BB0(Face(), index => corners[index], context with { DepthBias = 1 }, 0x200, colors));
    }

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[0x5D8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x258);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 74);
        NativeBattleVector[] vertices = [new(-100, -100, 0), new(100, -100, 0), new(-100, 100, 0), new(100, 100, 0), new(-50, -50, 0), new(150, -50, 0), new(-50, 150, 0), new(150, 150, 0)];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), (short)vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), (short)vertices[index].Y);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14 + index * 8), ushort.MaxValue);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x264), 36);
        for (int index = 0; index < 36; index++)
        {
            byte[] face = Face();
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x11110000u + (uint)index);
            face.CopyTo(bytes, 0x268 + index * 24);
        }
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x268 + 34 * 24 + 4 + corner * 2), (ushort)(corner * 2));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x268 + 35 * 24 + 4 + corner * 2), (ushort)(8 + corner * 2));
        }
        return new(0, 0xD215C4, bytes.ToImmutableArray());
    }

    private static byte[] Face()
    {
        byte[] face = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x2E909090);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x11110000);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x22220000);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x33334444);
        return face;
    }

    private static NativeBattleGfGeometryContext Context(uint flags = 0x30)
    {
        return new(flags, 0, 0, null, null, 0, 17, 2);
    }

    private static NativeBattleActionMatrix Matrix()
    {
        return new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
    }

    private static NativeBattleCameraProjection Projection()
    {
        return new(Matrix(), 216, 160, 108);
    }

    private static uint Screen(short x, short y)
    {
        return unchecked((ushort)x) | unchecked((uint)y << 16);
    }

    private static uint Word(NativeBattleGfGeometryAttempt attempt, int index)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(index * 4));
    }
}
