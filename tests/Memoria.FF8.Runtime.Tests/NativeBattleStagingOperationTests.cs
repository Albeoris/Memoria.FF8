namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStagingOperationTests
{
    private const uint Address = NativeBattleFileStaging.Address;

    [Fact]
    public void Nested_owner_rollback_restores_rebase_zero_proof_pointers_and_pages_created_in_the_child()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        uint originalBase = staging.BaseAddress;
        NativeBattleFileStagingWorkingOwner owner = staging.WorkingOwner;
        owner.BeginCheckpoint();
        staging.WriteUInt32(Address, 17);
        owner.BeginNestedCheckpoint();
        staging.Rebase(originalBase - 4096);
        staging.MarkUnknown(Address + 16, 4);
        staging.WriteResourcePointer(Address + 32, new(null, null) { CpuRow = 2 });
        staging.WriteUInt32(Address + 0x20000, 29);
        owner.RollbackNestedCheckpoint();
        Assert.Equal(originalBase, staging.BaseAddress);
        Assert.Equal(17u, staging.ReadUInt32(Address));
        Assert.Equal(0u, staging.ReadUInt32(Address + 16));
        Assert.Equal(0u, staging.ReadUInt32(Address + 0x20000));
        Assert.False(owner.TryGetPointer(Address + 32, out _));
        owner.RollbackCheckpoint();
        Assert.Equal(0u, staging.ReadUInt32(Address));
        Assert.False(owner.HasCheckpoint);
    }

    [Fact]
    public void Successful_short_overlay_publication_is_still_private_to_the_outer_operation_until_accepted()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        using (NativeBattleTaskOperation operation = new([]))
        {
            operation.AdmitStaging(staging);
            using NativeBattleFileStagingTransaction transaction = new(staging);
            transaction.View.WriteUInt32(Address, 23);
            transaction.View.MarkUnknown(Address + 8, 4);
            transaction.Freeze(transaction.View);
            Assert.Equal(1, staging.WorkingOwner.CheckpointDepth);
            Assert.Equal(23u, staging.ReadUInt32(Address));
        }
        Assert.Equal(0u, staging.ReadUInt32(Address));
        Assert.Equal(0u, staging.ReadUInt32(Address + 8));
    }

    [Fact]
    public void Accepted_staging_prefix_survives_both_nested_operations_and_later_suffix_failure()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        using (NativeBattleTaskOperation outer = new([]))
        {
            outer.AdmitStaging(staging);
            staging.WriteUInt32(Address, 1);
            using (NativeBattleTaskOperation inner = new([]))
            {
                inner.AdmitStaging(staging);
                staging.WriteUInt32(Address, 2);
                staging.MarkUnknown(Address + 8, 4);
                NativeBattleTaskOperation.AcceptPublishedStaging(staging);
                staging.WriteUInt32(Address, 3);
                staging.WriteUInt32(Address + 8, 4);
            }
            Assert.Equal(2u, staging.ReadUInt32(Address));
            Assert.Throws<NotSupportedException>(() => staging.ReadUInt32(Address + 8));
        }
        Assert.Equal(2u, staging.ReadUInt32(Address));
        Assert.Throws<NotSupportedException>(() => staging.ReadUInt32(Address + 8));
        Assert.False(staging.WorkingOwner.HasCheckpoint);
    }

    [Fact]
    public void Dirty_view_does_not_accept_owner_prefix_but_clean_frozen_base_with_registered_view_can_accept()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        using (NativeBattleTaskOperation operation = new([]))
        {
            operation.AdmitStaging(staging);
            staging.WriteUInt32(Address, 1);
            using NativeBattleFileStagingTransaction transaction = new(staging);
            transaction.View.WriteUInt32(Address, 2);
            NativeBattleTaskOperation.AcceptPublishedStaging(transaction.View);
            Assert.Equal(1u, staging.ReadUInt32(Address));
            NativeBattleTaskOperation.AcceptPublishedStaging(staging);
            transaction.Freeze(transaction.View);
            NativeBattleTaskOperation.AcceptPublishedStaging(staging);
            staging.WriteUInt32(Address, 3);
        }
        Assert.Equal(2u, staging.ReadUInt32(Address));
    }

    [Fact]
    public void Rollback_diagnostic_failure_finishes_every_page_metadata_and_ambient_operation()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        uint originalBase = staging.BaseAddress;
        using NativeBattleTaskOperation operation = new([]);
        operation.AdmitStaging(staging);
        staging.Rebase(originalBase - 4096);
        staging.WriteUInt32(Address, 17);
        staging.WriteUInt32(Address + 0x20000, 23);
        staging.MarkUnknown(Address + 8, 4);
        staging.WriteResourcePointer(Address + 16, new(null, null) { CpuRow = 2 });
        using (NativeBattleTelemetry.BeginScope(new FailingRollbackTraffic()))
        {
            Assert.Throws<InvalidOperationException>(() => operation.Dispose());
        }
        Assert.False(NativeBattleTaskOperation.IsActive);
        Assert.False(staging.WorkingOwner.HasCheckpoint);
        Assert.Equal(originalBase, staging.BaseAddress);
        Assert.Equal(0u, staging.ReadUInt32(Address));
        Assert.Equal(0u, staging.ReadUInt32(Address + 0x20000));
        Assert.Equal(0u, staging.ReadUInt32(Address + 8));
        Assert.False(staging.WorkingOwner.TryGetPointer(Address + 16, out _));
    }

    [Fact]
    public void Warmed_nested_owner_checkpoints_reuse_page_and_metadata_journal_storage()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().WriteUInt32(Address, 0);
        NativeBattleFileStagingWorkingOwner owner = staging.WorkingOwner;
        owner.BeginCheckpoint();
        for (uint value = 1; value <= 1000; value++)
        {
            owner.BeginNestedCheckpoint();
            staging.WriteUInt32(Address, value);
            owner.RollbackNestedCheckpoint();
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint value = 1; value <= 1000; value++)
        {
            owner.BeginNestedCheckpoint();
            staging.WriteUInt32(Address, value);
            owner.RollbackNestedCheckpoint();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        owner.RollbackCheckpoint();
        Assert.Equal(0u, staging.ReadUInt32(Address));
    }

    private sealed class FailingRollbackTraffic : INativeBattleTelemetrySink
    {
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1005 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                throw new InvalidOperationException("Rollback diagnostic sink failed.");
            }
        }
    }
}
