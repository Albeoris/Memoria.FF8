using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingSinglePageUnknownTests
{
    private const uint Address = 0x3000000;
    private const int PageLength = 4096;

    [Theory]
    [InlineData(3, 2)]
    [InlineData(4092, 4)]
    [InlineData(4093, 4)]
    [InlineData(4095, 2)]
    [InlineData(4096, 1)]
    [InlineData(8190, 8)]
    public void Unknown_ranges_preserve_hidden_bytes_neighbors_and_exact_page_version_changes(int offset, int length)
    {
        byte[] bytes = Enumerable.Range(0, 3 * PageLength).Select(index => unchecked((byte)(index + 1))).ToArray();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Address, bytes);
        staging.WriteByte(Address + 20 * PageLength, 0xA7);
        long version = staging.StorageVersion;

        staging.MarkUnknown(Address + (uint)offset, length);

        (byte[] data, bool[] known) = staging.ReadSnapshot(Address, bytes.Length);
        Assert.Equal(bytes, data);
        Assert.Equal(Enumerable.Range(0, bytes.Length).Select(index => index < offset || index >= offset + length), known);
        int touchedPages = (offset + length - 1) / PageLength - offset / PageLength + 1;
        Assert.Equal(version + touchedPages, staging.StorageVersion);
        Assert.Equal(0xA7, staging.ReadByte(Address + 20 * PageLength));
        Assert.Throws<NotSupportedException>(() => staging.ReadByte(Address + (uint)offset));
    }

    [Fact]
    public void Missing_single_page_invalidates_only_selected_loader_zero_without_materializing_storage()
    {
        const uint selected = 0x255FEC2;
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        staging.WriteByte(Address, 0xB6);
        int pages = staging.AllocatedPageCount;

        staging.MarkUnknown(selected, 4);

        Assert.Equal(pages, staging.AllocatedPageCount);
        Assert.Equal(new byte[2], staging.Read(selected - 2, 2));
        Assert.Equal(new byte[2], staging.Read(selected + 4, 2));
        Assert.Throws<NotSupportedException>(() => staging.Read(selected, 4));
        Assert.Equal(0xB6, staging.ReadByte(Address));
    }

    [Fact]
    public void Partial_immutable_page_outside_stored_extent_does_not_materialize_or_change_version()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().BindResource(Address, ImmutableArray.Create<byte>(1, 2, 3, 4));
        long version = staging.StorageVersion;
        int pages = staging.AllocatedPageCount;

        staging.MarkUnknown(Address + 100, 4);

        Assert.Equal(version, staging.StorageVersion);
        Assert.Equal(pages, staging.AllocatedPageCount);
        Assert.Equal(0, staging.MaterializedPageCount);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, staging.Read(Address, 4));
        Assert.Throws<NotSupportedException>(() => staging.ReadByte(Address + 100));
    }

    [Fact]
    public void Partial_immutable_page_marks_only_the_existing_stored_overlap()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().BindResource(Address, ImmutableArray.Create<byte>(1, 2, 3, 4));
        long version = staging.StorageVersion;

        staging.MarkUnknown(Address + 2, 4);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, staging.ReadSnapshot(Address, 4).Data);
        Assert.Equal(new[] { true, true, false, false, false, false }, staging.ReadSnapshot(Address, 6).Known);
        Assert.Equal(version + 1, staging.StorageVersion);
        Assert.Equal(1, staging.MaterializedPageCount);
    }

    [Fact]
    public void Current_unknown_write_preserves_pinned_bytes_snapshot_and_unrelated_pointer()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Address, [1, 2, 3, 4]);
        staging.WriteResourcePointer(Address + 32, new(null, 0x12345678));
        staging.WriteResourcePointer(Address + 64, null);
        NativeBattleFileStaging snapshot = NativeBattleStorageSnapshots.Capture(staging);
        using NativeBattleStagingReadWindow window = staging.OpenReadWindow(Address, 68, maximumRetainedBytes: 128);

        staging.MarkUnknown(Address + 1, 2);
        staging.MarkUnknown(Address + 33, 1);

        Assert.Equal(2, window.ReadByte(1));
        Assert.Equal(3, window.ReadByte(2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, snapshot.Read(Address, 4));
        Assert.Equal(0x12345678u, snapshot.ReadResourcePointer(Address + 32)!.StagingAddress);
        Assert.False(staging.TryGetResourcePointerMetadata(Address + 32, out _));
        Assert.True(staging.TryGetResourcePointerMetadata(Address + 64, out NativeBattleResourcePointer? untouched));
        Assert.Null(untouched);
        Assert.Equal(new[] { true, false, false, true }, staging.ReadSnapshot(Address, 4).Known);
    }

    [Fact]
    public void Checkpoint_rollback_and_publication_preserve_unknownness_and_old_owner_facts()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Address, [1, 2, 3, 4]);
        NativeBattleFileStaging snapshot = NativeBattleStorageSnapshots.Capture(staging);
        using NativeBattleFileStagingTransaction transaction = new(staging);
        transaction.BeginCheckpoint();
        transaction.View.MarkUnknown(Address + 1, 1);
        Assert.False(transaction.View.ReadSnapshot(Address + 1, 1).Known[0]);
        transaction.RollbackCheckpoint();
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, transaction.View.Read(Address, 4));

        transaction.View.MarkUnknown(Address + 2, 1);
        transaction.Freeze(transaction.View);

        Assert.Equal(new[] { true, true, false, true }, staging.ReadSnapshot(Address, 4).Known);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, snapshot.Read(Address, 4));
        long acceptedVersion = staging.StorageVersion;
        transaction.View.MarkUnknown(Address, 1);
        transaction.Dispose();
        Assert.Equal(1, staging.ReadByte(Address));
        Assert.Equal(acceptedVersion, staging.StorageVersion);
    }

    [Fact]
    public void Offset_and_physical_address_limits_preserve_validation_and_storage_versions()
    {
        uint lastRepresentable = NativeBattleFileStaging.Address + (uint)int.MaxValue - 1;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(lastRepresentable - 3, [1, 2, 3, 4]);
        long version = staging.StorageVersion;
        staging.MarkUnknown(lastRepresentable + 1, 0);
        Assert.Equal(version, staging.StorageVersion);

        staging.MarkUnknown(lastRepresentable, 1);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, staging.ReadSnapshot(lastRepresentable - 3, 4).Data);
        Assert.Equal(new[] { true, true, true, false }, staging.ReadSnapshot(lastRepresentable - 3, 4).Known);
        Assert.Equal(version + 1, staging.StorageVersion);
        long accepted = staging.StorageVersion;
        Assert.Throws<InvalidDataException>(() => staging.MarkUnknown(lastRepresentable + 1, 1));
        Assert.Throws<InvalidDataException>(() => staging.MarkUnknown(uint.MaxValue, 0));
        Assert.Throws<InvalidDataException>(() => staging.MarkUnknown(uint.MaxValue, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => staging.MarkUnknown(uint.MaxValue, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => staging.MarkUnknown(Address, -1));
        Assert.Equal(accepted, staging.StorageVersion);
    }

    [Fact]
    public void Invalid_range_precedes_readonly_and_disposed_owner_guards()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteByte(Address, 1);
        NativeBattleFileStaging snapshot = NativeBattleStorageSnapshots.Capture(staging);
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.MarkUnknown(uint.MaxValue, 2));
        Assert.Throws<InvalidOperationException>(() => snapshot.MarkUnknown(Address, 1));
        NativeBattleFileStagingTransaction transaction = new(staging);
        NativeBattleFileStaging view = transaction.View;
        transaction.Dispose();
        Assert.Throws<ArgumentOutOfRangeException>(() => view.MarkUnknown(Address, -1));
        Assert.Throws<ObjectDisposedException>(() => view.MarkUnknown(Address, 1));
        Assert.Equal(1, staging.ReadByte(Address));
    }

    [Fact]
    public void Unrelated_pages_do_not_add_key_array_allocation_to_single_page_invalidation()
    {
        NativeBattleFileStaging narrow = NativeBattleFileStaging.Unknown().WriteByte(Address, 1);
        NativeBattleFileStaging many = NativeBattleFileStaging.Unknown();
        for (int index = 0; index < 512; index++)
        {
            many.WriteByte(Address + (uint)(index * PageLength), 1);
        }
        for (int index = 0; index < 32; index++)
        {
            narrow.MarkUnknown(Address, 1);
            many.MarkUnknown(Address, 1);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
        {
            narrow.MarkUnknown(Address, 1);
        }
        long narrowBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
        {
            many.MarkUnknown(Address, 1);
        }
        long manyBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(manyBytes <= narrowBytes + 4096, $"Single-page invalidations allocated {manyBytes} bytes with unrelated pages, versus {narrowBytes} without them.");
        Assert.Equal(512, many.AllocatedPageCount);
    }
}
