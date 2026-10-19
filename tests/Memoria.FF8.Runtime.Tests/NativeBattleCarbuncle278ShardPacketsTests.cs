using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278ShardPacketsTests
{
    [Fact]
    public void Raw_quad_colors_signed_palette_and_reverse_count_buckets_preserve_native_order()
    {
        NativeBattleActionMatrix main = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleGfGeometryBatch batch = NativeBattleCarbuncle278ShardPackets.Emit683490(Resource(), 0, main, new(main, 288, 160, 108), 17);
        Assert.Equal(80, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 40 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        Assert.Equal(new[] { 19, 18 }, batch.Attempts.Select(attempt => attempt.Primitive!.Bucket).ToArray());
        Assert.All(batch.Attempts, attempt =>
        {
            Assert.NotNull(attempt.Primitive);
            Assert.Equal(0x2C061104u, attempt.Primitive!.Words[1]);
            Assert.Equal(0xFFA0FCFAu, attempt.Primitive.Words[3]);
            Assert.Equal(0x0002FCFEu, attempt.Primitive.Words[5]);
            Assert.Equal(0x0000FFFAu, attempt.Primitive.Words[7]);
            Assert.Equal(0x0000FFFEu, attempt.Primitive.Words[9]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
        });
        byte[] rotated = Resource().ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(rotated.AsSpan(20 + 12), 2048);
        Assert.Equal(batch.Attempts[0].Primitive!.Words.ToArray(), NativeBattleCarbuncle278ShardPackets.Emit683490(rotated.ToImmutableArray(), 0, main, new(main, 288, 160, 108), 17).Attempts[0].Primitive!.Words.ToArray());
    }

    [Fact]
    public void Clipped_quads_reuse_cursor_but_keep_partial_native_stores_and_unwritten_screen_bytes()
    {
        NativeBattleActionMatrix main = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGfGeometryBatch batch = NativeBattleCarbuncle278ShardPackets.Emit683490(Resource(), 0, main, new(main, 288, 160, 108), 17);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.All(batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[14]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[22]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[29]);
        });
        Assert.Throws<InvalidDataException>(() => NativeBattleCarbuncle278ShardPackets.Emit683490(Resource().Slice(0, 30), 0, main, new(main, 288, 160, 108), 17));
        byte[] missing = Resource().ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(missing.AsSpan(20 + 14), 0x4000);
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278ShardPackets.Emit683490(missing.ToImmutableArray(), 0, main, new(main, 288, 160, 108), 17));
    }

    private static ImmutableArray<byte> Resource()
    {
        byte[] bytes = new byte[60];
        bytes[5] = 0xFE;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 2);
        for (int index = 0; index < 2; index++)
        {
            Span<byte> quad = bytes.AsSpan(20 + index * 20, 20);
            quad[0] = 4;
            quad[1] = 17;
            quad[2] = 6;
            quad[3] = 0x2C;
            quad[4] = 250;
            quad[5] = 252;
            BinaryPrimitives.WriteUInt16LittleEndian(quad[6..], 0x20);
            BinaryPrimitives.WriteUInt16LittleEndian(quad[14..], 0x1002);
            BinaryPrimitives.WriteInt16LittleEndian(quad[16..], 4096);
            BinaryPrimitives.WriteInt16LittleEndian(quad[18..], 2048);
        }
        return bytes.ToImmutableArray();
    }
}
