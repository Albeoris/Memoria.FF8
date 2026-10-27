using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleTaskOperation
{
    private readonly List<(NativeBattleFileStagingWorkingOwner Owner, int Depth)> stagingScopes = [];

    internal void AdmitStaging(NativeBattleFileStaging? staging)
    {
        RequireCurrent();
        if (staging is null || staging.IsSnapshot)
        {
            return;
        }
        NativeBattleFileStagingWorkingOwner owner = staging.WorkingOwner;
        owner.RetainedForOperations = true;
        EnlistRetainedStaging(owner);
    }

    internal static void EnlistRetainedStaging(NativeBattleFileStagingWorkingOwner owner)
    {
        if (!owner.RetainedForOperations || current is null || owner.HasCheckpoint && !OwnsStaging(owner))
        {
            return;
        }
        current.EnlistStaging(owner);
    }

    private static bool OwnsStaging(NativeBattleFileStagingWorkingOwner owner)
    {
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            if (operation.FindStaging(owner) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private int FindStaging(NativeBattleFileStagingWorkingOwner owner)
    {
        for (int index = 0; index < stagingScopes.Count; index++)
        {
            if (ReferenceEquals(stagingScopes[index].Owner, owner))
            {
                return index;
            }
        }
        return -1;
    }

    private void EnlistStaging(NativeBattleFileStagingWorkingOwner owner)
    {
        if (FindStaging(owner) >= 0)
        {
            return;
        }
        parent?.EnlistStaging(owner);
        stagingScopes.EnsureCapacity(stagingScopes.Count + 1);
        if (owner.HasCheckpoint)
        {
            owner.BeginNestedCheckpoint();
        }
        else
        {
            owner.BeginCheckpoint();
        }
        stagingScopes.Add((owner, owner.CheckpointDepth));
    }

    internal static void AcceptPublishedStaging(NativeBattleFileStaging? staging)
    {
        if (staging is null || staging.IsSnapshot)
        {
            return;
        }
        NativeBattleFileStagingWorkingOwner owner = staging.WorkingOwner;
        owner.RetainedForOperations = true;
        EnlistRetainedStaging(owner);
        if (current is null || staging.Transaction is not null || owner.HasUnpublishedOverlays)
        {
            return;
        }
        int expected = owner.CheckpointDepth;
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            int index = operation.FindStaging(owner);
            if (index >= 0 && operation.stagingScopes[index].Depth != expected--)
            {
                throw new InvalidOperationException("Published staging prefix crosses an unfinished owner journal.");
            }
        }
        ExceptionDispatchInfo? failure = null;
        current.CommitPublishedStaging(owner, ref failure);
        current.ReopenPublishedStaging(owner);
        failure?.Throw();
    }

    private void CommitPublishedStaging(NativeBattleFileStagingWorkingOwner owner, ref ExceptionDispatchInfo? failure)
    {
        int index = FindStaging(owner);
        if (index >= 0)
        {
            try
            {
                if (stagingScopes[index].Depth > 1)
                {
                    owner.CommitNestedCheckpoint();
                }
                else
                {
                    owner.CommitCheckpoint();
                }
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
        parent?.CommitPublishedStaging(owner, ref failure);
    }

    private void ReopenPublishedStaging(NativeBattleFileStagingWorkingOwner owner)
    {
        parent?.ReopenPublishedStaging(owner);
        int index = FindStaging(owner);
        if (index >= 0)
        {
            if (owner.HasCheckpoint)
            {
                owner.BeginNestedCheckpoint();
            }
            else
            {
                owner.BeginCheckpoint();
            }
            stagingScopes[index] = (owner, owner.CheckpointDepth);
        }
    }

    private void PreflightStaging()
    {
        foreach (var scope in stagingScopes)
        {
            if (scope.Owner.CheckpointDepth != scope.Depth)
            {
                throw new InvalidOperationException("Task staging journals must complete in logical nesting order.");
            }
        }
    }

    private void CompleteStaging(bool rollback, ref ExceptionDispatchInfo? failure)
    {
        for (int index = stagingScopes.Count - 1; index >= 0; index--)
        {
            var scope = stagingScopes[index];
            try
            {
                if (rollback)
                {
                    if (scope.Depth > 1)
                    {
                        scope.Owner.RollbackNestedCheckpoint();
                    }
                    else
                    {
                        scope.Owner.RollbackCheckpoint();
                    }
                }
                else if (scope.Depth > 1)
                {
                    scope.Owner.CommitNestedCheckpoint();
                }
                else
                {
                    scope.Owner.CommitCheckpoint();
                }
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
    }
}
