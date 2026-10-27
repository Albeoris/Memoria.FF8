using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleTaskPoolCheckpoints
{
    internal static void Begin(ImmutableArray<NativeBattleEffectTaskPool> pools, Span<bool> owned)
    {
        Validate(pools, owned);
        if (owned.Contains(true))
        {
            throw new InvalidOperationException("Task pool checkpoint flags are already in use.");
        }
        try
        {
            for (int index = 0; index < pools.Length; index++)
            {
                if (!pools[index].HasCheckpoint)
                {
                    pools[index].BeginCheckpoint();
                    owned[index] = true;
                }
            }
        }
        catch
        {
            Rollback(pools, owned);
            throw;
        }
    }

    internal static void Commit(ImmutableArray<NativeBattleEffectTaskPool> pools, Span<bool> owned)
    {
        Preflight(pools, owned, false);
        for (int index = pools.Length - 1; index >= 0; index--)
        {
            if (owned[index])
            {
                pools[index].CommitCheckpoint();
                owned[index] = false;
            }
        }
    }

    internal static void Rollback(ImmutableArray<NativeBattleEffectTaskPool> pools, Span<bool> owned)
    {
        Preflight(pools, owned, true);
        for (int index = pools.Length - 1; index >= 0; index--)
        {
            if (owned[index])
            {
                if (pools[index].HasCheckpoint)
                {
                    pools[index].RollbackCheckpoint();
                }
                owned[index] = false;
            }
        }
    }

    private static void Preflight(ImmutableArray<NativeBattleEffectTaskPool> pools, Span<bool> owned, bool allowCompleted)
    {
        Validate(pools, owned);
        for (int index = pools.Length - 1; index >= 0; index--)
        {
            if (!owned[index])
            {
                continue;
            }
            if (!pools[index].HasCheckpoint)
            {
                if (!allowCompleted)
                {
                    throw new InvalidOperationException("Task pool checkpoint was completed outside its group.");
                }
                continue;
            }
            NativeBattleWorkingMemory storage = pools[index].CheckpointStorage;
            int depth = storage.CheckpointDepth;
            for (int later = index + 1; later < pools.Length; later++)
            {
                if (owned[later] && pools[later].HasCheckpoint && ReferenceEquals(storage, pools[later].CheckpointStorage))
                {
                    depth--;
                }
            }
            if (pools[index].CurrentCheckpointDepth != depth)
            {
                throw new InvalidOperationException("Task pool checkpoint group must complete in physical journal order.");
            }
        }
    }

    private static void Validate(ImmutableArray<NativeBattleEffectTaskPool> pools, Span<bool> owned)
    {
        if (pools.IsDefault || owned.Length != pools.Length)
        {
            throw new ArgumentException("Task pool checkpoint flags must match the actual pool collection.");
        }
    }
}
