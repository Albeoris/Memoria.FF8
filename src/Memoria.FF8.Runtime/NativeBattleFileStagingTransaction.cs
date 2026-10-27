using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Private changed-byte stores commit accepted prefixes into a persistent staging authority.</summary>
internal sealed class NativeBattleFileStagingTransaction : IDisposable
{
    private readonly NativeBattleFileStaging source;
    private readonly NativeBattleFileStagingWorkingOwner owner;
    private readonly NativeBattleFileStagingOverlay overlay;
    private bool disposed;

    public NativeBattleFileStagingTransaction(NativeBattleFileStaging source) : this(source, false)
    {
    }

    private NativeBattleFileStagingTransaction(NativeBattleFileStaging source, bool privateLayer)
    {
        if (!privateLayer && source.Transaction is not null)
        {
            throw new InvalidOperationException("Native staging transactions cannot be nested.");
        }
        source.Validate();
        this.source = source;
        owner = source.WorkingOwner;
        NativeBattleFileStagingOverlay? parent = privateLayer ? source.Transaction?.ActiveOverlay ?? owner.EffectivePublicOverlay() : null;
        overlay = owner.LeaseOverlay();
        overlay.TransactionOwner = this;
        if (privateLayer)
        {
            overlay.BeginPrivateChild(parent);
        }
        View = source with { Transaction = this };
    }

    internal static NativeBattleFileStagingTransaction CreatePrivateChild(NativeBattleFileStaging source) => new(source, true);
    internal NativeBattleFileStagingOverlay ActiveOverlay
    {
        get
        {
            RequireActive();
            return overlay;
        }
    }

    internal void CaptureReadWindowPrefix(NativeBattleStagingReadWindowLease lease)
    {
        RequireActive();
        overlay.CaptureReadWindowPrefix(lease);
    }

    public NativeBattleFileStaging View { get; }
    public uint BaseAddress
    {
        get
        {
            RequireActive();
            return overlay.BaseAddress;
        }
    }
    public ulong KnownExtentEnd
    {
        get
        {
            RequireActive();
            return overlay.KnownExtentEnd;
        }
    }

    public byte[] Read(uint address, int length)
    {
        RequireRange(address, length);
        if (!overlay.IsKnown(address, length))
        {
            throw new NotSupportedException("Native retained memory read reaches unobserved bytes.");
        }
        return ReadSnapshot(address, length).Data;
    }

    public bool IsKnown(uint address, int length)
    {
        RequireRange(address, length);
        return overlay.IsKnown(address, length);
    }

    public (byte[] Data, bool[] Known) ReadSnapshot(uint address, int length)
    {
        RequireRange(address, length);
        byte[] data = new byte[length];
        bool[] known = new bool[length];
        overlay.ReadWithKnownness(address, data, known);
        return (data, known);
    }

    public void ReadWithKnownness(uint address, Span<byte> data, Span<bool> known)
    {
        RequireRange(address, data.Length);
        if (known.Length != data.Length)
        {
            throw new ArgumentException("Native staging data and knownness destinations must have equal lengths.", nameof(known));
        }
        overlay.ReadWithKnownness(address, data, known);
    }

    public void Rebase(uint address)
    {
        RequireActive();
        overlay.Rebase(address);
    }

    public void Write(uint address, ReadOnlySpan<byte> bytes)
    {
        RequireRange(address, bytes.Length);
        overlay.Write(address, bytes);
    }

    public void WriteRaw(uint address, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
    {
        RequireRange(address, bytes.Length);
        if (known.Length != bytes.Length)
        {
            throw new ArgumentException("Native staging raw stores require their complete knownness mask.", nameof(known));
        }
        overlay.Write(address, bytes);
        for (int index = 0; index < known.Length;)
        {
            if (known[index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < known.Length && !known[index])
            {
                index++;
            }
            overlay.MarkUnknown(checked(address + (uint)first), index - first);
        }
    }

    public void MarkUnknown(uint address, int length)
    {
        RequireRange(address, length);
        overlay.MarkUnknown(address, length);
    }

    public void ForgetLoaderZero(uint address, int length)
    {
        RequireRange(address, length);
        overlay.ForgetLoaderZero(address, length);
    }

    public bool IntersectsLoaderZero(uint address, int length)
    {
        RequireRange(address, length);
        return overlay.IntersectsLoaderZero(address, length);
    }

    public bool TryGetPointer(uint address, out NativeBattleResourcePointer? pointer)
    {
        RequireRange(address, 4);
        return overlay.TryGetPointer(address, out pointer);
    }

    public void SetPointerMetadata(uint address, NativeBattleResourcePointer? pointer)
    {
        RequireRange(address, 4);
        overlay.SetPointerMetadata(address, pointer);
    }

    public void InvalidatePointers(uint address, int length)
    {
        RequireRange(address, length);
        overlay.InvalidatePointers(address, length);
    }

    public IEnumerable<KeyValuePair<uint, NativeBattleResourcePointer?>> EnumerateResourcePointers()
    {
        RequireActive();
        using var pointers = overlay.EnumerateResourcePointers().GetEnumerator();
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

    public int CheckpointDepth
    {
        get
        {
            RequireActive();
            return overlay.CheckpointDepth;
        }
    }

    public int BeginNestedCheckpoint()
    {
        RequireActive();
        return overlay.BeginNestedCheckpoint();
    }

    public void CommitNestedCheckpoint(int depth)
    {
        RequireActive();
        overlay.CommitNestedCheckpoint(depth);
    }

    public void RollbackNestedCheckpoint(int depth)
    {
        RequireActive();
        overlay.RollbackNestedCheckpoint(depth);
    }

    public ImmutableDictionary<uint, NativeBattleResourcePointer?> CapturePointers()
    {
        RequireActive();
        return overlay.CapturePointers();
    }

    public ImmutableArray<NativeBattleLoaderZeroRegion> CaptureLoaderZeros()
    {
        RequireActive();
        return overlay.CaptureLoaderZeros();
    }

    public void BeginCheckpoint()
    {
        RequireActive();
        overlay.BeginCheckpoint();
    }

    public void CommitCheckpoint()
    {
        RequireActive();
        overlay.CommitCheckpoint();
    }

    public void RollbackCheckpoint()
    {
        RequireActive();
        overlay.RollbackCheckpoint();
    }

    public NativeBattleFileStaging Freeze(NativeBattleFileStaging current)
    {
        RequireActive();
        if (!ReferenceEquals(current.Transaction, this))
        {
            throw new InvalidOperationException("Native staging publication requires its current transaction view.");
        }
        owner.RequireReadWindowMutation();
        overlay.Publish();
        return source;
    }

    private void RequireRange(uint address, int length)
    {
        RequireActive();
        NativeBattleFileStaging.RequirePhysicalRange(address, length);
    }

    internal void RequireActive()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        overlay.RequireLayerActive();
    }

    public void Dispose()
    {
        if (!disposed)
        {
            owner.CloseReadWindows(this);
            disposed = true;
            owner.ReturnOverlay(overlay);
        }
    }
}
