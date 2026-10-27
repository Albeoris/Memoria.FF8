using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleFileStagingWorkingOwner
{
    private sealed class OwnerCheckpoint
    {
        internal uint Base;
        internal int Pointers;
        internal int Zeros;
        internal readonly List<NativeBattleWorkingMemory> Pages = [];
    }

    private readonly List<OwnerCheckpoint> checkpoints = [];
    private int checkpointDepth;
    internal int CheckpointDepth => checkpointDepth;
    internal bool RetainedForOperations { get; set; }
    internal bool HasUnpublishedOverlays
    {
        get
        {
            foreach (NativeBattleFileStagingOverlay overlay in activeOverlays)
            {
                if (overlay.IsPublicLayer && overlay.HasPendingChanges)
                {
                    return true;
                }
            }
            return false;
        }
    }

    private void PrepareOperationMutation() => NativeBattleTaskOperation.EnlistRetainedStaging(this);

    public void BeginCheckpoint()
    {
        if (checkpoint)
        {
            throw new InvalidOperationException("Native staging root checkpoint requires its committed owner boundary.");
        }
        pointerUndo.Clear();
        zeroUndo.Clear();
        OpenCheckpoint();
    }

    internal void BeginNestedCheckpoint()
    {
        RequireCheckpoint();
        OpenCheckpoint();
    }

    private void OpenCheckpoint()
    {
        if (checkpointDepth == checkpoints.Count)
        {
            checkpoints.Add(new());
        }
        OwnerCheckpoint frame = checkpoints[checkpointDepth];
        frame.Base = BaseAddress;
        frame.Pointers = pointerUndo.Count;
        frame.Zeros = zeroUndo.Count;
        frame.Pages.Clear();
        checkpointDepth++;
    }

    private void EnlistPage(NativeBattleWorkingMemory page)
    {
        for (int depth = 0; depth < checkpointDepth; depth++)
        {
            OwnerCheckpoint frame = checkpoints[depth];
            if (frame.Pages.Contains(page))
            {
                continue;
            }
            frame.Pages.EnsureCapacity(frame.Pages.Count + 1);
            if (depth == 0)
            {
                page.BeginCheckpoint();
            }
            else
            {
                page.BeginNestedCheckpoint();
            }
            frame.Pages.Add(page);
        }
    }

    internal int BeginPublicationCheckpoint()
    {
        PrepareOperationMutation();
        if (checkpoint)
        {
            BeginNestedCheckpoint();
        }
        else
        {
            BeginCheckpoint();
        }
        return checkpointDepth;
    }

    public void CommitCheckpoint()
    {
        RequireCheckpoint();
        if (checkpointDepth != 1)
        {
            throw new InvalidOperationException("Native staging root cannot publish unfinished nested changes.");
        }
        ExceptionDispatchInfo? failure = CompleteCheckpoint(false);
        failure?.Throw();
    }

    internal void CommitNestedCheckpoint()
    {
        RequireNestedCheckpoint();
        ExceptionDispatchInfo? failure = CompleteCheckpoint(false);
        failure?.Throw();
    }

    public void RollbackCheckpoint()
    {
        RequireCheckpoint();
        ExceptionDispatchInfo? failure = null;
        while (checkpointDepth != 0)
        {
            ExceptionDispatchInfo? currentFailure = CompleteCheckpoint(true);
            failure ??= currentFailure;
        }
        failure?.Throw();
    }

    internal void RollbackNestedCheckpoint()
    {
        RequireNestedCheckpoint();
        ExceptionDispatchInfo? failure = CompleteCheckpoint(true);
        failure?.Throw();
    }

    internal void CommitPublicationCheckpoint(int depth)
    {
        RequirePublicationDepth(depth);
        if (depth == 1)
        {
            CommitCheckpoint();
        }
        else
        {
            CommitNestedCheckpoint();
        }
    }

    internal void RollbackPublicationCheckpoint(int depth)
    {
        RequirePublicationDepth(depth);
        if (depth == 1)
        {
            RollbackCheckpoint();
        }
        else
        {
            RollbackNestedCheckpoint();
        }
    }

    private ExceptionDispatchInfo? CompleteCheckpoint(bool rollback)
    {
        int depth = checkpointDepth;
        OwnerCheckpoint frame = checkpoints[depth - 1];
        ExceptionDispatchInfo? failure = null;
        foreach (NativeBattleWorkingMemory page in frame.Pages)
        {
            try
            {
                if (rollback)
                {
                    if (depth == 1)
                    {
                        page.RollbackCheckpoint();
                    }
                    else
                    {
                        page.RollbackNestedCheckpoint();
                    }
                }
                else if (depth == 1)
                {
                    page.CommitCheckpoint();
                }
                else
                {
                    page.CommitNestedCheckpoint();
                }
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
        if (rollback)
        {
            for (int index = pointerUndo.Count - 1; index >= frame.Pointers; index--)
            {
                PointerUndo entry = pointerUndo[index];
                if (entry.Present)
                {
                    pointers[entry.Address] = entry.Pointer;
                }
                else
                {
                    pointers.Remove(entry.Address);
                }
            }
            for (int index = zeroUndo.Count - 1; index >= frame.Zeros; index--)
            {
                ZeroUndo entry = zeroUndo[index];
                if (entry.Removed is { } removed)
                {
                    zeros.Insert(entry.Index, removed);
                }
                else
                {
                    zeros.RemoveAt(entry.Index);
                }
            }
            pointerUndo.RemoveRange(frame.Pointers, pointerUndo.Count - frame.Pointers);
            zeroUndo.RemoveRange(frame.Zeros, zeroUndo.Count - frame.Zeros);
            BaseAddress = frame.Base;
            version++;
        }
        frame.Pages.Clear();
        checkpointDepth--;
        if (checkpointDepth == 0)
        {
            pointerUndo.Clear();
            zeroUndo.Clear();
        }
        return failure;
    }

    private void RequireNestedCheckpoint()
    {
        if (checkpointDepth < 2)
        {
            throw new InvalidOperationException("Native staging requires its active nested owner checkpoint.");
        }
    }

    private void RequirePublicationDepth(int depth)
    {
        if (depth != checkpointDepth || depth == 0)
        {
            throw new InvalidOperationException("Native staging publications must close in logical nesting order.");
        }
    }
}

internal sealed partial class NativeBattleFileStagingOverlay
{
    internal bool IsPublicLayer { get; set; } = true;
    internal bool HasPendingChanges => checkpoint || bytes.Count != 0 || pointers.Count != 0 || unknown.Count != 0 || forgottenZeros.Count != 0 || BaseAddress != source.BaseAddress;
}
