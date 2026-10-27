namespace Memoria.FF8.Runtime;

/// <summary>A bounded suffix journal over the retained pending owner; Freeze accepts its current prefix.</summary>
internal sealed class NativeBattleLogicalOrderingTransaction : IDisposable
{
    private bool disposed;

    public NativeBattleLogicalOrderingTransaction(NativeBattleLogicalOrdering source)
    {
        if (source.Transaction is not null)
        {
            throw new InvalidOperationException("Native ordering pool transactions cannot be nested.");
        }
        Validation = RequireActive;
        Writer = new(source);
        View = new(source, this);
    }

    internal Action Validation { get; }
    internal NativeBattleLogicalOrdering.Builder Writer { get; }
    public NativeBattleLogicalOrdering View { get; }
    public void BeginCheckpoint()
    {
        RequireActive();
        Writer.BeginCheckpoint();
    }
    public void CommitCheckpoint()
    {
        RequireActive();
        Writer.CommitCheckpoint();
    }
    public void RollbackCheckpoint()
    {
        RequireActive();
        Writer.RollbackCheckpoint();
    }
    public NativeBattleLogicalOrdering Freeze()
    {
        RequireActive();
        return Writer.PublishRoot();
    }
    internal NativeBattleLogicalOrdering FreezeKnownAncestorPrefix()
    {
        RequireActive();
        return Writer.PublishKnownAncestorPrefix();
    }
    internal NativeBattleLogicalOrdering.Delta SuspendCheckpointDelta()
    {
        RequireActive();
        return Writer.SuspendCheckpointDelta();
    }
    internal NativeBattleLogicalOrdering.Delta SuspendDelta()
    {
        RequireActive();
        return Writer.SuspendDelta();
    }
    internal void ResumeDelta(NativeBattleLogicalOrdering.Delta delta)
    {
        RequireActive();
        Writer.ResumeDelta(delta);
    }
    internal void RequireActive() => ObjectDisposedException.ThrowIf(disposed, this);
    public void Dispose()
    {
        if (!disposed)
        {
            Writer.Dispose();
            disposed = true;
        }
    }
}
