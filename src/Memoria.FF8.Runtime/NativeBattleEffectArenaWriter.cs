using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A bounded unpublished producer, or a forwarding adapter borrowing an enclosing transaction.</summary>
internal sealed class NativeBattleEffectArenaWriter : IDisposable
{
    private readonly NativeBattleEffectArena source;
    internal ImmutableArray<NativeBattleTextureFileSource>? TextureFiles => transaction is null ? overlay!.TextureFiles : NativeBattleTextureFileMetadata.Project(transaction.TextureFiles, transactionOffset, source.Bound);
    private readonly NativeBattleEffectArenaTransaction? transaction;
    private readonly NativeBattleArenaOverlay? overlay;
    private bool disposed;
    private readonly int transactionOffset;

    public NativeBattleEffectArenaWriter(NativeBattleEffectArena source) : this(source, false)
    {
    }

    internal NativeBattleEffectArenaWriter(NativeBattleEffectArena source, bool privateLayer)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.RequireWritable();
        source.Validate();
        this.source = source;
        if (source.TryEffectiveRoute(0, source.Bound, out NativeBattleEffectArenaTransaction? parent, out int routedOffset))
        {
            if (privateLayer)
            {
                overlay = source.RentOverlay();
                overlay.BindPrivateParent(parent!.Overlay, routedOffset);
                return;
            }
            transaction = parent;
            transactionOffset = routedOffset;
        }
        overlay = transaction is null ? source.RentOverlay() : null;
    }

    public NativeBattleEffectArenaWriter Write(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireActive();
        source.RequireRange(offset, bytes.Length);
        if (transaction is not null)
        {
            transaction.Write(checked(transactionOffset + offset), bytes);
        }
        else
        {
            overlay!.Write(offset, bytes);
        }
        return this;
    }

    internal void WriteWithKnownness(int offset, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
    {
        RequireActive();
        source.RequireRange(offset, bytes.Length);
        if (transaction is not null)
        {
            transaction.WriteWithKnownness(checked(transactionOffset + offset), bytes, known);
        }
        else
        {
            overlay!.WriteWithKnownness(offset, bytes, known);
        }
    }

    internal void WriteRetainedBytes(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireActive();
        source.RequireRange(offset, bytes.Length);
        if (transaction is not null)
        {
            transaction.WriteRetainedBytes(checked(transactionOffset + offset), bytes);
        }
        else
        {
            overlay!.WriteRetainedBytes(offset, bytes);
        }
    }

    public NativeBattleEffectArenaWriter MarkUnknown(int offset, int length)
    {
        RequireActive();
        source.RequireRange(offset, length);
        if (transaction is not null)
        {
            transaction.MarkUnknown(checked(transactionOffset + offset), length);
        }
        else
        {
            overlay!.MarkUnknown(offset, length);
        }
        return this;
    }

    public NativeBattleEffectArena Freeze()
    {
        RequireActive();
        return transaction is null ? overlay!.Commit() : source;
    }

    internal void SetTextureFiles(ImmutableArray<NativeBattleTextureFileSource> files) => SetTextureFiles(0, source.Bound, files);

    internal void SetTextureFiles(int offset, int length, ImmutableArray<NativeBattleTextureFileSource> files)
    {
        RequireActive();
        source.RequireRange(offset, length);
        if (transaction is not null)
        {
            transaction.SetTextureFiles(checked(transactionOffset + offset), length, files);
        }
        else
        {
            overlay!.SetTextureFiles(offset, length, files);
        }
    }

    internal void SetResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        RequireActive();
        source.RequireRange(offset, 4);
        if (transaction is not null)
        {
            transaction.SetResourcePointer(checked(transactionOffset + offset), pointer);
        }
        else
        {
            overlay!.SetResourcePointer(offset, pointer);
        }
    }

    internal bool TryGetResourcePointer(int offset, out NativeBattleResourcePointer? pointer)
    {
        RequireActive();
        source.RequireRange(offset, 4);
        return transaction is not null ? transaction.TryGetResourcePointer(checked(transactionOffset + offset), out pointer) : overlay!.TryGetResourcePointer(offset, out pointer);
    }

    internal NativeBattleArenaOverlay OwnedOverlay
    {
        get
        {
            RequireActive();
            return overlay ?? throw new InvalidOperationException("Borrowed native writers cannot publish a group independently.");
        }
    }

    internal NativeBattleArenaRanges DataRanges() => transaction is null ? overlay!.DataRanges() : source.EnumerateDataRanges();
    internal NativeBattleArenaRanges UnknownRanges() => transaction is null ? overlay!.UnknownRanges() : source.EnumerateUnknownRanges();

    internal int StoredLength
    {
        get
        {
            RequireActive();
            return transaction is null ? overlay!.StoredLength : Math.Clamp(transaction.StoredLength - transactionOffset, 0, source.Bound);
        }
    }

    internal IEnumerable<KeyValuePair<int, NativeBattleResourcePointer?>> EnumerateResourcePointers()
    {
        RequireActive();
        return transaction is null ? overlay!.EnumerateResourcePointers() : source.EnumerateResourcePointers();
    }

    internal void ValidateExplicitCoverage(int capacity)
    {
        RequireActive();
        if (transaction is not null)
        {
            source.ValidateExplicitCoverage(capacity);
        }
        else
        {
            overlay!.ValidateExplicitCoverage(capacity);
        }
    }

    internal void BeginReadThrough() => RequireActive();

    internal byte[] ReadThrough(int offset, int count)
    {
        source.RequireRange(offset, count);
        byte[] result = new byte[count];
        ReadIntoThrough(offset, result);
        return result;
    }

    internal void ReadIntoThrough(int offset, Span<byte> destination)
    {
        RequireActive();
        source.RequireRange(offset, destination.Length);
        if (transaction is not null)
        {
            transaction.ReadInto(checked(transactionOffset + offset), destination);
        }
        else
        {
            overlay!.ReadInto(offset, destination);
        }
    }

    internal void ReadWithKnownnessThrough(int offset, Span<byte> destination, Span<bool> known)
    {
        RequireActive();
        source.RequireRange(offset, destination.Length);
        if (transaction is not null)
        {
            transaction.ReadWithKnownness(checked(transactionOffset + offset), destination, known);
        }
        else
        {
            overlay!.ReadWithKnownness(offset, destination, known);
        }
    }

    internal (byte[] Data, bool[] Known) ReadSnapshotThrough(int offset, int count)
    {
        source.RequireRange(offset, count);
        byte[] data = new byte[count];
        bool[] known = new bool[count];
        ReadWithKnownnessThrough(offset, data, known);
        return (data, known);
    }

    internal byte[] ReadCurrent(int offset, int count) => ReadThrough(offset, count);
    internal void BeginCheckpoint()
    {
        RequireActive();
        if (transaction is not null)
        {
            throw new InvalidOperationException("A forwarding arena writer cannot acquire its borrowed transaction checkpoint.");
        }
        overlay!.BeginCheckpoint();
    }

    internal void CommitCheckpoint()
    {
        RequireActive();
        overlay!.CommitCheckpoint();
    }

    internal void RollbackCheckpoint()
    {
        RequireActive();
        overlay!.RollbackCheckpoint();
    }

    internal void EndReadThrough() => Dispose();

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            if (overlay is not null)
            {
                source.ReturnOverlay(overlay);
            }
        }
    }

    private void RequireActive() => ObjectDisposedException.ThrowIf(disposed, this);
}
