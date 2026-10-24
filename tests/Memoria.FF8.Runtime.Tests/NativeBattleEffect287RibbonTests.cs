using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Ribbon;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect287RibbonTests
{
    [Fact]
    public void Emit_uses_52_byte_gt4_direct_taper_colors_and_current_fifo()
    {
        NativeBattleEffectArena rows = Rows();
        rows = Row(rows, 0, 100, 200, 128, 128);
        rows = Row(rows, 1, 300, 400, 256, 7);
        NativeBattleGfGeometryBatch batch = Emit(rows, [101u, 202u, 303u, 404u]);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal(52, batch.AllocationBytes);
        Assert.Equal(52, attempt.PacketLength);
        Assert.Equal(0, attempt.ByteOffsetRelative);
        Assert.Equal(13, primitive.Words.Length);
        Assert.Equal(0x0C000000u, primitive.Words[0]);
        Assert.Equal(0x3E808080u, primitive.Words[1]);
        Assert.Equal(0x00808080u, primitive.Words[4]);
        Assert.Equal(0x00070707u, primitive.Words[7]);
        Assert.Equal(0x00070707u, primitive.Words[10]);
        Assert.Equal(0x00C80064u, primitive.Words[2]);
        Assert.Equal(0x00CE0068u, primitive.Words[5]);
        Assert.Equal(0x0190012Cu, primitive.Words[8]);
        Assert.Equal(0x01960130u, primitive.Words[11]);
        Assert.Equal(0x3E14C048u, primitive.Words[3]);
        Assert.Equal(0x00B9C078u, primitive.Words[6]);
        Assert.Equal(65, primitive.Bucket);
        Assert.Equal(new[] { 101, 202, 303, 404 }, primitive.VertexPayload4.ToArray());
    }

    [Fact]
    public void Upper_uv_words_remain_physical_holes_and_are_not_consumed_by_logical_projection()
    {
        NativeBattleEffectArena rows = Row(Row(Rows(), 0, 1, 2, 128, 128), 1, 3, 4, 128, 128);
        NativeBattleGfGeometryBatch batch = Emit(rows, [1u, 2u, 3u, 4u]);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        int[] holes = [38, 39, 50, 51];
        for (int offset = 0; offset < 52; offset++)
        {
            Assert.Equal(holes.Contains(offset) ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, attempt.WriteMask[offset]);
        }
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, 0, 52, _ => throw new InvalidOperationException("Ignored UV bytes must not be consumed."));
        NativeBattleGeometryPrimitive primitive = Assert.Single(resolved.Attempts).Primitive!;
        Assert.Equal(0x0000C048u, primitive.Words[9]);
        Assert.Equal(0x0000C078u, primitive.Words[12]);
        Assert.Equal(attempt.WriteMask, Assert.Single(resolved.Attempts).WriteMask);
    }

    [Fact]
    public void Seven_segments_use_one_final_current_fifo_and_contiguous_52_byte_allocations()
    {
        NativeBattleEffectArena rows = Rows();
        for (int index = 0; index < 8; index++)
        {
            rows = Row(rows, index, index * 10, index * 20, 128 + index, 128);
        }
        NativeBattleGfGeometryBatch batch = Emit(rows, [41u, 42u, 43u, 44u]);
        Assert.Equal(364, batch.AllocationBytes);
        Assert.Equal(7, batch.Attempts.Length);
        for (int index = 0; index < batch.Attempts.Length; index++)
        {
            Assert.Equal(index * 52, batch.Attempts[index].ByteOffsetRelative);
            Assert.Equal(new[] { 41, 42, 43, 44 }, batch.Attempts[index].Primitive!.VertexPayload4.ToArray());
        }
    }

    [Theory]
    [InlineData(0, 128)]
    [InlineData(128, 0)]
    [InlineData(-1, 128)]
    [InlineData(128, -1)]
    public void Emit_requires_both_signed_quarter_depths_strictly_positive(int first, int second)
    {
        NativeBattleEffectArena rows = Row(Row(Rows(), 0, 100, 200, first, 128), 1, 116, 200, second, 128);
        NativeBattleGfGeometryBatch result = Emit(rows, [1u, 2u, 3u, 4u]);
        Assert.Empty(result.Attempts);
        Assert.Equal(0, result.AllocationBytes);
    }

    [Fact]
    public void Current_fifo_unknowns_and_wrong_width_fail_instead_of_using_endpoint_depths()
    {
        NativeBattleEffectArena rows = Row(Row(Rows(), 0, 100, 200, 128, 128), 1, 116, 200, 128, 128);
        Assert.Throws<NotSupportedException>(() => Emit(rows, [1u, null, 3u, 4u]));
        Assert.Throws<InvalidDataException>(() => Emit(rows, [1u, 2u, 3u]));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(1024, 4)]
    [InlineData(4096, 1)]
    public void Extrusion_uses_8320_over_depth_plus_1024_and_preserves_holes(int depth, int width)
    {
        NativeBattleEffectArena rows = Row(Row(Rows(), 0, 100, 200, depth, 128), 1, 116, 200, depth, 128).MarkUnknown(6, 2).MarkUnknown(12, 2);
        (NativeBattleEffectArena result, short? previousX, short? previousY) = Extrude4350(rows, 0, 1, false, null, null, Distance(16, 16));
        Assert.Equal(100, Word(result, 0));
        Assert.Equal(200 - width, Word(result, 2));
        Assert.Equal(100, Word(result, 8));
        Assert.Equal(200 + width, Word(result, 10));
        Assert.Equal((short)0, previousX);
        Assert.Equal((short)4096, previousY);
        Assert.Equal(new[] { false, false }, result.ReadSnapshot(6, 2).Known);
        Assert.Equal(new[] { false, false }, result.ReadSnapshot(12, 2).Known);
        Assert.Equal(128, Word(result, 14));
    }

    [Theory]
    [InlineData(-1, 128)]
    [InlineData(128, -1)]
    public void Negative_current_or_next_depth_leaves_rows_and_tangent_unchanged(int current, int next)
    {
        NativeBattleEffectArena rows = Row(Row(Rows(), 0, 100, 200, current, 128), 1, 116, 200, next, 128);
        (NativeBattleEffectArena result, short? previousX, short? previousY) = Extrude4350(rows, 0, 1, true, -123, 456, null!);
        Assert.Same(rows, result);
        Assert.Equal((short)-123, previousX);
        Assert.Equal((short)456, previousY);
    }

    [Fact]
    public void First_zero_tangent_marks_depth_negative_without_inventing_history()
    {
        NativeBattleEffectArena rows = Row(Row(Rows(), 0, 100, 200, 128, 128), 1, 100, 200, 128, 128);
        (NativeBattleEffectArena result, short? previousX, short? previousY) = Extrude4350(rows, 0, 1, false, null, null, Distance(0, 0));
        Assert.Equal(-1, Word(result, 4));
        Assert.Equal(100, Word(result, 0));
        Assert.Equal(104, Word(result, 8));
        Assert.Null(previousX);
        Assert.Null(previousY);
    }

    [Fact]
    public void Later_zero_tangent_reuses_current_history_and_applies_effect_width()
    {
        NativeBattleEffectArena rows = Row(Row(Rows(), 0, 100, 200, 1024, 128), 1, 100, 200, 1024, 128);
        (NativeBattleEffectArena result, short? previousX, short? previousY) = Extrude4350(rows, 0, 1, true, 2048, -4096, Distance(0, 0));
        Assert.Equal(98, Word(result, 0));
        Assert.Equal(204, Word(result, 2));
        Assert.Equal(102, Word(result, 8));
        Assert.Equal(196, Word(result, 10));
        Assert.Equal((short)2048, previousX);
        Assert.Equal((short)-4096, previousY);
    }

    [Fact]
    public void Opposite_tangent_doubles_current_miter_but_publishes_single_normalized_history()
    {
        NativeBattleEffectArena rows = Row(Row(Rows(), 0, 100, 200, 1024, 128), 1, 84, 200, 1024, 128);
        (NativeBattleEffectArena result, short? previousX, short? previousY) = Extrude4350(rows, 0, 1, true, 0, 4096, Distance(16, 16));
        Assert.Equal(208, Word(result, 2));
        Assert.Equal(192, Word(result, 10));
        Assert.Equal((short)0, previousX);
        Assert.Equal((short)-4096, previousY);
    }

    [Fact]
    public void Final_endpoint_copies_center_to_both_sides_without_reading_distance_or_history()
    {
        NativeBattleEffectArena rows = Row(Rows(), 7, 100, 200, 0, 128);
        (NativeBattleEffectArena result, short? previousX, short? previousY) = Extrude4350(rows, 7, null, true, null, null, null!);
        Assert.Equal(result.Read(112, 4), result.Read(120, 4));
        Assert.Null(previousX);
        Assert.Null(previousY);
    }

    private static NativeBattleEffectArena Rows()
    {
        NativeBattleEffectArena rows = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 8; index++)
        {
            rows = Row(rows, index, 0, 0, -1, 0);
        }
        return rows;
    }

    private static NativeBattleEffectArena Row(NativeBattleEffectArena rows, int index, int x, int y, int depth, int brightness)
    {
        int offset = index * 16;
        return rows.Write(offset, BitConverter.GetBytes(unchecked((short)x))).Write(offset + 2, BitConverter.GetBytes(unchecked((short)y))).Write(offset + 4, BitConverter.GetBytes(unchecked((short)depth))).Write(offset + 8, BitConverter.GetBytes(unchecked((short)(x + 4)))).Write(offset + 10, BitConverter.GetBytes(unchecked((short)(y + 6)))).Write(offset + 14, BitConverter.GetBytes(unchecked((short)brightness)));
    }

    private static short Word(NativeBattleEffectArena rows, int offset) => BinaryPrimitives.ReadInt16LittleEndian(rows.Read(offset, 2));
    private static NativeBattleEffect287Resources Distance(int index, byte value) => new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(NativeBattleEffect287Profile.DistanceAddress + (uint)index, ImmutableArray.Create(value)));
}
