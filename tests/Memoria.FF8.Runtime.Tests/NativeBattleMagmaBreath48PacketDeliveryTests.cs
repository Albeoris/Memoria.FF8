using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMagmaBreath48PacketDeliveryTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Primary = 0x26481B8;

    [Fact]
    public void Flash_keeps_mixed_flags_native_order_partial_mode_tag_and_one_combined_cursor()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0x6000, 36) };
        NativeBattleMagmaBreath48State state = NativeBattleMagmaBreath48State.FromColdProcess().WriteDword(Primary, Base + 0x6000);
        NativeBattleMagmaBreath48PacketStep result = NativeBattleMagmaBreath48PacketDelivery.Submit(memory, state, NativeBattleLogicalOrdering.Clear(), Flash());
        Assert.Equal(Base + 0x6024, result.State.ReadDword(Primary));
        Assert.Equal(new[] { 0x6000, 0x6018 }, result.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.Equal(new ushort[] { 1, 0 }, result.Ordering.Proxies.Select(proxy => proxy.Flags20));
        Assert.Empty(result.Ordering.Proxies[0].VertexPayload4);
        Assert.Equal(new[] { 0, 0, 0, 0 }, result.Ordering.Proxies[1].VertexPayload4);
        Assert.Equal(0x2A000007u, U32(result.Memory.Arena!.Read(0x6004, 4)));
        Assert.Equal(0xE100002Au, U32(result.Memory.Arena.Read(0x601C, 4)));
        Assert.Equal(0u, U32(result.Memory.Arena.Read(0x6020, 4)));
        Assert.Throws<NotSupportedException>(() => memory.Arena!.Read(0x6000, 1));
    }

    [Fact]
    public void Full_proxy_bank_retains_mode_low_tag_knownness_and_native_allocation()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 36) };
        NativeBattleMagmaBreath48State state = NativeBattleMagmaBreath48State.FromColdProcess().WriteDword(Primary, Base);
        NativeBattleLogicalOrdering full = NativeBattleLogicalOrdering.Clear() with { ProxyCursor = NativeBattlePrimitiveOrdering.ProxyCapacity, Proxies = Enumerable.Repeat(new NativeBattleLogicalProxy(new("20DFAB8", 0, 512), 0, [0, 0, 0, 0], null), NativeBattlePrimitiveOrdering.ProxyCapacity).ToImmutableArray() };
        NativeBattleMagmaBreath48PacketStep result = NativeBattleMagmaBreath48PacketDelivery.Submit(memory, state, full, Flash());
        Assert.Equal(NativeBattlePrimitiveOrdering.ProxyCapacity, result.Ordering.Proxies.Length);
        Assert.Empty(result.Ordering.Packets);
        Assert.Equal(Base + 36, result.State.ReadDword(Primary));
        Assert.Equal(0x05000000u, U32(result.Memory.Arena!.Read(0, 4)));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(24, 3));
        Assert.Equal(new byte[] { 2 }, result.Memory.Arena.Read(27, 1));
    }

    [Fact]
    public void Independently_restored_packet_descriptions_and_state_continue_by_value()
    {
        NativeBattleModulePacketBatch batch = Flash();
        NativeBattleModulePacketBatch restoredBatch = JsonSerializer.Deserialize<NativeBattleModulePacketBatch>(JsonSerializer.Serialize(batch))!;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleMagmaBreath48State state = NativeBattleMagmaBreath48State.FromColdProcess().WriteDword(Primary, Base);
        NativeBattleMagmaBreath48PacketStep first = NativeBattleMagmaBreath48PacketDelivery.Submit(memory, state, NativeBattleLogicalOrdering.Clear(), batch);
        NativeBattleMagmaBreath48PacketStep restored = JsonSerializer.Deserialize<NativeBattleMagmaBreath48PacketStep>(JsonSerializer.Serialize(first))!;
        NativeBattleMagmaBreath48PacketStep Next(NativeBattleMagmaBreath48PacketStep step, NativeBattleModulePacketBatch description) => NativeBattleMagmaBreath48PacketDelivery.Submit(step.Memory, step.State, step.Ordering, description);
        Assert.Equal(JsonSerializer.Serialize(Next(first, batch)), JsonSerializer.Serialize(Next(restored, restoredBatch)));
    }

    [Theory]
    [InlineData(0x26481B8u)]
    [InlineData(0x26481D4u)]
    public void Geometry_updates_only_its_actual_cursor_and_TIM_owner(uint cursor)
    {
        NativeBattlePrimitiveProcess common = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = common };
        NativeBattleMagmaBreath48State state = NativeBattleMagmaBreath48State.FromColdProcess().WriteDword(cursor, Base + 64);
        NativeBattleGfGeometryAttempt attempt = Flash().CapturedWrites!.Attempts[1] with { ByteOffsetRelative = 0 };
        NativeBattleMagmaBreath48PacketStep result = NativeBattleMagmaBreath48PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), new([attempt], 12), cursor, 0);
        Assert.Equal(Base + 76, result.State.ReadDword(cursor));
        Assert.Same(common, result.Memory.PrimitiveProcess);
        Assert.Equal("20DFAB8", Assert.Single(result.Ordering.Packets).Reference.Bank);
        Assert.Equal(0xE100002Au, U32(result.Memory.Arena!.Read(68, 4)));
    }

    [Fact]
    public void Missing_stores_mismatched_flags_description_and_outside_cursors_fail_before_publication()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleMagmaBreath48State state = NativeBattleMagmaBreath48State.FromColdProcess().WriteDword(Primary, Base);
        NativeBattleModulePacketBatch batch = Flash();
        Assert.Throws<NotSupportedException>(() => NativeBattleMagmaBreath48PacketDelivery.Submit(memory, state, NativeBattleLogicalOrdering.Clear(), batch with { CapturedWrites = null }));
        NativeBattleAllocatedPrimitive mismatch = batch.Primitives[0] with { Primitive = batch.Primitives[0].Primitive with { Bucket = 9 } };
        Assert.Throws<InvalidDataException>(() => NativeBattleMagmaBreath48PacketDelivery.Submit(memory, state, NativeBattleLogicalOrdering.Clear(), batch with { Primitives = batch.Primitives.SetItem(0, mismatch) }));
        Assert.Throws<InvalidDataException>(() => NativeBattleMagmaBreath48PacketDelivery.Submit(memory, state.WriteDword(Primary, Base + NativeBattleEffectArena.Length - 32), NativeBattleLogicalOrdering.Clear(), batch));
        Assert.Throws<InvalidDataException>(() => NativeBattleMagmaBreath48PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), new([], 0), 0x1D8E054, 0));
        Assert.Equal(new byte[36], memory.Arena!.Read(0, 36));
        Assert.Equal(Base, state.ReadDword(Primary));
    }

    private static NativeBattleModulePacketBatch Flash()
    {
        NativeBattleGeometryPrimitive quad = new(7, [0x05000000, 0x2A000007, 0, 320, 0xD80000, 0xD80140], []);
        NativeBattleGeometryPrimitive mode = new(7, [0x02000000, 0xE100002A, 0], [0, 0, 0, 0]);
        NativeBattleGfGeometryAttempt first = Attempt(quad, 0);
        NativeBattleGfGeometryAttempt second = Attempt(mode, 24);
        second = second with { WriteMask = second.WriteMask.SetItem(0, NativeBattleGfPacketByteState.Untouched).SetItem(1, NativeBattleGfPacketByteState.Untouched).SetItem(2, NativeBattleGfPacketByteState.Untouched) };
        return new([new(quad, 0, 1), new(mode, 24, 0)], 36) { CapturedWrites = new([first, second], 36) };
    }

    private static NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive primitive, int offset)
    {
        byte[] bytes = new byte[primitive.Words.Length * 4];
        for (int index = 0; index < primitive.Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
        }
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive);
    }

    private static uint U32(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
}
