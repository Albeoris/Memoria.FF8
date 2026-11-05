using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Private changed-byte stores commit accepted prefixes into a persistent staging authority.</summary>
internal sealed class NativeBattleFileStagingTransaction : IDisposable
{
    private readonly NativeBattleFileStaging source;
    private readonly NativeBattleFileStagingWorkingOwner owner;
    private readonly NativeBattleFileStagingOverlay overlay;
    private bool disposed;
    private readonly NativeBattleFileStagingTransaction? privateParent;
    private Dictionary<object, NativeBattleFileStagingTransaction>? fieldResources;
    private bool publicationFailed;

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
        privateParent = parent?.TransactionOwner;
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

    internal NativeBattleFileStaging EnlistFieldResource(NativeFieldResourceOwner resource)
    {
        RequireActive();
        resource.Validate();
        object identity = resource.Storage.OwnerIdentity;
        fieldResources ??= new(ReferenceEqualityComparer.Instance);
        if (!fieldResources.TryGetValue(identity, out NativeBattleFileStagingTransaction? transaction))
        {
            transaction = privateParent is null ? new(resource.Storage) : CreatePrivateChild(privateParent.EnlistFieldResource(resource));
            try
            {
                for (int depth = 0; depth < CheckpointDepth; depth++)
                {
                    transaction.BeginNestedCheckpoint();
                }
                fieldResources.Add(identity, transaction);
            }
            catch
            {
                transaction.Dispose();
                throw;
            }
        }
        return transaction.View;
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

    internal void BindResource(uint address, ImmutableArray<byte> bytes, int sourceOffset, int length)
    {
        RequireRange(address, length);
        overlay.BindResource(address, bytes, sourceOffset, length);
    }

    internal bool TryBorrowResource(uint address, int length, out ImmutableArray<byte> bytes, out int sourceOffset)
    {
        RequireRange(address, length);
        return overlay.TryBorrowResource(address, length, out bytes, out sourceOffset);
    }

    internal bool TryResource(uint address, int length, out ImmutableArray<byte> bytes)
    {
        RequireRange(address, length);
        return overlay.TryResource(address, length, out bytes);
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
        if (fieldResources is null)
        {
            return overlay.BeginNestedCheckpoint();
        }
        foreach (NativeBattleFileStagingTransaction resource in fieldResources.Values)
        {
            if (resource.CheckpointDepth != CheckpointDepth)
            {
                throw new InvalidOperationException("Parent checkpoint cannot interrupt an active field resource scope.");
            }
        }
        int depth = overlay.BeginNestedCheckpoint();
        foreach (NativeBattleFileStagingTransaction resource in fieldResources.Values)
        {
            resource.BeginNestedCheckpoint();
        }
        return depth;
    }

    public void CommitNestedCheckpoint(int depth)
    {
        RequireActive();
        if (fieldResources is null)
        {
            overlay.CommitNestedCheckpoint(depth);
            return;
        }
        RequireFieldCheckpoint(depth);
        foreach (NativeBattleFileStagingTransaction resource in fieldResources.Values)
        {
            resource.CommitNestedCheckpoint(depth);
        }
        overlay.CommitNestedCheckpoint(depth);
    }

    public void RollbackNestedCheckpoint(int depth)
    {
        RequireActive();
        if (fieldResources is null)
        {
            overlay.RollbackNestedCheckpoint(depth);
            return;
        }
        RequireFieldCheckpoint(depth);
        foreach (NativeBattleFileStagingTransaction resource in fieldResources.Values)
        {
            resource.RollbackNestedCheckpoint(depth);
        }
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
        if (CheckpointDepth != 0)
        {
            throw new InvalidOperationException("Native staging checkpoint is already active.");
        }
        BeginNestedCheckpoint();
    }

    public void CommitCheckpoint()
    {
        RequireActive();
        CommitNestedCheckpoint(1);
    }

    public void RollbackCheckpoint()
    {
        RequireActive();
        RollbackNestedCheckpoint(1);
    }

    public NativeBattleFileStaging Freeze(NativeBattleFileStaging current)
    {
        RequireActive();
        if (!ReferenceEquals(current.Transaction, this))
        {
            throw new InvalidOperationException("Native staging publication requires its current transaction view.");
        }
        owner.RequireReadWindowMutation();
        if (CheckpointDepth != 0)
        {
            throw new InvalidOperationException("Native staging publication requires committed field resource checkpoints.");
        }
        if (fieldResources is null)
        {
            overlay.Publish();
            return source;
        }
        List<(NativeBattleFileStagingTransaction Resource, int Depth)> publications = [];
        try
        {
            foreach (NativeBattleFileStagingTransaction resource in fieldResources.Values)
            {
                resource.owner.RequireReadWindowMutation();
                int depth = resource.privateParent is { } parent ? parent.BeginNestedCheckpoint() : resource.owner.BeginPublicationCheckpoint();
                publications.Add((resource, depth));
            }
            foreach (NativeBattleFileStagingTransaction resource in fieldResources.Values)
            {
                resource.Freeze(resource.View);
            }
            overlay.Publish();
            foreach ((NativeBattleFileStagingTransaction resource, int depth) in publications)
            {
                if (resource.privateParent is { } parent)
                {
                    parent.CommitNestedCheckpoint(depth);
                }
                else
                {
                    resource.owner.CommitPublicationCheckpoint(depth);
                }
            }
        }
        catch
        {
            publicationFailed = true;
            for (int index = publications.Count - 1; index >= 0; index--)
            {
                (NativeBattleFileStagingTransaction resource, int depth) = publications[index];
                if (resource.privateParent is { } parent)
                {
                    parent.RollbackNestedCheckpoint(depth);
                }
                else
                {
                    resource.owner.RollbackPublicationCheckpoint(depth);
                }
            }
            throw;
        }
        return source;
    }

    private void RequireFieldCheckpoint(int depth)
    {
        if (depth <= 0 || CheckpointDepth != depth || fieldResources!.Values.Any(resource => resource.CheckpointDepth != depth))
        {
            throw new InvalidOperationException("Field resource checkpoint completion requires the current matching parent depth.");
        }
    }

    private void RequireRange(uint address, int length)
    {
        RequireActive();
        NativeBattleFileStaging.RequirePhysicalRange(address, length);
    }

    internal void RequireActive()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (publicationFailed)
        {
            throw new InvalidOperationException("Rejected composite staging publication must be disposed before rebuilding its resource scopes.");
        }
        overlay.RequireLayerActive();
    }

    public void Dispose()
    {
        if (!disposed)
        {
            if (fieldResources is not null)
            {
                foreach (NativeBattleFileStagingTransaction resource in fieldResources.Values)
                {
                    resource.Dispose();
                }
            }
            owner.CloseReadWindows(this);
            disposed = true;
            owner.ReturnOverlay(overlay);
        }
    }
}
