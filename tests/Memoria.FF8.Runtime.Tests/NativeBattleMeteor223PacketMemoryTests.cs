using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeteor223PacketMemoryTests
{
    private const int PacketStart = (int)NativeBattleEffectNodeMemory.ArenaAddress + 0x8000;

    [Fact]
    public void Packet_overlap_preserves_little_endian_lanes_and_rejects_unwritten_native_holes()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame.Arena.Write(0x8000, new byte[] { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC });
        NativeBattleGfGeometryBatch? published = null;
        NativeBattleMeteor223GridMemory memory = Memory(frame, batch => published = batch);
        memory.BeginPackets(PacketStart, 1, 1);
        Assert.Throws<NotSupportedException>(() => memory.Bytes[PacketStart, 0]);
        memory.Dwords[PacketStart, 1] = 0x11223344;
        memory.Bytes[PacketStart, 2] = 0xAA;
        Assert.Equal(0x1122AA44, memory.Dwords[PacketStart, 1]);
        Assert.Throws<NotSupportedException>(() => memory.Bytes[PacketStart, 0]);
        Assert.Throws<NotSupportedException>(() => memory.Bytes[PacketStart, 5]);
        memory.Words[PacketStart, 3] = 0xFFFE;
        Assert.Equal((short)-2, memory.SignedWords[PacketStart, 3]);
        Assert.Equal(unchecked((int)0xFFFEAA44), memory.Dwords[PacketStart, 1]);
        Assert.Same(frame, memory.Publish(PacketStart + 0x44));
        Assert.Equal(3, published!.Attempts.Length);
        NativeBattleGfGeometryAttempt first = published.Attempts[0];
        Assert.Equal(new byte[] { 0, 0x44, 0x33, 0x22, 0x11, 0, 0, 0 }, first.Data.ToArray());
        Assert.Equal(new[] { NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched }, first.WriteMask.ToArray());
        Assert.Equal(new byte[] { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC }, frame.Arena.Read(0x8000, 8));
    }

    [Fact]
    public void Link_zero_placeholders_do_not_authorize_reads_or_change_after_later_native_stores()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        NativeBattleGfGeometryBatch? published = null;
        NativeBattleMeteor223GridMemory memory = Memory(frame, batch => published = batch);
        memory.BeginPackets(PacketStart, 1, 1);
        memory.Dwords[PacketStart, 4] = 0x11223344;
        memory.Bytes[PacketStart, 5] = 0xAA;
        memory.Link(0x1004, PacketStart, 0, 0);
        memory.Publish(PacketStart + 0x44);
        NativeBattleGfGeometryBatch firstPublication = published!;
        NativeBattleGfGeometryAttempt firstStore = firstPublication.Attempts[0];
        NativeBattleGfGeometryAttempt link = firstPublication.Attempts[2];
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, firstStore.Data.ToArray());
        Assert.Equal(0u, link.Primitive!.Words[0]);
        Assert.Equal(0x1122AA44u, link.Primitive.Words[1]);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0x44, 0xAA, 0x22, 0x11 }, link.Data.AsSpan(0, 8).ToArray());
        Assert.All(link.Data.Skip(8), value => Assert.Equal((byte)0, value));
        Assert.All(link.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Throws<NotSupportedException>(() => memory.Bytes[PacketStart, 3]);
        Assert.Throws<NotSupportedException>(() => memory.Bytes[PacketStart, 8]);
        memory.Bytes[PacketStart, 6] = 0xFF;
        memory.Bytes[PacketStart, 48] = 0x55;
        Assert.Equal(unchecked((int)0x11FFAA44), memory.Dwords[PacketStart, 4]);
        Assert.Equal((byte)0x55, memory.Bytes[PacketStart, 48]);
        Assert.Throws<NotSupportedException>(() => memory.Bytes[PacketStart, 47]);
        memory.Publish(PacketStart + 0x44);
        Assert.Equal(3, firstPublication.Attempts.Length);
        Assert.Equal(5, published!.Attempts.Length);
        Assert.Equal(0x1122AA44u, link.Primitive.Words[1]);
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, firstStore.Data.ToArray());
        Assert.Equal((byte)0x22, link.Data[6]);
        Assert.Equal((byte)0, link.Data[48]);
        Assert.True(published.NativeUnlinkedAllocation);
        Assert.Equal(0x44, published.AllocationBytes);
    }

    [Fact]
    public void Future_packet_writes_preserve_sparse_knownness_at_both_native_envelope_edges()
    {
        NativeBattleMeteor223GridMemory memory = Memory(WaterSchedulerFixture.Create(), _ =>
        {
        });
        memory.BeginPackets(PacketStart, 2, 3);
        int length = ((2 + 2) * 3 + 2) * 0x44;
        memory.Bytes[PacketStart, length - 1] = 0xEE;
        Assert.Equal((byte)0xEE, memory.Bytes[PacketStart, length - 1]);
        Assert.Throws<NotSupportedException>(() => memory.Bytes[PacketStart, 0]);
        Assert.Throws<NotSupportedException>(() => memory.Bytes[PacketStart, length - 2]);
        memory.Words[PacketStart, 0] = 0x8001;
        Assert.Equal(unchecked((short)0x8001), memory.SignedWords[PacketStart, 0]);
        Assert.Equal((byte)0xEE, memory.Bytes[PacketStart, length - 1]);
        Assert.Throws<NotSupportedException>(() => memory.Bytes[PacketStart, 2]);
    }

    private static NativeBattleMeteor223GridMemory Memory(NativeBattleWaterExecutionFrame frame, Action<NativeBattleGfGeometryBatch> publish)
    {
        return new(frame, new((_, _) => throw new InvalidOperationException(), (current, _) => current, (current, _) => current, _ => throw new InvalidOperationException(), (current, _, batch) =>
        {
            publish(batch);
            return current;
        }, (_, _) => throw new InvalidOperationException()));
    }
}
