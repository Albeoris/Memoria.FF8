using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonPacketBatchTests
{
    [Fact]
    public void Linked_primitive_stores_retain_bytes_and_masks_without_allocating_constant_headers_per_packet()
    {
        NativeBattleGeometryPrimitive primitive = new(0, [0x01020304u], []) { PhysicalWriteMask = Known(4) };
        ImmutableArray<NativeBattleCommonPacketStore> actual = NativeBattleCommonPacketArena.PrimitiveStores(16, primitive, true);
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, actual[0].Data);
        Assert.Equal(new byte[] { 0, 0, 0 }, actual[1].Data);
        Assert.Equal(Enumerable.Repeat(NativeBattleGfPacketByteState.Unknown, 3), actual[1].Mask);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 10000; index++)
        {
            ImmutableArray<NativeBattleCommonPacketStore> stores = NativeBattleCommonPacketArena.PrimitiveStores(index * 4, primitive, true);
            GC.KeepAlive(stores);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated <= 2400000, $"Linked packet stores allocated {allocated} bytes for 10000 one-word packets.");
    }

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
    public void Sparse_writes_preserve_unknown_prefix_suffix_and_stored_bytes_under_unknown_spans()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(400, [7, 8, 9, 10], Known(4));
        ImmutableArray<NativeBattleCommonPacketStore> stores =
        [
            new(401, [0], [NativeBattleGfPacketByteState.Unknown]),
            new(114687, [23], Known(1)),
            new(0, [17], Known(1)),
            new(402, [18, 19], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched])
        ];
        NativeBattlePrimitiveProcess scalar = source;
        foreach (NativeBattleCommonPacketStore store in stores)
        {
            scalar = scalar.ApplyCommonWrite(store.Offset, store.Data, store.Mask);
        }
        NativeBattlePrimitiveProcess result = source.ApplyCommonWrites(stores);
        result.Validate();
        Assert.Equal(scalar.CommonPacketMemory!.Bank070Buffer0!.UnknownRegions, result.CommonPacketMemory!.Bank070Buffer0!.UnknownRegions);
        Assert.Equal(new byte[] { 7, 8, 18, 10 }, result.CommonPacketMemory.Bank070Buffer0.Regions.Single().Data.AsSpan().Slice(400, 4).ToArray());
        Assert.Equal(new byte[] { 17 }, result.ReadCommonPacket(0, 1));
        Assert.Equal(new byte[] { 23 }, result.ReadCommonPacket(114687, 1));
        Assert.Throws<NotSupportedException>(() => result.ReadCommonPacket(401, 1));
    }

    [Fact]
    public void Unknown_only_batch_preserves_an_unresolved_bank_without_inventing_stored_bytes()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattlePrimitiveProcess result = source.ApplyCommonWrites([new(70000, [99, 98], [NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Unknown])]);
        result.Validate();
        NativeBattleEffectArena arena = result.CommonPacketMemory!.Bank070Buffer0!;
        Assert.Empty(arena.Regions);
        Assert.Equal(new NativeBattleEffectUnknownRegion(0, 114688), Assert.Single(arena.UnknownRegions));
        Assert.Throws<NotSupportedException>(() => result.ReadCommonPacket(70000, 1));
        Assert.Null(source.CommonPacketMemory);
    }

    [Fact]
    public void Mixed_ordered_batches_match_scalar_byte_knowledge_and_storage()
    {
        Random random = new(2718);
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        for (int iteration = 0; iteration < 30; iteration++)
        {
            ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
            for (int store = 0; store < 8; store++)
            {
                byte[] data = new byte[random.Next(1, 17)];
                random.NextBytes(data);
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, data.Length).Select(_ => (NativeBattleGfPacketByteState)random.Next(3)).ToImmutableArray();
                stores.Add(new(random.Next(0, 512 - data.Length), data.ToImmutableArray(), mask));
            }
            NativeBattlePrimitiveProcess scalar = source;
            foreach (NativeBattleCommonPacketStore store in stores)
            {
                scalar = scalar.ApplyCommonWrite(store.Offset, store.Data, store.Mask);
            }
            NativeBattlePrimitiveProcess result = source.ApplyCommonWrites(stores.ToImmutable());
            result.Validate();
            NativeBattleEffectArena expected = scalar.CommonPacketMemory!.Bank070Buffer0!;
            NativeBattleEffectArena actual = result.CommonPacketMemory!.Bank070Buffer0!;
            Assert.Equal(expected.UnknownRegions, actual.UnknownRegions);
            byte[] expectedBytes = new byte[512];
            byte[] actualBytes = new byte[512];
            foreach (NativeBattleEffectMemoryRegion region in expected.Regions)
            {
                region.Data.AsSpan().CopyTo(expectedBytes.AsSpan(region.Offset));
            }
            foreach (NativeBattleEffectMemoryRegion region in actual.Regions)
            {
                region.Data.AsSpan().CopyTo(actualBytes.AsSpan(region.Offset));
            }
            Assert.Equal(expectedBytes, actualBytes);
            source = result;
        }
    }

    [Fact]
    public void Small_writes_allocate_for_the_affected_span_instead_of_the_physical_bank()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        ImmutableArray<NativeBattleCommonPacketStore> stores = [new(100000, [1, 2, 3, 4], Known(4))];
        source.ApplyCommonWrites(stores);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattlePrimitiveProcess result = source.ApplyCommonWrites(stores);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 1, 16 * 1024);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.ReadCommonPacket(100000, 4));
        Assert.Throws<NotSupportedException>(() => result.ReadCommonPacket(99999, 1));
        Assert.Throws<NotSupportedException>(() => result.ReadCommonPacket(100004, 1));
        result.Validate();
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
