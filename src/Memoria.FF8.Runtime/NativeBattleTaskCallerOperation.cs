using System.Collections.Immutable;
using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime;

/// <summary>Explicit session publication boundaries, including banks admitted while its public arena overlay is active.</summary>
internal sealed class NativeBattleTaskCallerOperation : IDisposable
{
    [ThreadStatic]
    private static NativeBattleTaskCallerOperation? current;
    private readonly NativeBattleTaskCallerOperation? parent;
    private readonly NativeBattleEffectArenaTransaction arena;
    private readonly List<(NativeBattleWorkingMemory Storage, int Depth)> scopes = [];
    private bool completed;
    private bool overlayCheckpoint;
    private bool prepared;
    internal int AcceptedPrefixes { get; private set; }
    internal NativeBattleEffectArena SourceArena { get; }

    internal static bool IsActive => current is not null;

    internal static NativeBattleGraphicsState PublishEffectMetadata(NativeBattleGraphicsState published, NativeBattleOriginalEffectState effect) => published with { OriginalEffect = effect };

    internal NativeBattleTaskCallerOperation(NativeBattleEffectArenaTransaction arena, ImmutableArray<NativeBattleEffectTaskPool> pools)
    {
        this.arena = arena;
        SourceArena = arena.Source;
        parent = current;
        current = this;
        try
        {
            Enlist(arena.Source.Working);
            Admit(pools);
            arena.BeginCheckpoint();
            overlayCheckpoint = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void Admit(ImmutableArray<NativeBattleEffectTaskPool> pools)
    {
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            Enlist(pool.CheckpointStorage);
        }
    }

    internal void EnlistArena(NativeBattleEffectArena storage) => Enlist(storage.Working);

    internal void AdmitCommon(NativeBattleCommonPacketMemory? memory)
    {
        if (memory?.Bank070Buffer0 is { } first)
        {
            EnlistArena(first);
        }
        if (memory?.Bank070Buffer1 is { } second)
        {
            EnlistArena(second);
        }
        if (memory?.Bank094Buffer0 is { } third)
        {
            EnlistArena(third);
        }
        if (memory?.Bank094Buffer1 is { } fourth)
        {
            EnlistArena(fourth);
        }
    }

    internal void PreparePrefix()
    {
        RequireCurrent();
        Preflight();
        AcceptOverlay();
    }


    internal static void EnlistAutomaticKernel(NativeBattleWorkingMemory storage) => current?.Enlist(storage);

    internal static bool AllowsOverlayBinding(NativeBattleWorkingMemory storage, int offset, int length)
    {
        if (current is null || !ReferenceEquals(current.arena.Source.Working, storage))
        {
            return false;
        }
        return NativeBattleEffectArenaTransaction.TryRoute(storage, offset, length, out NativeBattleEffectArenaTransaction? transaction, out _) && ReferenceEquals(transaction, current.arena);
    }

    internal bool IsPreparedFor(NativeBattleWorkingMemory storage)
    {
        if (!ReferenceEquals(current, this) || !prepared)
        {
            return false;
        }
        foreach (var scope in scopes)
        {
            if (ReferenceEquals(scope.Storage, storage))
            {
                return true;
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
        return true;
    }

    private void Enlist(NativeBattleWorkingMemory storage)
    {
        foreach (var scope in scopes)
        {
            if (ReferenceEquals(scope.Storage, storage))
            {
                return;
            }
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
        NativeBattleTaskBankOwner.RegisterCallerCheckpoint(storage);
    }

    internal void AcceptPrefix(NativeBattleLogicalOrdering? ordering = null, NativeBattleFileStaging? staging = null)
    {
        RequireCurrent();
        Preflight();
        NativeBattleTaskOperation.PreflightCallerDomains(ordering, staging);
        if (!prepared)
        {
            AcceptOverlay();
        }
        ExceptionDispatchInfo? failure = CommitKernels();
        AcceptAutomaticAncestors(ref failure);
        NativeBattleTaskOperation.AcceptCallerDomains(ordering, staging, ref failure);
        prepared = false;
        AcceptedPrefixes++;
        ReopenKernels();
        arena.BeginCheckpoint();
        overlayCheckpoint = true;
        failure?.Throw();
    }

    internal void Complete(NativeBattleLogicalOrdering? ordering = null, NativeBattleFileStaging? staging = null)
    {
        RequireCurrent();
        Preflight();
        NativeBattleTaskOperation.PreflightCallerDomains(ordering, staging);
        if (!prepared)
        {
            AcceptOverlay();
        }
        ExceptionDispatchInfo? failure;
        try
        {
            failure = CommitKernels();
            AcceptAutomaticAncestors(ref failure);
            NativeBattleTaskOperation.AcceptCallerDomains(ordering, staging, ref failure);
            prepared = false;
            AcceptedPrefixes++;
        }
        finally
        {
            Unregister();
            completed = true;
            current = parent;
        }
        failure?.Throw();
    }

    private void AcceptOverlay()
    {
        if (!overlayCheckpoint)
        {
            throw new InvalidOperationException("Explicit task caller lost its arena publication checkpoint.");
        }
        arena.CommitCheckpoint();
        overlayCheckpoint = false;
        arena.Freeze();
        prepared = true;
    }

    private ExceptionDispatchInfo? CommitKernels()
    {
        ExceptionDispatchInfo? failure = null;
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
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
        return failure;
    }

    private void AcceptAutomaticAncestors(ref ExceptionDispatchInfo? failure)
    {
        foreach (var scope in scopes)
        {
            try
            {
                NativeBattleTaskOperation.AcceptCallerPrefix(scope.Storage, this);
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
    }

    private void ReopenKernels()
    {
        for (int index = 0; index < scopes.Count; index++)
        {
            NativeBattleWorkingMemory storage = scopes[index].Storage;
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

    private void Unregister()
    {
        for (int index = scopes.Count - 1; index >= 0; index--)
        {
            NativeBattleTaskBankOwner.UnregisterCallerCheckpoint(scopes[index].Storage);
        }
    }

    private void Preflight()
    {
        foreach (var scope in scopes)
        {
            if (scope.Storage.CheckpointDepth != scope.Depth)
            {
                throw new InvalidOperationException("Explicit task caller journals must complete in physical nesting order.");
            }
        }
    }

    private void RequireCurrent()
    {
        if (completed || !ReferenceEquals(current, this))
        {
            throw new InvalidOperationException("Explicit task callers must complete in nested call order.");
        }
    }

    public void Dispose()
    {
        if (completed)
        {
            return;
        }
        RequireCurrent();
        Preflight();
        ExceptionDispatchInfo? failure = null;
        try
        {
            try
            {
                if (overlayCheckpoint)
                {
                    arena.RollbackCheckpoint();
                    overlayCheckpoint = false;
                }
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            try
            {
                arena.Dispose();
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
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
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                }
                finally
                {
                    NativeBattleTaskBankOwner.UnregisterCallerCheckpoint(storage);
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
}
