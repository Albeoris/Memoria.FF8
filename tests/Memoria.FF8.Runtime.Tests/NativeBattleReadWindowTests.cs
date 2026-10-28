namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReadWindowTests
{
    [Fact]
    public void UncapturedKnownnessReadUsesOneBulkSourceCallAndEmptyReadsDoNotTouchTheSource()
    {
        using NativeBattleWorkingMemory memory = new(32, initialKnownZero: false);
        memory.WriteRetainedBytes(4, [1, 2, 3, 4]);
        BulkSource source = new(memory);
        using NativeBattleReadWindow window = memory.OpenReadWindow(source, 4, 8, 8);
        byte[] bytes = new byte[8];
        bool[] known = new bool[8];
        window.ReadWithKnownness(0, bytes, known);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 }, bytes);
        Assert.All(known, item => Assert.False(item));
        Assert.Equal(new[] { (4, 8) }, source.BulkReads);
        Assert.Equal(0, source.ByteReads);
        window.ReadWithKnownness(8, Span<byte>.Empty, Span<bool>.Empty);
        Assert.Single(source.BulkReads);
    }

    [Fact]
    public void MixedCapturedAndLiveRunsNeverReadChangedBackingForCapturedLanes()
    {
        using NativeBattleWorkingMemory memory = new(16, initialKnownZero: false);
        memory.WriteWithKnownness(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new bool[] { true, false, true, false, true, false, true, false });
        BulkSource source = new(memory);
        using NativeBattleReadWindow window = memory.OpenReadWindow(source, 0, 8, 2);
        memory.Write(2, [90, 91]);
        source.ForbiddenFirst = 2;
        source.ForbiddenEnd = 4;
        source.BulkReads.Clear();
        source.ByteReads = 0;
        byte[] bytes = new byte[8];
        bool[] known = new bool[8];
        window.ReadWithKnownness(0, bytes, known);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, bytes);
        Assert.Equal(new bool[] { true, false, true, false, true, false, true, false }, known);
        Assert.Equal(new[] { (0, 2), (4, 4) }, source.BulkReads);
        Assert.Equal(0, source.ByteReads);
        Assert.Equal(2, window.RetainedByteCount);
        Assert.Throws<InvalidOperationException>(() => memory.WriteByte(4, 99));
        Assert.Equal(5, memory.ReadByte(4));
    }

    [Fact]
    public void ScalarFallbackKeepsAcceptedPrefixAndReadIntoUnknownFailureKeepsDestinationUnchanged()
    {
        using NativeBattleWorkingMemory memory = new(16, initialKnownZero: false);
        memory.WriteWithKnownness(0, new byte[] { 1, 2, 3, 4 }, new bool[] { true, false, true, true });
        ScalarSource source = new(memory) { ForbiddenFirst = 2, ForbiddenEnd = 3 };
        using NativeBattleReadWindow window = memory.OpenReadWindow(source, 0, 4, 4);
        byte[] bytes = [99, 99, 99, 99];
        bool[] known = [true, true, true, true];
        Assert.Throws<InvalidOperationException>(() => window.ReadWithKnownness(0, bytes, known));
        Assert.Equal(new byte[] { 1, 2, 99, 99 }, bytes);
        Assert.Equal(new bool[] { true, false, true, true }, known);
        bytes = [99, 99, 99, 99];
        Assert.Throws<NotSupportedException>(() => window.ReadInto(0, bytes));
        Assert.Equal(new byte[] { 99, 99, 99, 99 }, bytes);
        Assert.Throws<InvalidDataException>(() => window.ReadWithKnownness(5, bytes, new bool[1]));
        Assert.Throws<ArgumentException>(() => window.ReadWithKnownness(0, bytes, new bool[1]));
        window.Dispose();
        Assert.Throws<ObjectDisposedException>(() => window.ReadWithKnownness(5, bytes, new bool[1]));
    }

    private class ScalarSource(NativeBattleWorkingMemory memory) : INativeBattleReadWindowSource
    {
        protected NativeBattleWorkingMemory Memory { get; } = memory;
        public int ByteReads;
        public int ForbiddenFirst = -1;
        public int ForbiddenEnd = -1;
        public long ReadWindowGeneration => 0;
        public void ValidateReadWindow(long generation) => ((INativeBattleReadWindowSource)Memory).ValidateReadWindow(generation);
        public void SeedReadWindow(NativeBattleReadWindowLease lease) => ((INativeBattleReadWindowSource)Memory).SeedReadWindow(lease);
        public bool ObservesLayer(INativeBattleReadWindowSource layer) => ReferenceEquals(this, layer) || ReferenceEquals(Memory, layer);

        protected void RequireReadable(int offset, int length)
        {
            if (offset < ForbiddenEnd && offset + length > ForbiddenFirst)
            {
                throw new InvalidOperationException("Captured backing must not be read.");
            }
        }

        public void ReadWindowByte(int absoluteOffset, out byte value, out bool known)
        {
            ByteReads++;
            RequireReadable(absoluteOffset, 1);
            ((INativeBattleReadWindowSource)Memory).ReadWindowByte(absoluteOffset, out value, out known);
        }
    }

    private sealed class BulkSource(NativeBattleWorkingMemory memory) : ScalarSource(memory), INativeBattleReadWindowSource
    {
        public List<(int Offset, int Length)> BulkReads { get; } = [];

        void INativeBattleReadWindowSource.ReadWindowWithKnownness(int absoluteOffset, Span<byte> destination, Span<bool> known)
        {
            RequireReadable(absoluteOffset, destination.Length);
            BulkReads.Add((absoluteOffset, destination.Length));
            Memory.ReadWithKnownness(absoluteOffset, destination, known);
        }
    }

    [Fact]
    public void Opening_copies_no_source_and_emit_like_writes_preserve_later_stream_reads()
    {
        using NativeBattleWorkingMemory memory = new(80 * 1024);
        memory.WriteUInt32(0, 7);
        memory.WriteUInt32(40000, 0x12345678);
        using NativeBattleReadWindow source = memory.OpenReadWindow(0, memory.LogicalLength);
        Assert.Equal(0, source.RetainedByteCount);
        Assert.Equal(0, memory.ReadWindowCapturedByteCount);
        Assert.Equal(7u, source.ReadUInt32(0));
        memory.WriteUInt32(40000, 0x87654321);
        Assert.Equal(0x12345678u, source.ReadUInt32(40000));
        Assert.Equal(0x87654321u, memory.ReadUInt32(40000));
        Assert.Equal(4, source.RetainedByteCount);
        Assert.Equal(4, memory.ReadWindowCapturedByteCount);
    }

    [Fact]
    public void Nested_windows_keep_independent_original_hidden_bytes_and_knownness()
    {
        using NativeBattleWorkingMemory memory = new(64, initialKnownZero: false);
        memory.WriteRetainedBytes(8, [0xA5, 0xB6, 0xC7, 0xD8]);
        using NativeBattleReadWindow first = memory.OpenReadWindow(8, 8);
        memory.Write(8, [1, 2, 3, 4]);
        using NativeBattleReadWindow second = memory.OpenReadWindow(8, 8);
        memory.SetResourcePointer(8, new(null, 0x12345678));
        byte[] original = new byte[4];
        bool[] known = new bool[4];
        first.ReadWithKnownness(0, original, known);
        Assert.Equal(new byte[] { 0xA5, 0xB6, 0xC7, 0xD8 }, original);
        Assert.All(known, value => Assert.False(value));
        Assert.Throws<NotSupportedException>(() => first.ReadUInt32(0));
        Assert.Equal(0x04030201u, second.ReadUInt32(0));
        memory.MarkUnknown(8, 4);
        Assert.Equal(0x04030201u, second.ReadUInt32(0));
        second.Dispose();
        first.Dispose();
        Assert.False(memory.TryGetResourcePointer(8, out _));
        Assert.False(memory.IsKnown(8, 4));
        Assert.Throws<ObjectDisposedException>(() => first.ReadByte(0));
    }

    [Fact]
    public void Existing_undo_lanes_are_seeded_at_open_and_rollback_never_needs_observation_growth()
    {
        using NativeBattleWorkingMemory memory = new(32);
        memory.Write(4, [1, 2, 3, 4]);
        memory.BeginCheckpoint();
        memory.Write(4, [5, 6, 7, 8]);
        using NativeBattleReadWindow source = memory.OpenReadWindow(4, 8);
        Assert.Equal(4, source.RetainedByteCount);
        memory.WriteUInt32(8, 0x12345678);
        long copied = memory.ReadWindowCapturedByteCount;
        memory.RollbackCheckpoint();
        Assert.Equal(copied, memory.ReadWindowCapturedByteCount);
        Assert.Equal(0x08070605u, source.ReadUInt32(0));
        Assert.Equal(0u, source.ReadUInt32(4));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, memory.BorrowRawReadOnly(4, 4).ToArray());
        Assert.Equal(0u, memory.ReadUInt32(8));
    }

    [Fact]
    public void Open_budget_failure_preserves_parent_journal_and_future_budget_failure_precedes_stores()
    {
        using NativeBattleWorkingMemory memory = new(32);
        memory.Write(4, [1, 2, 3, 4]);
        memory.BeginCheckpoint();
        memory.Write(4, [5, 6, 7, 8]);
        int journal = memory.JournalEntryCount;
        Assert.Throws<InvalidOperationException>(() => memory.OpenReadWindow(4, 4, maximumRetainedBytes: 3));
        Assert.Equal(journal, memory.JournalEntryCount);
        memory.RollbackCheckpoint();
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, memory.BorrowRawReadOnly(4, 4).ToArray());
        using NativeBattleReadWindow source = memory.OpenReadWindow(0, 16, maximumRetainedBytes: 1);
        Assert.Throws<InvalidOperationException>(() => memory.Write(0, [9, 8]));
        Assert.Equal(0, source.RetainedByteCount);
        Assert.Equal(0u, memory.ReadUInt32(0));
    }

    [Fact]
    public void Multiple_windows_share_an_explicit_aggregate_budget_before_any_write()
    {
        using NativeBattleWorkingMemory memory = new(16, maxJournalEntries: 3);
        using NativeBattleReadWindow first = memory.OpenReadWindow(0, 4);
        using NativeBattleReadWindow second = memory.OpenReadWindow(0, 4);
        Assert.Throws<InvalidOperationException>(() => memory.Write(0, [1, 2]));
        Assert.Equal(0, first.RetainedByteCount);
        Assert.Equal(0, second.RetainedByteCount);
        Assert.Equal(0u, memory.ReadUInt32(0));
        memory.WriteByte(0, 7);
        Assert.Equal(0, first.ReadByte(0));
        Assert.Equal(0, second.ReadByte(0));
        Assert.Equal(7, memory.ReadByte(0));
    }

    [Fact]
    public void Logical_overlay_windows_observe_staged_values_and_physical_writes_without_changing_base_reads()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(32, [1, 2, 3, 4]);
        using NativeBattleEffectArenaTransaction transaction = new(arena);
        transaction.View.WriteByte(32, 5);
        using NativeBattleReadWindow committed = arena.OpenReadWindow(32, 8);
        using NativeBattleReadWindow staged = transaction.View.OpenReadWindow(32, 8);
        Assert.Equal(1, committed.ReadByte(0));
        Assert.Equal(5, staged.ReadByte(0));
        transaction.BeginCheckpoint();
        transaction.View.WriteByte(32, 9);
        using NativeBattleReadWindow checkpoint = transaction.View.OpenReadWindow(32, 8);
        transaction.RollbackCheckpoint();
        Assert.Equal(9, checkpoint.ReadByte(0));
        arena.WriteByte(33, 8);
        Assert.Equal(2, staged.ReadByte(1));
        Assert.Equal(2, committed.ReadByte(1));
        transaction.View.WriteByte(32, 7);
        transaction.Freeze();
        Assert.Equal(1, committed.ReadByte(0));
        Assert.Equal(5, staged.ReadByte(0));
        Assert.Equal(9, checkpoint.ReadByte(0));
        Assert.Equal(new byte[] { 7, 8, 3, 4 }, arena.Read(32, 4));
        transaction.Dispose();
        Assert.Throws<ObjectDisposedException>(() => staged.ReadByte(0));
        arena.WriteByte(32, 10);
        Assert.Equal(1, committed.ReadByte(0));
    }

    [Fact]
    public void Warmed_windows_allocate_no_store_journals_and_large_leases_do_not_clear_retained_capacity()
    {
        using NativeBattleWorkingMemory memory = new(65536);
        memory.Write(0, new byte[65536]);
        using (NativeBattleReadWindow large = memory.OpenReadWindow(0, memory.LogicalLength))
        {
            memory.Write(0, new byte[65536]);
            Assert.Equal(65536, large.RetainedByteCount);
        }
        long reset = memory.ReadWindowResetByteCount;
        for (uint index = 0; index < 1000; index++)
        {
            using NativeBattleReadWindow window = memory.OpenReadWindow(32000, 4);
            memory.WriteUInt32(32000, index);
            _ = window.ReadUInt32(0);
        }
        long copied = memory.ReadWindowCapturedByteCount;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint index = 0; index < 1000; index++)
        {
            using NativeBattleReadWindow window = memory.OpenReadWindow(32000, 4);
            memory.WriteUInt32(32000, index);
            _ = window.ReadUInt32(0);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(4000, memory.ReadWindowCapturedByteCount - copied);
        Assert.Equal(reset, memory.ReadWindowResetByteCount);
    }

    [Fact]
    public void Revoke_invalidates_reads_and_disposal_does_not_undo_real_mutations_or_new_leases()
    {
        using NativeBattleWorkingMemory memory = new(16);
        NativeBattleReadWindow old = memory.OpenReadWindow(0, 8);
        memory.WriteByte(0, 7);
        old.Dispose();
        using NativeBattleReadWindow current = memory.OpenReadWindow(0, 8);
        old.Dispose();
        Assert.Equal(7, current.ReadByte(0));
        Assert.Equal(7, memory.ReadByte(0));
        memory.Revoke();
        Assert.Throws<ObjectDisposedException>(() => current.ReadByte(0));
        current.Dispose();
    }
}
