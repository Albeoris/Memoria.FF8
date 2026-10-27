using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaMeshPacketsTests
{
    private static NativeBattleCuragaMeshContext Context => new(0x30, null, null, null, null, null, 17, 2);
    private static NativeBattleActionMatrix Matrix => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Fact]
    public void FT4_preserves_original_full_DWORD_UV_stores_including_packed_fourth_UV_and_zeroed_last_padding()
    {
        NativeBattleGfGeometryBatch batch = Emit(Corners(), Context);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive packet = attempt.Primitive!;
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal(273, packet.Bucket);
        Assert.Equal(0x09000000u, packet.Words[0]);
        Assert.Equal(0x2C808080u, packet.Words[1]);
        Assert.Equal(0x12340201u, packet.Words[3]);
        Assert.Equal(0xABCD0403u, packet.Words[5]);
        Assert.Equal(0x08070605u, packet.Words[7]);
        Assert.Equal(0x0807u, packet.Words[9]);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(new[] { 4096, 4096, 4096, 4096 }, packet.VertexPayload4);
    }

    [Fact]
    public void GT4_reuses_quad_geometry_and_material_contract_with_late_original_color_DWORDs()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaMeshPackets.EmitGt4_8800B0(GouraudFace(), index => Corners()[index], Context);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive packet = attempt.Primitive!;
        Assert.Equal(52, batch.AllocationBytes);
        Assert.Equal(0x0C000000u, packet.Words[0]);
        Assert.Equal(0x3C808080u, packet.Words[1]);
        Assert.Equal(0xAA202020u, packet.Words[4]);
        Assert.Equal(0xBB606060u, packet.Words[7]);
        Assert.Equal(0xCCC0C0C0u, packet.Words[10]);
        Assert.Equal(0x12340201u, packet.Words[3]);
        Assert.Equal(0xABCD0403u, packet.Words[6]);
        Assert.Equal(0x08070605u, packet.Words[9]);
        Assert.Equal(0x0807u, packet.Words[12]);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void GT4_near_reject_keeps_UV_prefix_and_leaves_secondary_color_slots_untouched()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        corners[0] = corners[0] with { Flags = 0x80020000 };
        NativeBattleGfGeometryAttempt attempt = Assert.Single(NativeBattleCuragaMeshPackets.EmitGt4_8800B0(GouraudFace(), index => index == 3 ? throw new InvalidOperationException("Fourth near vertex read.") : corners[index], Context).Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[26]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[50]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[16]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[28]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[40]);
    }

    [Fact]
    public void GT4_fade_uses_the_original_fourth_color_command_BYTE_for_all_three_secondary_FIFO_colors()
    {
        NativeBattleSummonColors constants = new(0, 0, 1f / 4096, 4503601774854144d);
        NativeBattleCuragaMeshContext context = Context with { Flags = 0xA0, Fade = 2048, FarColor = 0x404040 };
        NativeBattleGeometryPrimitive packet = Assert.Single(NativeBattleCuragaMeshPackets.EmitGt4_8800B0(GouraudFace(), index => Corners()[index], context, constants).Attempts).Primitive!;
        Assert.Equal(0x3C606060u, packet.Words[1]);
        Assert.Equal(0xCC303030u, packet.Words[4]);
        Assert.Equal(0xCC505050u, packet.Words[7]);
        Assert.Equal(0xCC808080u, packet.Words[10]);
    }

    [Fact]
    public void GT4_uses_its_own_semitransparency_and_backface_flag_bits()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        (corners[1], corners[2]) = (corners[2], corners[1]);
        Assert.Null(Assert.Single(NativeBattleCuragaMeshPackets.EmitGt4_8800B0(GouraudFace(), index => corners[index], Context with { Flags = 0x10 }).Attempts).Primitive);
        NativeBattleGeometryPrimitive packet = Assert.Single(NativeBattleCuragaMeshPackets.EmitGt4_8800B0(GouraudFace(), index => corners[index], Context with { Flags = 0x22 }).Attempts).Primitive!;
        Assert.Equal(0x3E808080u, packet.Words[1]);
        NativeBattleGeometryPrimitive cleared = Assert.Single(NativeBattleCuragaMeshPackets.EmitGt4_8800B0(GouraudFace(), index => corners[index], Context with { Flags = 0x2A }).Attempts).Primitive!;
        Assert.Equal(0x3C808080u, cleared.Words[1]);
    }

    [Fact]
    public void Near_rejection_preserves_native_prefix_but_does_not_read_material_overrides_or_fourth_vertex()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        corners[1] = corners[1] with { Flags = 0x80020000 };
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaMeshPackets.EmitFt4_87F6C0(Face(), index => index == 3 ? throw new InvalidOperationException("Fourth vertex was read before admission.") : corners[index], Context with { Flags = 0xC00 });
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[14]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[32]);
    }

    [Fact]
    public void Material_add_overrides_precede_winding_and_take_priority_over_replacement_bits()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        (corners[1], corners[2]) = (corners[2], corners[1]);
        NativeBattleCuragaMeshContext context = Context with { Flags = 0xF00, TexturePage = 0x0102, Clut = 0x20 };
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaMeshPackets.EmitFt4_87F6C0(Face(), index => index == 3 ? throw new InvalidOperationException("Backface read fourth vertex.") : corners[index], context);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0xACCF, BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(22)));
        Assert.Equal(0x1254, BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(14)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
    }

    [Fact]
    public void Material_replacement_and_preserved_UV_DWORD_offset_keep_native_carries()
    {
        NativeBattleCuragaMeshContext context = Context with { Flags = 0x1300, TexturePage = 0xB6, Clut = 0x3E94, UvOffset = 0x000100FF };
        NativeBattleGeometryPrimitive packet = Assert.Single(Emit(Corners(), context).Attempts).Primitive!;
        Assert.Equal(0x3E940300u, packet.Words[3]);
        Assert.Equal(0x00B60502u, packet.Words[5]);
        Assert.Equal(0x09070704u, packet.Words[7]);
        Assert.Equal(0x0907u, packet.Words[9]);
    }

    [Fact]
    public void Default_UV_reset_ignores_unowned_preserved_offset_and_color_clear_takes_priority_over_force_semi()
    {
        byte[] face = Face();
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x2E808080);
        NativeBattleGeometryPrimitive packet = Assert.Single(NativeBattleCuragaMeshPackets.EmitFt4_87F6C0(face, index => Corners()[index], Context with { Flags = 0x35 }).Attempts).Primitive!;
        Assert.Equal(0x2C808080u, packet.Words[1]);
        Assert.Equal(0x12340201u, packet.Words[3]);
    }

    [Fact]
    public void All_outside_on_one_axis_rejects_even_when_vertices_straddle_opposite_viewport_edges()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        for (int index = 0; index < 4; index++)
        {
            corners[index] = corners[index] with { Screen = XY(index % 2 == 0 ? (short)-1 : (short)2561, 100) };
        }
        NativeBattleGfGeometryBatch batch = Emit(corners, Context);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void Exact_screen_edges_and_fourth_near_flags_are_admitted()
    {
        NativeBattleModuleProjectedVertex[] corners = [new(XY(0, 0), 4096, 0), new(XY(2560, 0), 4096, 0), new(XY(0, 1728), 4096, 0), new(XY(2560, 1728), 4096, 0x80060000)];
        Assert.Equal(40, Emit(corners, Context).AllocationBytes);
    }

    [Fact]
    public void Fade_requires_actual_written_context_only_after_geometry_admission()
    {
        NativeBattleSummonColors constants = new(0, 0, 1f / 4096, 4503601774854144d);
        NativeBattleCuragaMeshContext context = Context with { Flags = 0x70, Fade = 2048, FarColor = 0x404040 };
        NativeBattleGeometryPrimitive packet = Assert.Single(NativeBattleCuragaMeshPackets.EmitFt4_87F6C0(Face(), index => Corners()[index], context, constants).Attempts).Primitive!;
        Assert.Equal(0x2C606060u, packet.Words[1]);
        Assert.Throws<NotSupportedException>(() => Emit(Corners(), Context with { Flags = 0x70 }));
        NativeBattleModuleProjectedVertex[] rejected = Corners().Select(vertex => vertex with { Screen = XY(-1, -1) }).ToArray();
        Assert.Equal(0, Emit(rejected, Context with { Flags = 0x70 }).AllocationBytes);
    }

    [Theory]
    [InlineData(0x24, 2, 3, 4, 0)]
    [InlineData(0x2C, 1, 2, 3, 4)]
    [InlineData(0x62, 0, 0, 0, 0)]
    public void Proxy_payload_uses_actual_opcode_after_four_current_depth_loads(byte opcode, int a, int b, int c, int d)
    {
        byte[] face = Face();
        face[3] = opcode;
        NativeBattleModuleProjectedVertex[] corners = Corners();
        for (int index = 0; index < 4; index++)
        {
            corners[index] = corners[index] with { Depth = (ushort)(index + 1) };
        }
        Assert.Equal(new[] { a, b, c, d }, Assert.Single(NativeBattleCuragaMeshPackets.EmitFt4_87F6C0(face, index => corners[index], Context).Attempts).Primitive!.VertexPayload4);
    }

    [Fact]
    public void Dispatcher_uses_original_vertex_WORD_times_four_and_skips_zero_groups()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(Mesh(), Matrix, new(null, 216, 160, 108), Context, _ => throw new InvalidOperationException("Default vertices were overridden."));
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Single(batch.Attempts);
        Assert.Equal(79, batch.Attempts[0].Primitive!.Bucket);
    }

    [Fact]
    public void Dispatcher_preserved_vertex_pointer_requires_and_uses_only_selected_XYZ_reads()
    {
        List<int> reads = [];
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(Mesh(), Matrix, new(null, 216, 160, 108), Context with { Flags = 0x2030 }, offset =>
        {
            reads.Add(offset);
            return new((short)(offset % 16 == 0 ? -100 : 100), (short)(offset < 16 ? -100 : 100), 1000);
        });
        Assert.Equal(new[] { 0, 8, 16, 24 }, reads);
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaMeshPackets.EmitBatch87EEB0(Mesh(), Matrix, new(null, 216, 160, 108), Context with { Flags = 0x2030 }));
    }

    [Fact]
    public void Dispatcher_reaches_GT4_group_seven_using_original_thirty_six_byte_source_records()
    {
        byte[] mesh = new byte[108];
        Mesh().AsSpan().Slice(0, 40).CopyTo(mesh);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(68), 1);
        GouraudFace().CopyTo(mesh, 72);
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(mesh.ToImmutableArray(), Matrix, new(null, 216, 160, 108), Context);
        Assert.Equal(52, batch.AllocationBytes);
        Assert.Equal(0xCCC0C0C0u, Assert.Single(batch.Attempts).Primitive!.Words[10]);
    }

    [Fact]
    public void Dispatcher_names_exact_next_unsupported_leaf_and_negative_FT4_count_matches_native_empty_loop()
    {
        byte[] mesh = Mesh().ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(40), 1);
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => NativeBattleCuragaMeshPackets.EmitBatch87EEB0(mesh.ToImmutableArray(), Matrix, new(null, 216, 160, 108), Context));
        Assert.Contains("87EFF0", error.Message);
        mesh = Mesh().ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(52), -1);
        // Subsequent group DWORDs are read immediately after that header when the signed count is nonpositive.
        Array.Clear(mesh, 56, 40);
        Assert.Equal(0, NativeBattleCuragaMeshPackets.EmitBatch87EEB0(mesh.ToImmutableArray(), Matrix, new(null, 216, 160, 108), Context).AllocationBytes);
    }

    [Fact]
    public void Partial_packet_JSON_round_trip_preserves_rejected_mask_and_original_cursor_offset()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        corners[0] = corners[0] with { Flags = 0x80040000 };
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaMeshPackets.EmitFt4_87F6C0(Face(), index => corners[index], Context, byteOffset: 120);
        NativeBattleGfGeometryBatch restored = RoundTrip(batch);
        Assert.Equal(SnapshotJson(batch), SnapshotJson(restored));
        Assert.Equal(120, restored.Attempts[0].ByteOffsetRelative);
    }

    private static NativeBattleGfGeometryBatch Emit(NativeBattleModuleProjectedVertex[] corners, NativeBattleCuragaMeshContext context)
    {
        return NativeBattleCuragaMeshPackets.EmitFt4_87F6C0(Face(), index => corners[index], context);
    }

    private static NativeBattleModuleProjectedVertex[] Corners()
    {
        return [new(XY(100, 100), 4096, 0), new(XY(200, 100), 4096, 0), new(XY(100, 200), 4096, 0), new(XY(200, 200), 4096, 0)];
    }

    private static byte[] Face()
    {
        byte[] result = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(result, 0x2C808080);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), 0xABCD0403);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20), 0x08070605);
        return result;
    }

    private static byte[] GouraudFace()
    {
        byte[] result = new byte[36];
        Face().CopyTo(result, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result, 0x3C808080);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(24), 0xAA202020);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(28), 0xBB606060);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(32), 0xCCC0C0C0);
        return result;
    }

    private static ImmutableArray<byte> Mesh()
    {
        byte[] result = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(result, 40);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4), 4);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(8 + corner * 8), (short)(corner % 2 == 0 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(10 + corner * 8), (short)(corner < 2 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(12 + corner * 8), 1000);
        }
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(52), 1);
        Face().CopyTo(result, 56);
        return result.ToImmutableArray();
    }

    private static uint XY(short x, short y)
    {
        return (ushort)x | unchecked((uint)(ushort)y << 16);
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
