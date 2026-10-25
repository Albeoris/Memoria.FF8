using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterMeshPacketTests
{
    private const uint Ot = 0x1D8E058;

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(17)]
    public void Retained_float_scale_applies_to_unsigned_depth_metadata_with_native_truncation(short type)
    {
        NativeBattleWaterMeshResult result = NativeBattleWaterMeshPackets.EmitSelected(Frame(), new(type, 1, Face(type)), Records([0, 10, 20, 30], [100, 200, 300, 400]), [0xFFFF8001, 0xABCD0002, 65535, 999], 0x5000, Resolve, depthScale: -0.5f);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Packets.Attempts).Primitive!;
        Assert.Equal(type == 17 ? new[] { -16384, -1, -32767, -499 } : new[] { -16384, -1, -32767, 0 }, primitive.VertexPayload4);
    }

    [Theory]
    [InlineData(7, 36, 0x30)]
    [InlineData(9, 40, 0x34)]
    [InlineData(17, 44, 0x38)]
    public void Zero_NCLIP_is_accepted_and_packet_layout_traversal_and_raw_proxy_depths_are_exact(short type, int length, byte command)
    {
        NativeBattleWaterMeshResult result = Emit(type, Records([0, 0x0000000A, 0x00000014, 0x0000001E], [100, 200, 300, 400]));
        Assert.Equal(length, result.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(type == 17 ? 17 + 62 : 17 + 50, primitive.Bucket);
        Assert.Equal(type == 17 ? new[] { 32769, 2, 65535, 999 } : new[] { 32769, 2, 65535, 0 }, primitive.VertexPayload4);
        Assert.Equal((uint)(0x5000 + length), result.Frame.ReadTemporaryDword(0x60));
        Assert.Equal(0u, result.Frame.ReadTemporaryDword(0xF0));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(221);
        memory = memory with { Arena = memory.Arena!.Write(0x1000, Enumerable.Repeat((byte)0xCC, 96).ToArray()) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0x1000, NativeBattleLogicalOrdering.Clear(), result.Packets);
        Assert.Equal(0x1000 + length, written.Cursor);
        byte[] actual = written.Memory.Arena!.Read(0x1000, length);
        Assert.Equal((byte)(length / 4 - 1), actual[3]);
        NativeBattlePrimitiveProcess common = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = "off_B6D070" };
        common = common.ApplyCommonWrite(0, Enumerable.Repeat((byte)0xCC, 96).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 96).ToImmutableArray());
        NativeBattleCommonPacketWrite commonWrite = NativeBattleCommonPacketArena.Append(common, NativeBattleLogicalOrdering.Clear(), result.Packets);
        Assert.Equal(length, commonWrite.Process.ByteCursor);
        Assert.Equal(actual, commonWrite.Process.ReadCommonPacket(0, length));
        Assert.Equal(primitive.VertexPayload4.ToArray(), Assert.Single(commonWrite.Ordering.Proxies).VertexPayload4.ToArray());

        if (type == 9)
        {
            Assert.Equal((ushort)0x1122, BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(12)));
            Assert.Equal((ushort)0xFFF3, BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(14)));
            Assert.Equal((ushort)0x3344, BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(24)));
            Assert.Equal((ushort)0x13, BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(26)));
            Assert.Equal((ushort)0x5566, BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(36)));
            Assert.Equal((ushort)0xCCCC, BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(38)));
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
        }
        else
        {
            Assert.Equal(0xE1000003u, BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(4)));
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(8)));
        }
        byte[] lengths = new byte[256];
        lengths[0xE1] = 1;
        lengths[0x30] = 6;
        lengths[0x34] = 9;
        lengths[0x38] = 8;
        ImmutableArray<NativeBattleLogicalCommand> commands = written.Ordering.Traverse(lengths.ToImmutableArray());
        Assert.Equal(type == 9 ? new byte[] { command } : new byte[] { 0xE1, command }, commands.Select(item => item.Opcode));
        Assert.Equal(result.Packets.Attempts[0].Data.ToArray(), JsonSerializer.Deserialize<NativeBattleGfGeometryBatch>(JsonSerializer.Serialize(result.Packets))!.Attempts[0].Data.ToArray());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(17)]
    public void Negative_NCLIP_retains_only_precull_SXY_stores_and_bypass_does_not_bypass_clip46(short type)
    {
        ImmutableArray<byte> records = Records([0, 0x000A0000, 10, 0x000A000A], [100, 200, 300, 400]);
        NativeBattleWaterMeshResult rejected = Emit(type, records);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(rejected.Packets.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, rejected.Packets.AllocationBytes);
        Assert.Equal(0x5000u, rejected.Frame.ReadTemporaryDword(0x60));
        int[] offsets = type == 9 ? [8, 20, 32] : type == 17 ? [16, 24, 32, 40] : [16, 24, 32];
        Assert.Equal(offsets.Length * 4, attempt.WriteMask.Count(mask => mask == NativeBattleGfPacketByteState.Known));
        foreach (int offset in offsets)
        {
            Assert.All(attempt.WriteMask.Skip(offset).Take(4), mask => Assert.Equal(NativeBattleGfPacketByteState.Known, mask));
        }
        NativeBattleWaterMeshResult admitted = Emit(type, records, 0x10);
        Assert.NotNull(admitted.Packets.Attempts[0].Primitive);
        ImmutableArray<byte> clipped = records.SetItem(6, 2);
        Assert.Null(Emit(type, clipped, 0x10).Packets.Attempts[0].Primitive);
        ImmutableArray<byte> otherFlag = records.SetItem(6, 1);
        Assert.NotNull(Emit(type, otherFlag, 0x10).Packets.Attempts[0].Primitive);
    }

    [Fact]
    public void Accepted_then_rejected_face_preserves_last_partial_writes_at_the_unchanged_next_cursor()
    {
        byte[] faces = Face(7).AddRange(Face(7)).ToArray();
        for (int corner = 0; corner < 3; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(20 + 12 + corner * 2), (ushort)(24 + corner * 8));
        }
        ImmutableArray<byte> records = Records([0, 10, 20, 0, 0x000A0000, 10], [100, 100, 100, 200, 200, 200]);
        NativeBattleWaterMeshResult result = NativeBattleWaterMeshPackets.EmitSelected(Frame(), new(7, 2, faces.ToImmutableArray()), records, [1, 2, 3, 4, 5, 6], 0x5000, Resolve);
        Assert.Equal(36, result.Packets.AllocationBytes);
        Assert.Equal(2, result.Packets.Attempts.Length);
        Assert.Equal(36, result.Packets.Attempts[1].ByteOffsetRelative);
        Assert.Null(result.Packets.Attempts[1].Primitive);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(221);
        memory = memory with { Arena = memory.Arena!.Write(0x1000, Enumerable.Repeat((byte)0xA5, 72).ToArray()) };
        NativeBattleEffectPacketWrite write = NativeBattleEffectPacketArena.Append(memory, 0x1000, NativeBattleLogicalOrdering.Clear(), result.Packets);
        Assert.Equal(0x1024, write.Cursor);
        byte[] tail = write.Memory.Arena!.Read(0x1024, 36);
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 16), tail.Take(16));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(16)));
        Assert.Equal(0x000A0000u, BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(24)));
        Assert.Equal(10u, BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(32)));
    }

    [Fact]
    public void Depth_formula_uses_wrapped_triangle_sum_and_unsigned_masked_quad_sum()
    {
        uint? inserted = null;
        int Capture(uint pointer)
        {
            inserted = pointer;
            return Resolve(pointer);
        }
        NativeBattleWaterMeshPackets.EmitSelected(Frame(), new(7, 1, Face(7)), Records([0, 10, 20], [0xFFFC, 0xFFFC, 0xFFFC]), [0xFFFFFFFF, 0xABCD8000, 0x1234FFFF], 0, Capture);
        uint triangle = (((0xFFFCu * 3) & 0xFFFF) / 3) & 0x3FFC;
        Assert.Equal(Ot + 17 * 4 + triangle, inserted);
        NativeBattleWaterMeshPackets.EmitSelected(Frame(), new(17, 1, Face(17)), Records([0, 10, 20, 30], [0x80001000, 0x80001000, 0x80001000, 0x80001000]), [1, 2, 3, 4], 0, Capture);
        Assert.Equal(Ot + 17 * 4 + 0x1000u, inserted);
    }

    [Fact]
    public void Packed_RGB_uses_round_before_shift_and_retains_semi_command_bits_without_DPCS()
    {
        NativeBattleWaterExecutionFrame frame = Frame().WriteTemporaryDword(0x84, 0x00FF8001).WriteTemporaryDword(0x8C, 0x02000000);
        byte[] face = Face(7).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x0001017F);
        NativeBattleWaterMeshResult result = NativeBattleWaterMeshPackets.EmitSelected(frame, new(7, 1, face.ToImmutableArray()), Records([0, 10, 20], [100, 100, 100]), [1, 2, 3], 0, Resolve);
        Assert.Equal(0x32020101u, BinaryPrimitives.ReadUInt32LittleEndian(result.Packets.Attempts[0].Data.AsSpan().Slice(12)));
    }

    [Fact]
    public void JSON_next_emission_preserves_current_inputs_and_unproved_types_or_selectors_fail()
    {
        NativeBattleWaterExecutionFrame frame = Frame();
        NativeBattleWaterExecutionFrame cold = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        ImmutableArray<byte> records = Records([0, 10, 20], [100, 200, 300]);
        NativeBattleWaterMeshResult first = NativeBattleWaterMeshPackets.EmitSelected(frame, new(9, 1, Face(9)), records, [1, 2, 3], 0x2000, Resolve);
        NativeBattleWaterMeshResult next = NativeBattleWaterMeshPackets.EmitSelected(cold, new(9, 1, Face(9)), records, [1, 2, 3], 0x2000, Resolve);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(next));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshPackets.EmitSelected(frame, new(8, 0, []), records, [1, 2, 3], 0, Resolve));
        byte[] bad = Face(7).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(12), 1);
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterMeshPackets.EmitSelected(frame, new(7, 1, bad.ToImmutableArray()), records, [1, 2, 3], 0, Resolve));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterMeshPackets.EmitSelected(frame, new(7, 1, Face(7)), records, [1], 0, Resolve));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(17)]
    public void Zero_count_is_not_admitted_as_a_native_empty_group(short type)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterMeshPackets.EmitSelected(Frame(), new(type, 0, []), Records([0, 10, 20, 30], [100, 100, 100, 100]), [1, 2, 3, 4], 0, Resolve));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(17)]
    public void Accepted_untextured_groups_do_not_consume_unknown_CLUT_delta(short type)
    {
        NativeBattleWaterExecutionFrame frame = UnknownFields(Frame(), 0x54);
        NativeBattleWaterMeshResult result = NativeBattleWaterMeshPackets.EmitSelected(frame, new(type, 1, Face(type)), Records([0, 10, 20, 30], [100, 100, 100, 100]), [1, 2, 3, 4], 0, Resolve);
        Assert.NotNull(Assert.Single(result.Packets.Attempts).Primitive);
        Assert.False(result.Frame.TemporaryFrame.Known[0x54]);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(17)]
    public void Culled_faces_preserve_SXY_without_consuming_unknown_packet_color_CLUT_or_OT_fields(short type)
    {
        NativeBattleWaterExecutionFrame frame = UnknownFields(Frame(), 0x50, 0x54, 0x5C, 0x8C);
        NativeBattleWaterMeshResult result = NativeBattleWaterMeshPackets.EmitSelected(frame, new(type, 1, Face(type)), Records([0, 0x000A0000, 10, 0x000A000A], [100, 100, 100, 100]), [1, 2, 3, 4], 0, Resolve);
        Assert.Null(Assert.Single(result.Packets.Attempts).Primitive);
        Assert.Contains(result.Packets.Attempts[0].WriteMask, mask => mask == NativeBattleGfPacketByteState.Known);
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshPackets.EmitSelected(UnknownFields(Frame(), 0x54), new(9, 1, Face(9)), Records([0, 10, 20], [100, 100, 100]), [1, 2, 3], 0, Resolve));
    }

    private static NativeBattleWaterExecutionFrame UnknownFields(NativeBattleWaterExecutionFrame frame, params int[] offsets)
    {
        bool[] known = frame.TemporaryFrame.Known.ToArray();
        foreach (int offset in offsets)
        {
            Array.Fill(known, false, offset, 4);
        }
        return frame with { TemporaryFrame = frame.TemporaryFrame with { Known = known.ToImmutableArray() } };
    }

    internal static NativeBattleWaterExecutionFrame Frame()
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(1));
        return frame.WriteTemporaryDword(0x50, 0xE1000003).WriteTemporaryDword(0x54, 3).WriteTemporaryDword(0x5C, Ot + 17 * 4).WriteTemporaryDword(0x84, 0x00808080).WriteTemporaryDword(0x8C, 0).WriteTemporaryDword(0x90, 0).WriteTemporaryDword(0xF0, 0xAABBCCDD);
    }

    private static NativeBattleWaterMeshResult Emit(short type, ImmutableArray<byte> records, uint flags = 0) => NativeBattleWaterMeshPackets.EmitSelected(Frame().WriteTemporaryDword(0x90, flags), new(type, 1, Face(type)), records, [0xFFFF8001, 0xABCD0002, 65535, 999], 0x5000, Resolve);
    private static int Resolve(uint pointer)
    {
        Assert.True(pointer >= Ot && (pointer - Ot) % 4 == 0);
        return checked((int)((pointer - Ot) / 4));
    }

    private static ImmutableArray<byte> Face(short type)
    {
        int stride = type == 7 ? 20 : type == 9 ? 28 : 24;
        byte[] face = new byte[stride];
        for (int corner = 0; corner < (type == 17 ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(corner * 4), 0x00808080);
        }
        int first = type == 7 ? 12 : type == 9 ? 18 : 16;
        for (int corner = 0; corner < (type == 17 ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(first + corner * 2), (ushort)(corner * 8));
        }
        if (type == 9)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(12), 0x1122);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(14), 0x3344);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(16), 0x5566);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(24), 0xFFF0);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(26), 0x10);
        }
        return face.ToImmutableArray();
    }

    private static ImmutableArray<byte> Records(uint[] screens, uint[] depths)
    {
        byte[] data = new byte[screens.Length * 8];
        for (int index = 0; index < screens.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 8), screens[index]);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 8 + 4), depths[index]);
        }
        return data.ToImmutableArray();
    }
}
