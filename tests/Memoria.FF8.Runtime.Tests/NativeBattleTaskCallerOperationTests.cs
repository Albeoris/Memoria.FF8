namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTaskCallerOperationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Late_public_overlay_binding_tracks_owner_lifetime_and_rolls_back_only_the_unaccepted_suffix(bool publish)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool root = Pool(0x3000000, 0x3100000);
        root.Append(1);
        NativeBattleEffectTaskPool born = Pool(NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 0x3200000);
        NativeBattleModulePoolTask stable = born.Slots[0].WriteDword(12, 77);
        NativeBattleEffectArena oldBank = NativeBattleEffectTaskMemory.CaptureBank(born);
        using NativeBattleEffectArenaTransaction transaction = new(arena);
        using (NativeBattleTaskCallerOperation caller = new(transaction, [root]))
        {
            Assert.Throws<InvalidOperationException>(() => root.Advance((current, slot) =>
            {
                current.Slots[slot].WriteDword(12, 9);
                born.BindArena(transaction.View);
                stable.WriteDword(12, 17);
                Assert.Equal(17u, transaction.View.ReadUInt32(0x100 + 12));
                Assert.Equal(0u, arena.ReadUInt32(0x100 + 12));
                Assert.Throws<ObjectDisposedException>(() => oldBank.ReadUInt32(12));
                if (publish)
                {
                    caller.Admit([root, born]);
                    caller.AcceptPrefix();
                }
                stable.WriteDword(12, 99);
                current.Slots[slot].WriteDword(12, 100);
                throw new InvalidOperationException("Unpublished service suffix failed.");
            }));
        }
        Assert.False(NativeBattleTaskCallerOperation.IsActive);
        Assert.Equal(publish ? 17u : 0u, arena.ReadUInt32(0x100 + 12));
        Assert.Equal(publish ? 9u : 0u, root.Slots[0].ReadDword(12));
        Assert.Equal(publish, born.IsBoundToStorage(arena, NativeBattleEffectNodeMemory.ArenaAddress));
        if (publish)
        {
            Assert.Equal(17u, stable.ReadDword(12));
            Assert.Throws<ObjectDisposedException>(() => oldBank.ReadUInt32(12));
        }
        else
        {
            Assert.Equal(77u, stable.ReadDword(12));
            Assert.Equal(77u, oldBank.ReadUInt32(12));
        }
    }

    [Fact]
    public void Accepted_effect_metadata_keeps_new_banks_reachable_without_publishing_local_render_fields()
    {
        NativeBattleEffectTaskPool root = Pool(0x3000000, 0x3100000);
        NativeBattleEffectTaskPool born = Pool(NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 0x3200000);
        NativeBattleOriginalEffectState before = new(274, 273, new(274, 0, 0), null, null) { Pools = [root] };
        NativeBattleOriginalEffectState accepted = before with { Pools = [root, born] };
        NativeBattleGraphicsState published = new([], [], 7, 42) { OriginalEffect = before };
        NativeBattleGraphicsState local = published with { Flags = 99, Callback = 100 };

        NativeBattleGraphicsState retained = NativeBattleTaskCallerOperation.PublishEffectMetadata(published, accepted);

        Assert.Same(accepted, retained.OriginalEffect);
        Assert.Same(born, retained.OriginalEffect!.Pools[1]);
        Assert.Equal(published.Flags, retained.Flags);
        Assert.Equal(published.Callback, retained.Callback);
        Assert.NotEqual(local.Flags, retained.Flags);
        Assert.Equal(published.Models, retained.Models);
        Assert.Same(published.PrimitiveFrame, retained.PrimitiveFrame);
    }

    [Fact]
    public void Caller_prefix_commit_finishes_and_reopens_all_kernels_before_reporting_participant_diagnostics()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool root = Pool(0x3000000, 0x3100000);
        using NativeBattleEffectArenaTransaction transaction = new(arena);
        using (NativeBattleTaskCallerOperation caller = new(transaction, [root]))
        {
            FaultParticipant fault = new(commit: true, rollback: false);
            root.CheckpointStorage.EnlistCheckpointParticipant(fault);
            transaction.View.WriteUInt32(0x100, 17);
            root.Slots[0].WriteDword(12, 23);
            Assert.Throws<DiagnosticException>(() => caller.AcceptPrefix());
            Assert.Equal(1, caller.AcceptedPrefixes);
            Assert.Equal(0, fault.Frames);
            Assert.Equal(1, arena.Working.CheckpointDepth);
            Assert.Equal(1, root.CheckpointStorage.CheckpointDepth);
            transaction.View.WriteUInt32(0x100, 99);
            root.Slots[0].WriteDword(12, 100);
        }
        Assert.Equal(17u, arena.ReadUInt32(0x100));
        Assert.Equal(23u, root.Slots[0].ReadDword(12));
    }

    [Fact]
    public void Automatic_commit_and_rollback_finish_all_kernels_and_restore_ambient_state_after_diagnostics()
    {
        foreach (bool commit in new[] { false, true })
        {
            NativeBattleEffectTaskPool first = Pool(0x3000000, 0x3100000);
            NativeBattleEffectTaskPool second = Pool(0x3200000, 0x3300000);
            NativeBattleTaskOperation operation = new([first, second]);
            FaultParticipant a = new(commit, !commit);
            FaultParticipant b = new(commit, !commit);
            first.CheckpointStorage.EnlistCheckpointParticipant(a);
            second.CheckpointStorage.EnlistCheckpointParticipant(b);
            first.Slots[0].WriteDword(12, 17);
            second.Slots[0].WriteDword(12, 23);
            if (commit)
            {
                Assert.Throws<DiagnosticException>(() => operation.Commit());
            }
            else
            {
                Assert.Throws<DiagnosticException>(() => operation.Dispose());
            }
            Assert.False(NativeBattleTaskOperation.IsActive);
            Assert.Equal(0, a.Frames);
            Assert.Equal(0, b.Frames);
            Assert.Equal(0, first.CheckpointStorage.CheckpointDepth);
            Assert.Equal(0, second.CheckpointStorage.CheckpointDepth);
            Assert.Equal(commit ? 17u : 0u, first.Slots[0].ReadDword(12));
            Assert.Equal(commit ? 23u : 0u, second.Slots[0].ReadDword(12));
            operation.Dispose();
        }
    }

    [Fact]
    public void Automatic_nested_acceptance_reopens_all_matching_marks_after_commit_diagnostics()
    {
        NativeBattleEffectTaskPool pool = Pool(0x3000000, 0x3100000);
        using (NativeBattleTaskOperation outer = new([pool]))
        {
            using (NativeBattleTaskOperation inner = new([pool]))
            {
                FaultParticipant fault = new(commit: true, rollback: false);
                pool.CheckpointStorage.EnlistCheckpointParticipant(fault);
                pool.Slots[0].WriteDword(12, 17);
                Assert.Throws<DiagnosticException>(() => NativeBattleTaskOperation.AcceptPublishedArena(pool.CheckpointStorage));
                Assert.Equal(0, fault.Frames);
                Assert.Equal(2, pool.CheckpointStorage.CheckpointDepth);
                pool.Slots[0].WriteDword(12, 99);
            }
            Assert.Equal(17u, pool.Slots[0].ReadDword(12));
        }
        Assert.Equal(17u, pool.Slots[0].ReadDword(12));
    }

    [Fact]
    public void Explicit_caller_acceptance_rebases_automatic_ancestors_before_later_pass_failure()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool main = Pool(NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 0x3100000).BindArena(arena);
        NativeBattleEffectTaskPool privatePool = Pool(0x3200000, 0x3300000);
        using (NativeBattleTaskOperation root = new([main, privatePool]))
        {
            using NativeBattleEffectArenaTransaction transaction = new(arena);
            using (NativeBattleTaskCallerOperation caller = new(transaction, [main, privatePool]))
            {
                main.Slots[0].WriteDword(12, 17);
                privatePool.Slots[0].WriteDword(12, 23);
                caller.AcceptPrefix();
                Assert.Equal(2, arena.Working.CheckpointDepth);
                Assert.Equal(2, privatePool.CheckpointStorage.CheckpointDepth);
                main.Slots[0].WriteDword(12, 99);
                privatePool.Slots[0].WriteDword(12, 100);
            }
            Assert.Equal(17u, main.Slots[0].ReadDword(12));
            Assert.Equal(23u, privatePool.Slots[0].ReadDword(12));
        }
        Assert.Equal(17u, arena.ReadUInt32(0x100 + 12));
        Assert.Equal(23u, privatePool.Slots[0].ReadDword(12));
    }

    [Fact]
    public void Frozen_empty_public_transaction_can_handoff_its_prefix_to_an_automatic_ancestor()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 0x3100000).BindArena(arena);
        using (NativeBattleTaskOperation operation = new([pool]))
        {
            using (NativeBattleEffectArenaTransaction transaction = new(arena))
            {
                transaction.BeginCheckpoint();
                transaction.View.WriteUInt32(0x100 + 12, 17);
                Assert.Throws<InvalidOperationException>(() => NativeBattleTaskOperation.AcceptPublishedArena(arena.Working));
                transaction.CommitCheckpoint();
                transaction.Freeze();
                NativeBattleTaskOperation.AcceptPublishedArena(arena.Working);
            }
            pool.Slots[0].WriteDword(12, 99);
        }
        Assert.Equal(17u, pool.Slots[0].ReadDword(12));
    }

    private static NativeBattleEffectTaskPool Pool(uint bank, uint descriptor) => NativeBattleEffectTaskPool.Initialize(descriptor, bank, 32, 1);
    private sealed class DiagnosticException : Exception
    {
    }
    private sealed class FaultParticipant(bool commit, bool rollback) : INativeBattleCheckpointParticipant
    {
        public int Frames { get; private set; }
        public void BeginCheckpoint() => Frames++;
        public void CommitCheckpoint()
        {
            Frames--;
            if (commit)
            {
                throw new DiagnosticException();
            }
        }
        public void RollbackCheckpoint()
        {
            Frames--;
            if (rollback)
            {
                throw new DiagnosticException();
            }
        }
    }
}
