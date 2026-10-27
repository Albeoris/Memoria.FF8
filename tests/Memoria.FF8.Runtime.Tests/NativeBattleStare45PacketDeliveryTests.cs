using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStare45PacketDeliveryTests
{
    [Fact]
    public void Mode_precedes_polygon_in_COMMON_with_exact_reservation_and_independent_restore()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleStare45State state = NativeBattleStare45State.FromColdProcess();
        NativeBattleGfGeometryBatch batch = Batch();
        NativeBattleStare45PacketStep first = NativeBattleStare45PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), batch, 0x1D8E054, 0);
        Assert.Equal(68, first.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(new[] { 56, 16 }, first.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.Equal(0xE1000020u, BinaryPrimitives.ReadUInt32LittleEndian(first.Memory.PrimitiveProcess.ReadCommonPacket(60, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(first.Memory.PrimitiveProcess.ReadCommonPacket(64, 4)));
        Assert.Same(state, first.State);
        Assert.Same(memory.Arena, first.Memory.Arena);
        NativeBattleStare45PacketStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleStare45PacketStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleStare45PacketStep Next(NativeBattleStare45PacketStep step) => NativeBattleStare45PacketDelivery.Deliver(step.Memory, step.State, step.Ordering, batch, 0x1D8E054, 0);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retained_consumed_byte_requires_current_COMMON_owner_and_reaches_logical_words(bool observed)
    {
        NativeBattleEffectMemory memory = Memory();
        if (observed)
        {
            memory = memory with { PrimitiveProcess = memory.PrimitiveProcess!.ApplyCommonWrite(20, [(byte)0xA5], [NativeBattleGfPacketByteState.Known]) };
        }
        NativeBattleGfGeometryBatch batch = Batch();
        NativeBattleGfGeometryAttempt polygon = batch.Attempts[0];
        batch = batch with { Attempts = batch.Attempts.SetItem(0, polygon with { WriteMask = polygon.WriteMask.SetItem(4, NativeBattleGfPacketByteState.Untouched) }) };
        if (!observed)
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleStare45PacketDelivery.Deliver(memory, NativeBattleStare45State.FromColdProcess(), NativeBattleLogicalOrdering.Clear(), batch, 0x1D8E054, 0));
            Assert.Equal(16, memory.PrimitiveProcess!.ByteCursor);
            return;
        }
        NativeBattleStare45PacketStep actual = NativeBattleStare45PacketDelivery.Deliver(memory, NativeBattleStare45State.FromColdProcess(), NativeBattleLogicalOrdering.Clear(), batch, 0x1D8E054, 0);
        Assert.Equal((byte)0xA5, actual.Memory.PrimitiveProcess!.ReadCommonPacket(20, 1)[0]);
        Assert.Equal(0xA5u, actual.Ordering.Packets[1].Words[1] & 0xFF);
    }

    [Fact]
    public void Rejected_prefix_is_written_without_cursor_advance_or_mode_link()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleGfGeometryAttempt prefix = Packet(0, [0x09000000, 0x2E001234, 0], false);
        NativeBattleStare45PacketStep actual = NativeBattleStare45PacketDelivery.Deliver(memory, NativeBattleStare45State.FromColdProcess(), NativeBattleLogicalOrdering.Clear(), new([prefix], 0), 0x1D8E054, 0);
        Assert.Equal(16, actual.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Empty(actual.Ordering.Packets);
        Assert.Equal(0x2E001234u, BinaryPrimitives.ReadUInt32LittleEndian(actual.Memory.PrimitiveProcess.ReadCommonPacket(20, 4)));
        Assert.Throws<NotSupportedException>(() => memory.PrimitiveProcess!.ReadCommonPacket(20, 4));
    }

    [Fact]
    public void Wrong_cursor_and_unknown_consumed_store_fail_before_publication()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleStare45State state = NativeBattleStare45State.FromColdProcess();
        NativeBattleGfGeometryBatch batch = Batch();
        Assert.Throws<InvalidDataException>(() => NativeBattleStare45PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), batch, 0x26543B4, 0));
        NativeBattleGfGeometryAttempt polygon = batch.Attempts[0];
        batch = batch with { Attempts = batch.Attempts.SetItem(0, polygon with { WriteMask = polygon.WriteMask.SetItem(4, NativeBattleGfPacketByteState.Unknown) }) };
        Assert.Throws<NotSupportedException>(() => NativeBattleStare45PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), batch, 0x1D8E054, 0));
        Assert.Equal(16, memory.PrimitiveProcess!.ByteCursor);
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 16 } };
    private static NativeBattleGfGeometryBatch Batch()
    {
        NativeBattleGfGeometryAttempt polygon = Packet(0, [0x09000000, 0x2E123400, 0, 0, 0, 0, 0, 0, 0, 0]);
        NativeBattleGfGeometryAttempt link = polygon with { WriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToImmutableArray() };
        return new([polygon with { Primitive = null }, Packet(40, [0x02000000, 0xE1000020, 0]), link], 52);
    }

    private static NativeBattleGfGeometryAttempt Packet(int offset, uint[] words, bool linked = true)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray();
        return new(offset, bytes.ToImmutableArray(), mask, linked ? new(17, words.ToImmutableArray(), [1, 2, 3, 4]) : null);
    }
}
