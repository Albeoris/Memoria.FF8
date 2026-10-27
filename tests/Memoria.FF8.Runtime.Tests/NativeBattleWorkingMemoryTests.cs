using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWorkingMemoryTests
{
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

    private sealed class ThrowingTrafficSink(NativeBattleTrafficKind failureKind) : INativeBattleTelemetrySink
    {
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1005 && kind == failureKind)
            {
                throw new InvalidOperationException("Injected diagnostic sink failure.");
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
