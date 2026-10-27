using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTaskOrderingOperationTests
{
    [Fact]
    public void Later_failure_restores_source_main_tasks_and_ordering_after_successful_local_helper_freezes()
    {
        var fixture = Fixture();
        string original = Snapshot(fixture.Ordering);
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleTaskOperation operation = new(fixture.Memory, fixture.Effect);
            operation.AdmitOrdering(fixture.Ordering);
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(fixture.Source);
            using NativeBattleNodeBankOperation nodes = new(fixture.Source);
            nodes.WriteUInt32(0, 11);
            nodes.Publish();
            fixture.Memory.Arena!.WriteUInt32(0, 12);
            fixture.Pool.Append(0x1234);
            fixture.Ordering.Prepend(17, new("20DFAB8", 0, 0), [0x01000000, 0xE6000000], 0, []);
            throw new InvalidOperationException("Later unpublished helper failed.");
        }));
        Assert.Equal(0u, fixture.Source.ReadUInt32(0));
        Assert.Equal(0u, fixture.Memory.Arena!.ReadUInt32(0));
        Assert.Null(fixture.Pool.Head);
        Assert.Equal(original, Snapshot(fixture.Ordering));
    }

    [Fact]
    public void Native_publication_keeps_accepted_prefix_after_a_later_local_producer_freeze_and_failure()
    {
        var fixture = Fixture();
        NativeBattleLogicalOrdering expected = Packet(NativeBattleLogicalOrdering.Clear(), 0);
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleTaskOperation operation = new(fixture.Memory, fixture.Effect);
            operation.AdmitOrdering(fixture.Ordering);
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(fixture.Source);
            using NativeBattleLogicalOrderingTransaction producer = new(fixture.Ordering);
            fixture.Source.Arena.WriteUInt32(0, 1);
            fixture.Memory.Arena!.WriteUInt32(0, 1);
            fixture.Pool.Append(0x1234);
            Packet(producer.View, 0);
            producer.Freeze();
            NativeBattleTaskOperation.AcceptPublishedEffect(fixture.Effect, fixture.Memory, producer.View);
            fixture.Source.Arena.WriteUInt32(0, 2);
            fixture.Memory.Arena.WriteUInt32(0, 2);
            fixture.Pool.Slots[0].WriteDword(12, 2);
            Packet(producer.View, 16);
            producer.Freeze();
            throw new InvalidOperationException("Later unpublished callback failed.");
        }));
        Assert.Equal(1u, fixture.Source.ReadUInt32(0));
        Assert.Equal(1u, fixture.Memory.Arena!.ReadUInt32(0));
        Assert.Equal(0, fixture.Pool.Head);
        Assert.Equal(0x1234u, fixture.Pool.Slots[0].Callback);
        Assert.Equal(0u, fixture.Pool.Slots[0].ReadDword(12));
        Assert.Equal(Snapshot(expected), Snapshot(fixture.Ordering));
    }

    [Fact]
    public void Dirty_native_producer_is_rejected_before_any_other_domain_accepts()
    {
        var fixture = Fixture();
        string original = Snapshot(fixture.Ordering);
        using (NativeBattleTaskOperation operation = new(fixture.Memory, fixture.Effect))
        {
            operation.AdmitOrdering(fixture.Ordering);
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(fixture.Source);
            using NativeBattleLogicalOrderingTransaction producer = new(fixture.Ordering);
            fixture.Source.Arena.WriteUInt32(0, 7);
            fixture.Memory.Arena!.WriteUInt32(0, 8);
            Packet(producer.View, 0);
            Assert.Throws<InvalidOperationException>(() => NativeBattleTaskOperation.AcceptPublishedEffect(fixture.Effect, fixture.Memory, producer.View));
        }
        Assert.Equal(0u, fixture.Source.ReadUInt32(0));
        Assert.Equal(0u, fixture.Memory.Arena!.ReadUInt32(0));
        Assert.Equal(original, Snapshot(fixture.Ordering));
    }

    [Fact]
    public void Ordering_acceptance_survives_a_kernel_commit_diagnostic_error_after_physical_commit()
    {
        var fixture = Fixture();
        NativeBattleLogicalOrdering expected = Packet(NativeBattleLogicalOrdering.Clear(), 0);
        using (NativeBattleTaskOperation operation = new(fixture.Memory, fixture.Effect))
        {
            operation.AdmitOrdering(fixture.Ordering);
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(fixture.Source);
            using NativeBattleLogicalOrderingTransaction producer = new(fixture.Ordering);
            fixture.Source.Arena.WriteUInt32(0, 1);
            fixture.Memory.Arena!.WriteUInt32(0, 1);
            fixture.Memory.Arena.Working.EnlistCheckpointParticipant(new FailingCommit());
            Packet(producer.View, 0);
            producer.Freeze();
            Assert.Throws<InvalidOperationException>(() => NativeBattleTaskOperation.AcceptPublishedEffect(fixture.Effect, fixture.Memory, producer.View));
            fixture.Source.Arena.WriteUInt32(0, 2);
            fixture.Memory.Arena.WriteUInt32(0, 2);
            Packet(producer.View, 16);
            producer.Freeze();
        }
        Assert.Equal(1u, fixture.Source.ReadUInt32(0));
        Assert.Equal(1u, fixture.Memory.Arena!.ReadUInt32(0));
        Assert.Equal(Snapshot(expected), Snapshot(fixture.Ordering));
    }

    [Fact]
    public void Admission_rejects_unknown_native_scope_or_mid_callback_owner_replacement()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        using (NativeBattleLogicalOrderingTransaction producer = new(ordering))
        {
            using NativeBattleTaskOperation operation = new([]);
            Assert.Throws<InvalidOperationException>(() => operation.AdmitOrdering(ordering));
        }
        using (NativeBattleTaskOperation operation = new([]))
        {
            operation.AdmitOrdering(ordering);
            Assert.Throws<InvalidOperationException>(() => operation.AdmitOrdering(NativeBattleLogicalOrdering.Clear()));
            Assert.Throws<InvalidOperationException>(() => NativeBattleTaskOperation.AcceptPublishedOrdering(NativeBattleLogicalOrdering.Clear()));
        }
        Assert.Empty(ordering.Packets);
    }

    private static NativeBattleLogicalOrdering Packet(NativeBattleLogicalOrdering ordering, int offset) => ordering.Prepend(17, new("20DFAB8", 0, offset), [0x01000000, 0xE6000000], 0, []);
    private static string Snapshot(NativeBattleLogicalOrdering ordering) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleEffectTaskPool Pool, NativeBattleNodeSource Source, NativeBattleLogicalOrdering Ordering) Fixture()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x3000000, 0x3100000, 24, 1);
        NativeBattleEffectMemory memory = new(NativeBattleEffectArena.Clear(), 0, 0, [], 0, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(1, 0, new(1, 1, 0), null, null) { Pools = [pool] };
        return (memory, effect, pool, new([0, 0, 0, 0]), NativeBattleLogicalOrdering.Clear());
    }

    private sealed class FailingCommit : INativeBattleCheckpointParticipant
    {
        public void BeginCheckpoint()
        {
        }
        public void CommitCheckpoint() => throw new InvalidOperationException("Commit diagnostic failed.");
        public void RollbackCheckpoint()
        {
        }
    }
}
