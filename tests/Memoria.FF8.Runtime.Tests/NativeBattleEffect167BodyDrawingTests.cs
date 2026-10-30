using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect167BodyDrawingTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 4096)]
    [InlineData(false, -4096)]
    public void Effect326_capture_follows_actual_clipped_rows_full_matrices_native_FIFO_and_default167(bool quad, short plane)
    {
        var native = Draw(Geometry(quad, false), normalX: plane);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Draw(Geometry(quad, false), normalX: plane, pilotIdentity: new(326, 0x24F0C88, 0, 123, 7));
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.NotEmpty(captured.Batch.Attempts);
        Assert.Equal(captured.Batch.Attempts.Length, captured.Batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        foreach (var attempt in captured.Batch.Attempts)
        {
            var primitive = attempt.Primitive!;
            var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal((ushort)326, geometry.Identity.Effect);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
            Assert.Equal(2000, geometry.A.Z);
        }
        Assert.All(native.Batch.Attempts, attempt => Assert.Null(attempt.Primitive!.PilotGeometry));
    }

    [Fact]
    public void Effect326_capture_does_not_read_deferred_bad_fourth_or_clipping_planes_after_first_rejection()
    {
        byte[] geometry = Geometry(true, false);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(54), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(56), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(58), 0xFFF);
        var native = Draw(geometry);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Draw(geometry, pilotIdentity: new(326, 1, 2, 3, 4));
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Empty(captured.Batch.Attempts);
    }


    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Current_textured_faces_keep_partial_UV_stores_and_all_four_current_depths(bool quad, bool semi)
    {
        NativeBattleEffect167BodyStep result = Draw(Geometry(quad, semi));
        Assert.Equal(quad ? 64 : 32, result.Batch.AllocationBytes);
        Assert.Equal(quad ? 2 : 1, result.Batch.Attempts.Length);
        foreach (NativeBattleGfGeometryAttempt attempt in result.Batch.Attempts)
        {
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            Assert.Equal((semi ? 0x26000000u : 0x24000000u) | 0x123456, primitive.Words[1]);
            Assert.Equal(0x1234u, primitive.Words[3] >> 16);
            Assert.Equal(0x0045u, primitive.Words[5] >> 16);
            Assert.Equal(4, primitive.VertexPayload4.Length);
            Assert.All(attempt.WriteMask.Skip(30).Take(2), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        }
        Assert.Equal(new[] { 777, 2000, 2000, 2000 }, result.Batch.Attempts[0].Primitive!.VertexPayload4);
        Assert.DoesNotContain(true, result.Temporary464.ReadSnapshot(0x24, 4).Known);
        Assert.DoesNotContain(true, result.Temporary464.ReadSnapshot(0x2C, 4).Known);
        Assert.DoesNotContain(true, result.Temporary464.ReadSnapshot(0x4C, 4).Known);
        Assert.DoesNotContain(true, result.Scratch.ReadSnapshot(6, 2).Known);
        Assert.Null(result.Memory.GeometryRegisters!.V1[6]);
        Assert.Null(result.Memory.GeometryRegisters.V1[7]);
        Assert.Equal(17 + (int)U(result.Temporary464, 0x38) / 4, result.Batch.Attempts[^1].Primitive!.Bucket);
    }

    [Fact]
    public void Zero_area_rejects_before_plane_distances_and_before_packet_stores()
    {
        byte[] geometry = Geometry(false, false);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(52 + 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(52 + 4), 0);
        NativeBattleEffect167BodyStep result = Draw(geometry);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0u, U(result.Temporary464, 0x30));
        Assert.DoesNotContain(true, result.Temporary464.ReadSnapshot(0x7C, 4).Known);
        Assert.Equal(0, result.Batch.AllocationBytes);
    }

    [Fact]
    public void Quad_first_gate_rejection_skips_second_triangle_even_with_invalid_fourth_index()
    {
        byte[] geometry = Geometry(true, false);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(54), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(56), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(58), 0xFFF);
        NativeBattleEffect167BodyStep result = Draw(geometry);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0u, U(result.Temporary464, 0x30));
    }

    [Fact]
    public void Object_mask_skips_unreached_bone_and_vertex_consumption()
    {
        byte[] geometry = Geometry(false, false);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(10), short.MaxValue);
        NativeBattleEffect167BodyStep result = Draw(geometry, mask: 0);
        Assert.Empty(result.Batch.Attempts);
        Assert.DoesNotContain(true, result.Scratch.ReadSnapshot(0, 32).Known);
    }

    [Theory]
    [InlineData(4096, 32)]
    [InlineData(-4096, 40)]
    public void Crossing_plane_draws_both_halves_in_native_sign_order_and_restores_only_translation(short normalX, int firstBytes)
    {
        NativeBattleEffect167BodyStep result = Draw(Geometry(false, false), normalX: normalX);
        Assert.Equal(72, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        Assert.Equal(firstBytes, result.Batch.Attempts[0].Data.Length);
        Assert.Equal(firstBytes, result.Batch.Attempts[1].ByteOffsetRelative);
        Assert.Equal(3u, U(result.Temporary464, 0x14));
        Assert.Equal((short)0, S(result.Temporary464, 0x70));
        Assert.Equal(0, result.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(2000, result.Memory.GeometryRegisters.Translation[2]);
        Assert.All(result.Batch.Attempts, attempt => Assert.Equal(4, attempt.Primitive!.VertexPayload4.Length));
        Assert.NotEqual(result.Batch.Attempts[0].Primitive!.Words[2], result.Batch.Attempts[1].Primitive!.Words[2]);
    }

    [Fact]
    public void Intersection_writes_exactly_eight_bytes_and_preserves_edge_metadata_and_json()
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 464);
        context = Point(context, 0x40, -100, 20, 30).Write(0x46, BitConverter.GetBytes((ushort)0x0A14));
        context = Point(context, 0x50, 100, 40, 50).Write(0x56, BitConverter.GetBytes((ushort)0x1428));
        context = Point(context, 0xA0, 4096, 0, 0).Write(0xA6, BitConverter.GetBytes((short)0));
        context = context.Write(0x78, BitConverter.GetBytes(0x12345678u)).Write(0x7C, BitConverter.GetBytes(-100));
        NativeBattleEffectArena result = NativeBattleEffect167BodyDrawing.Intersect(context, 0x40, 0x50, 0xA0, 0x70);
        Assert.Equal((short)0, S(result, 0x70));
        Assert.Equal((short)30, S(result, 0x72));
        Assert.Equal((short)40, S(result, 0x74));
        Assert.Equal((ushort)0x0F1E, W(result, 0x76));
        Assert.Equal(0x12345678u, U(result, 0x78));
        Assert.Equal(-100, unchecked((int)U(result, 0x7C)));
        NativeBattleEffectArena restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(result.ReadSnapshot(0x70, 16).Data, restored.ReadSnapshot(0x70, 16).Data);
        Assert.Equal(result.ReadSnapshot(0x70, 16).Known, restored.ReadSnapshot(0x70, 16).Known);
    }

    [Fact]
    public void Intersection_zero_denominator_uses_one_and_keeps_unclamped_U_carry()
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear();
        context = Point(context, 0x40, 0, 0, 0).Write(0x46, BitConverter.GetBytes((ushort)0x00FF));
        context = Point(context, 0x50, 0, 0, 0).Write(0x56, BitConverter.GetBytes((ushort)0x0000));
        context = Point(context, 0xA0, 0, 0, 0).Write(0xA6, BitConverter.GetBytes((short)1));
        NativeBattleEffectArena result = NativeBattleEffect167BodyDrawing.Intersect(context, 0x40, 0x50, 0xA0, 0x70);
        Assert.Equal((ushort)510, W(result, 0x76));
    }

    private static NativeBattleEffect167BodyStep Draw(byte[] geometry, uint mask = 1, short normalX = 0, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 464).Write(0x10, BitConverter.GetBytes(mask)).Write(0x18, BitConverter.GetBytes(0x123456u));
        context = Point(context, 0xA0, normalX, 0, 0).Write(0xA6, BitConverter.GetBytes((short)0));
        NativeBattlePackedMatrix32 view = NativeBattlePackedMatrix32.FromMatrix(Identity(2000), null);
        context = CopyMatrix(context, 0x190, view);
        context = CopyMatrix(context, 0x1B0, view);
        context = context.Write(0x1B0 + 20, BitConverter.GetBytes(1000));
        return NativeBattleEffect167BodyDrawing.Draw(Memory(), context, 0x10000, ReadGeometry, 0x20000, [NativeBattlePackedMatrix32.FromMatrix(Identity(0), null)], NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 32, new(pilotIdentity.HasValue ? Identity(0) : null, 100, 160, 108), Enumerable.Repeat(3u, 56).ToImmutableArray(), 0, pilotIdentity: pilotIdentity);

        NativeBattleEffectArena ReadGeometry(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, geometry.AsSpan(checked((int)(address - 0x20000)), length).ToArray());
    }

    private static byte[] Geometry(bool quad, bool semi)
    {
        byte[] bytes = new byte[52 + (quad ? 20 : 16)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 4);
        (short X, short Y)[] vertices = [(-100, -100), (100, -100), (-100, 100), (100, 100)];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 6), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16 + index * 6), vertices[index].Y);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(40 + (quad ? 2 : 0)), 1);
        for (int index = 0; index < (quad ? 4 : 3); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52 + index * 2), (ushort)index);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(58), quad ? (ushort)3 : (ushort)0x7788);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x12340102);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), semi ? 0x02450304u : 0x00450304u);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(68), 0x0506);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(70), 0x0708);
        }
        return bytes;
    }

    internal static NativeBattleEffectMemory Memory(int z = 0) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(z), true, true, null) with { Sz = [11, 22, 33, 777] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ScriptedLighting = NativeBattleScriptedLightingMemory.Unknown() };
    internal static NativeBattleActionMatrix Identity(int z) => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096);
    private static NativeBattleEffectArena CopyMatrix(NativeBattleEffectArena arena, int offset, NativeBattlePackedMatrix32 matrix)
    {
        arena = arena.Write(offset, matrix.Bytes.ToArray());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                arena = arena.MarkUnknown(offset + index, 1);
            }
        }
        return arena;
    }
    private static NativeBattleEffectArena Point(NativeBattleEffectArena arena, int offset, short x, short y, short z) => arena.Write(offset, BitConverter.GetBytes(x)).Write(offset + 2, BitConverter.GetBytes(y)).Write(offset + 4, BitConverter.GetBytes(z));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort W(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
