using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTaskOperationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Newly_reached_retained_arena_enlists_before_its_public_overlay_and_rolls_back_with_nested_passes(bool nested)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena.AdmitRetained(new());
        using (NativeBattleTaskOperation outer = new([]))
        {
            if (nested)
            {
                using NativeBattleTaskOperation inner = new([]);
                Publish();
                inner.Commit();
            }
            else
            {
                Publish();
            }
            Assert.Equal(10u, arena.ReadUInt32(0));
        }
        Assert.Equal(0u, arena.ReadUInt32(0));
        Assert.False(arena.Working.HasCheckpoint);

        void Publish()
        {
            using NativeBattleEffectArenaTransaction transaction = new(arena);
            transaction.View.WriteUInt32(0, 10);
            Assert.Equal(0u, arena.Working.ReadUInt32(0));
            transaction.Freeze();
        }
    }

    [Fact]
    public void Newly_reached_retained_arena_keeps_accepted_prefix_and_rolls_back_later_overlay_publication()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena.AdmitRetained(new());
        using (NativeBattleTaskOperation operation = new([]))
        {
            using (NativeBattleEffectArenaTransaction prefix = new(arena))
            {
                prefix.View.WriteUInt32(0, 10);
                prefix.Freeze();
            }
            NativeBattleTaskOperation.AcceptPublishedArena(arena.Working);
            using (NativeBattleEffectArenaTransaction suffix = new(arena))
            {
                suffix.View.WriteUInt32(0, 20);
                suffix.View.WriteUInt32(4, 30);
                suffix.Freeze();
            }
        }
        Assert.Equal(10u, arena.ReadUInt32(0));
        Assert.Equal(0u, arena.ReadUInt32(4));
        Assert.False(arena.Working.HasCheckpoint);
    }

    [Fact]
    public void Standalone_failed_pass_rolls_back_completed_callbacks_queue_removals_and_same_pass_appends()
    {
        NativeBattleEffectTaskPool pool = Pool(0x3000000, 0x3100000, 3);
        pool.Append(1);
        pool.Append(2);
        Assert.Throws<InvalidOperationException>(() => pool.Advance((current, slot) =>
        {
            current.Slots[slot].WriteDword(12, (uint)(slot + 10));
            if (slot == 0)
            {
                current.Append(3);
                return new(current, 2);
            }
            throw new InvalidOperationException("Second callback failed.");
        }));
        Assert.Equal(0, pool.Head);
        Assert.Equal(1, pool.Tail);
        Assert.Equal(1, pool.Slots[0].Next);
        Assert.Null(pool.Slots[1].Next);
        Assert.Equal((ushort)1, pool.Slots[0].Flags);
        Assert.Equal((ushort)0, pool.Slots[2].Flags);
        Assert.All(pool.Slots, task => Assert.Equal(0u, task.ReadDword(12)));
    }

    [Fact]
    public void Hierarchy_failure_rolls_back_a_successful_nested_pass_in_an_independent_child_kernel()
    {
        NativeBattleEffectTaskPool root = Pool(0x3000000, 0x3100000, 2);
        NativeBattleEffectTaskPool child = Pool(0x3200000, 0x3300000, 1);
        root.Append(1);
        root.Append(2);
        child.Append(3);
        NativeBattleEffectHierarchy hierarchy = new([root, child]);
        Assert.Throws<InvalidOperationException>(() => hierarchy.Advance(0, (current, address) =>
        {
            if (address != root.BankAddress)
            {
                throw new InvalidOperationException("Later root callback failed.");
            }
            current.Read(address).WriteDword(12, 10);
            NativeBattleEffectHierarchyStep nested = current.Advance(1, (inner, selected) =>
            {
                inner.Read(selected).WriteDword(12, 20);
                return new(inner, 0);
            });
            return new(nested.Hierarchy, 0);
        }));
        Assert.Equal(0u, root.Slots[0].ReadDword(12));
        Assert.Equal(0u, child.Slots[0].ReadDword(12));
        Assert.Equal(0, child.Head);
    }

    [Fact]
    public void Explicit_caller_journal_keeps_its_accepted_prefix_when_a_later_callback_fails()
    {
        NativeBattleEffectTaskPool pool = Pool(0x3000000, 0x3100000, 2);
        pool.Append(1);
        pool.Append(2);
        pool.BeginCheckpoint();
        Assert.Throws<InvalidOperationException>(() => pool.Advance((current, slot) =>
        {
            current.Slots[slot].WriteDword(12, (uint)(slot + 10));
            if (slot == 0)
            {
                current.CommitCheckpoint();
                current.BeginCheckpoint();
                return new(current, 0);
            }
            throw new InvalidOperationException("Unaccepted callback failed.");
        }));
        Assert.True(pool.HasCheckpoint);
        pool.RollbackCheckpoint();
        Assert.Equal(10u, pool.Slots[0].ReadDword(12));
        Assert.Equal(0u, pool.Slots[1].ReadDword(12));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dynamic_binding_restores_prior_owner_on_failure_and_retires_it_only_after_success(bool commit)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena.WriteUInt32(0x100 + 12, 88);
        NativeBattleEffectTaskPool root = Pool(0x3000000, 0x3100000, 1);
        root.Append(1);
        NativeBattleEffectTaskPool born = Pool(NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 0x3200000, 1);
        born.Append(2);
        NativeBattleModulePoolTask stable = born.Slots[0].WriteDword(12, 77);
        NativeBattleEffectArena oldBank = NativeBattleEffectTaskMemory.CaptureBank(born);
        NativeBattleEffectTaskStep Advance() => root.Advance((current, slot) =>
        {
            born.BindArena(arena);
            Assert.Throws<ObjectDisposedException>(() => oldBank.ReadUInt32(12));
            stable.WriteDword(12, 99);
            if (!commit)
            {
                throw new InvalidOperationException("Binding callback failed.");
            }
            return new(current, 0);
        });
        if (commit)
        {
            Advance();
            Assert.True(born.IsBoundToStorage(arena, NativeBattleEffectNodeMemory.ArenaAddress));
            Assert.Equal(99u, arena.ReadUInt32(0x100 + 12));
            Assert.Throws<ObjectDisposedException>(() => oldBank.ReadUInt32(12));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => Advance());
            Assert.False(born.IsBoundToStorage(arena, NativeBattleEffectNodeMemory.ArenaAddress));
            Assert.Equal(77u, stable.ReadDword(12));
            Assert.Equal(77u, oldBank.ReadUInt32(12));
            Assert.Equal(88u, arena.ReadUInt32(0x100 + 12));
        }
    }

    [Fact]
    public void Same_kernel_binding_rolls_back_control_without_suspending_or_destroying_its_bytes()
    {
        NativeBattleEffectTaskPool root = Pool(0x3000000, 0x3100000, 1);
        root.Append(1);
        NativeBattleEffectTaskPool born = Pool(0x3200000, 0x3300000, 1);
        NativeBattleEffectArena bank = NativeBattleEffectTaskMemory.CaptureBank(born);
        Assert.Throws<InvalidOperationException>(() => root.Advance((current, slot) =>
        {
            born.BindStorage(bank, born.BankAddress);
            born.Slots[0].WriteDword(12, 123);
            throw new InvalidOperationException("Binding failed.");
        }));
        Assert.False(born.IsBoundToStorage(bank, born.BankAddress));
        Assert.Equal(0u, born.Slots[0].ReadDword(12));
        Assert.Equal(0u, bank.ReadUInt32(12));
    }

    [Fact]
    public void Same_kernel_binding_inside_its_own_pass_preserves_the_operation_checkpoint()
    {
        NativeBattleEffectTaskPool pool = Pool(0x3000000, 0x3100000, 1);
        pool.Append(1);
        NativeBattleEffectArena bank = NativeBattleEffectTaskMemory.CaptureBank(pool);
        Assert.Throws<InvalidOperationException>(() => pool.Advance((current, slot) =>
        {
            current.BindStorage(bank, current.BankAddress);
            current.Slots[slot].WriteDword(12, 123);
            throw new InvalidOperationException("Same-owner callback failed.");
        }));
        Assert.False(pool.IsBoundToStorage(bank, pool.BankAddress));
        Assert.Equal(0u, bank.ReadUInt32(12));
        Assert.Equal(0, pool.Head);
    }

    [Fact]
    public void Published_main_kernel_prefix_survives_nested_failure_while_unpublished_private_kernels_roll_back()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool main = Pool(NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 0x3100000, 1).BindArena(arena);
        NativeBattleEffectTaskPool privatePool = Pool(0x3200000, 0x3300000, 1);
        using (NativeBattleTaskOperation outer = new([main, privatePool]))
        {
            main.Slots[0].WriteDword(12, 1);
            privatePool.Slots[0].WriteDword(12, 10);
            using (NativeBattleTaskOperation inner = new([main, privatePool]))
            {
                main.Slots[0].WriteDword(12, 2);
                privatePool.Slots[0].WriteDword(12, 20);
                NativeBattleTaskOperation.AcceptPublishedArena(arena.Working);
                main.Slots[0].WriteDword(12, 3);
                privatePool.Slots[0].WriteDword(12, 30);
            }
            Assert.Equal(2u, main.Slots[0].ReadDword(12));
            Assert.Equal(10u, privatePool.Slots[0].ReadDword(12));
        }
        Assert.Equal(2u, arena.ReadUInt32(0x100 + 12));
        Assert.Equal(0u, privatePool.Slots[0].ReadDword(12));
    }

    [Fact]
    public void Memory_publication_does_not_accept_an_explicit_caller_pool_scope()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = Pool(NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 0x3100000, 1).BindArena(arena);
        pool.BeginCheckpoint();
        using (NativeBattleTaskOperation operation = new([pool]))
        {
            pool.Slots[0].WriteDword(12, 10);
            NativeBattleTaskOperation.AcceptPublishedArena(arena.Working);
            Assert.True(pool.HasCheckpoint);
            operation.Commit();
        }
        pool.RollbackCheckpoint();
        Assert.Equal(0u, pool.Slots[0].ReadDword(12));
    }

    [Fact]
    public void Publication_rejects_a_foreign_nested_marker_before_accepting_any_automatic_delta()
    {
        NativeBattleEffectTaskPool pool = Pool(0x3000000, 0x3100000, 1);
        using (NativeBattleTaskOperation operation = new([pool]))
        {
            pool.Slots[0].WriteDword(12, 10);
            pool.CheckpointStorage.BeginNestedCheckpoint();
            Assert.Throws<InvalidOperationException>(() => NativeBattleTaskOperation.AcceptPublishedArena(pool.CheckpointStorage));
            pool.CheckpointStorage.RollbackNestedCheckpoint();
        }
        Assert.Equal(0u, pool.Slots[0].ReadDword(12));
    }

    private static NativeBattleEffectTaskPool Pool(uint bank, uint descriptor, int count) => NativeBattleEffectTaskPool.Initialize(descriptor, bank, 48, checked((ushort)count));
}
