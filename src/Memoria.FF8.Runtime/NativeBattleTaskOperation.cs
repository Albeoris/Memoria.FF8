using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>One unpublished task pass. Explicit caller pool journals retain their own publication policy.</summary>
internal sealed partial class NativeBattleTaskOperation : IDisposable
{
    [ThreadStatic]
    private static NativeBattleTaskOperation? current;
    private readonly NativeBattleTaskOperation? parent;
    private readonly List<(NativeBattleWorkingMemory Storage, int Depth)> scopes = [];
    private readonly HashSet<NativeBattleWorkingMemory> observed = [];
    private bool completed;

    internal NativeBattleTaskOperation(ImmutableArray<NativeBattleEffectTaskPool> pools)
    {
        parent = current;
        current = this;
        try
        {
            foreach (NativeBattleEffectTaskPool pool in pools)
            {
                Enlist(pool.CheckpointStorage);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal static bool IsActive => current is not null;

    internal static void AcceptPublishedArena(NativeBattleWorkingMemory storage) => AcceptPublishedArena(storage, allowCallerOverlay: false);

    internal static void AcceptCallerPrefix(NativeBattleWorkingMemory storage, NativeBattleTaskCallerOperation authority)
    {
        if (!authority.IsPreparedFor(storage))
        {
            throw new InvalidOperationException("Caller acceptance requires its prepared physical publication scope.");
        }
        AcceptPublishedArena(storage, allowCallerOverlay: true);
    }

    private static void AcceptPublishedArena(NativeBattleWorkingMemory storage, bool allowCallerOverlay)
    {
        if (current is null)
        {
            return;
        }
        int count = 0;
        int expectedDepth = storage.CheckpointDepth;
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            int index = operation.FindScope(storage);
            if (index < 0)
            {
                continue;
            }
            if (operation.scopes[index].Depth != expectedDepth--)
            {
                throw new InvalidOperationException("Published task arena prefix crosses an unfinished caller journal.");
            }
            count++;
        }
        if (count == 0)
        {
            return;
        }
        if (NativeBattleEffectArenaTransaction.HasActive(storage) && !allowCallerOverlay)
        {
            NativeBattleEffectArenaTransaction.RequireCommittedForCapture(storage);
        }
        //These exact checkpoint depths have already been opened. Kernel frame/mark capacities are retained.
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        current.CommitPublishedKernel(storage, ref failure);
        current.ReopenPublishedKernel(storage);
        failure?.Throw();
    }

    private int FindScope(NativeBattleWorkingMemory storage)
    {
        for (int index = 0; index < scopes.Count; index++)
        {
            if (ReferenceEquals(scopes[index].Storage, storage))
            {
                return index;
            }
        }
        return -1;
    }

    private void CommitPublishedKernel(NativeBattleWorkingMemory storage, ref System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure)
    {
        int index = FindScope(storage);
        if (index >= 0)
        {
            try
            {
                if (scopes[index].Depth > 1)
                {
                    storage.CommitNestedCheckpoint();
                }
                else
                {
                    storage.CommitCheckpoint();
                }
            }
            catch (Exception exception)
            {
                failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
            }
        }
        parent?.CommitPublishedKernel(storage, ref failure);
    }

    private void ReopenPublishedKernel(NativeBattleWorkingMemory storage)
    {
        parent?.ReopenPublishedKernel(storage);
        int index = FindScope(storage);
        if (index >= 0)
        {
            if (storage.HasCheckpoint)
            {
                storage.BeginNestedCheckpoint();
            }
            else
            {
                storage.BeginCheckpoint();
            }
            scopes[index] = (storage, storage.CheckpointDepth);
        }
    }

    internal static bool OwnsKernel(NativeBattleWorkingMemory storage)
    {
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            foreach (var scope in operation.scopes)
            {
                if (ReferenceEquals(scope.Storage, storage))
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal static bool EnlistBinding(NativeBattleWorkingMemory storage)
    {
        if (current is null)
        {
            return false;
        }
        current.Enlist(storage);
        return storage.HasCheckpoint;
    }

    private void Enlist(NativeBattleWorkingMemory storage)
    {
        if (observed.Contains(storage))
        {
            return;
        }
        NativeBattleTaskCallerOperation.EnlistAutomaticKernel(storage);
        parent?.Enlist(storage);
        if (NativeBattleTaskBankOwner.HasLocalCheckpointFor(storage))
        {
            observed.Add(storage);
            return;
        }
        if (NativeBattleEffectArenaTransaction.HasActive(storage))
        {
            throw new InvalidOperationException("A task pass inside a public arena transaction requires its explicit caller pool journal.");
        }
        if (storage.HasCheckpoint)
        {
            storage.BeginNestedCheckpoint();
        }
        else
        {
            storage.BeginCheckpoint();
        }
        scopes.Add((storage, storage.CheckpointDepth));
        observed.Add(storage);
    }

    internal void Commit()
    {
        RequireCurrent();
        Preflight();
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        try
        {
            CompleteOrdering(false, ref failure);
            CompleteStaging(false, ref failure);
            for (int index = scopes.Count - 1; index >= 0; index--)
            {
                (NativeBattleWorkingMemory storage, int depth) = scopes[index];
                try
                {
                    if (depth > 1)
                    {
                        storage.CommitNestedCheckpoint();
                    }
                    else
                    {
                        storage.CommitCheckpoint();
                    }
                }
                catch (Exception exception)
                {
                    failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        finally
        {
            completed = true;
            current = parent;
        }
        failure?.Throw();
    }

    public void Dispose()
    {
        if (completed)
        {
            return;
        }
        RequireCurrent();
        Preflight();
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        try
        {
            CompleteOrdering(true, ref failure);
            CompleteStaging(true, ref failure);
            for (int index = scopes.Count - 1; index >= 0; index--)
            {
                (NativeBattleWorkingMemory storage, int depth) = scopes[index];
                try
                {
                    if (depth > 1)
                    {
                        storage.RollbackNestedCheckpoint();
                    }
                    else
                    {
                        storage.RollbackCheckpoint();
                    }
                }
                catch (Exception exception)
                {
                    failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        finally
        {
            completed = true;
            current = parent;
        }
        failure?.Throw();
    }

    private void Preflight()
    {
        PreflightOrdering();
        PreflightStaging();
        foreach ((NativeBattleWorkingMemory storage, int depth) in scopes)
        {
            if (storage.CheckpointDepth != depth)
            {
                throw new InvalidOperationException("Task operation journals must complete in physical nesting order.");
            }
        }
    }

    private void RequireCurrent()
    {
        if (completed || !ReferenceEquals(current, this))
        {
            throw new InvalidOperationException("Task operations must complete in nested call order.");
        }
    }
}
