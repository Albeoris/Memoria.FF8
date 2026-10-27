namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlSecondaryCallerTests
{
    [Fact]
    public void Public_arena_requires_pool_admission_before_entering_Advance_not_inside_its_callback()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521728, NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 32, 2).BindArena(arena);
        pool.Append(1);
        using NativeBattleEffectArenaTransaction transaction = new(arena);
        bool entered = false;
        Assert.Throws<InvalidOperationException>(() => pool.Advance((current, slot) =>
        {
            entered = true;
            return new(current, 0);
        }));
        Assert.False(entered);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_secondary_caller_publishes_completed_callback_prefix_after_later_callback_failure(bool provisional)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521728, NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 32, 2).BindArena(arena);
        pool.Append(1);
        pool.Append(2);
        NativeBattleLogicalOrdering pending = NativeBattleLogicalOrdering.Clear();
        using (NativeBattleTaskOperation root = new([pool]))
        {
            root.AdmitOrdering(pending);
            using NativeBattleLogicalOrderingTransaction? continuation = provisional ? new(pending) : null;
            if (continuation is not null)
            {
                Packet(continuation.View, 0);
            }
            using NativeBattleEffectArenaTransaction transaction = new(arena);
            using NativeBattleTaskCallerOperation caller = new(transaction, [pool]);
            using NativeBattleLogicalOrderingTransaction ordering = new(pending);
            bool[] owned = new bool[1];
            bool active = false;
            Assert.Throws<InvalidDataException>(() =>
            {
                try
                {
                    pool.Advance((current, slot) =>
                    {
                        ordering.BeginCheckpoint();
                        NativeBattleTaskPoolCheckpoints.Begin([pool], owned);
                        active = true;
                        current.Slots[slot].WriteDword(12, (uint)(17 + slot));
                        Packet(ordering.View, 16 + slot * 16);
                        if (slot == 1)
                        {
                            throw new InvalidDataException("Later secondary callback failed before publication.");
                        }
                        NativeBattleTaskPoolCheckpoints.Commit([pool], owned);
                        caller.CommitOverlaySuffix();
                        ordering.CommitCheckpoint();
                        active = false;
                        Assert.Equal(0u, arena.ReadUInt32(0x100 + 12));
                        Assert.Equal(17u, transaction.View.ReadUInt32(0x100 + 12));
                        return new(current, 0);
                    });
                }
                catch
                {
                    if (active)
                    {
                        NativeBattleTaskPoolCheckpoints.Rollback([pool], owned);
                        caller.RollbackOverlaySuffix();
                        ordering.RollbackCheckpoint();
                    }
                    throw;
                }
                finally
                {
                    caller.PreparePrefix();
                    NativeBattleLogicalOrdering accepted = BattleSession.FreezeNativeQuezacotlOrderingPublication(ordering.Freeze(), provisional ? pending : null, continuation);
                    caller.Complete(accepted);
                }
            });
        }
        Assert.Equal(17u, pool.Slots[0].ReadDword(12));
        Assert.Equal(0u, pool.Slots[1].ReadDword(12));
        Assert.Equal(1, pool.Slots[0].Next);
        Assert.Null(pool.Slots[1].Next);
        Assert.Equal(provisional ? 2 : 1, pending.Packets.Length);
        Assert.Equal(17u, arena.ReadUInt32(0x100 + 12));
        Assert.Equal(0u, arena.ReadUInt32(0x100 + 32 + 12));
        Assert.False(NativeBattleTaskCallerOperation.IsActive);
        Assert.False(NativeBattleTaskOperation.IsActive);
    }

    [Fact]
    public void Caller_overlay_suffixes_stay_private_until_physical_publication_and_reject_prepared_access()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        using NativeBattleEffectArenaTransaction transaction = new(arena);
        using NativeBattleTaskCallerOperation caller = new(transaction, []);
        transaction.View.WriteUInt32(0x100, 17);
        caller.CommitOverlaySuffix();
        Assert.Equal(0u, arena.ReadUInt32(0x100));
        Assert.Equal(17u, transaction.View.ReadUInt32(0x100));
        transaction.View.WriteUInt32(0x100, 99);
        caller.RollbackOverlaySuffix();
        Assert.Equal(0u, arena.ReadUInt32(0x100));
        Assert.Equal(17u, transaction.View.ReadUInt32(0x100));
        caller.PreparePrefix();
        Assert.Equal(17u, arena.ReadUInt32(0x100));
        Assert.Throws<InvalidOperationException>(() => caller.CommitOverlaySuffix());
        Assert.Throws<InvalidOperationException>(() => caller.RollbackOverlaySuffix());
        caller.Complete();
    }
    private static void Packet(NativeBattleLogicalOrdering pending, int offset) => pending.Prepend(20, new("20DFAB8", 0, offset), [0x01000000, 0xE6000000], 0, []);
}
