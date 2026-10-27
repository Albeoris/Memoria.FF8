using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingOwnedStorageTests
{
    private const uint Address = NativeBattleFileStaging.Address;

    [Fact]
    public void Loader_zero_reads_and_bulk_unknown_marks_do_not_materialize_sparse_pages()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        object owner = staging.OwnerIdentity;
        Assert.Equal(0u, staging.ReadUInt32(Address));
        Assert.Equal(0, staging.AllocatedPageCount);
        using NativeBattleFileStagingTransaction transaction = new(staging);
        Assert.Equal(0u, transaction.View.ReadUInt32(Address + 4));
        transaction.View.MarkUnknown(Address, 1024 * 1024);
        Assert.Throws<NotSupportedException>(() => transaction.View.ReadByte(Address));
        Assert.Equal(0u, staging.ReadUInt32(Address));
        transaction.Freeze(transaction.View);
        Assert.Throws<NotSupportedException>(() => staging.ReadByte(Address));
        Assert.Equal(0, staging.AllocatedPageCount);
        Assert.Same(owner, staging.OwnerIdentity);
        transaction.View.WriteUInt32(Address + 8, 0x12345678);
        Assert.Equal(0, staging.AllocatedPageCount);
        transaction.Freeze(transaction.View);
        Assert.Equal(0x12345678u, staging.ReadUInt32(Address + 8));
        Assert.Equal(1, staging.AllocatedPageCount);
    }

    [Fact]
    public void Chunk_knownness_preflight_combines_explicit_stores_with_trimmed_loader_zero_provenance()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().WriteUInt32(Address + 8, 0x12345678);
        byte[] data = staging.Read(Address, 64);
        Assert.Equal(0x78, data[8]);
        staging.MarkUnknown(Address + 12, 1);
        Assert.Throws<NotSupportedException>(() => staging.Read(Address, 64));
        Assert.Equal(0x12345678u, staging.ReadUInt32(Address + 8));
        Assert.Equal(new byte[51], staging.Read(Address + 13, 51));
        Assert.False(staging.ReadSnapshot(Address + 12, 1).Known[0]);
    }

    [Fact]
    public void Snapshots_keep_hidden_unknown_bytes_and_independent_restores_with_the_old_shape_extended_only_when_needed()
    {
        NativeBattleFileStaging live = NativeBattleFileStaging.Unknown().Write(Address, [1, 2, 3, 4]);
        live.MarkUnknown(Address + 1, 2);
        NativeBattleFileStaging first = Capture(live);
        Assert.Throws<NotSupportedException>(() => live.ReadResourcePointer(Address));
        string json = JsonSerializer.Serialize(first);
        Assert.Contains("RetainedUnknownRanges", json);
        NativeBattleFileStaging frozen = JsonSerializer.Deserialize<NativeBattleFileStaging>(json)!;
        Assert.Throws<InvalidOperationException>(() => frozen.WriteByte(Address, 9));
        NativeBattleFileStaging a = NativeBattleStorageSnapshots.Restore(frozen);
        NativeBattleFileStaging b = NativeBattleStorageSnapshots.Restore(frozen);
        a.WriteByte(Address, 8);
        live.WriteByte(Address, 9);
        Assert.Equal((byte)1, b.ReadByte(Address));
        var retained = b.ReadSnapshot(Address, 4);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, retained.Data);
        Assert.Equal(new[] { true, false, false, true }, retained.Known);
        Assert.Equal(json, JsonSerializer.Serialize(first));
        Assert.NotSame(a.OwnerIdentity, b.OwnerIdentity);
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(live));

        NativeBattleFileStaging old = JsonSerializer.Deserialize<NativeBattleFileStaging>("{\"KnownRanges\":[{\"Offset\":8,\"Data\":[2,3]}]}")!;
        Assert.Equal(new byte[] { 2, 3 }, old.Read(Address + 8, 2));
        Assert.DoesNotContain("RetainedUnknownRanges", JsonSerializer.Serialize(old));
        Assert.Throws<InvalidOperationException>(() => live.KnownRanges);
        Assert.Throws<InvalidOperationException>(() => live.RetainedUnknownRanges);
    }

    [Fact]
    public void Repeated_publications_keep_one_working_authority_and_explicit_snapshot_retains_the_accepted_prefix()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().WriteUInt32(Address, 1);
        object identity = source.OwnerIdentity;
        using NativeBattleFileStagingTransaction transaction = new(source);
        NativeBattleFileStaging view = transaction.View.WriteUInt32(Address, 2);
        Assert.Equal(1u, source.ReadUInt32(Address));
        Assert.Equal(2u, view.ReadUInt32(Address));
        NativeBattleFileStaging accepted = transaction.Freeze(view);
        NativeBattleFileStaging snapshot = Capture(accepted);
        view.WriteUInt32(Address, 3);
        Assert.Equal(2u, source.ReadUInt32(Address));
        Assert.Same(source, transaction.Freeze(view));
        Assert.Equal(3u, accepted.ReadUInt32(Address));
        Assert.Equal(2u, snapshot.ReadUInt32(Address));
        view.WriteUInt32(Address, 4);
        transaction.Dispose();
        Assert.Equal(3u, source.ReadUInt32(Address));
        Assert.Same(identity, source.OwnerIdentity);
        Assert.Throws<ObjectDisposedException>(() => view.ReadByte(Address));
    }

    [Fact]
    public void Private_checkpoint_and_disposal_preserve_allocation_cursor_origin_zero_provenance_and_pointer_metadata()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.FromColdProcess().WriteUInt32(NativeBattleTemporaryMemory.Cursor, NativeBattleTemporaryMemory.Address);
        source.WriteResourcePointer(Address + 32, new(null, 0x12345678));
        string before = JsonSerializer.Serialize(Capture(source));
        using NativeBattleFileStagingTransaction transaction = new(source);
        NativeBattleFileStaging view = transaction.View;
        transaction.BeginCheckpoint();
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(view, 44);
        allocation.Staging.WriteByte(Address + 33, 0x56);
        allocation.Staging.MarkUnknown(Address + 64, 4);
        allocation.Staging.WriteByte(source.BaseAddress - 1, 10);
        Assert.Equal(NativeBattleTemporaryMemory.Address + 44u, view.ReadUInt32(NativeBattleTemporaryMemory.Cursor));
        Assert.Equal(NativeBattleTemporaryMemory.Address, source.ReadUInt32(NativeBattleTemporaryMemory.Cursor));
        Assert.Throws<InvalidOperationException>(() => Capture(view));
        Assert.Throws<InvalidOperationException>(() => transaction.Freeze(view));
        transaction.RollbackCheckpoint();
        Assert.Equal(NativeBattleTemporaryMemory.Address, view.ReadUInt32(NativeBattleTemporaryMemory.Cursor));
        Assert.Equal(source.BaseAddress, view.BaseAddress);
        Assert.Equal(0x12345678u, view.ReadResourcePointer(Address + 32)!.StagingAddress);
        Assert.Equal(0u, view.ReadUInt32(Address + 64));
        Assert.Equal(before, JsonSerializer.Serialize(Capture(source)));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    public void Overlapping_raw_copy_preserves_unknown_backing_values_and_invalidates_only_destination_pointer_provenance(int first, int target)
    {
        NativeBattleFileStaging live = NativeBattleFileStaging.Unknown().Write(Address, Enumerable.Range(1, 16).Select(value => (byte)value).ToArray());
        live.MarkUnknown(Address + 3, 2);
        live.WriteResourcePointer(Address + 12, new(null, 0x12345678));
        var expected = live.ReadSnapshot(Address, 16);
        expected.Data.AsSpan(first, 12).CopyTo(expected.Data.AsSpan(target));
        expected.Known.AsSpan(first, 12).CopyTo(expected.Known.AsSpan(target));
        live.CopyMemory(Address + (uint)first, Address + (uint)target, 12);
        var actual = live.ReadSnapshot(Address, 16);
        Assert.Equal(expected.Data, actual.Data);
        Assert.Equal(expected.Known, actual.Known);
        if (target == 0)
        {
            Assert.Equal(0x12345678u, live.ReadResourcePointer(Address + 12)!.StagingAddress);
        }
        else
        {
            Assert.Equal(new NativeBattleResourcePointer(null, live.ReadUInt32(Address + 12)), live.ReadResourcePointer(Address + 12));
        }
        NativeBattleFileStaging restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(Capture(live)))!);
        Assert.Equal(actual.Data, restored.ReadSnapshot(Address, 16).Data);
        Assert.Equal(actual.Known, restored.ReadSnapshot(Address, 16).Known);
    }

    [Fact]
    public void Failed_delta_publication_rolls_back_earlier_page_stores_and_all_metadata()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.FromColdProcess().WriteUInt32(Address, 9);
        source.WriteResourcePointer(Address + 16, new(null, 123));
        string original = JsonSerializer.Serialize(Capture(source));
        using NativeBattleFileStagingTransaction transaction = new(source);
        transaction.View.Write(Address + 4060, Enumerable.Repeat((byte)7, 64).ToArray());
        transaction.View.MarkUnknown(Address + 16, 4);
        FailOnceSink sink = new(source);
        using (NativeBattleTelemetry.BeginScope(sink))
        {
            Assert.Throws<InvalidOperationException>(() => transaction.Freeze(transaction.View));
        }
        Assert.True(sink.ObservedBothPages);
        Assert.Equal(original, JsonSerializer.Serialize(Capture(source)));
        Assert.Equal(123u, source.ReadResourcePointer(Address + 16)!.StagingAddress);
        transaction.Freeze(transaction.View);
        Assert.Equal((byte)7, source.ReadByte(Address + 4060));
        Assert.Throws<NotSupportedException>(() => source.ReadUInt32(Address + 16));
    }

    [Fact]
    public void Warmed_scalar_stores_across_callbacks_keep_pages_and_report_only_written_payload()
    {
        NativeBattleFileStaging memory = NativeBattleFileStaging.Unknown().Write(Address, new byte[8192]);
        for (int index = 0; index < 1000; index++)
        {
            memory.WriteUInt32(Address + (uint)(index * 4), (uint)index);
        }
        object identity = memory.OwnerIdentity;
        int pages = memory.AllocatedPageCount;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int callback = 0; callback < 10; callback++)
        {
            for (int index = 0; index < 100; index++)
            {
                memory.WriteUInt32(Address + (uint)((callback * 100 + index) * 4), (uint)(index + 1000));
            }
        }
        long delta = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(0, delta);
        Assert.Same(identity, memory.OwnerIdentity);
        Assert.Equal(pages, memory.AllocatedPageCount);
        PayloadSink sink = new();
        using (NativeBattleTelemetry.BeginScope(sink))
        {
            for (int index = 0; index < 1000; index++)
            {
                memory.WriteUInt32(Address + (uint)(index * 4), (uint)index);
            }
        }
        Assert.Equal(4000, sink.Copies);
        Assert.Equal(4000, sink.Fills);
        Assert.Equal(0, sink.Growth);
    }

    [Fact]
    public void Invalid_pointer_and_physical_ranges_fail_before_mutating_or_rebasing_storage()
    {
        NativeBattleFileStaging memory = NativeBattleFileStaging.Unknown().WriteUInt32(Address, 7);
        string prior = JsonSerializer.Serialize(Capture(memory));
        Assert.Throws<InvalidDataException>(() => memory.WriteResourcePointer(Address - 4, new(null, 1, -1)));
        Assert.Throws<InvalidDataException>(() => memory.Write(uint.MaxValue, [1, 2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.MarkUnknown(uint.MaxValue, 2));
        Assert.Throws<ArgumentException>(() => memory.ReadWithKnownness(Address, new byte[2], new bool[1]));
        Assert.Equal(prior, JsonSerializer.Serialize(Capture(memory)));
        var top = memory.ReadSnapshot(uint.MaxValue, 1);
        Assert.Equal((byte)0, top.Data[0]);
        Assert.False(top.Known[0]);
    }

    [Fact]
    public void Raw_mixed_store_preserves_hidden_payload_and_known_extent_without_a_live_snapshot_export()
    {
        NativeBattleFileStaging memory = NativeBattleFileStaging.FromColdProcess();
        Assert.Equal((ulong)memory.BaseAddress, memory.KnownExtentEnd);
        memory.WriteWithKnownness(Address, [5, 6, 7, 8], [true, false, false, true]);
        var actual = memory.ReadSnapshot(Address, 4);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, actual.Data);
        Assert.Equal(new[] { true, false, false, true }, actual.Known);
        Assert.Equal((ulong)Address + 4, memory.KnownExtentEnd);
        memory.MarkUnknown(Address + 3, 1);
        Assert.Equal((ulong)Address + 1, memory.KnownExtentEnd);
        memory.MarkUnknown(Address, 1);
        Assert.Equal((ulong)memory.BaseAddress, memory.KnownExtentEnd);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, memory.ReadSnapshot(Address, 4).Data);
        NativeBattleFileStaging snapshot = Capture(memory);
        Assert.Equal((ulong)memory.BaseAddress, snapshot.KnownExtentEnd);
    }

    [Fact]
    public void Reused_overlay_resets_only_changed_entries_after_a_larger_private_operation()
    {
        NativeBattleFileStaging memory = NativeBattleFileStaging.Unknown();
        using (NativeBattleFileStagingTransaction large = new(memory))
        {
            large.View.Write(Address, Enumerable.Repeat((byte)1, 8192).ToArray());
            large.Freeze(large.View);
        }
        OverlayTraffic sink = new();
        using (NativeBattleTelemetry.BeginScope(sink))
        {
            for (int index = 0; index < 10; index++)
            {
                using NativeBattleFileStagingTransaction small = new(memory);
                small.View.WriteUInt32(Address, (uint)index);
                small.Freeze(small.View);
            }
        }
        Assert.Equal(40, sink.Copies);
        Assert.Equal(0, sink.Clears);
    }

    private static NativeBattleFileStaging Capture(NativeBattleFileStaging memory) => NativeBattleStorageSnapshots.Capture(memory);

    private sealed class PayloadSink : INativeBattleTelemetrySink
    {
        public long Copies;
        public long Fills;
        public long Growth;
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1005 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                Copies += payloadBytes;
            }
            else if (siteId == 1005 && kind == NativeBattleTrafficKind.Fill)
            {
                Fills += payloadBytes;
            }
            else if (siteId is 1012 or 1013)
            {
                Growth += payloadBytes;
            }
        }
    }

    private sealed class FailOnceSink(NativeBattleFileStaging source) : INativeBattleTelemetrySink
    {
        private bool failed;
        private int secondPageCopies;
        public bool ObservedBothPages { get; private set; }
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (!failed && siteId == 1005 && kind == NativeBattleTrafficKind.CpuCopyPayload && payloadBytes == 104 && ++secondPageCopies == 2)
            {
                //The52-lane mixed store reports104 bytes for its journal and then its actual byte/knownness copies.
                ObservedBothPages = source.ReadByte(Address + 4060) == 7 && source.ReadByte(Address + 4123) == 7;
                failed = true;
                throw new InvalidOperationException("Injected publication failure.");
            }
        }
    }

    private sealed class OverlayTraffic : INativeBattleTelemetrySink
    {
        public long Copies;
        public long Clears;
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1035 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                Copies += payloadBytes;
            }
            else if (siteId == 1035 && kind == NativeBattleTrafficKind.ZeroFill)
            {
                Clears += payloadBytes;
            }
        }
    }
}
