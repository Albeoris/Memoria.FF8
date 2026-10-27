namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRollbackFailureTests
{
    [Fact]
    public void Descriptor_rollback_diagnostic_failure_still_closes_body_metadata_and_local_scopes()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x3000000, 0x3100000, 24, 2);
        pool.BeginCheckpoint();
        pool.Append(0x1234);
        pool.Slots[0].WriteDword(12, 99);
        using (NativeBattleTelemetry.BeginScope(new FailingRollbackTraffic()))
        {
            Assert.Throws<InvalidOperationException>(() => pool.RollbackCheckpoint());
        }
        Assert.False(pool.HasCheckpoint);
        Assert.False(pool.CheckpointStorage.HasCheckpoint);
        Assert.False(pool.RequireOwner().DescriptorStorage.HasCheckpoint);
        Assert.Null(pool.Head);
        Assert.Null(pool.Tail);
        Assert.Equal((ushort)0, pool.Slots[0].Flags);
        Assert.Equal(0u, pool.Slots[0].ReadDword(12));
        pool.BeginCheckpoint();
        pool.CommitCheckpoint();
    }

    [Fact]
    public void Failed_rollback_diagnostics_do_not_skip_other_kernels_or_leak_the_ambient_operation()
    {
        NativeBattleEffectTaskPool first = NativeBattleEffectTaskPool.Initialize(0x3000000, 0x3100000, 24, 1);
        NativeBattleEffectTaskPool second = NativeBattleEffectTaskPool.Initialize(0x3200000, 0x3300000, 24, 1);
        NativeBattleTaskOperation operation = new([first, second]);
        first.Append(0x1234);
        second.Append(0x5678);
        first.Slots[0].WriteDword(12, 17);
        second.Slots[0].WriteDword(12, 23);
        using (NativeBattleTelemetry.BeginScope(new FailingRollbackTraffic()))
        {
            Assert.Throws<InvalidOperationException>(() => operation.Dispose());
        }
        Assert.False(NativeBattleTaskOperation.IsActive);
        Assert.False(first.CheckpointStorage.HasCheckpoint);
        Assert.False(second.CheckpointStorage.HasCheckpoint);
        Assert.Null(first.Head);
        Assert.Null(second.Head);
        Assert.Equal(0u, first.Slots[0].ReadDword(12));
        Assert.Equal(0u, second.Slots[0].ReadDword(12));
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
