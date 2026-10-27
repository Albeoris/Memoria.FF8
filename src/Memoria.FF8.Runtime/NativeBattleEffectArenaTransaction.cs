namespace Memoria.FF8.Runtime;

/// <summary>A private ordered overlay; publication commits only its changes into the same retained working owner.</summary>
internal sealed partial class NativeBattleEffectArenaTransaction : IDisposable
{
    private readonly NativeBattleEffectArenaWriter writer;
    private bool disposed;

    public NativeBattleEffectArenaTransaction(NativeBattleEffectArena source) : this(source, false)
    {
    }

    private NativeBattleEffectArenaTransaction(NativeBattleEffectArena source, bool privateLayer)
    {
        if (!privateLayer && source.Transaction is not null)
        {
            throw new InvalidOperationException("Native arena pool transactions cannot be nested.");
        }
        writer = new(source, privateLayer);
        View = new(source, this);
        try
        {
            if (!privateLayer)
            {
                RegisterRoute();
            }
        }
        catch
        {
            writer.Dispose();
            throw;
        }
    }

    internal static NativeBattleEffectArenaTransaction CreatePrivateChild(NativeBattleEffectArena source) => new(source, true);
    internal NativeBattleArenaOverlay Overlay => writer.OwnedOverlay;
    internal NativeBattleEffectArena Source => writer.OwnedOverlay.Source;

    internal static void CommitGroup(IReadOnlyList<NativeBattleEffectArenaTransaction> transactions)
    {
        for (int index = 0; index < transactions.Count; index++)
        {
            NativeBattleArenaOverlay overlay = transactions[index].writer.OwnedOverlay;
            for (int previous = 0; previous < index; previous++)
            {
                if (ReferenceEquals(overlay.Kernel, transactions[previous].writer.OwnedOverlay.Kernel))
                {
                    throw new InvalidOperationException("A native arena publication group requires distinct physical owners.");
                }
            }
            overlay.PrepareCommit();
        }
        int opened = 0;
        try
        {
            for (; opened < transactions.Count; opened++)
            {
                transactions[opened].writer.OwnedOverlay.BeginPublication();
            }
            for (int index = 0; index < transactions.Count; index++)
            {
                transactions[index].writer.OwnedOverlay.ApplyPrepared();
            }
            for (int index = 0; index < transactions.Count; index++)
            {
                transactions[index].writer.OwnedOverlay.PublishPrepared();
            }
        }
        catch (Exception failure)
        {
            List<Exception>? rollbackFailures = null;
            for (int index = opened - 1; index >= 0; index--)
            {
                try
                {
                    transactions[index].writer.OwnedOverlay.RollbackPublication();
                }
                catch (Exception rollbackFailure)
                {
                    (rollbackFailures ??= []).Add(rollbackFailure);
                }
            }
            if (rollbackFailures is not null)
            {
                failure.Data["NativeBattleEffectArenaTransaction.RollbackFailures"] = rollbackFailures.ToArray();
            }
            throw;
        }
        for (int index = opened - 1; index >= 0; index--)
        {
            transactions[index].writer.OwnedOverlay.CommitPublication();
        }

    }

    public NativeBattleEffectArena View { get; }
    internal System.Collections.Immutable.ImmutableArray<NativeBattleTextureFileSource>? TextureFiles => writer.TextureFiles;
    internal void SetTextureFiles(System.Collections.Immutable.ImmutableArray<NativeBattleTextureFileSource> files) => writer.SetTextureFiles(files);
    internal void SetTextureFiles(int offset, int length, System.Collections.Immutable.ImmutableArray<NativeBattleTextureFileSource> files) => writer.SetTextureFiles(offset, length, files);
    internal NativeBattleArenaRanges DataRanges() => writer.DataRanges();
    internal NativeBattleArenaRanges UnknownRanges() => writer.UnknownRanges();
    internal int StoredLength => writer.StoredLength;
    internal void ValidateExplicitCoverage(int capacity) => writer.ValidateExplicitCoverage(capacity);
    internal void ValidateExplicitCoverageWindow(int offset, int length) => writer.OwnedOverlay.ValidateExplicitCoverageWindow(offset, length);
    internal IEnumerable<KeyValuePair<int, NativeBattleResourcePointer?>> EnumerateResourcePointers()
    {
        RequireActive();
        using var pointers = writer.EnumerateResourcePointers().GetEnumerator();
        while (true)
        {
            RequireActive();
            if (!pointers.MoveNext())
            {
                yield break;
            }
            yield return pointers.Current;
        }
    }
    internal bool TryGetResourcePointer(int offset, out NativeBattleResourcePointer? pointer) => writer.TryGetResourcePointer(offset, out pointer);
    internal void SetResourcePointer(int offset, NativeBattleResourcePointer? pointer) => writer.SetResourcePointer(offset, pointer);

    internal void WriteResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        RequireActive();
        NativeBattleEffectArena.ValidatePointer(pointer);
        if ((pointer?.StagingAddress ?? pointer?.Cpu?.PhysicalAddress) is uint numeric)
        {
            Span<byte> bytes = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes, numeric);
            writer.Write(offset, bytes);
        }
        else
        {
            writer.MarkUnknown(offset, 4);
        }
        writer.SetResourcePointer(offset, pointer);
    }

    public byte[] Read(int offset, int count)
    {
        RequireActive();
        return writer.ReadThrough(offset, count);
    }

    public void ReadInto(int offset, Span<byte> destination)
    {
        RequireActive();
        writer.ReadIntoThrough(offset, destination);
    }

    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        RequireActive();
        writer.ReadWithKnownnessThrough(offset, destination, known);
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
            try
            {
                writer.Dispose();
            }
            finally
            {
                UnregisterRoute();
                disposed = true;
            }
        }
    }
}
