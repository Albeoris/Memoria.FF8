using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A private producer pass retains ordered common stores until an immutable owner is needed.</summary>
internal sealed class NativeBattleCommonPacketTransaction : IDisposable
{
    private readonly List<NativeBattleCommonPacketStore> writes = [];
    private NativeBattlePrimitiveProcess state;
    private NativeBattlePrimitiveProcess? checkpoint;
    private int applied;
    private int checkpointApplied;
    private int checkpointCount;
    private bool disposed;

    public NativeBattleCommonPacketTransaction(NativeBattlePrimitiveProcess source)
    {
        if (source.CommonTransaction is not null)
        {
            throw new InvalidOperationException("Common packet transactions cannot be nested.");
        }
        source.ValidateCommonPacketMemory();
        state = source;
        View = source with { CommonTransaction = this };
    }

    public NativeBattlePrimitiveProcess View { get; }

    public void Append(ImmutableArray<NativeBattleCommonPacketStore> stores)
    {
        RequireActive();
        writes.AddRange(stores);
    }

    public byte[] Read(int offset, int count)
    {
        Materialize();
        return state.ReadCommonPacket(offset, count);
    }

    public void MarkUnknown(int offset, int count)
    {
        Materialize();
        state = state.MarkCommonUnknown(offset, count);
    }

    public void BeginCheckpoint()
    {
        RequireActive();
        if (checkpoint is not null)
        {
            throw new InvalidOperationException("Common packet checkpoints cannot be nested.");
        }
        checkpoint = state;
        checkpointApplied = applied;
        checkpointCount = writes.Count;
    }

    public void CommitCheckpoint()
    {
        RequireCheckpoint();
        checkpoint = null;
    }

    public void RollbackCheckpoint()
    {
        RequireCheckpoint();
        state = checkpoint!;
        applied = checkpointApplied;
        writes.RemoveRange(checkpointCount, writes.Count - checkpointCount);
        checkpoint = null;
    }

    public NativeBattlePrimitiveProcess Freeze(NativeBattlePrimitiveProcess process)
    {
        Materialize();
        return process with { CommonPacketMemory = state.CommonPacketMemory, CommonTransaction = null };
    }

    private void Materialize()
    {
        RequireActive();
        if (applied < writes.Count)
        {
            state = state.ApplyCommonWrites(writes.Skip(applied).ToImmutableArray());
            applied = writes.Count;
        }
    }

    private void RequireActive() => ObjectDisposedException.ThrowIf(disposed, this);

    private void RequireCheckpoint()
    {
        RequireActive();
        if (checkpoint is null)
        {
            throw new InvalidOperationException("Common packet rollback requires its active checkpoint.");
        }
    }

    public void Dispose()
    {
        disposed = true;
    }
}
