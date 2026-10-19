using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaProjectedSpritesTests
{
    private static NativeBattleActionMatrix Matrix => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
    private static NativeBattleCameraProjection Projection => new(null, 1000, 0, 0);

    [Fact]
    public void Quad_observer_runs_before_projection_rejection_with_each_actual_quad_matrix()
    {
        byte[] quad = Quad();
        List<NativeBattleActionMatrix> observed = [];
        ImmutableArray<byte> resource = Resource(false, quad);
        NativeBattleActionMatrix input = Matrix with { Z = -1000 };
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, 0, input, Projection, 0, 17, 2, observeQuadMatrix: observed.Add);
        NativeBattleActionMatrix actual = Assert.Single(observed);
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(input, quad);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.All(batch.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    [Fact]
    public void Flags_zero_uses_copied_explicit_GTE_matrix_original_color_palette_and_clamped_UV_edges()
    {
        byte[] quad = Quad();
        quad[1] = 73;
        quad[4] = 250;
        quad[5] = 252;
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(6), 0x2000);
        NativeBattleModulePacketBatch batch = NativeBattleCuragaProjectedSprites.Emit8DD260(Resource(false, quad), 0, Matrix, Projection, 0, 17, 2);
        NativeBattleAllocatedPrimitive allocated = Assert.Single(batch.Primitives);
        NativeBattleGeometryPrimitive packet = allocated.Primitive;
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal(0, allocated.ByteOffset);
        Assert.Equal(0, allocated.Flags20);
        Assert.Equal(0x2E494949u, packet.Words[1]);
        Assert.Equal(XY(0, 0), packet.Words[2]);
        Assert.Equal(XY(2048, 0), packet.Words[4]);
        Assert.Equal(XY(0, 1024), packet.Words[6]);
        Assert.Equal(XY(2048, 1024), packet.Words[8]);
        Assert.Equal(0x1FC0FCFAu, packet.Words[3]);
        Assert.Equal(0x0123FCFFu, packet.Words[5]);
        Assert.Equal(0xFFFAu, packet.Words[7]);
        Assert.Equal(0xFFFFu, packet.Words[9]);
        Assert.Equal(79, packet.Bucket);
        Assert.Equal(new[] { 1000, 1000, 1000, 1000 }, packet.VertexPayload4);
    }

    [Fact]
    public void Camera_roll_left_multiplies_rotation_and_preserves_the_copied_translation()
    {
        NativeBattleModulePacketBatch batch = NativeBattleCuragaProjectedSprites.Emit8DD260(Resource(false, Quad()), 0, Matrix with { X = 7, Y = 11 }, Projection, 1024, 17, 2);
        ImmutableArray<uint> words = Assert.Single(batch.Primitives).Primitive.Words;
        Assert.Equal(XY(56, 88), words[2]);
        Assert.Equal(XY(56, -1960), words[4]);
        Assert.Equal(XY(1080, 88), words[6]);
        Assert.Equal(XY(1080, -1960), words[8]);
    }

    [Fact]
    public void Extended_frame_links_E1_before_FT4_reserves_twelve_bytes_and_preserves_all_padding()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(Resource(true, Quad()), 0, Matrix, Projection, 0, 17, 2);
        Assert.Equal(52, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 40, 0 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000220 }, batch.Attempts[1].Primitive!.Words);
        Assert.Equal(new[] { 0, 0, 0, 0 }, batch.Attempts[1].Primitive!.VertexPayload4);
        Assert.Equal(12, batch.Attempts[1].PacketLength);
        Assert.All(batch.Attempts[1].WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.All(batch.Attempts[2].WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, batch.Attempts[0].WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, batch.Attempts[0].WriteMask[38]);
    }

    [Fact]
    public void Rejection_preserves_only_native_prefix_and_first_accepted_quad_establishes_one_reused_depth_bucket()
    {
        byte[] first = Quad();
        byte[] second = Quad();
        byte[] third = Quad();
        BinaryPrimitives.WriteInt16LittleEndian(second.AsSpan(8), 100);
        BinaryPrimitives.WriteInt16LittleEndian(third.AsSpan(8), 200);
        NativeBattleActionMatrix varyingDepth = Matrix with { Rotation = [4096, 0, 0, 0, 4096, 0, 4096, 0, 4096], Z = 50 };
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(Resource(true, first, second, third), 0, varyingDepth, new(null, 216, 160, 108), 0, 17, 2);
        Assert.Equal(92, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 0, 40, 0, 52 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        NativeBattleGfGeometryAttempt rejected = batch.Attempts[0];
        Assert.Null(rejected.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[14]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[21]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[28]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[36]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[22]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[29]);
        Assert.All(batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(128, attempt.Primitive!.Bucket));
        Assert.Equal(new[] { 3250, 3506, 3250, 3506 }, batch.Attempts[4].Primitive!.VertexPayload4);
    }

    [Fact]
    public void Offscreen_and_backfacing_quads_have_no_added_culling_gate()
    {
        byte[] quad = Quad();
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(14), 0x0C00);
        BinaryPrimitives.WriteInt16LittleEndian(quad.AsSpan(16), -4096);
        BinaryPrimitives.WriteInt16LittleEndian(quad.AsSpan(18), 4096);
        NativeBattleModulePacketBatch batch = NativeBattleCuragaProjectedSprites.Emit8DD260(Resource(false, quad), 0, Matrix with { X = 3000 }, Projection, 0, 17, 2);
        Assert.Single(batch.Primitives);
        Assert.Equal(40, batch.AllocationBytes);
    }

    [Theory]
    [InlineData(0x24, 1000, 1000, 1000, 0)]
    [InlineData(0x2C, 1000, 1000, 1000, 1000)]
    [InlineData(0x62, 0, 0, 0, 0)]
    public void Proxy_payload_follows_actual_source_opcode(byte opcode, int a, int b, int c, int d)
    {
        byte[] quad = Quad();
        quad[3] = opcode;
        NativeBattleModulePacketBatch batch = NativeBattleCuragaProjectedSprites.Emit8DD260(Resource(false, quad), 0, Matrix, Projection, 0, 17, 2);
        Assert.Equal(new[] { a, b, c, d }, Assert.Single(batch.Primitives).Primitive.VertexPayload4);
    }

    [Fact]
    public void Selected_frame_uses_WORD_directory_offset_and_reads_next_entry_without_clamping_phase()
    {
        byte[] data = Resource(false, Quad()).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(10), 0xFFFF);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(12), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(14), 0);
        Assert.Equal(40, NativeBattleCuragaProjectedSprites.Emit8DD260(data.ToImmutableArray(), 1, Matrix, Projection, 0, 17, 2).AllocationBytes);
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaProjectedSprites.Emit8DD260(data.ToImmutableArray(), 0, Matrix, Projection, 0, 17, 2));
    }

    [Fact]
    public void Unsupported_palette_context_and_unknown_projection_fail_at_their_actual_inputs()
    {
        byte[] quad = Quad();
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(14), 0x4000);
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaProjectedSprites.Emit8DD260(Resource(false, quad), 0, Matrix, Projection, 0, 17, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaProjectedSprites.Emit8DD260(Resource(false, Quad()), 0, Matrix, new(null, null, 0, 0), 0, 17, 2));
    }

    [Fact]
    public void Exact_attempt_JSON_round_trip_preserves_reserved_and_rejected_store_masks()
    {
        NativeBattleGfGeometryBatch source = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(Resource(true, Quad()), 0, Matrix, Projection, 0, 17, 2);
        NativeBattleGfGeometryBatch restored = JsonSerializer.Deserialize<NativeBattleGfGeometryBatch>(JsonSerializer.Serialize(source))!;
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(restored));
    }

    internal static byte[] Quad()
    {
        byte[] result = new byte[20];
        result[0] = 16;
        result[1] = 128;
        result[2] = 8;
        result[3] = 0x2E;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(14), 0x0123);
        return result;
    }

    internal static ImmutableArray<byte> Resource(bool extended, params byte[][] quads)
    {
        byte[] result = new byte[20 + quads.Length * 20];
        result[4] = 0xFF;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), (uint)quads.Length | (extended ? 0x80000000 : 0));
        for (int index = 0; index < quads.Length; index++)
        {
            quads[index].CopyTo(result, 20 + index * 20);
        }
        return result.ToImmutableArray();
    }

    private static uint XY(short x, short y)
    {
        return (ushort)x | unchecked((uint)(ushort)y << 16);
    }
}
