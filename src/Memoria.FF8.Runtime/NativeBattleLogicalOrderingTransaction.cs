namespace Memoria.FF8.Runtime;

/// <summary>Private ordered pool appends share their indexes and publish one immutable ordering table.</summary>
internal sealed class NativeBattleLogicalOrderingTransaction : IDisposable
{
    private bool disposed;

    public NativeBattleLogicalOrderingTransaction(NativeBattleLogicalOrdering source)
    {
        if (source.Transaction is not null)
        {
            throw new InvalidOperationException("Native ordering pool transactions cannot be nested.");
        }
        Writer = new(source);
        View = source with { Transaction = this };
    }

    internal NativeBattleLogicalOrdering.Builder Writer { get; }
    public NativeBattleLogicalOrdering View { get; }
    public void BeginCheckpoint() => Writer.BeginCheckpoint();
    public void CommitCheckpoint() => Writer.CommitCheckpoint();
    public void RollbackCheckpoint() => Writer.RollbackCheckpoint();

    public NativeBattleLogicalOrdering Freeze()
    {
        RequireActive();
        return Writer.ToImmutable();
    }

    internal void RequireActive() => ObjectDisposedException.ThrowIf(disposed, this);
    public void Dispose() => disposed = true;
}
