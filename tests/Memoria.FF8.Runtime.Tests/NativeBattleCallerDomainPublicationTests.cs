using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCallerDomainPublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_caller_prefix_survives_later_root_failure_in_main_tasks_ordering_and_staging(bool complete)
    {
        CheckNativePrefix(complete, false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Commit_diagnostic_finishes_all_domains_before_reporting_accepted_native_prefix(bool complete)
    {
        CheckNativePrefix(complete, true);
    }

    private static void CheckNativePrefix(bool complete, bool failCommit)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectMemory memory = new(arena, 0, 0, [], 0, 0, 0, 0);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x3000000, 0x3100000, 24, 1);
        NativeBattleOriginalEffectState effect = new(1, 0, new(1, 1, 0), null, null) { Pools = [pool] };
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess();
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleTaskOperation root = new(memory, effect);
            root.AdmitOrdering(ordering);
            root.AdmitStaging(staging);
            using NativeBattleEffectArenaTransaction packets = new(arena);
            using NativeBattleTaskCallerOperation caller = new(packets, [pool]);
            using NativeBattleLogicalOrderingTransaction pending = new(ordering);
            using NativeBattleFileStagingTransaction files = new(staging);
            packets.View.WriteUInt32(0, 1);
            pool.Append(0x1234);
            Packet(pending.View, 0);
            pending.Freeze();
            files.View.WriteUInt32(NativeBattleFileStaging.Address, 1);
            NativeBattleFileStaging publishedFiles = files.Freeze(files.View);
            if (failCommit)
            {
                arena.Working.EnlistCheckpointParticipant(new FailingCommit());
            }
            Action publish = complete ? () => caller.Complete(pending.View, publishedFiles) : () => caller.AcceptPrefix(pending.View, publishedFiles);
            if (failCommit)
            {
                Assert.Throws<InvalidOperationException>(publish);
            }
            else
            {
                publish();
            }
            Assert.Equal(1, caller.AcceptedPrefixes);
            packets.View.WriteUInt32(0, 2);
            pool.Slots[0].WriteDword(12, 2);
            Packet(pending.View, 16);
            pending.Freeze();
            files.View.WriteUInt32(NativeBattleFileStaging.Address, 2);
            throw new InvalidOperationException("Later root callback failed.");
        }));
        Assert.False(NativeBattleTaskOperation.IsActive);
        Assert.False(NativeBattleTaskCallerOperation.IsActive);
        Assert.Equal(1u, arena.ReadUInt32(0));
        Assert.Equal(1u, staging.ReadUInt32(NativeBattleFileStaging.Address));
        Assert.False(staging.WorkingOwner.HasCheckpoint);
        Assert.Equal(0, pool.Head);
        Assert.Equal(0x1234u, pool.Slots[0].Callback);
        Assert.Equal(0u, pool.Slots[0].ReadDword(12));
        Assert.Equal(Snapshot(Packet(NativeBattleLogicalOrdering.Clear(), 0)), Snapshot(ordering));
    }

    private static NativeBattleLogicalOrdering Packet(NativeBattleLogicalOrdering ordering, int offset) => ordering.Prepend(17, new("20DFAB8", 0, offset), [0x01000000, 0xE6000000], 0, []);
    private static string Snapshot(NativeBattleLogicalOrdering ordering) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));

    private sealed class FailingCommit : INativeBattleCheckpointParticipant
    {
        public void BeginCheckpoint()
        {
        }
        public void CommitCheckpoint() => throw new InvalidOperationException("Accepted commit diagnostic failed.");
        public void RollbackCheckpoint()
        {
        }
    }
}
