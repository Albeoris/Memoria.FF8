using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSigh46PacketDeliveryTests
{
    [Theory]
    [InlineData(NativeBattleSigh46Profile.PrimaryCursorGlobal)]
    [InlineData(NativeBattleSigh46Profile.SecondaryCursorGlobal)]
    public void Current_TIM_cursor_and_partial_packet_knownness_survive_independent_restore(uint cursor)
    {
        const uint address = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(64, 12) };
        NativeBattleSigh46State state = NativeBattleSigh46State.FromColdProcess().WriteDword(cursor, address + 64);
        NativeBattleGeometryPrimitive primitive = new(17, [0x02000000, 0xE100002A, 0], [0, 0, 0, 0]);
        byte[] bytes = new byte[12];
        for (int index = 0; index < primitive.Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 12).ToImmutableArray().SetItem(0, NativeBattleGfPacketByteState.Untouched).SetItem(1, NativeBattleGfPacketByteState.Untouched).SetItem(2, NativeBattleGfPacketByteState.Untouched);
        NativeBattleGfGeometryBatch batch = new([new(0, bytes.ToImmutableArray(), mask, primitive)], 12);
        NativeBattleEffectMemory before = NativeBattleStorageSnapshots.Capture(memory);
        NativeBattleSigh46State beforeState = NativeBattleStorageSnapshots.Capture(state);
        NativeBattleSigh46PacketStep first = NativeBattleSigh46PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), batch, cursor, 0);
        Assert.Equal(address + 76, first.State.ReadDword(cursor));
        Assert.Equal(0xE100002Au, BinaryPrimitives.ReadUInt32LittleEndian(first.Memory.Arena!.Read(68, 4)));
        Assert.Throws<NotSupportedException>(() => before.Arena!.Read(64, 3));
        Assert.Equal(3, first.Memory.Arena.Read(64, 3).Length);
        Assert.Equal("20DFAB8", Assert.Single(first.Ordering.Packets).Reference.Bank);
        NativeBattleSigh46PacketStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleSigh46PacketStep>(SnapshotJson(first))!);
        NativeBattleSigh46PacketStep Next(NativeBattleSigh46PacketStep step) => NativeBattleSigh46PacketDelivery.Deliver(step.Memory, step.State, step.Ordering, batch, cursor, 0);
        Assert.Equal(SnapshotJson(Next(first)), SnapshotJson(Next(restored)));
        Assert.Equal(address + 64, beforeState.ReadDword(cursor));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length + 4u)]
    public void Outside_TIM_cursor_fails_before_any_owner_is_published(uint address)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleSigh46State state = NativeBattleSigh46State.FromColdProcess().WriteDword(NativeBattleSigh46Profile.PrimaryCursorGlobal, address);
        Assert.Throws<InvalidDataException>(() => NativeBattleSigh46PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), new([], 0), NativeBattleSigh46Profile.PrimaryCursorGlobal, 0));
        Assert.Equal(address, state.ReadDword(NativeBattleSigh46Profile.PrimaryCursorGlobal));
        Assert.Throws<InvalidDataException>(() => NativeBattleSigh46PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), new([], 0), 0x264EC68, 0));
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
