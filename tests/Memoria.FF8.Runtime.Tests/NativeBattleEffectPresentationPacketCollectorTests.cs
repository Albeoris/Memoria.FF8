using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectPresentationPacketCollectorTests
{
    [Fact]
    public void Callback_collection_matches_ordered_recording_across_banks_buffers_rejections_and_duplicate_references()
    {
        NativeBattleEffectPresentationPackets.Collector collector = new();
        ImmutableArray<NativeBattlePrimitiveReference> expected = [];
        string bank = new("20DFAB8".ToCharArray());
        NativeBattleGfGeometryAttempt accepted = Attempt(8, true);
        NativeBattleGfGeometryAttempt rejected = Attempt(int.MaxValue, false);
        NativeBattleGfGeometryBatch first = new([accepted, rejected, accepted], 16);
        NativeBattleGfGeometryBatch second = new([Attempt(-8, true), Attempt(24, true)], 32);
        expected = NativeBattleEffectPresentationPackets.Record(expected, bank, 0, 128, first);
        collector.Record(bank, 0, 128, first);
        expected = NativeBattleEffectPresentationPackets.Record(expected, "off_B6D070", 1, 64, second);
        collector.Record("off_B6D070", 1, 64, second);
        ImmutableArray<NativeBattlePrimitiveReference> actual = collector.Capture();
        Assert.Equal(expected.ToArray(), actual.ToArray());
        Assert.Equal(new int[] { 136, 136, 56, 88 }, actual.Select(reference => reference.ByteOffset).ToArray());
        Assert.Same(bank, actual[0].Bank);
        Assert.Same(accepted, first.Attempts[0]);
        Assert.Same(accepted, first.Attempts[2]);
        Assert.True(accepted.Data.Equals(first.Attempts[0].Data));
        Assert.True(accepted.WriteMask.Equals(first.Attempts[0].WriteMask));
        Assert.Same(accepted.Primitive, first.Attempts[0].Primitive);
    }

    [Fact]
    public void Captured_receipts_remain_immutable_and_keep_reference_identity_when_the_local_collector_continues()
    {
        NativeBattleEffectPresentationPackets.Collector collector = new();
        collector.Record("20DFAB8", 0, 0, new([Attempt(0, true)], 8));
        ImmutableArray<NativeBattlePrimitiveReference> first = collector.Capture();
        NativeBattlePrimitiveReference held = Assert.Single(first);
        for (int index = 1; index < 1024; index++)
        {
            collector.Record("20DFAB8", (byte)(index & 1), index * 8, new([Attempt(0, true)], 8));
        }
        ImmutableArray<NativeBattlePrimitiveReference> later = collector.Capture();
        Assert.Single(first);
        Assert.Same(held, first[0]);
        Assert.Same(held, later[0]);
        Assert.Equal(1024, later.Length);
        NativeBattleEffectPresentationPackets.Collector independent = new();
        Assert.Empty(independent.Capture());
        Assert.Single(first);
    }

    [Fact]
    public void Rejected_attempts_keep_the_existing_no_offset_validation_and_no_array_publication_behavior()
    {
        NativeBattleEffectPresentationPackets.Collector collector = new();
        NativeBattlePrimitiveReference held = new("20DFAB8", 0, 32);
        ImmutableArray<NativeBattlePrimitiveReference> existing = [held];
        NativeBattleGfGeometryBatch rejected = new([Attempt(int.MaxValue, false)], 0);
        ImmutableArray<NativeBattlePrimitiveReference> unchanged = NativeBattleEffectPresentationPackets.Record(existing, "20DFAB8", 0, int.MaxValue, rejected);
        collector.Record("20DFAB8", 0, int.MaxValue, rejected);
        Assert.True(existing.Equals(unchanged));
        Assert.Same(held, unchanged[0]);
        Assert.Empty(collector.Capture());
        ImmutableArray<NativeBattlePrimitiveReference> missing = default;
        Assert.True(NativeBattleEffectPresentationPackets.Record(missing, "20DFAB8", 0, int.MaxValue, rejected).IsDefault);
    }

    [Fact]
    public void A_throwing_callback_cannot_replace_previously_published_receipts_with_its_local_prefix()
    {
        NativeBattlePrimitiveReference held = new("20DFAB8", 0, 16);
        ImmutableArray<NativeBattlePrimitiveReference> published = [held];
        NativeBattleGfGeometryBatch overflow = new([Attempt(0, true), Attempt(int.MaxValue, true)], 16);
        Assert.Throws<OverflowException>(() => NativeBattleEffectPresentationPackets.Record([], "20DFAB8", 0, 8, overflow));
        Assert.Throws<OverflowException>(() =>
        {
            NativeBattleEffectPresentationPackets.Collector callback = new();
            callback.Record("off_B6D070", 1, 0, new([Attempt(0, true)], 8));
            callback.Record("20DFAB8", 0, 8, overflow);
            published = callback.Capture();
        });
        Assert.Same(held, Assert.Single(published));
    }

    [Fact]
    public void Repeated_single_attempt_batches_allocate_linearly_in_the_callback_collector()
    {
        NativeBattleGfGeometryBatch batch = new([Attempt(0, true)], 8);
        Run(false, 16);
        Run(true, 16);
        long before = GC.GetAllocatedBytesForCurrentThread();
        ImmutableArray<NativeBattlePrimitiveReference> previous = Run(false, 1024);
        long oldBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        ImmutableArray<NativeBattlePrimitiveReference> current = Run(true, 1024);
        long newBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(previous.ToArray(), current.ToArray());
        Assert.True(newBytes * 8 < oldBytes, $"Collector allocated {newBytes} bytes; repeated immutable appends allocated {oldBytes} bytes.");

        ImmutableArray<NativeBattlePrimitiveReference> Run(bool collect, int count)
        {
            if (collect)
            {
                NativeBattleEffectPresentationPackets.Collector collector = new();
                for (int index = 0; index < count; index++)
                {
                    collector.Record("20DFAB8", 0, index * 8, batch);
                }
                return collector.Capture();
            }
            ImmutableArray<NativeBattlePrimitiveReference> packets = [];
            for (int index = 0; index < count; index++)
            {
                packets = NativeBattleEffectPresentationPackets.Record(packets, "20DFAB8", 0, index * 8, batch);
            }
            return packets;
        }
    }

    private static NativeBattleGfGeometryAttempt Attempt(int offset, bool accepted)
    {
        return new(offset, [0, 1, 2, 3, 4, 5, 6, 7], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known], accepted ? new(1, [0x01000000, 0xE1000000], []) : null);
    }
}
