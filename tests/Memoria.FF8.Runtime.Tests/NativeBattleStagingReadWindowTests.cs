namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStagingReadWindowTests
{
    private const uint Address = NativeBattleFileStaging.Address;
    private const uint Crossing = Address / 4096 * 4096 + 4094;

    [Fact]
    public void Cross_page_reads_keep_original_words_without_an_initial_payload_copy()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Crossing, [1, 2, 3, 4]);
        using NativeBattleStagingReadWindow window = staging.OpenReadWindow(Crossing, 4);
        Assert.Equal(0, window.RetainedByteCount);
        Assert.Equal(0x04030201u, window.ReadUInt32(0));
        staging.WriteUInt32(Crossing, 0x12345678);
        Assert.Equal(0x04030201u, window.ReadUInt32(0));
        Assert.Equal(0x12345678u, staging.ReadUInt32(Crossing));
        Assert.Equal(4, window.RetainedByteCount);
        Assert.Throws<InvalidDataException>(() => window.ReadUInt16(3));
    }

    [Fact]
    public void Shared_cross_page_budget_failure_precedes_every_source_store_and_pointer_change()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Crossing, [1, 2, 3, 4]);
        using NativeBattleStagingReadWindow window = staging.OpenReadWindow(Crossing, 4, maximumRetainedBytes: 3);
        long version = staging.StorageVersion;
        Assert.Throws<InvalidOperationException>(() => staging.WriteUInt32(Crossing, 0x12345678));
        Assert.Equal(version, staging.StorageVersion);
        Assert.Equal(0x04030201u, staging.ReadUInt32(Crossing));
        Assert.Equal(0x04030201u, window.ReadUInt32(0));
        Assert.Equal(2, window.RetainedByteCount);
        Assert.Throws<ArgumentException>(() => staging.WorkingOwner.WriteWithKnownness(Crossing, [9, 8, 7, 6], [true]));
        Assert.Equal(0x04030201u, staging.ReadUInt32(Crossing));
        staging.WriteUInt16(Crossing, 0x6655);
        Assert.Equal(0x04036655u, staging.ReadUInt32(Crossing));
        Assert.Equal(0x04030201u, window.ReadUInt32(0));
    }

    [Fact]
    public void Absent_pages_and_loader_zero_proof_remain_original_after_page_creation_and_mask_changes()
    {
        uint address = Address + 0x10000;
        NativeBattleFileStaging cold = NativeBattleFileStaging.FromColdProcess();
        using NativeBattleStagingReadWindow zero = cold.OpenReadWindow(address, 16, maximumRetainedBytes: 0);
        Assert.Equal(0, cold.AllocatedPageCount);
        cold.WriteUInt32(address, 0x12345678);
        cold.MarkUnknown(address + 8, 4);
        Assert.Equal(0u, zero.ReadUInt32(0));
        Assert.Equal(0u, zero.ReadUInt32(8));
        Assert.Equal(0, zero.RetainedByteCount);
        Assert.Throws<NotSupportedException>(() => cold.ReadUInt32(address + 8));
        NativeBattleFileStaging unknown = NativeBattleFileStaging.Unknown();
        using NativeBattleStagingReadWindow absent = unknown.OpenReadWindow(address, 4, maximumRetainedBytes: 0);
        unknown.WriteUInt32(address, 0x87654321);
        Assert.Throws<NotSupportedException>(() => absent.ReadUInt32(0));
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        absent.ReadWithKnownness(0, bytes, known);
        Assert.Equal(new byte[4], bytes);
        Assert.All(known, value => Assert.False(value));
    }

    [Fact]
    public void Frozen_zero_metadata_applies_to_original_raw_unknown_page_bytes()
    {
        uint address = Address + 0x300;
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        staging.WorkingOwner.WriteWithKnownness(address, [0xA5, 0xB6], [false, false]);
        using NativeBattleStagingReadWindow window = staging.OpenReadWindow(address, 8);
        Assert.Equal(0, window.ReadUInt16(0));
        staging.MarkUnknown(address, 2);
        Assert.Equal(0, window.ReadUInt16(0));
        Assert.Throws<NotSupportedException>(() => staging.ReadUInt16(address));
        var hidden = staging.ReadSnapshot(address, 2);
        Assert.Equal(new byte[] { 0xA5, 0xB6 }, hidden.Data);
        Assert.All(hidden.Known, value => Assert.False(value));
    }

    [Fact]
    public void Initial_overlay_cells_unknown_ranges_and_forgotten_zero_metadata_survive_rollback_and_publication()
    {
        uint address = Address + 0x200;
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().Write(address, [1, 2, 3, 4, 5, 6, 7, 8]);
        using NativeBattleFileStagingTransaction transaction = new(staging);
        transaction.View.WriteByte(address, 9);
        transaction.View.ForgetLoaderZero(address + 12, 2);
        int depth = transaction.BeginNestedCheckpoint();
        transaction.View.MarkUnknown(address + 2, 2);
        transaction.View.WriteByte(address + 3, 77);
        using NativeBattleStagingReadWindow window = transaction.View.OpenReadWindow(address, 16);
        Assert.Equal(2, window.RetainedByteCount);
        Assert.Equal((ushort)0x0209, window.ReadUInt16(0));
        Assert.Throws<NotSupportedException>(() => window.ReadUInt16(2));
        Assert.Throws<NotSupportedException>(() => window.ReadUInt16(12));
        transaction.RollbackNestedCheckpoint(depth);
        transaction.View.WriteUInt32(address, 0x11223344);
        transaction.Freeze(transaction.View);
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        window.ReadWithKnownness(0, bytes, known);
        Assert.Equal(new byte[] { 9, 2, 3, 77 }, bytes);
        Assert.Equal(new[] { true, true, false, true }, known);
        Assert.Equal(0x11223344u, staging.ReadUInt32(address));
        transaction.Dispose();
        Assert.Throws<ObjectDisposedException>(() => window.ReadByte(0));
    }

    [Fact]
    public void Existing_page_undo_is_preseeded_or_open_fails_without_blocking_parent_rollback()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Crossing, [1, 2, 3, 4]);
        staging.WorkingOwner.BeginCheckpoint();
        staging.Write(Crossing, [5, 6, 7, 8]);
        Assert.Throws<InvalidOperationException>(() => staging.OpenReadWindow(Crossing, 4, maximumRetainedBytes: 3));
        staging.WorkingOwner.RollbackCheckpoint();
        Assert.Equal(0x04030201u, staging.ReadUInt32(Crossing));
        staging.WorkingOwner.BeginCheckpoint();
        staging.Write(Crossing, [5, 6, 7, 8]);
        using NativeBattleStagingReadWindow window = staging.OpenReadWindow(Crossing, 4, maximumRetainedBytes: 4);
        Assert.Equal(4, window.RetainedByteCount);
        staging.WorkingOwner.RollbackCheckpoint();
        Assert.Equal(0x08070605u, window.ReadUInt32(0));
        Assert.Equal(0x04030201u, staging.ReadUInt32(Crossing));
    }

    [Fact]
    public void Warmed_page_and_metadata_leases_reuse_storage_without_per_callback_allocations()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().WriteUInt32(Address, 0);
        for (uint value = 0; value < 1000; value++)
        {
            using NativeBattleStagingReadWindow window = staging.OpenReadWindow(Address, 4);
            staging.WriteUInt32(Address, value);
            _ = window.ReadUInt32(0);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint value = 0; value < 1000; value++)
        {
            using NativeBattleStagingReadWindow window = staging.OpenReadWindow(Address, 4);
            staging.WriteUInt32(Address, value);
            _ = window.ReadUInt32(0);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(999u, staging.ReadUInt32(Address));
    }
}
