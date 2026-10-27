using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrivateOrderingTests
{
    private const int RootOffset = 0x1000;
    private const uint RootAddress = 0x20DFAB8 + RootOffset;

    [Fact]
    public void Private_roots_share_actual_proxy_cells_and_physical_packet_links_without_changing_main_table()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleLogicalOrdering original = NativeBattleLogicalOrdering.Clear();
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.AppendPrivate(memory, 0x2000, original, Batch(2), RootAddress);
        Assert.Equal(original.Table, written.Ordering.Table);
        Assert.Equal(2, written.Ordering.ProxyCursor);
        Assert.Equal(0x1C48840u, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena!.Read(RootOffset + 4, 4)));
        Assert.Equal(RootAddress & 0xFFFFFF, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena.Read(0x2000, 4)) & 0xFFFFFF);
        Assert.Equal(0xC48828u, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena.Read(0x200C, 4)) & 0xFFFFFF);
        Assert.Equal((byte)2, written.Ordering.Proxies[0].NextHigh22);
        Assert.Equal((byte)1, written.Ordering.Proxies[1].NextHigh22);
        ImmutableArray<NativeBattleLogicalCommand> commands = Traverse(written.Memory, written.Ordering);
        Assert.Equal(new uint[] { 0xE6000001, 0xE6000000 }, commands.Select(command => Assert.Single(command.Words)));
        Assert.All(commands, command => Assert.Equal(1, command.OrderingBucket));
        Assert.Empty(written.Ordering.Traverse(Lengths(), (reference, count) => written.Memory.Arena.Read(reference.ByteOffset, count)));
        NativeBattleEffectMemory restoredMemory = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(written.Memory))!;
        NativeBattleLogicalOrdering restoredOrdering = JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(JsonSerializer.Serialize(written.Ordering))!;
        Assert.Equal(JsonSerializer.Serialize(commands), JsonSerializer.Serialize(Traverse(restoredMemory, restoredOrdering)));
    }

    [Fact]
    public void Full_shared_proxy_bank_still_writes_packet_but_never_reads_or_replaces_private_root()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { Arena = memory.Arena!.MarkUnknown(RootOffset + 4, 4) };
        NativeBattleLogicalOrdering ordering = NativeBattleOrderingTestFixture.Working(NativeBattleLogicalOrdering.Clear(), data => data with { ProxyCursor = NativeBattlePrimitiveOrdering.ProxyCapacity, Proxies = Enumerable.Repeat(new NativeBattleLogicalProxy(new("20DFAB8", 0, 0), 0, [], null), NativeBattlePrimitiveOrdering.ProxyCapacity).ToImmutableArray() });
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.AppendPrivate(memory, 0x2000, ordering, Batch(1), RootAddress);
        Assert.Equal(NativeBattlePrimitiveOrdering.ProxyCapacity, written.Ordering.ProxyCursor);
        Assert.Equal(ordering.Table, written.Ordering.Table);
        Assert.Equal((byte)1, written.Memory.Arena!.Read(0x2003, 1)[0]);
        Assert.Throws<NotSupportedException>(() => written.Memory.Arena.Read(RootOffset + 4, 4));
    }

    [Fact]
    public void Addressed_root_cycles_fail_without_guessing_a_global_table_index()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleLogicalOrdering.Clear().Traverse(Lengths(), initialOrderingAddress: RootAddress, readAddressedRoot: _ => new(RootAddress, 0)));
    }

    private static ImmutableArray<NativeBattleLogicalCommand> Traverse(NativeBattleEffectMemory memory, NativeBattleLogicalOrdering ordering) => ordering.Traverse(Lengths(), (reference, count) => memory.Arena!.Read(reference.ByteOffset, count), RootAddress + 4, address => new(BinaryPrimitives.ReadUInt32LittleEndian(memory.Arena!.Read(checked((int)(address - 0x20DFAB8)), 4)), checked((int)((address - RootAddress) / 4))));

    private static NativeBattleEffectMemory Memory()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(201);
        return memory with { Arena = memory.Arena!.Write(RootOffset, BitConverter.GetBytes(uint.MaxValue)).Write(RootOffset + 4, BitConverter.GetBytes(RootAddress)) };
    }

    private static ImmutableArray<byte> Lengths()
    {
        byte[] lengths = new byte[256];
        lengths[0xE6] = 1;
        return lengths.ToImmutableArray();
    }

    private static NativeBattleGfGeometryBatch Batch(int count)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int index = 0; index < count; index++)
        {
            ImmutableArray<uint> words = [0x01000000, 0xE6000000u + (uint)index, 0];
            byte[] bytes = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, words[0]);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), words[1]);
            ImmutableArray<NativeBattleGfPacketByteState> mask = [NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched];
            attempts.Add(new(index * 12, bytes.ToImmutableArray(), mask, new(1, words, [])));
        }
        return new(attempts.ToImmutable(), count * 12);
    }
}
