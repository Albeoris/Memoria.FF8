namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStagingPrivateChildTests
{
    private const uint Address = NativeBattleFileStaging.Address;

    [Fact]
    public void Child_reads_pending_parent_and_publishes_only_into_that_parent_until_parent_freeze()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().WriteUInt32(Address, 1);
        using NativeBattleFileStagingTransaction parent = new(staging);
        parent.View.WriteUInt32(Address, 2);
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(staging);
        Assert.Equal(2u, child.View.ReadUInt32(Address));
        child.View.WriteUInt32(Address, 3);
        Assert.Equal(2u, parent.View.ReadUInt32(Address));
        Assert.Equal(1u, staging.ReadUInt32(Address));
        child.Freeze(child.View);
        Assert.Equal(3u, parent.View.ReadUInt32(Address));
        Assert.Equal(1u, staging.ReadUInt32(Address));
        parent.Freeze(parent.View);
        Assert.Equal(3u, staging.ReadUInt32(Address));
        Assert.Throws<ObjectDisposedException>(() => child.View.ReadUInt32(Address));
    }

    [Fact]
    public void Child_freeze_preserves_existing_parent_checkpoint_and_rollback_keeps_earlier_pending_prefix()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().WriteUInt32(Address, 1);
        using NativeBattleFileStagingTransaction parent = new(staging);
        parent.View.WriteUInt32(Address, 2);
        parent.BeginCheckpoint();
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View);
        child.View.WriteUInt32(Address, 3);
        child.View.WriteResourcePointer(Address + 8, new(null, null) { CpuRow = 2 });
        child.Freeze(child.View);
        Assert.Equal(1, parent.CheckpointDepth);
        Assert.Equal(3u, parent.View.ReadUInt32(Address));
        Assert.Equal(2, parent.View.ReadResourcePointer(Address + 8)!.CpuRow);
        parent.RollbackCheckpoint();
        Assert.Equal(2u, parent.View.ReadUInt32(Address));
        Assert.Null(parent.View.ReadResourcePointer(Address + 8)!.CpuRow);
        Assert.Equal(1u, staging.ReadUInt32(Address));
    }

    [Fact]
    public void Parent_merge_failure_restores_parent_delta_and_checkpoint_then_allows_retry()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        using NativeBattleFileStagingTransaction parent = new(staging);
        parent.View.WriteUInt32(Address, 2);
        parent.BeginCheckpoint();
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View);
        child.View.WriteUInt32(Address, 3);
        using (NativeBattleTelemetry.BeginScope(new FailingMergeTraffic()))
        {
            Assert.Throws<InvalidOperationException>(() => child.Freeze(child.View));
        }
        Assert.Equal(1, parent.CheckpointDepth);
        Assert.Equal(2u, parent.View.ReadUInt32(Address));
        Assert.Equal(3u, child.View.ReadUInt32(Address));
        child.Freeze(child.View);
        Assert.Equal(3u, parent.View.ReadUInt32(Address));
        parent.RollbackCheckpoint();
        Assert.Equal(2u, parent.View.ReadUInt32(Address));
    }

    [Fact]
    public void Child_read_window_captures_unique_effective_cells_and_original_unknown_masks_across_parent_rollback()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().WriteUInt32(Address, 0x01020304);
        using NativeBattleFileStagingTransaction parent = new(staging);
        parent.View.WriteUInt32(Address, 0x11121314);
        parent.BeginCheckpoint();
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View);
        child.View.MarkUnknown(Address, 4);
        child.View.WriteByte(Address + 1, 0x77);
        using NativeBattleStagingReadWindow window = child.View.OpenReadWindow(Address, 4, maximumRetainedBytes: 4);
        Assert.Equal(4, window.RetainedByteCount);
        parent.View.WriteUInt32(Address, 0x21222324);
        child.View.WriteByte(Address + 1, 0x88);
        parent.RollbackCheckpoint();
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        window.ReadWithKnownness(0, bytes, known);
        Assert.Equal(new byte[] { 0x14, 0x77, 0x12, 0x11 }, bytes);
        Assert.Equal(new[] { false, true, false, false }, known);
        Assert.Equal(0x01020304u, staging.ReadUInt32(Address));
    }

    [Fact]
    public void Stable_borrow_resolves_the_current_parent_without_retaining_a_disposed_view()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        NativeBattleFileStaging borrowed;
        using (NativeBattleFileStagingTransaction first = new(staging))
        {
            borrowed = first.View.BorrowOwner();
            first.View.WriteUInt32(Address, 3);
            first.Freeze(first.View);
        }
        using NativeBattleFileStagingTransaction second = new(staging);
        second.View.WriteUInt32(Address, 9);
        byte[] data = new byte[4];
        bool[] known = new bool[4];
        borrowed.ReadEffectiveWithKnownness(Address, data, known);
        Assert.Equal(new byte[] { 9, 0, 0, 0 }, data);
        Assert.All(known, Assert.True);
        Assert.Equal(3u, borrowed.ReadUInt32(Address));
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(borrowed);
        Assert.Equal(9u, child.View.ReadUInt32(Address));
        child.View.WriteUInt32(Address, 11);
        child.Freeze(child.View);
        Assert.Equal(11u, second.View.ReadUInt32(Address));
        Assert.Equal(3u, staging.ReadUInt32(Address));
    }

    [Fact]
    public void Standalone_child_freeze_uses_logical_owner_journals_and_later_setter_accepts_only_its_prefix()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        using (NativeBattleTaskOperation operation = new([]))
        {
            operation.AdmitStaging(staging);
            using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(staging);
            child.View.WriteUInt32(Address, 5);
            child.Freeze(child.View);
            NativeBattleTaskOperation.AcceptPublishedStaging(staging);
            staging.WriteUInt32(Address, 6);
        }
        Assert.Equal(5u, staging.ReadUInt32(Address));
    }

    [Fact]
    public void Warmed_child_merges_reuse_delta_and_parent_rollback_storage()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        using NativeBattleFileStagingTransaction parent = new(staging);
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View);
        for (uint value = 1; value <= 1000; value++)
        {
            parent.BeginCheckpoint();
            child.View.WriteUInt32(Address, value);
            child.Freeze(child.View);
            parent.RollbackCheckpoint();
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint value = 1; value <= 1000; value++)
        {
            parent.BeginCheckpoint();
            child.View.WriteUInt32(Address, value);
            child.Freeze(child.View);
            parent.RollbackCheckpoint();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private sealed class FailingMergeTraffic : INativeBattleTelemetrySink
    {
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1035 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                throw new InvalidOperationException("Child merge diagnostic sink failed.");
            }
        }
    }
}
