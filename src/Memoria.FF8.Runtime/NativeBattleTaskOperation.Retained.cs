using System.Collections.Immutable;
using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleTaskOperation
{
    private readonly NativeBattleRetainedStorageAuthority? effectAuthority;
    private readonly NativeBattleRetainedStorageAuthority? originalEffectAuthority;

    internal NativeBattleTaskOperation(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) : this(memory, effect.Pools)
    {
        try
        {
            NativeBattleRetainedEffectStorage.AdmitEffect(effect);
            originalEffectAuthority = effect.RetainedAuthority;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal NativeBattleTaskOperation(NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools) : this(AdmitEffectPools(memory, pools))
    {
        effectAuthority = memory.RetainedAuthority;
        try
        {
            AdmitKnownArena(memory.Arena);
            NativeBattleCommonPacketMemory? common = memory.PrimitiveProcess?.CommonPacketMemory;
            AdmitKnownArena(common?.Bank070Buffer0);
            AdmitKnownArena(common?.Bank070Buffer1);
            AdmitKnownArena(common?.Bank094Buffer0);
            AdmitKnownArena(common?.Bank094Buffer1);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static ImmutableArray<NativeBattleEffectTaskPool> AdmitEffectPools(NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools)
    {
        NativeBattleRetainedEffectStorage.Admit(memory);
        return pools;
    }

    private void AdmitKnownArena(NativeBattleEffectArena? arena)
    {
        if (arena is null || arena.IsSnapshot)
        {
            return;
        }
        arena.AdmitRetained(effectAuthority!);
        Enlist(arena.Working);
    }

    internal static NativeBattleTaskOperation ForEffect(NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools) => new(memory, pools);

    internal static NativeBattleEffectArena AdmitCurrentRetainedArena(NativeBattleEffectArena arena)
    {
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            if (operation.effectAuthority is { } authority)
            {
                arena.AdmitRetained(authority);
                EnlistRetained(arena.Working);
                break;
            }
        }
        return arena;
    }

    internal static T AdmitCurrentRetainedEffectStorage<T>(T storage) where T : class
    {
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            if (operation.originalEffectAuthority is not { } authority)
            {
                continue;
            }
            NativeBattleEffectArena? arena = storage is NativeBattleNodeSource node ? node.Arena : storage as NativeBattleEffectArena;
            if (arena is not null)
            {
                if (!arena.HasRetainedAuthority)
                {
                    arena.AdmitRetained(authority);
                }
                EnlistRetained(arena.Working);
            }
            else
            {
                NativeBattleRetainedEffectStorage.AdmitStorage(storage, authority, true);
            }
            break;
        }
        return storage;
    }

    internal static void AcceptPublishedEffectStorage(NativeBattleNodeSource storage) => AcceptPublishedEffectStorage(storage.Arena);

    internal static void AcceptPublishedEffectStorage(NativeBattleEffectArena storage)
    {
        if (current is null || storage.Transaction is not null || NativeBattleEffectArenaTransaction.HasUnpublishedChanges(storage.Working))
        {
            return;
        }
        ValidatePublication(storage.Working);
        ExceptionDispatchInfo? failure = null;
        current.CommitPublishedKernel(storage.Working, ref failure);
        current.ReopenPublishedKernel(storage.Working);
        failure?.Throw();
    }

    internal static void EnlistRetained(NativeBattleWorkingMemory storage)
    {
        if (storage.RetainedAuthority is null)
        {
            return;
        }
        if (storage.HasCheckpoint && !OwnsKernel(storage))
        {
            // An explicit producer already owns this journal and its publication boundary.
            return;
        }
        if (current is null)
        {
            NativeBattleTaskCallerOperation.EnlistAutomaticKernel(storage);
        }
        else
        {
            current.Enlist(storage);
        }
    }

    internal static void AcceptPublishedMemory(NativeBattleEffectMemory? memory) => AcceptPublication(memory, default, true, null);

    internal static void AcceptPublishedPools(ImmutableArray<NativeBattleEffectTaskPool> pools, NativeBattleEffectMemory? memory) => AcceptPublication(memory, pools, false, null);

    internal static void AcceptPublishedEffect(NativeBattleOriginalEffectState effect, NativeBattleEffectMemory? memory)
    {
        NativeBattleRetainedEffectStorage.AdmitEffect(effect);
        AcceptPublication(memory, effect.Pools, true, effect.RetainedAuthority);
    }

    private static void AcceptPublication(NativeBattleEffectMemory? memory, ImmutableArray<NativeBattleEffectTaskPool> pools, bool includeRetained, NativeBattleRetainedStorageAuthority? publishedEffectAuthority)
    {
        if (current is null)
        {
            return;
        }
        NativeBattleWorkingMemory? main = memory?.Arena is { IsSnapshot: false } arena ? arena.Working : null;
        bool deferMain = main is not null && (memory!.Arena!.Transaction is not null || NativeBattleEffectArenaTransaction.HasUnpublishedChanges(main));
        NativeBattleRetainedStorageAuthority? authority = publishedEffectAuthority ?? (includeRetained ? memory?.RetainedAuthority : null);
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            for (int index = 0; index < operation.scopes.Count; index++)
            {
                var scope = operation.scopes[index];
                if (EligiblePublication(operation, scope.Storage, authority, main, deferMain, pools, includeRetained))
                {
                    ValidatePublication(scope.Storage);
                }
            }
        }
        ExceptionDispatchInfo? failure = null;
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            for (int index = 0; index < operation.scopes.Count; index++)
            {
                var scope = operation.scopes[index];
                if (EligiblePublication(operation, scope.Storage, authority, main, deferMain, pools, includeRetained))
                {
                    current.CommitPublishedKernel(scope.Storage, ref failure);
                    current.ReopenPublishedKernel(scope.Storage);
                }
            }
        }
        failure?.Throw();
    }

    private static bool EligiblePublication(NativeBattleTaskOperation operation, NativeBattleWorkingMemory storage, NativeBattleRetainedStorageAuthority? authority, NativeBattleWorkingMemory? main, bool deferMain, ImmutableArray<NativeBattleEffectTaskPool> pools, bool includeRetained)
    {
        if (deferMain && ReferenceEquals(storage, main))
        {
            return false;
        }
        bool eligible = includeRetained && ReferenceEquals(storage, main) || authority is not null && ReferenceEquals(storage.RetainedAuthority, authority);
        if (!pools.IsDefault)
        {
            foreach (NativeBattleEffectTaskPool pool in pools)
            {
                eligible |= ReferenceEquals(storage, pool.CheckpointStorage);
            }
        }
        if (!eligible)
        {
            return false;
        }
        for (NativeBattleTaskOperation? inner = current; inner is not null && !ReferenceEquals(inner, operation); inner = inner.parent)
        {
            if (inner.FindScope(storage) >= 0)
            {
                return false;
            }
        }
        return true;
    }

    private static void ValidatePublication(NativeBattleWorkingMemory storage)
    {
        int expectedDepth = storage.CheckpointDepth;
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            int index = operation.FindScope(storage);
            if (index >= 0 && operation.scopes[index].Depth != expectedDepth--)
            {
                throw new InvalidOperationException("Published retained memory crosses an unfinished caller journal.");
            }
        }
        NativeBattleEffectArenaTransaction.RequireCommittedForCapture(storage);
    }
}

internal sealed partial class NativeBattleEffectArenaTransaction
{
    internal static bool HasUnpublishedChanges(NativeBattleWorkingMemory storage) => ActiveRoutes.TryGetValue(storage, out ActiveRoute? route) && route.Transaction is { } transaction && transaction.Overlay.HasPendingChanges;
}
