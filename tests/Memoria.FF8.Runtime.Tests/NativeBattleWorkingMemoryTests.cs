using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWorkingMemoryTests
{
    [Fact]
    public void Escaped_fresh_owners_do_not_allocate_unused_checkpoint_bookkeeping()
    {
        Assert.SkipUnless(Environment.Is64BitProcess, "The allocation bound covers the supported 64-bit hosts.");
        for (int index = 0; index < 32; index++)
        {
            using NativeBattleWorkingMemory warmup = new(16);
            Assert.Equal(0, warmup.JournalEntryCount);
            Assert.Equal(0, warmup.CheckpointDepth);
        }
        NativeBattleWorkingMemory[] owners = new NativeBattleWorkingMemory[1024];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < owners.Length; index++)
        {
            owners[index] = new(16);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        try
        {
            Assert.InRange(allocated, 1, owners.Length * 640L - 1);
            Assert.All(owners, owner =>
            {
                Assert.Equal(0, owner.JournalEntryCount);
                Assert.Equal(0, owner.CheckpointDepth);
                Assert.False(owner.HasCheckpoint);
            });
            GC.KeepAlive(owners);
        }
        finally
        {
            foreach (NativeBattleWorkingMemory owner in owners)
            {
                owner.Dispose();
            }
        }
    }

    [Fact]
    public void First_lazy_journal_allocation_failure_does_not_prevent_a_later_checkpoint_retry()
    {
        using NativeBattleWorkingMemory memory = new(128);
        memory.Write(4, [1, 2, 3, 4]);
        memory.MarkUnknown(5, 1);
        string original = JsonSerializer.Serialize(memory.Capture());
        memory.BeginCheckpoint();
        using (NativeBattleTelemetry.BeginScope(new ThrowingTrafficSink(NativeBattleTrafficKind.CpuCopyPayload, new OutOfMemoryException("Injected first-journal telemetry allocation failure."))))
        {
            Assert.Throws<OutOfMemoryException>(() => memory.WriteWithKnownness(3, new byte[] { 9, 8, 7, 6 }, new[] { false, true, false, true }));
        }
        memory.RollbackCheckpoint();
        Assert.Equal(original, JsonSerializer.Serialize(memory.Capture()));
        memory.BeginCheckpoint();
        memory.Write(4, [9, 8, 7, 6]);
        memory.BeginNestedCheckpoint();
        memory.MarkUnknown(0, 16);
        memory.RollbackNestedCheckpoint();
        memory.CommitCheckpoint();
        byte[] actual = new byte[4];
        memory.ReadInto(4, actual);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, actual);
        Assert.False(memory.HasCheckpoint);
    }

    [Fact]
    public void Private_mixed_stores_do_not_preallocate_unused_publication_journal_buffers()
    {
        byte[] data = [1, 2, 3, 4];
        bool[] known = [true, false, true, true];
        Measure(false);
        Measure(true);
        long privateBytes = Measure(false);
        long publicationBytes = Measure(true);
        Assert.True(publicationBytes - privateBytes >= 32 * 64, $"Private stores saved only {publicationBytes - privateBytes} bytes across 32 fresh owners.");

        long Measure(bool preparePublication)
        {
            int sum = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 32; index++)
            {
                using NativeBattleWorkingMemory memory = new(16);
                if (preparePublication)
                {
                    memory.ReserveStorageReceipts(1);
                }
                memory.WriteWithKnownness(0, data, known);
                sum += memory.ReadByte(0);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(32, sum);
            return allocated;
        }
    }

    [Fact]
    public void First_checkpoint_after_private_stores_preallocates_coverage_before_nested_changes()
    {
        using NativeBattleWorkingMemory memory = new(128);
        memory.Write(4, [1, 2, 3, 4]);
        memory.Write(16, [5, 6, 7, 8]);
        memory.MarkUnknown(5, 1);
        string original = JsonSerializer.Serialize(memory.Capture());
        memory.BeginCheckpoint();
        memory.WriteWithKnownness(6, new byte[] { 9, 8, 7, 6, 5, 4 }, new[] { true, false, true, false, true, true });
        memory.BeginNestedCheckpoint();
        memory.MarkUnknown(0, 32);
        memory.Write(12, [10, 11, 12, 13]);
        memory.CommitNestedCheckpoint();
        memory.RollbackCheckpoint();
        Assert.Equal(original, JsonSerializer.Serialize(memory.Capture()));
    }

    [Fact]
    public void Admission_after_private_stores_keeps_published_prefix_and_rolls_back_nested_suffix()
    {
        using NativeBattleWorkingMemory memory = new(128);
        using NativeBattleWorkingMemory expected = new(128);
        memory.Write(4, [1, 2, 3, 4]);
        expected.Write(4, [1, 2, 3, 4]);
        memory.AdmitRetained(new());
        using (NativeBattleTaskOperation operation = new([]))
        {
            memory.WriteWithKnownness(8, new byte[] { 5, 6, 7, 8 }, new[] { true, false, true, true });
            expected.WriteWithKnownness(8, new byte[] { 5, 6, 7, 8 }, new[] { true, false, true, true });
            NativeBattleTaskOperation.AcceptPublishedArena(memory);
            using NativeBattleTaskOperation nested = new([]);
            memory.MarkUnknown(0, 20);
            memory.Write(2, [9, 8, 7, 6]);
            nested.Commit();
        }
        Assert.Equal(JsonSerializer.Serialize(expected.Capture()), JsonSerializer.Serialize(memory.Capture()));
        Assert.False(memory.HasCheckpoint);
    }

    [Fact]
    public void First_journal_capacity_rejection_after_private_stores_keeps_bytes_and_coverage()
    {
        using NativeBattleWorkingMemory memory = new(128, maxJournalEntries: 2);
        memory.Write(4, [1, 2, 3, 4]);
        memory.MarkUnknown(5, 1);
        string original = JsonSerializer.Serialize(memory.Capture());
        object backing = memory.BackingIdentity;
        memory.BeginCheckpoint();
        Assert.Throws<InvalidOperationException>(() => memory.WriteWithKnownness(3, new byte[] { 9, 8, 7, 6 }, new[] { false, true, false, true }));
        Assert.Same(backing, memory.BackingIdentity);
        Assert.Equal(0, memory.JournalEntryCount);
        memory.RollbackCheckpoint();
        Assert.Equal(original, JsonSerializer.Serialize(memory.Capture()));
    }

    [Fact]
    public void Injected_allocation_exception_during_first_journaled_store_leaves_rollback_complete()
    {
        using NativeBattleWorkingMemory memory = new(128);
        memory.Write(4, [1, 2, 3, 4]);
        memory.MarkUnknown(5, 1);
        string original = JsonSerializer.Serialize(memory.Capture());
        memory.BeginCheckpoint();
        using (NativeBattleTelemetry.BeginScope(new ThrowingTrafficSink(NativeBattleTrafficKind.CpuCopyPayload, new OutOfMemoryException("Injected telemetry allocation failure."))))
        {
            Assert.Throws<OutOfMemoryException>(() => memory.WriteWithKnownness(3, new byte[] { 9, 8, 7, 6 }, new[] { false, true, false, true }));
        }
        memory.RollbackCheckpoint();
        Assert.Equal(original, JsonSerializer.Serialize(memory.Capture()));
    }

    [Fact]
    public void Ownership_suspension_retains_storage_for_rollback_without_reactivating_old_read_windows()
    {
        using NativeBattleWorkingMemory memory = new(64, initialCapacity: 64);
        memory.WriteUInt32(8, 0x12345678);
        object backing = memory.BackingIdentity;
        using NativeBattleReadWindow previous = memory.OpenReadWindow(8, 4);
        memory.SuspendOwnership();
        Assert.Throws<ObjectDisposedException>(() => memory.ReadUInt32(8));
        Assert.Throws<ObjectDisposedException>(() => memory.WriteUInt32(8, 0));
        Assert.Throws<ObjectDisposedException>(() => memory.Capture());
        Assert.Throws<ObjectDisposedException>(() => previous.ReadUInt32(0));
        memory.ResumeOwnership();
        Assert.Same(backing, memory.BackingIdentity);
        Assert.Equal(0x12345678u, memory.ReadUInt32(8));
        Assert.Throws<ObjectDisposedException>(() => previous.ReadUInt32(0));
        using NativeBattleReadWindow current = memory.OpenReadWindow(8, 4);
        Assert.Equal(0x12345678u, current.ReadUInt32(0));
        memory.SuspendOwnership();
        memory.CompleteSuspension();
        Assert.Throws<ObjectDisposedException>(() => memory.ResumeOwnership());
        Assert.Throws<ObjectDisposedException>(() => memory.ReadUInt32(8));
    }

    [Fact]
    public void Physical_receipts_follow_mixed_stores_hidden_bytes_and_nested_parent_rollback()
    {
        using NativeBattleWorkingMemory memory = new(64, initialKnownZero: false);
        Assert.Empty(memory.DataFootprint.Ranges);
        Assert.Equal((0, 64), Assert.Single(memory.ExplicitCoverage.Ranges));
        memory.WriteWithKnownness(8, [0xA5, 0xB6], [true, false]);
        memory.WriteRetainedBytes(9, [0xC7]);
        memory.Copy(8, 16, 2);
        memory.SetResourcePointer(24, new(null, 0x12345678));
        memory.SetResourcePointer(32, null);
        memory.MarkUnknown(40, 4);
        Assert.Equal(new[] { (8, 10), (16, 18), (24, 28) }, memory.DataFootprint.Ranges);
        NativeBattleWorkingMemorySnapshot captured = memory.Capture();
        Assert.Equal(3, captured.DataFootprint.Length);
        Assert.Equal((0, 64), Assert.Single(memory.ExplicitCoverage.Ranges));
        memory.BeginCheckpoint();
        memory.Write(0, [1, 2]);
        memory.BeginNestedCheckpoint();
        memory.WriteByte(12, 7);
        memory.CommitNestedCheckpoint();
        memory.MarkUnknown(8, 2);
        memory.RollbackCheckpoint();
        Assert.Equal(captured.DataFootprint.ToArray(), memory.Capture().DataFootprint.ToArray());
        using NativeBattleWorkingMemory restored = NativeBattleWorkingMemory.Restore(captured);
        Assert.Equal(memory.DataFootprint.Ranges, restored.DataFootprint.Ranges);
        Assert.Equal(memory.ExplicitCoverage.Ranges, restored.ExplicitCoverage.Ranges);
        Assert.Equal(memory.UnknownCoverage.Ranges, restored.UnknownCoverage.Ranges);
        NativeBattleEffectArenaSnapshot visible = NativeBattleEffectArena.BorrowWorking(restored, 0, 64).Capture();
        Assert.Equal(new[] { 8, 16, 24 }, visible.Regions.Select(region => region.Offset));
        Assert.Equal((byte)0xC7, visible.ReadSnapshot(9, 1).Data[0]);
        Assert.False(visible.ReadSnapshot(9, 1).Known[0]);
        AssertStorageEqual(memory, restored);
    }

    [Fact]
    public void Physical_receipts_are_consistent_when_post_store_diagnostics_throw()
    {
        using NativeBattleWorkingMemory memory = new(64);
        using (NativeBattleTelemetry.BeginScope(new ThrowingTrafficSink(NativeBattleTrafficKind.CpuCopyPayload)))
        {
            Assert.Throws<InvalidOperationException>(() => memory.WriteByte(8, 7));
        }
        Assert.Equal((8, 9), Assert.Single(memory.DataFootprint.Ranges));
        Assert.Equal((8, 9), Assert.Single(memory.ExplicitCoverage.Ranges));
        Assert.Equal(7, memory.ReadByte(8));
        using (NativeBattleTelemetry.BeginScope(new ThrowingTrafficSink(NativeBattleTrafficKind.Fill)))
        {
            Assert.Throws<InvalidOperationException>(() => memory.MarkUnknown(20, 1));
        }
        Assert.Equal((8, 9), Assert.Single(memory.DataFootprint.Ranges));
        Assert.Equal((20, 21), Assert.Single(memory.UnknownCoverage.Ranges));
        NativeBattleEffectArenaSnapshot visible = NativeBattleEffectArena.BorrowWorking(memory, 0, 64).Capture();
        Assert.Equal(7, visible.ReadByte(8));
        Assert.False(visible.ReadSnapshot(20, 1).Known[0]);
    }

    [Fact]
    public void Lazy_metadata_participants_follow_child_commits_and_parent_rollbacks_without_full_snapshots()
    {
        using NativeBattleWorkingMemory memory = new(16);
        MetadataParticipant metadata = new() { Value = 10 };
        memory.BeginCheckpoint();
        memory.BeginNestedCheckpoint();
        memory.EnlistCheckpointParticipant(metadata);
        memory.EnlistCheckpointParticipant(metadata);
        Assert.Equal(2, metadata.Marks);
        metadata.Value = 20;
        memory.WriteByte(0, 1);
        memory.CommitNestedCheckpoint();
        Assert.Equal(1, metadata.Marks);
        memory.RollbackCheckpoint();
        Assert.Equal(10, metadata.Value);
        Assert.Equal(0, metadata.Marks);
        Assert.Equal((byte)0, memory.ReadByte(0));

        memory.BeginCheckpoint();
        memory.EnlistCheckpointParticipant(metadata);
        metadata.Value = 11;
        memory.BeginNestedCheckpoint();
        memory.EnlistCheckpointParticipant(metadata);
        metadata.Value = 12;
        memory.RollbackNestedCheckpoint();
        Assert.Equal(11, metadata.Value);
        memory.CommitCheckpoint();
        Assert.Equal(11, metadata.Value);
        Assert.Equal(0, metadata.Marks);
    }

    private sealed class MetadataParticipant : INativeBattleCheckpointParticipant
    {
        private readonly Stack<int> marks = [];
        public int Value { get; set; }
        public int Marks => marks.Count;
        public void BeginCheckpoint() => marks.Push(Value);
        public void CommitCheckpoint() => _ = marks.Pop();
        public void RollbackCheckpoint() => Value = marks.Pop();
    }

    [Fact]
    public void Nested_changes_preserve_parent_undo_across_commits_and_restore_only_the_failed_suffix()
    {
        using NativeBattleWorkingMemory memory = new(128);
        memory.SetResourcePointer(4, new(null, 0x12345678));
        memory.BeginCheckpoint();
        memory.WriteByte(0, 1);
        memory.BeginNestedCheckpoint();
        memory.WriteByte(5, 0xA5);
        memory.SetResourcePointer(80, new(null, 99));
        Assert.Equal(2, memory.CheckpointDepth);
        Assert.Throws<InvalidOperationException>(memory.CommitCheckpoint);
        memory.RollbackNestedCheckpoint();
        Assert.Equal(1, memory.CheckpointDepth);
        Assert.Equal((byte)1, memory.ReadByte(0));
        Assert.Equal(0x12345678u, memory.ReadResourcePointer(4)!.StagingAddress);
        Assert.Equal(8, memory.StoredLength);
        memory.BeginNestedCheckpoint();
        memory.WriteByte(0, 2);
        memory.CommitNestedCheckpoint();
        Assert.Equal((byte)2, memory.ReadByte(0));
        memory.RollbackCheckpoint();
        Assert.Equal(0, memory.CheckpointDepth);
        Assert.Equal((byte)0, memory.ReadByte(0));
        Assert.Equal(0x12345678u, memory.ReadResourcePointer(4)!.StagingAddress);
        memory.BeginCheckpoint();
        memory.BeginNestedCheckpoint();
        memory.WriteRetainedBytes(4, [3, 4, 5, 6]);
        memory.RollbackCheckpoint();
        Assert.Equal(0, memory.CheckpointDepth);
        Assert.Equal(0x12345678u, memory.ReadUInt32(4));
    }

    [Fact]
    public void Mixed_knownness_stores_preserve_hidden_bytes_and_use_one_undo_entry_per_changed_lane()
    {
        using NativeBattleWorkingMemory memory = new(64, maxJournalEntries: 4);
        memory.WriteUInt32(0, 0x12345678);
        memory.BeginCheckpoint();
        memory.ReserveCheckpointCapacity(4, 0);
        memory.WriteWithKnownness(0, [1, 2, 3, 4], [true, false, true, false]);
        Assert.Equal(4, memory.JournalEntryCount);
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        memory.ReadWithKnownness(0, bytes, known);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
        Assert.Equal(new[] { true, false, true, false }, known);
        Assert.Throws<NotSupportedException>(() => memory.ReadUInt32(0));
        memory.RollbackCheckpoint();
        Assert.Equal(0x12345678u, memory.ReadUInt32(0));
        long version = memory.Version;
        Assert.Throws<ArgumentException>(() => memory.WriteWithKnownness(0, [1, 2], [true]));
        Assert.Equal(version, memory.Version);
        Assert.Equal(0x12345678u, memory.ReadUInt32(0));
    }

    [Fact]
    public void Captured_extent_tracks_physical_stores_not_reserved_capacity_and_rollback_restores_it()
    {
        using NativeBattleWorkingMemory memory = new(1024, initialCapacity: 512);
        Assert.Equal(0, memory.StoredLength);
        Assert.Empty(memory.Capture().Regions);
        memory.WriteByte(7, 42);
        Assert.Equal(8, memory.StoredLength);
        Assert.Equal(512, memory.Capacity);
        Assert.Equal(8, memory.Capture().Regions.Single().Data.Length);
        memory.BeginCheckpoint();
        memory.ReserveCheckpointCapacity(4, 1);
        memory.SetResourcePointer(800, new(null, 0x12345678));
        Assert.Equal(804, memory.StoredLength);
        using (var pointers = memory.EnumerateResourcePointers())
        {
            Assert.True(pointers.MoveNext());
            Assert.Equal(800, pointers.Current.Key);
            Assert.False(pointers.MoveNext());
        }
        memory.RollbackCheckpoint();
        Assert.Equal(8, memory.StoredLength);
        Assert.Equal(0, memory.ResourcePointerCount);
        Assert.Equal(0u, memory.ReadUInt32(800));
        using NativeBattleWorkingMemory restored = NativeBattleWorkingMemory.Restore(memory.Capture());
        Assert.Equal(8, restored.StoredLength);
        Assert.Equal((byte)42, restored.ReadByte(7));
        Assert.Equal(0u, restored.ReadUInt32(800));
    }

    [Fact]
    public void An_over_budget_commit_can_be_rejected_before_any_physical_store()
    {
        using NativeBattleWorkingMemory memory = new(64, maxJournalEntries: 4);
        memory.WriteUInt32(0, 0x12345678);
        memory.BeginCheckpoint();
        Assert.Throws<InvalidOperationException>(() => memory.ReserveCheckpointCapacity(4, 1));
        Assert.Equal(0x12345678u, memory.ReadUInt32(0));
        Assert.Equal(0, memory.JournalEntryCount);
        memory.RollbackCheckpoint();
    }

    [Fact]
    public void Lazy_memory_preserves_zero_holes_unknown_backing_bytes_and_scalar_endianness()
    {
        using NativeBattleWorkingMemory memory = new(1024 * 1024);
        Assert.Equal(0, memory.Capacity);
        Assert.Equal(0u, memory.ReadUInt32(1024 * 1024 - 4));
        Assert.Equal(0, memory.Capacity);
        memory.WriteUInt32(8, 0x89ABCDEF);
        Assert.InRange(memory.Capacity, 12, 64);
        Assert.Equal(unchecked((int)0x89ABCDEF), memory.ReadInt32(8));
        Assert.Equal(unchecked((short)0x89AB), memory.ReadInt16(10));
        Assert.Equal((byte)0xEF, memory.ReadByte(8));
        memory.MarkUnknown(9, 2);
        Assert.Throws<NotSupportedException>(() => memory.ReadUInt32(8));
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        memory.ReadWithKnownness(8, bytes, known);
        Assert.Equal(new byte[] { 0xEF, 0xCD, 0xAB, 0x89 }, bytes);
        Assert.Equal(new[] { true, false, false, true }, known);
        memory.WriteUInt16(9, 0xABCD);
        Assert.Equal(0x89ABCDEFu, memory.ReadUInt32(8));
        using NativeBattleWorkingMemory unknown = new(4096, initialKnownZero: false);
        Assert.Equal(0, unknown.Capacity);
        Assert.Throws<NotSupportedException>(() => unknown.ReadByte(4095));
        unknown.WriteByte(2, 0);
        Assert.Equal((byte)0, unknown.ReadByte(2));
        Assert.Throws<NotSupportedException>(() => unknown.ReadByte(3));
        memory.Copy(1024 * 1024 - 4, 0, 4);
        Assert.Equal(0u, memory.ReadUInt32(0));
        Assert.InRange(memory.Capacity, 12, 64);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    [InlineData(0, 0)]
    public void Raw_copy_has_memmove_semantics_for_bytes_knownness_and_destination_provenance(int source, int destination)
    {
        using NativeBattleWorkingMemory memory = new(64);
        byte[] input = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        memory.Write(0, input);
        input[0] = 255;
        memory.MarkUnknown(3, 2);
        memory.SetResourcePointer(12, new(null, 0x12345678));
        byte[] expected = new byte[16];
        bool[] expectedKnown = new bool[16];
        memory.ReadWithKnownness(0, expected, expectedKnown);
        expected.AsSpan(source, 12).CopyTo(expected.AsSpan(destination));
        expectedKnown.AsSpan(source, 12).CopyTo(expectedKnown.AsSpan(destination));
        memory.Copy(source, destination, 12);
        byte[] actual = new byte[16];
        bool[] actualKnown = new bool[16];
        memory.ReadWithKnownness(0, actual, actualKnown);
        Assert.Equal(expected, actual);
        Assert.Equal(expectedKnown, actualKnown);
        Assert.Equal(destination == 0, memory.TryGetResourcePointer(12, out _));
        actual[0] = 255;
        memory.ReadWithKnownness(0, actual, actualKnown);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Pointer_descriptors_validate_before_mutation_and_equal_partial_stores_invalidate_metadata()
    {
        using NativeBattleWorkingMemory memory = new(64);
        memory.WriteUInt32(4, 0x11223344);
        memory.SetResourcePointer(4, new(null, 0x11223344));
        Assert.True(memory.TryGetResourcePointer(4, out NativeBattleResourcePointer? numeric));
        Assert.Equal(0x11223344u, numeric!.StagingAddress);
        memory.WriteByte(5, 0x33);
        Assert.False(memory.TryGetResourcePointer(4, out _));
        Assert.Equal(0x11223344u, memory.ReadResourcePointer(4)!.StagingAddress);
        NativeBattleResourcePointer symbolic = new(null, null) { CpuRow = 2 };
        memory.SetResourcePointer(4, symbolic);
        Assert.Equal(symbolic, memory.ReadResourcePointer(4));
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        memory.ReadWithKnownness(4, bytes, known);
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, bytes);
        Assert.All(known, value => Assert.False(value));
        NativeBattleWorkingMemorySnapshot before = memory.Capture();
        Assert.Throws<InvalidDataException>(() => memory.SetResourcePointer(4, new(null, 12, -1)));
        Assert.Throws<InvalidDataException>(() => memory.SetResourcePointer(4, new(null, 12) { CpuRow = 1 }));
        Assert.Equal(symbolic, memory.ReadResourcePointer(4));
        using NativeBattleWorkingMemory restored = NativeBattleWorkingMemory.Restore(before);
        AssertStorageEqual(restored, memory);
        memory.MarkUnknown(7, 1);
        Assert.False(memory.TryGetResourcePointer(4, out _));
        memory.SetResourcePointer(8, null);
        Assert.True(memory.TryGetResourcePointer(8, out NativeBattleResourcePointer? explicitNull));
        Assert.Null(explicitNull);
    }

    [Fact]
    public void Checkpoint_rolls_back_repeated_overlapping_writes_knownness_pointer_replacement_and_growth()
    {
        using NativeBattleWorkingMemory memory = new(4096, initialKnownZero: false);
        memory.Write(0, [1, 2, 3, 4, 5, 6, 7, 8]);
        memory.MarkUnknown(3, 2);
        memory.SetResourcePointer(12, new(null, 100));
        memory.SetResourcePointer(20, new(null, null) { CpuRow = 1 });
        NativeBattleWorkingMemorySnapshot original = memory.Capture();
        memory.BeginCheckpoint();
        Assert.Throws<InvalidOperationException>(memory.BeginCheckpoint);
        memory.Write(2, [8, 7, 6, 5, 4]);
        memory.MarkUnknown(1, 4);
        memory.Copy(0, 3, 7);
        memory.SetResourcePointer(12, new(null, 200));
        memory.WriteByte(21, 0);
        memory.SetResourcePointer(22, new(null, null) { CpuRow = 3 });
        memory.WriteUInt32(4000, 0xABCDEF01);
        Assert.Equal(0xABCDEF01u, memory.ReadUInt32(4000));
        Assert.Throws<InvalidOperationException>(() => memory.Capture());
        Assert.True(memory.JournalEntryCount > 0);
        memory.RollbackCheckpoint();
        Assert.Equal(0, memory.JournalEntryCount);
        Assert.False(memory.HasCheckpoint);
        using NativeBattleWorkingMemory restored = NativeBattleWorkingMemory.Restore(original);
        AssertStorageEqual(restored, memory);
        Assert.Throws<NotSupportedException>(() => memory.ReadByte(4000));
        memory.BeginCheckpoint();
        memory.WriteUInt32(4000, 123);
        memory.CommitCheckpoint();
        Assert.Equal(123u, memory.ReadUInt32(4000));
        Assert.Throws<InvalidOperationException>(memory.RollbackCheckpoint);
    }

    [Fact]
    public void Journal_limit_and_invalid_arguments_fail_before_mutation()
    {
        using NativeBattleWorkingMemory memory = new(64, maxJournalEntries: 2);
        memory.BeginCheckpoint();
        object backing = memory.BackingIdentity;
        Assert.Throws<InvalidOperationException>(() => memory.WriteUInt32(0, 0x12345678));
        Assert.Same(backing, memory.BackingIdentity);
        Assert.Equal(0, memory.Capacity);
        Assert.Equal(0, memory.JournalEntryCount);
        Assert.Equal(0u, memory.ReadUInt32(0));
        Assert.Throws<InvalidDataException>(() => memory.WriteUInt32(61, 1));
        Assert.Throws<InvalidDataException>(() => memory.MarkUnknown(-1, 1));
        Assert.Throws<InvalidDataException>(() => memory.Copy(0, int.MaxValue, 4));
        Assert.Throws<InvalidDataException>(() => memory.ReadInto(0, new byte[65]));
        Assert.Throws<ArgumentException>(() => memory.ReadWithKnownness(0, new byte[4], new bool[3]));
        memory.RollbackCheckpoint();
        memory.Revoke();
        Assert.Throws<ObjectDisposedException>(() => memory.ReadByte(0));
        Assert.Throws<ObjectDisposedException>(() => memory.WriteByte(0, 0));
        Assert.Throws<ObjectDisposedException>(() => memory.Capture());
        Assert.Throws<ObjectDisposedException>(() => memory.Capacity);
    }

    [Fact]
    public void Snapshots_and_two_restores_are_independent_including_unknown_underlying_bytes()
    {
        using NativeBattleWorkingMemory live = new(512, initialKnownZero: false);
        byte[] input = [1, 2, 3, 4];
        live.Write(2, input);
        live.MarkUnknown(3, 1);
        live.SetResourcePointer(12, new(null, 987));
        NativeBattleWorkingMemorySnapshot first = live.Capture();
        input[0] = 255;
        live.WriteByte(2, 9);
        NativeBattleWorkingMemorySnapshot second = live.Capture();
        live.WriteByte(2, 10);
        using NativeBattleWorkingMemory a = NativeBattleWorkingMemory.Restore(first);
        using NativeBattleWorkingMemory b = NativeBattleWorkingMemory.Restore(first);
        using NativeBattleWorkingMemory c = NativeBattleWorkingMemory.Restore(second);
        Assert.Equal((byte)1, a.ReadByte(2));
        Assert.Equal((byte)9, c.ReadByte(2));
        a.WriteByte(2, 20);
        Assert.Equal((byte)1, b.ReadByte(2));
        Assert.Equal((byte)10, live.ReadByte(2));
        Assert.Throws<NotSupportedException>(() => b.ReadByte(511));
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        b.ReadWithKnownness(2, bytes, known);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
        Assert.Equal(new[] { true, false, true, true }, known);
        Assert.Throws<InvalidDataException>(() => NativeBattleWorkingMemory.Restore(first with { ResourcePointers = first.ResourcePointers.SetItem(12, new(null, 988)) }));
    }

    [Fact]
    public void Diagnostic_sink_failure_cannot_leave_physical_bytes_knownness_provenance_or_versions_inconsistent()
    {
        using NativeBattleWorkingMemory memory = new(64, initialCapacity: 64);
        NativeBattleResourcePointer symbolic = new(null, null) { CpuRow = 2 };
        memory.SetResourcePointer(4, symbolic);
        long version = memory.Version;
        using (NativeBattleTelemetry.BeginScope(new ThrowingTrafficSink(NativeBattleTrafficKind.CpuCopyPayload)))
        {
            Assert.Throws<InvalidOperationException>(() => memory.WriteUInt32(4, 0x12345678));
        }
        Assert.Equal(0x12345678u, memory.ReadUInt32(4));
        Assert.False(memory.TryGetResourcePointer(4, out _));
        Assert.True(memory.Version > version);

        using (NativeBattleTelemetry.BeginScope(new ThrowingTrafficSink(NativeBattleTrafficKind.CpuCopyPayload)))
        {
            Assert.Throws<InvalidOperationException>(() => memory.SetResourcePointer(12, new(null, 0x87654321)));
        }
        Assert.Equal(0x87654321u, memory.ReadUInt32(12));
        Assert.Equal(0x87654321u, memory.ReadResourcePointer(12)!.StagingAddress);
        using (NativeBattleTelemetry.BeginScope(new ThrowingTrafficSink(NativeBattleTrafficKind.CpuCopyPayload)))
        {
            Assert.Throws<InvalidOperationException>(() => memory.Copy(4, 12, 4));
        }
        Assert.Equal(0x12345678u, memory.ReadUInt32(12));
        Assert.False(memory.TryGetResourcePointer(12, out _));
        memory.SetResourcePointer(12, new(null, 0x12345678));
        version = memory.Version;
        using (NativeBattleTelemetry.BeginScope(new ThrowingTrafficSink(NativeBattleTrafficKind.Fill)))
        {
            Assert.Throws<InvalidOperationException>(() => memory.MarkUnknown(13, 1));
        }
        Assert.Throws<NotSupportedException>(() => memory.ReadUInt32(12));
        Assert.False(memory.TryGetResourcePointer(12, out _));
        Assert.True(memory.Version > version);

        memory.SetResourcePointer(12, new(null, 0x12345678));
        memory.BeginCheckpoint();
        memory.SetResourcePointer(12, symbolic);
        using (NativeBattleTelemetry.BeginScope(new ThrowingTrafficSink(NativeBattleTrafficKind.CpuCopyPayload)))
        {
            Assert.Throws<InvalidOperationException>(memory.RollbackCheckpoint);
        }
        Assert.False(memory.HasCheckpoint);
        Assert.Equal(0, memory.JournalEntryCount);
        Assert.Equal(0x12345678u, memory.ReadUInt32(12));
        Assert.Equal(0x12345678u, memory.ReadResourcePointer(12)!.StagingAddress);
        Assert.NotNull(memory.Capture());
    }

    [Fact]
    public void Warmed_thousand_four_byte_writes_keep_capacity_and_copy_only_the_written_payload()
    {
        const int length = 1024 * 1024;
        using NativeBattleWorkingMemory memory = new(length, initialCapacity: length);
        byte[] populated = Enumerable.Repeat((byte)1, length).ToArray();
        memory.Write(0, populated);
        for (int index = 0; index < 1000; index++)
        {
            memory.WriteUInt32(index * 16, (uint)index);
        }
        object backing = memory.BackingIdentity;
        long growth = memory.GrowthCopyBytes;
        long written = memory.BytesWritten;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int callback = 0; callback < 10; callback++)
        {
            for (int index = 0; index < 100; index++)
            {
                memory.WriteUInt32((callback * 100 + index) * 16, (uint)(index + 1000));
            }
        }
        long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(0, allocationDelta);
        Assert.Same(backing, memory.BackingIdentity);
        Assert.Equal(length, memory.Capacity);
        Assert.Equal(growth, memory.GrowthCopyBytes);
        Assert.Equal(4000, memory.BytesWritten - written);
        NativeBattleWorkingMemorySnapshot snapshot = memory.Capture();
        using NativeBattleWorkingMemory restored = NativeBattleWorkingMemory.Restore(snapshot);
        Assert.Equal(1099u, restored.ReadUInt32(999 * 16));
        restored.WriteUInt32(999 * 16, 0);
        Assert.Equal(1099u, memory.ReadUInt32(999 * 16));
    }

    [Fact]
    public void Registered_copy_and_fill_traffic_charge_only_changed_payload_until_explicit_capture_and_restore()
    {
        const int length = 1024 * 1024;
        using NativeBattleWorkingMemory memory = new(length, initialCapacity: length);
        memory.Write(0, Enumerable.Repeat((byte)1, length).ToArray());
        WorkingTrafficSink writes = new();
        using (NativeBattleTelemetry.BeginScope(writes))
        {
            for (int callback = 0; callback < 10; callback++)
            {
                for (int index = 0; index < 100; index++)
                {
                    memory.WriteUInt32((callback * 100 + index) * 16, (uint)(index + 1000));
                }
            }
        }
        Assert.Equal(4000, writes.WriteCopyBytes);
        Assert.Equal(4000, writes.WriteFillBytes);
        Assert.Equal(0, writes.GrowthCopyBytes);
        Assert.Equal(0, writes.GrowthFillBytes);
        Assert.Equal(0, writes.BoundaryCopyBytes);

        WorkingTrafficSink capture = new();
        NativeBattleWorkingMemorySnapshot snapshot;
        using (NativeBattleTelemetry.BeginScope(capture))
        {
            snapshot = memory.Capture();
        }
        Assert.Equal(length, capture.BoundaryCopyBytes);
        Assert.Equal(0, capture.WriteCopyBytes);
        Assert.Equal(0, capture.GrowthCopyBytes);
        Assert.Equal(length, Assert.Single(snapshot.Regions).Data.Length);

        WorkingTrafficSink hydration = new();
        NativeBattleWorkingMemory restored;
        using (NativeBattleTelemetry.BeginScope(hydration))
        {
            restored = NativeBattleWorkingMemory.Restore(snapshot);
        }
        using (restored)
        {
            Assert.Equal(length, hydration.BoundaryCopyBytes);
            Assert.Equal(0, hydration.WriteCopyBytes);
            Assert.True(hydration.GrowthFillBytes >= length * 2L);
            object backing = restored.BackingIdentity;
            WorkingTrafficSink continuation = new();
            using (NativeBattleTelemetry.BeginScope(continuation))
            {
                restored.WriteUInt32(0, 0x12345678);
            }
            Assert.Equal(4, continuation.WriteCopyBytes);
            Assert.Equal(4, continuation.WriteFillBytes);
            Assert.Equal(0, continuation.BoundaryCopyBytes);
            Assert.Equal(0, continuation.GrowthCopyBytes);
            Assert.Equal(0, continuation.GrowthFillBytes);
            Assert.Same(backing, restored.BackingIdentity);
        }
    }

    private sealed class WorkingTrafficSink : INativeBattleTelemetrySink
    {
        public long WriteCopyBytes;
        public long WriteFillBytes;
        public long GrowthCopyBytes;
        public long GrowthFillBytes;
        public long BoundaryCopyBytes;

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1005 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                WriteCopyBytes += payloadBytes;
            }
            else if (siteId == 1005 && kind == NativeBattleTrafficKind.Fill)
            {
                WriteFillBytes += payloadBytes;
            }
            else if (siteId == 1012 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                GrowthCopyBytes += payloadBytes;
            }
            else if (siteId == 1013 && kind is NativeBattleTrafficKind.Fill or NativeBattleTrafficKind.ZeroFill)
            {
                GrowthFillBytes += payloadBytes;
            }
            else if (siteId == 1002 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                BoundaryCopyBytes += payloadBytes;
            }
        }
    }

    private sealed class ThrowingTrafficSink(NativeBattleTrafficKind failureKind, Exception? failure = null) : INativeBattleTelemetrySink
    {
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1005 && kind == failureKind)
            {
                throw failure ?? new InvalidOperationException("Injected diagnostic sink failure.");
            }
        }
    }

    private static void AssertStorageEqual(NativeBattleWorkingMemory expected, NativeBattleWorkingMemory actual)
    {
        Assert.Equal(expected.LogicalLength, actual.LogicalLength);
        byte[] expectedData = new byte[expected.LogicalLength];
        byte[] actualData = new byte[actual.LogicalLength];
        bool[] expectedKnown = new bool[expected.LogicalLength];
        bool[] actualKnown = new bool[actual.LogicalLength];
        expected.ReadWithKnownness(0, expectedData, expectedKnown);
        actual.ReadWithKnownness(0, actualData, actualKnown);
        Assert.Equal(expectedData, actualData);
        Assert.Equal(expectedKnown, actualKnown);
        Assert.Equal(expected.Capture().ResourcePointers.OrderBy(entry => entry.Key), actual.Capture().ResourcePointers.OrderBy(entry => entry.Key));
    }
}
