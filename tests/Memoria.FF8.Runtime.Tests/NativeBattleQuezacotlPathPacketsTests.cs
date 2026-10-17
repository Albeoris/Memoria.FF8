using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlPathPacketsTests
{
    private const uint Head = 0x211FCB8;
    private static NativeBattleActionMatrix Captured => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection => new(null, 128, 0, 0);
    private static ImmutableArray<int?> Incoming => [11, 22, 33, 44];

    [Fact]
    public void Projection_stores_full_DWORD_screen_and_quarterdepth_before_1024_bias_outline()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        NativeBattleQuezacotlPathBatch batch = Emit(rows, 2);
        Assert.Equal(new[] { Head, Head + 28, Head, Head + 28 }, batch.RowWrites.Select(write => write.Address));
        NativeBattleGfGeometryAttempt projected = batch.RowWrites[0].Attempt;
        Assert.Equal(XY(0, 0), U32(projected.Data, 4));
        Assert.Equal(256u, U32(projected.Data, 8));
        Assert.Equal(Enumerable.Range(4, 8), Enumerable.Range(0, 28).Where(index => projected.WriteMask[index] == NativeBattleGfPacketByteState.Known));
        Assert.Equal(XY(0, -1), U32(batch.RowWrites[2].Attempt.Data, 4));
        Assert.Equal(XY(0, 1), U32(batch.RowWrites[2].Attempt.Data, 12));
        Assert.Equal(XY(100, 0), U32(batch.RowWrites[3].Attempt.Data, 4));
        Assert.Equal(XY(100, 0), U32(batch.RowWrites[3].Attempt.Data, 12));
        Assert.Equal(8, batch.RowWrites[2].Attempt.WriteMask.Count(state => state == NativeBattleGfPacketByteState.Known));
        Assert.All(batch.RowWrites, write => Assert.Null(write.Attempt.Primitive));
    }

    [Fact]
    public void GT4_retains_native_sparse_padding_signed_colors_bucket_and_full_FIFO()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        BinaryPrimitives.WriteInt16LittleEndian(rows[Head].AsSpan(0x14), -16);
        NativeBattleQuezacotlPathBatch batch = Emit(rows, 2);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.EffectPackets.Attempts);
        NativeBattleGeometryPrimitive packet = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(52, batch.EffectPackets.AllocationBytes);
        Assert.Equal(81, packet.Bucket);
        Assert.Equal(new[] { 33, 44, 1024, 1024 }, packet.VertexPayload4);
        Assert.Equal(0x0C000000u, packet.Words[0]);
        Assert.Equal(0x3EFFFFFFu, packet.Words[1]);
        Assert.Equal(0xFFFFFFFFu, packet.Words[4]);
        Assert.Equal(0x646464u, packet.Words[7]);
        Assert.Equal(0x3E94C008u, packet.Words[3]);
        Assert.Equal(0x00B6C038u, packet.Words[6]);
        Assert.Equal(0xC008u, packet.Words[9]);
        Assert.Equal(0xC038u, packet.Words[12]);
        Assert.All(new[] { 38, 39, 50, 51 }, index => Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[index]));
    }

    [Fact]
    public void Four_or_more_projections_replace_unknown_FIFO_with_actual_last_four_full_depths()
    {
        Dictionary<uint, byte[]> rows = Rows(5);
        NativeBattleQuezacotlPathBatch batch = Emit(rows, 5, default(ImmutableArray<int?>), selector => new(checked((short)(selector * (selector + 1) * 100)), 0, checked((short)((selector + 1) * 1024))));
        Assert.Equal(new int?[] { 2048, 3072, 4096, 5120 }, batch.FinalDepthFifo4);
        Assert.Equal(new[] { 0, 52, 104, 156 }, batch.EffectPackets.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.All(batch.EffectPackets.Attempts, attempt => Assert.Equal(new[] { 2048, 3072, 4096, 5120 }, attempt.Primitive!.VertexPayload4));
    }

    [Fact]
    public void Projection_traverses_until_NULL_independently_of_later_draw_count()
    {
        Dictionary<uint, byte[]> rows = Rows(5);
        NativeBattleQuezacotlPathBatch batch = Emit(rows, 2, default(ImmutableArray<int?>), selector => new(checked((short)(selector * (selector + 1) * 100)), 0, checked((short)((selector + 1) * 1024))));
        Assert.Equal(7, batch.RowWrites.Length);
        Assert.Equal(new int?[] { 2048, 3072, 4096, 5120 }, batch.FinalDepthFifo4);
        Assert.Single(batch.EffectPackets.Attempts);
        Assert.Equal(Head + 112, batch.RowWrites[4].Address);
    }

    [Fact]
    public void Short_chain_requires_only_the_incoming_FIFO_slots_not_overwritten_by_projection()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        NativeBattleQuezacotlPathBatch batch = Emit(rows, 2, [null, null, 33, 44]);
        Assert.Equal(new[] { 33, 44, 1024, 1024 }, Assert.Single(batch.EffectPackets.Attempts).Primitive!.VertexPayload4);
        Assert.Throws<NotSupportedException>(() => Emit(rows, 2, [11, 22, null, 44]));
        Assert.Throws<NotSupportedException>(() => Emit(rows, 2, default(ImmutableArray<int?>)));
    }

    [Fact]
    public void Degenerate_projection_keeps_ordered_DWORD_then_WORD_depth_stores_and_needs_no_FIFO()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        NativeBattleQuezacotlPathBatch batch = Emit(rows, 2, default(ImmutableArray<int?>), _ => new(0, 0, 1024));
        Assert.Empty(batch.EffectPackets.Attempts);
        Assert.Equal(new int?[] { null, null, 1024, 1024 }, batch.FinalDepthFifo4);
        NativeBattleGfGeometryAttempt depthWord = batch.RowWrites[2].Attempt;
        Assert.Equal(2, depthWord.WriteMask.Count(state => state == NativeBattleGfPacketByteState.Known));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, depthWord.WriteMask[10]);
        Apply(rows, batch.RowWrites);
        Assert.Equal(0x0000FFFFu, BinaryPrimitives.ReadUInt32LittleEndian(rows[Head].AsSpan(8)));
        Assert.Equal((byte)0xCC, rows[Head][12]);
    }

    [Fact]
    public void Zero_depth_rejects_packets_without_reading_intensity_or_unknown_FIFO()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        NativeBattleQuezacotlPathBatch batch = NativeBattleQuezacotlPathPackets.Emit6C79A0(Head, 2, (address, offset, length) =>
        {
            Assert.NotEqual(0x14, offset);
            return Read(rows, address, offset, length);
        }, selector => new(checked((short)(selector * 100)), 0, 0), Captured, Projection, Distances());
        Assert.Empty(batch.EffectPackets.Attempts);
        Assert.Equal(new int?[] { null, null, 0, 0 }, batch.FinalDepthFifo4);
    }

    [Fact]
    public void Captured_matrix_is_explicit_and_projection_View_is_not_a_substitute()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        NativeBattleQuezacotlPathBatch batch = NativeBattleQuezacotlPathPackets.Emit6C79A0(Head, 2, (address, offset, length) => Read(rows, address, offset, length), selector => new(checked((short)(selector * 100)), 0, 1024), Captured with { X = 64 }, Projection, Distances(), Incoming);
        Assert.Equal(XY(64, 0), U32(batch.RowWrites[0].Attempt.Data, 4));
    }

    [Fact]
    public void Vertex_selector_is_signed_and_only_native_reached_row_bytes_are_read()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        BinaryPrimitives.WriteInt16LittleEndian(rows[Head].AsSpan(), -1);
        List<short> selectors = [];
        NativeBattleQuezacotlPathBatch batch = NativeBattleQuezacotlPathPackets.Emit6C79A0(Head, 2, (address, offset, length) =>
        {
            Assert.Contains(offset, new[] { 0, 2, 0x14, 0x18 });
            Assert.False(address == Head && offset == 2);
            return Read(rows, address, offset, length);
        }, selector =>
        {
            selectors.Add(selector);
            return new(checked((short)(selector * 100)), 0, 1024);
        }, Captured, Projection, Distances(), Incoming);
        Assert.Equal(new short[] { -1, 1 }, selectors);
        Assert.Single(batch.EffectPackets.Attempts);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32768)]
    public void Caller_count_retains_its_signed_WORD_gate(int count)
    {
        Assert.Throws<InvalidDataException>(() => Emit(Rows(2), count));
    }

    [Fact]
    public void Missing_chain_rows_cycles_and_incomplete_reads_remain_strict()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        Assert.Throws<NotSupportedException>(() => Emit(rows, 3));
        BinaryPrimitives.WriteUInt32LittleEndian(rows[Head + 28].AsSpan(0x18), Head);
        Assert.Throws<NotSupportedException>(() => Emit(rows, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlPathPackets.Emit6C79A0(Head, 2, (_, _, _) => [], _ => new(0, 0, 1024), Captured, Projection, Distances(), Incoming));
    }

    [Fact]
    public void Unknown_projection_fields_and_invalid_FIFO_words_are_not_filled()
    {
        Dictionary<uint, byte[]> rows = Rows(2);
        Assert.Throws<InvalidDataException>(() => Emit(rows, 2, [1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => Emit(rows, 2, [1, 2, 3, 65536]));
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlPathPackets.Emit6C79A0(Head, 2, (address, offset, length) => Read(rows, address, offset, length), selector => new(checked((short)(selector * 100)), 0, 1024), Captured, Projection with { FocalLength = null }, Distances(), Incoming));
    }

    [Fact]
    public void JSON_roundtrip_preserves_ordered_row_stores_padding_and_partial_FIFO()
    {
        NativeBattleQuezacotlPathBatch batch = Emit(Rows(2), 2, default(ImmutableArray<int?>), _ => new(0, 0, 1024));
        string json = JsonSerializer.Serialize(batch);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleQuezacotlPathBatch>(json)));
    }

    private static NativeBattleQuezacotlPathBatch Emit(Dictionary<uint, byte[]> rows, int count, ImmutableArray<int?>? fifo = null, Func<short, NativeBattleVector>? vertex = null)
    {
        Func<short, NativeBattleVector> selectedVertex = vertex ?? (selector => new NativeBattleVector(checked((short)(selector * 100)), 0, 1024));
        return NativeBattleQuezacotlPathPackets.Emit6C79A0(Head, count, (address, offset, length) => Read(rows, address, offset, length), selectedVertex, Captured, Projection, Distances(), fifo ?? Incoming);
    }

    private static Dictionary<uint, byte[]> Rows(int count)
    {
        Dictionary<uint, byte[]> rows = [];
        for (int index = 0; index < count; index++)
        {
            byte[] row = Enumerable.Repeat((byte)0xCC, 28).ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(), (short)index);
            BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(2), 64);
            BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(0x14), index == 0 ? (short)4000 : (short)1600);
            BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(0x18), index < count - 1 ? Head + (uint)((index + 1) * 28) : 0);
            rows.Add(Head + (uint)(index * 28), row);
        }
        return rows;
    }

    private static ImmutableArray<byte> Read(Dictionary<uint, byte[]> rows, uint address, int offset, int length) => rows[address].AsSpan(offset, length).ToArray().ToImmutableArray();

    private static ImmutableArray<byte> Distances() => Enumerable.Range(0, 16384).Select(value => (byte)Math.Floor(Math.Sqrt((value & 127) * (value & 127) + (value >> 7) * (value >> 7)))).ToImmutableArray();

    private static uint U32(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset));

    private static uint XY(short x, short y) => (ushort)x | unchecked((uint)(ushort)y << 16);

    private static void Apply(Dictionary<uint, byte[]> rows, ImmutableArray<NativeBattleQuezacotlTrailRowWrite> writes)
    {
        foreach (NativeBattleQuezacotlTrailRowWrite write in writes)
        {
            for (int index = 0; index < write.Attempt.Data.Length; index++)
            {
                if (write.Attempt.WriteMask[index] == NativeBattleGfPacketByteState.Known)
                {
                    rows[write.Address][index] = write.Attempt.Data[index];
                }
            }
        }
    }
}
