using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlTrailPacketsTests
{
    private const uint Head = 0x20FC4B4;
    private static ImmutableArray<int> Fifo => [101, 202, 303, 404];

    [Fact]
    public void Two_row_outline_uses_512_bias_and_preserves_all_non_screen_row_bytes()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        NativeBattleQuezacotlTrailBatch batch = Emit(rows, 2);
        Assert.Equal(new[] { Head, Head + 40 }, batch.RowWrites.Select(write => write.Address));
        NativeBattleGfGeometryAttempt first = batch.RowWrites[0].Attempt;
        Assert.Equal(XY(0, -2), Read32(first.Data, 0x10));
        Assert.Equal(XY(0, 2), Read32(first.Data, 0x18));
        Assert.Equal(XY(100, 0), Read32(batch.RowWrites[1].Attempt.Data, 0x10));
        Assert.Equal(XY(100, 0), Read32(batch.RowWrites[1].Attempt.Data, 0x18));
        Assert.Equal(8, first.WriteMask.Count(state => state == NativeBattleGfPacketByteState.Known));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[6]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[0x14]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[0x20]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[0x24]);
        Assert.Null(first.Primitive);
        Assert.Equal(0, first.ByteOffsetRelative);
    }

    [Fact]
    public void Effect_GT4_uses_color_WORD_arithmetic_shift_and_frozen_full_depth_FIFO()
    {
        Dictionary<uint, byte[]> rows = Rows(3);
        NativeBattleQuezacotlTrailBatch batch = Emit(rows, 3);
        Assert.Equal(104, batch.EffectPackets.AllocationBytes);
        Assert.Equal(new[] { 0, 52 }, batch.EffectPackets.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        NativeBattleGeometryPrimitive first = batch.EffectPackets.Attempts[0].Primitive!;
        Assert.Equal(0x3EFAFAFAu, first.Words[1]);
        Assert.Equal(0x646464u, first.Words[7]);
        Assert.Equal(0x3E94C008u, first.Words[3]);
        Assert.Equal(0x00B6C038u, first.Words[6]);
        Assert.Equal(0xC008u, first.Words[9]);
        Assert.Equal(0xC038u, first.Words[12]);
        Assert.Equal(81, first.Bucket);
        Assert.All(batch.EffectPackets.Attempts, attempt =>
        {
            Assert.Equal(Fifo, attempt.Primitive!.VertexPayload4);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[50]);
        });
    }

    [Fact]
    public void Color_shift_is_signed_WORD_SAR_and_does_not_clamp_negative_native_values()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        BinaryPrimitives.WriteInt16LittleEndian(rows[Head].AsSpan(0x20), -16);
        BinaryPrimitives.WriteInt16LittleEndian(rows[Head + 40].AsSpan(0x20), 16);
        NativeBattleGeometryPrimitive packet = Assert.Single(Emit(rows, 2).EffectPackets.Attempts).Primitive!;
        Assert.Equal(0x3EFFFFFFu, packet.Words[1]);
        Assert.Equal(0xFFFFFFFFu, packet.Words[4]);
        Assert.Equal(0x00010101u, packet.Words[7]);
    }

    [Fact]
    public void Lazy_reads_do_not_require_unused_XYZ_head_width_tail_link_or_stale_right_screen()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        List<(uint Address, int Offset, int Length)> reads = [];
        NativeBattleQuezacotlTrailBatch batch = NativeBattleQuezacotlTrailPackets.Emit6C84C0(Head, 2, (address, offset, length) =>
        {
            Assert.True(offset >= 0x10 || offset == 6);
            Assert.False(address == Head && offset == 6);
            Assert.False(address == Head + 40 && offset == 0x24);
            Assert.NotEqual(0x18, offset);
            reads.Add((address, offset, length));
            return rows[address].AsSpan(offset, length).ToArray().ToImmutableArray();
        }, Distances(), Fifo);
        Assert.Single(batch.EffectPackets.Attempts);
        Assert.Contains((Head + 40, 6, 2), reads);
    }

    [Fact]
    public void Negative_following_depth_preserves_the_middle_rows_actual_old_right_screen()
    {
        Dictionary<uint, byte[]> rows = Rows(3);
        BinaryPrimitives.WriteInt16LittleEndian(rows[Head + 80].AsSpan(0x14), -1);
        BinaryPrimitives.WriteUInt32LittleEndian(rows[Head + 40].AsSpan(0x18), 0xABCD1234);
        NativeBattleQuezacotlTrailBatch batch = Emit(rows, 3);
        Assert.Single(batch.RowWrites);
        Assert.Equal(0xABCD1234u, Assert.Single(batch.EffectPackets.Attempts).Primitive!.Words[11]);
    }

    [Fact]
    public void Unknown_old_right_screen_is_read_only_when_native_packet_admission_requires_it()
    {
        Dictionary<uint, byte[]> rows = Rows(3);
        BinaryPrimitives.WriteInt16LittleEndian(rows[Head + 80].AsSpan(0x14), -1);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlTrailPackets.Emit6C84C0(Head, 3, (address, offset, length) =>
        {
            if (address == Head + 40 && offset == 0x18)
            {
                throw new NotSupportedException("Fixture retained right screen is unknown.");
            }
            return rows[address].AsSpan(offset, length).ToArray().ToImmutableArray();
        }, Distances(), Fifo));
    }

    [Fact]
    public void Degenerate_two_row_segment_only_marks_first_depth_WORD_and_emits_nothing_without_reading_FIFO()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        BinaryPrimitives.WriteUInt32LittleEndian(rows[Head + 40].AsSpan(0x10), XY(0, 0));
        NativeBattleQuezacotlTrailBatch batch = Emit(rows, 2, default(ImmutableArray<int>));
        Assert.Equal(2, batch.RowWrites.Length);
        NativeBattleGfGeometryAttempt first = batch.RowWrites[0].Attempt;
        Assert.Equal(2, first.WriteMask.Count(state => state == NativeBattleGfPacketByteState.Known));
        Assert.Equal(-1, BinaryPrimitives.ReadInt16LittleEndian(first.Data.AsSpan().Slice(0x14)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[0x10]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[0x16]);
        Assert.Empty(batch.EffectPackets.Attempts);
        Assert.Equal(0, batch.EffectPackets.AllocationBytes);
    }

    [Fact]
    public void Later_use_of_uninitialized_direction_after_degenerate_first_segment_remains_strict()
    {
        Dictionary<uint, byte[]> rows = Rows(3);
        BinaryPrimitives.WriteUInt32LittleEndian(rows[Head + 40].AsSpan(0x10), XY(0, 0));
        Assert.Throws<NotSupportedException>(() => Emit(rows, 3));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Caller_count_gate_is_an_explicit_signed_WORD_contract(int count)
    {
        Assert.Throws<InvalidDataException>(() => Emit(Rows(2), count));
    }

    [Fact]
    public void Null_or_aliased_chain_links_do_not_generate_synthetic_rows()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        BinaryPrimitives.WriteUInt32LittleEndian(rows[Head].AsSpan(0x24), 0);
        Assert.Throws<NotSupportedException>(() => Emit(rows, 2));
        BinaryPrimitives.WriteUInt32LittleEndian(rows[Head].AsSpan(0x24), Head);
        Assert.Throws<NotSupportedException>(() => Emit(rows, 2));
    }

    [Fact]
    public void Accepted_packets_require_explicit_fifo_and_row_reads_must_return_exact_requested_spans()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        Assert.Throws<NotSupportedException>(() => Emit(rows, 2, []));
        Assert.Throws<NotSupportedException>(() => Emit(rows, 2, [1, 2, 3, 65536]));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlTrailPackets.Emit6C84C0(Head, 2, (_, _, _) => [], Distances(), Fifo));
    }

    [Fact]
    public void Packet_and_row_write_JSON_round_trip_preserves_masks_and_canonical_row_addresses()
    {
        NativeBattleQuezacotlTrailBatch source = Emit(Rows(3), 3);
        NativeBattleQuezacotlTrailBatch restored = JsonSerializer.Deserialize<NativeBattleQuezacotlTrailBatch>(JsonSerializer.Serialize(source))!;
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(restored));
    }

    private static NativeBattleQuezacotlTrailBatch Emit(Dictionary<uint, byte[]> rows, int count, ImmutableArray<int>? fifo = null)
    {
        return NativeBattleQuezacotlTrailPackets.Emit6C84C0(Head, count, (address, offset, length) => rows[address].AsSpan(offset, length).ToArray().ToImmutableArray(), Distances(), fifo ?? Fifo);
    }

    private static Dictionary<uint, byte[]> Rows(int count)
    {
        Dictionary<uint, byte[]> result = [];
        for (int index = 0; index < count; index++)
        {
            byte[] data = Enumerable.Repeat((byte)0xCC, 40).ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(6), 64);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x10), XY((short)(index * 100), 0));
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x14), 256);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x18), 0x12345678);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x20), index == 0 ? (short)4000 : (short)1600);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x24), index < count - 1 ? Head + (uint)((index + 1) * 40) : 0);
            result.Add(Head + (uint)(index * 40), data);
        }
        return result;
    }

    private static ImmutableArray<byte> Distances()
    {
        // Controlled index fixture; actual Session binds original overlapping12B05F0 bytes.
        return Enumerable.Range(0, 16384).Select(value => (byte)Math.Floor(Math.Sqrt((value & 127) * (value & 127) + (value >> 7) * (value >> 7)))).ToImmutableArray();
    }

    private static uint Read32(ImmutableArray<byte> data, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset));
    }

    private static uint XY(short x, short y)
    {
        return (ushort)x | unchecked((uint)(ushort)y << 16);
    }
}
