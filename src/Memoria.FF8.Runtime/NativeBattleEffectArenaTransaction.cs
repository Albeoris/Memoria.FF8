namespace Memoria.FF8.Runtime;

/// <summary>A private native pool pass reads its ordered stores before publishing one immutable arena.</summary>
internal sealed class NativeBattleEffectArenaTransaction : IDisposable
{
    private readonly NativeBattleEffectArenaWriter writer;
    private bool disposed;

    public NativeBattleEffectArenaTransaction(NativeBattleEffectArena source)
    {
        if (source.Transaction is not null)
        {
            throw new InvalidOperationException("Native arena pool transactions cannot be nested.");
        }
        writer = new(source);
        writer.BeginReadThrough();
        View = source with { Transaction = this };
    }

    public NativeBattleEffectArena View { get; }
    internal System.Collections.Immutable.ImmutableDictionary<int, NativeBattleResourcePointer?>? ResourcePointers => writer.ResourcePointers;
    internal System.Collections.Immutable.ImmutableArray<NativeBattleTextureFileSource>? TextureFiles => writer.TextureFiles;
    internal void SetTextureFiles(System.Collections.Immutable.ImmutableArray<NativeBattleTextureFileSource> files) => writer.SetTextureFiles(files);

    internal void SetResourcePointer(int offset, NativeBattleResourcePointer? pointer) => writer.SetResourcePointer(offset, pointer);

    public byte[] Read(int offset, int count)
    {
        RequireActive();
        return writer.ReadThrough(offset, count);
    }

    public (byte[] Data, bool[] Known) ReadSnapshot(int offset, int count)
    {
        RequireActive();
        return writer.ReadSnapshotThrough(offset, count);
    }

    public void Write(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireActive();
        writer.Write(offset, bytes);
    }

    public void MarkUnknown(int offset, int count)
    {
        RequireActive();
        writer.MarkUnknown(offset, count);
    }

    public void BeginCheckpoint() => writer.BeginCheckpoint();
    public void CommitCheckpoint() => writer.CommitCheckpoint();
    public void RollbackCheckpoint() => writer.RollbackCheckpoint();

    public NativeBattleEffectArena Freeze()
    {
        RequireActive();
        return writer.Freeze();
    }

    internal void RequireActive() => ObjectDisposedException.ThrowIf(disposed, this);

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            writer.EndReadThrough();
        }
    }
}
