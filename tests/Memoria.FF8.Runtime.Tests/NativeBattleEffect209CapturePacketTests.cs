using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect209CapturePacketTests
{
    [Theory]
    [InlineData((short)0, (short)0, 17)]
    [InlineData((short)576, (short)256, 0x1121)]
    public void Capture_environment_keeps_current_page_flags_and_retained_tag_tail_with_exact_native_stores(short x, short y, int bucket)
    {
        byte[] current = Enumerable.Repeat((byte)0xCD, 92).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(current.AsSpan(0x14), 0x995);
        current[0x16] = 0;
        current[0x17] = 1;
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xFF, 64).ToArray());
        NativeBattleGfGeometryBatch batch = NativeBattleEffect209CapturePackets.Environment7E90(retained, current.ToImmutableArray(), x, y, bucket);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(64, batch.AllocationBytes);
        Assert.Equal(bucket, attempt.Primitive!.Bucket);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, attempt.Primitive.VertexPayload4.ToArray());
        Assert.Equal(0x08FFFFFFu, Dword(0));
        Assert.Equal(0xE3000000u | (uint)(ushort)x | ((uint)(ushort)y << 10), Dword(4));
        Assert.Equal(0xE1000D95u, Dword(16));
        Assert.Equal(0xE2000000u, Dword(20));
        Assert.Equal(0x02000000u, Dword(24));
        Assert.Equal((uint)(ushort)x | ((uint)(ushort)y << 16), Dword(28));
        Assert.Equal(320u | (216u << 16), Dword(32));
        for (int index = 0; index < 64; index++)
        {
            Assert.Equal(index is >= 3 and < 36 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[index]);
        }
        Assert.All(attempt.Data.AsSpan().Slice(36).ToArray(), value => Assert.Equal((byte)0xFF, value));

        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));
    }
}
