using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTaskPoolCheckpointTests
{
    [Fact]
    public void Shared_kernel_group_rolls_back_child_bodies_and_descriptors_in_reverse_order()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        ImmutableArray<NativeBattleEffectTaskPool> pools = [Pool(0x100).BindArena(arena), Pool(0x200).BindArena(arena), Pool(0x300).BindArena(arena)];
        bool[] owned = new bool[pools.Length];
        NativeBattleTaskPoolCheckpoints.Begin(pools, owned);
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            pool.Append(0x1234);
            pool.Slots[0].WriteDword(12, 99);
        }
        NativeBattleTaskPoolCheckpoints.Rollback(pools, owned);
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            Assert.Null(pool.Head);
            Assert.Equal(0u, pool.Slots[0].ReadDword(12));
            Assert.False(pool.HasCheckpoint);
        }
        Assert.DoesNotContain(true, owned);
    }

    [Fact]
    public void Existing_parent_scope_is_borrowed_and_retains_rollback_authority_after_group_commit()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        ImmutableArray<NativeBattleEffectTaskPool> pools = [Pool(0x100).BindArena(arena), Pool(0x200).BindArena(arena)];
        pools[0].BeginCheckpoint();
        bool[] owned = new bool[pools.Length];
        NativeBattleTaskPoolCheckpoints.Begin(pools, owned);
        Assert.False(owned[0]);
        Assert.True(owned[1]);
        pools[1].Append(0x1234);
        NativeBattleTaskPoolCheckpoints.Commit(pools, owned);
        Assert.True(pools[0].HasCheckpoint);
        Assert.False(pools[1].HasCheckpoint);
        pools[0].RollbackCheckpoint();
        Assert.Null(pools[1].Head);
    }

    [Fact]
    public void Foreign_nested_scope_blocks_group_commit_before_any_pool_is_committed()
    {
        ImmutableArray<NativeBattleEffectTaskPool> pools = [Pool(0x100), Pool(0x200)];
        bool[] owned = new bool[pools.Length];
        NativeBattleTaskPoolCheckpoints.Begin(pools, owned);
        pools[0].CheckpointStorage.BeginNestedCheckpoint();
        Assert.Throws<InvalidOperationException>(() => NativeBattleTaskPoolCheckpoints.Commit(pools, owned));
        Assert.True(pools[1].HasCheckpoint);
        pools[0].CheckpointStorage.RollbackNestedCheckpoint();
        NativeBattleTaskPoolCheckpoints.Rollback(pools, owned);
    }

    private static NativeBattleEffectTaskPool Pool(int offset) => NativeBattleEffectTaskPool.Initialize(0x3000000u + (uint)offset, NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset, 32, 2);
}
