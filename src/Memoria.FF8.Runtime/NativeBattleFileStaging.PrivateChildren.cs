namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleFileStagingOverlay
{
    private NativeBattleFileStagingOverlay? parent;
    private long parentGeneration;
    private long generation;
    private bool leased;
    internal NativeBattleFileStagingTransaction? TransactionOwner { get; set; }

    internal void BeginPrivateChild(NativeBattleFileStagingOverlay? ancestor)
    {
        ancestor?.RequireLayerActive();
        parent = ancestor;
        parentGeneration = ancestor?.generation ?? 0;
        IsPublicLayer = false;
        BaseAddress = ancestor?.BaseAddress ?? source.BaseAddress;
    }

    internal void RequireLayerActive()
    {
        ObjectDisposedException.ThrowIf(!leased, this);
        if (parent is not null)
        {
            parent.RequireLayerActive();
            ObjectDisposedException.ThrowIf(parent.generation != parentGeneration, this);
        }
    }

    private void ReadBaseline(uint address, Span<byte> data, Span<bool> known)
    {
        RequireLayerActive();
        if (parent is null)
        {
            source.ReadWithKnownness(address, data, known);
        }
        else
        {
            parent.ReadWithKnownness(address, data, known);
        }
    }

    private bool BaselineExplicitKnown(uint address) => parent is null ? source.IsExplicitKnown(address) : parent.ExplicitKnown(address);

    private bool ExplicitKnown(uint address)
    {
        RequireLayerActive();
        if (bytes.TryGetValue(address, out Cell cell))
        {
            return cell.Sequence >= LastUnknown(address);
        }
        return LastUnknown(address) == 0 && BaselineExplicitKnown(address);
    }

    private ulong LastWrittenEnd(NativeBattleFileStagingOverlay reader)
    {
        ulong last = parent?.LastWrittenEnd(reader) ?? reader.BaseAddress;
        foreach (var entry in bytes)
        {
            if (reader.ExplicitKnown(entry.Key))
            {
                last = Math.Max(last, (ulong)entry.Key + 1);
            }
        }
        return last;
    }

    private bool BaselinePointer(uint address, out NativeBattleResourcePointer? pointer) => parent is null ? source.TryGetPointer(address, out pointer) : parent.TryGetPointer(address, out pointer);

    private void CollectVisiblePointerKeys(uint address, int length, List<uint> output)
    {
        if (parent is null)
        {
            using var originals = source.EnumeratePointers();
            while (originals.MoveNext())
            {
                uint current = originals.Current.Key;
                if (Overlaps(current, address, length) && !output.Contains(current))
                {
                    output.Add(current);
                }
            }
        }
        else
        {
            parent.CollectVisiblePointerKeys(address, length, output);
        }
        foreach (uint current in pointers.Keys)
        {
            if (Overlaps(current, address, length) && !output.Contains(current))
            {
                output.Add(current);
            }
        }
    }

    private IEnumerable<KeyValuePair<uint, NativeBattleResourcePointer?>> BaselinePointers()
    {
        if (parent is not null)
        {
            foreach (var entry in parent.EnumerateResourcePointers())
            {
                yield return entry;
            }
        }
        else
        {
            using var originals = source.EnumeratePointers();
            while (originals.MoveNext())
            {
                yield return originals.Current;
            }
        }
    }

    private bool BaselineZero(uint address, int length) => parent is null ? source.IntersectsLoaderZero(address, length) : parent.IntersectsLoaderZero(address, length);

    private void PublishToParent()
    {
        RequireLayerActive();
        if (checkpoint)
        {
            throw new InvalidOperationException("Native child staging publication requires a closed local checkpoint.");
        }
        NativeBattleFileStagingOverlay destination = parent!;
        destination.bytes.EnsureCapacity(checked(destination.bytes.Count + bytes.Count + 1));
        destination.pointers.EnsureCapacity(checked(destination.pointers.Count + pointers.Count + 1));
        destination.byteUndo.EnsureCapacity(checked(destination.byteUndo.Count + bytes.Count));
        destination.unknown.EnsureCapacity(checked(destination.unknown.Count + unknown.Count + bytes.Count));
        destination.forgottenZeros.EnsureCapacity(checked(destination.forgottenZeros.Count + forgottenZeros.Count));
        int depth = destination.BeginNestedCheckpoint();
        try
        {
            destination.Rebase(Math.Min(BaseAddress, destination.BaseAddress));
            foreach (NativeBattleLoaderZeroRegion range in forgottenZeros)
            {
                destination.ForgetLoaderZero(range.Address, range.Length);
            }
            foreach (Unknown range in unknown)
            {
                destination.MarkUnknown(range.Address, range.Length);
            }
            sorted.Clear();
            sorted.EnsureCapacity(bytes.Count);
            foreach (uint address in bytes.Keys)
            {
                sorted.Add(address);
            }
            sorted.Sort();
            Span<byte> data = stackalloc byte[NativeBattleFileStagingWorkingOwner.PageLength];
            Span<bool> known = stackalloc bool[NativeBattleFileStagingWorkingOwner.PageLength];
            for (int index = 0; index < sorted.Count;)
            {
                uint first = sorted[index];
                int count = 0;
                do
                {
                    uint address = sorted[index++];
                    Cell cell = bytes[address];
                    data[count] = cell.Data;
                    known[count++] = cell.Sequence >= LastUnknown(address);
                }
                while (count < data.Length && index < sorted.Count && (ulong)first + (uint)count == sorted[index]);
                destination.Write(first, data[..count]);
                for (int lane = 0; lane < count;)
                {
                    if (known[lane])
                    {
                        lane++;
                        continue;
                    }
                    int begin = lane++;
                    while (lane < count && !known[lane])
                    {
                        lane++;
                    }
                    destination.MarkUnknown(checked(first + (uint)begin), lane - begin);
                }
            }
            foreach (var entry in pointers)
            {
                destination.StorePointer(entry.Key, entry.Value);
            }
            destination.CommitNestedCheckpoint(depth);
        }
        catch
        {
            if (destination.CheckpointDepth == depth)
            {
                destination.RollbackNestedCheckpoint(depth);
            }
            throw;
        }
        Reset();
        Begin();
        BeginPrivateChild(destination);
    }
}

internal sealed partial class NativeBattleFileStagingWorkingOwner
{
    internal NativeBattleFileStagingOverlay? EffectivePublicOverlay()
    {
        NativeBattleFileStagingOverlay? result = null;
        foreach (NativeBattleFileStagingOverlay overlay in activeOverlays)
        {
            if (!overlay.IsPublicLayer)
            {
                continue;
            }
            if (result is not null)
            {
                throw new InvalidOperationException("A borrowed staging view requires one unambiguous public overlay.");
            }
            overlay.RequireLayerActive();
            result = overlay;
        }
        return result;
    }
}

public sealed partial record NativeBattleFileStaging
{
    internal NativeBattleFileStaging EffectiveBorrowView()
    {
        if (Transaction is not null)
        {
            Transaction.RequireActive();
            return this;
        }
        NativeBattleFileStagingOverlay? layer = WorkingOwner.EffectivePublicOverlay();
        return layer is null ? this : layer.TransactionOwner?.View ?? throw new InvalidOperationException("Native public staging layer has no active transaction owner.");
    }

    internal NativeBattleStagingReadWindow OpenEffectiveReadWindow(uint address, int length) => EffectiveBorrowView().OpenReadWindow(address, length);

    internal IEnumerable<KeyValuePair<uint, NativeBattleResourcePointer?>> EnumerateEffectivePointers()
    {
        if (snapshot)
        {
            foreach (var entry in resourcePointers)
            {
                yield return entry;
            }
            yield break;
        }
        NativeBattleFileStaging current = EffectiveBorrowView();
        if (current.Transaction is { } transaction)
        {
            foreach (var entry in transaction.EnumerateResourcePointers())
            {
                yield return entry;
            }
        }
        else
        {
            using var pointers = current.WorkingOwner.EnumeratePointers();
            while (pointers.MoveNext())
            {
                yield return pointers.Current;
            }
        }
    }

    internal NativeBattleFileStaging BorrowOwner() => new(WorkingOwner);

    internal void ReadEffectiveWithKnownness(uint address, Span<byte> data, Span<bool> known)
    {
        RequirePhysicalRange(address, data.Length);
        if (data.Length != known.Length)
        {
            throw new ArgumentException("Native staging data and knownness destinations must match.", nameof(known));
        }
        NativeBattleFileStagingWorkingOwner current = WorkingOwner;
        NativeBattleFileStagingOverlay? layer = Transaction is { } transaction ? transaction.ActiveOverlay : current.EffectivePublicOverlay();
        if (layer is null)
        {
            current.ReadWithKnownness(address, data, known);
        }
        else
        {
            layer.ReadWithKnownness(address, data, known);
        }
    }
}
