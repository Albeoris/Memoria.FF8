using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonPacketBatchTests
{
    [Fact]
    public void Ordered_batch_preserves_overlapping_stores_untouched_bytes_and_prior_unknown_knowledge()
    {
        NativeBattlePrimitiveProcess original = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(8, [1, 2, 3, 4], Known(4));
        ImmutableArray<NativeBattleCommonPacketStore> stores =
        [
            new(9, [11, 22, 33], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Untouched]),
            new(10, [44, 55], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched]),
            new(100, [0, 7], Known(2)),
            new(101, [0], [NativeBattleGfPacketByteState.Unknown])
        ];
        NativeBattlePrimitiveProcess sequential = original;
        foreach (NativeBattleCommonPacketStore store in stores)
        {
            sequential = sequential.ApplyCommonWrite(store.Offset, store.Data, store.Mask);
        }
        NativeBattlePrimitiveProcess batch = original.ApplyCommonWrites(stores);
        batch.Validate();
        Assert.Equal(new byte[] { 1, 11, 44, 4 }, batch.ReadCommonPacket(8, 4));
        Assert.Equal(new byte[] { 0 }, batch.ReadCommonPacket(100, 1));
        Assert.Throws<NotSupportedException>(() => batch.ReadCommonPacket(12, 1));
        Assert.Throws<NotSupportedException>(() => batch.ReadCommonPacket(99, 1));
        Assert.Throws<NotSupportedException>(() => batch.ReadCommonPacket(101, 1));
        Assert.Equal(sequential.CommonPacketMemory!.Bank070Buffer0!.UnknownRegions, batch.CommonPacketMemory!.Bank070Buffer0!.UnknownRegions);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, original.ReadCommonPacket(8, 4));
    }

    [Fact]
    public void Empty_untouched_and_invalid_batches_never_invent_an_owner_or_modify_the_input()
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        Assert.Same(process, process.ApplyCommonWrites([]));
        Assert.Same(process, process.ApplyCommonWrites([new(0, [9], [NativeBattleGfPacketByteState.Untouched])]));
        Assert.Throws<InvalidDataException>(() => process.ApplyCommonWrites([new(0, [9], Known(1)), new(114688, [7], Known(1))]));
        Assert.Throws<InvalidDataException>(() => process.ApplyCommonWrites([new(0, [9], [])]));
        Assert.Throws<InvalidDataException>(() => process.ApplyCommonWrites([new(0, [9], [(NativeBattleGfPacketByteState)255])]));
        Assert.Null(process.CommonPacketMemory);
    }

    [Fact]
    public void A_thousand_packet_stores_allocate_within_one_physical_bank_budget_instead_of_cloning_each_growing_prefix()
    {
        ImmutableArray<NativeBattleCommonPacketStore> stores = Enumerable.Range(0, 1000).Select(index => new NativeBattleCommonPacketStore(index * 32, Enumerable.Repeat((byte)index, 32).ToImmutableArray(), Known(32))).ToImmutableArray();
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattlePrimitiveProcess result = source.ApplyCommonWrites(stores);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 1, 2 * 1024 * 1024);
        Assert.Equal(Enumerable.Repeat(unchecked((byte)999), 32), result.ReadCommonPacket(999 * 32, 32));
        Assert.Throws<NotSupportedException>(() => result.ReadCommonPacket(32000, 1));
        result.Validate();
    }

    private static ImmutableArray<NativeBattleGfPacketByteState> Known(int length) => Enumerable.Repeat(NativeBattleGfPacketByteState.Known, length).ToImmutableArray();
}
