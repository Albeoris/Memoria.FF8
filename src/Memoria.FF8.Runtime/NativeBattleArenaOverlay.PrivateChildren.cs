namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleArenaOverlay
{
    private NativeBattleArenaOverlay? privateParent;
    private int privateParentOffset;
    private long privateParentGeneration;
    private readonly List<ByteUndo> mergeBytes = [];
    private readonly List<PointerUndo> mergePointers = [];
    private readonly HashSet<int> mergePointerKeys = [];
    private bool mergingChild;
    private readonly NativeBattleArenaCoverage privateClassified = new();

    internal void BindPrivateParent(NativeBattleArenaOverlay parent, int offset)
    {
        parent.Source.RequireRange(offset, Source.Bound);
        privateParent = parent;
        privateParentOffset = offset;
        privateParentGeneration = ((INativeBattleReadWindowSource)parent).ReadWindowGeneration;
    }

    private void ValidatePrivateParent()
    {
        if (privateParent is not null)
        {
            ((INativeBattleReadWindowSource)privateParent).ValidateReadWindow(privateParentGeneration);
        }
    }

    private void ReadBaseWithKnownness(int offset, Span<byte> bytes, Span<bool> flags)
    {
        if (privateParent is not null)
        {
            privateParent.ReadWithKnownness(checked(privateParentOffset + offset), bytes, flags);
        }
        else
        {
            Source.ReadCommittedWithKnownness(offset, bytes, flags);
        }
    }

    private void ReadBaseInto(int offset, Span<byte> bytes)
    {
        if (privateParent is not null)
        {
            privateParent.ReadInto(checked(privateParentOffset + offset), bytes);
        }
        else
        {
            Source.ReadCommittedInto(offset, bytes);
        }
    }

    private bool BaseKnown(int offset)
    {
        if (privateParent is null)
        {
            return Source.Working.IsKnown(Source.WorkingOffset + offset, 1);
        }
        Span<byte> bytes = stackalloc byte[1];
        Span<bool> flags = stackalloc bool[1];
        privateParent.ReadWithKnownness(privateParentOffset + offset, bytes, flags);
        return flags[0];
    }

    private bool TryGetBaseResourcePointer(int offset, out NativeBattleResourcePointer? pointer)
    {
        if (privateParent is not null && OverlapsPrivateBytes(offset, 4))
        {
            pointer = null;
            return false;
        }
        return privateParent is not null ? privateParent.TryGetResourcePointer(checked(privateParentOffset + offset), out pointer) : Source.TryGetCommittedResourcePointer(offset, out pointer);
    }

    private bool OverlapsPrivateBytes(int offset, int length)
    {
        int end = Math.Min(present.Length, checked(offset + length));
        for (int index = offset; index < end; index++)
        {
            if (present[index])
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerable<KeyValuePair<int, NativeBattleResourcePointer?>> EnumerateBaseResourcePointers()
    {
        if (privateParent is null)
        {
            foreach (KeyValuePair<int, NativeBattleResourcePointer?> pointer in Source.EnumerateResourcePointers(true))
            {
                yield return pointer;
            }
            yield break;
        }
        foreach ((int offset, NativeBattleResourcePointer? pointer) in privateParent.EnumerateResourcePointers())
        {
            int relative = offset - privateParentOffset;
            if (relative >= 0 && relative <= Source.Bound - 4 && !OverlapsPrivateBytes(relative, 4))
            {
                yield return new(relative, pointer);
            }
        }
    }

    private IEnumerable<NativeBattleEffectStorageRange> EnumerateBaseRanges(bool unknown)
    {
        if (privateParent is null)
        {
            foreach (NativeBattleEffectStorageRange range in unknown ? Source.EnumerateUnknownRanges(true) : Source.EnumerateDataRanges(true))
            {
                yield return range;
            }
            yield break;
        }
        NativeBattleArenaRanges ranges = unknown ? privateParent.UnknownRanges() : privateParent.DataRanges();
        foreach (NativeBattleEffectStorageRange range in ranges)
        {
            int first = Math.Max(privateParentOffset, range.Offset);
            int end = Math.Min(privateParentOffset + Source.Bound, range.Offset + range.Length);
            if (first < end)
            {
                yield return new(first - privateParentOffset, end - first);
            }
        }
    }

    private NativeBattleEffectArena CommitPrivateChild()
    {
        NativeBattleArenaOverlay parent = privateParent!;
        _ = Source;
        if (checkpoint)
        {
            throw new InvalidOperationException("Private arena publication requires a committed child checkpoint.");
        }
        parent.MergePrivateChild(this, privateParentOffset);
        ClearChanges();
        return Source;
    }

    private void MergePrivateChild(NativeBattleArenaOverlay child, int destinationOffset)
    {
        _ = Source;
        if (mergingChild)
        {
            throw new InvalidOperationException("Private arena publication cannot reenter its parent merge.");
        }
        mergeBytes.Clear();
        mergePointers.Clear();
        mergePointerKeys.Clear();
        int originalDirtyCount = dirty.Count;
        int originalExtent = extent;
        int originalByteUndoCount = byteUndo.Count;
        int originalPointerUndoCount = pointerUndo.Count;
        child.dirty.Sort();
        mergeBytes.EnsureCapacity(child.dirty.Count);
        foreach (int offset in child.dirty)
        {
            int target = checked(destinationOffset + offset);
            PrepareWrite(target, 1);
            mergeBytes.Add(new(target, present[target], data[target], known[target], dataStore[target]));
        }
        foreach ((int offset, NativeBattleResourcePointer? _) in EnumerateResourcePointers())
        {
            foreach (int changed in child.dirty)
            {
                int target = checked(destinationOffset + changed);
                if (offset < target + 1 && offset + 4 > target)
                {
                    mergePointerKeys.Add(offset);
                    break;
                }
            }
        }
        foreach (int offset in child.pointers.Keys)
        {
            mergePointerKeys.Add(checked(destinationOffset + offset));
        }
        mergePointers.EnsureCapacity(mergePointerKeys.Count);
        pointers.EnsureCapacity(checked(pointers.Count + mergePointerKeys.Count));
        foreach (int offset in mergePointerKeys)
        {
            bool exists = pointers.TryGetValue(offset, out PointerChange value);
            mergePointers.Add(new(offset, exists, value));
        }
        dirty.EnsureCapacity(checked(dirty.Count + child.dirty.Count));
        if (checkpoint)
        {
            byteUndo.EnsureCapacity(checked(byteUndo.Count + child.dirty.Count));
            pointerUndo.EnsureCapacity(checked(pointerUndo.Count + mergePointerKeys.Count));
            pointerSaved.EnsureCapacity(checked(pointerSaved.Count + mergePointerKeys.Count));
        }
        mergingChild = true;
        try
        {
            for (int index = 0; index < child.dirty.Count;)
            {
                int first = child.dirty[index++];
                int end = first + 1;
                while (index < child.dirty.Count && child.dirty[index] == end && child.dataStore[end] == child.dataStore[first])
                {
                    index++;
                    end++;
                }
                if (child.dataStore[first])
                {
                    WriteWithKnownness(destinationOffset + first, child.data.AsSpan(first, end - first), child.known.AsSpan(first, end - first));
                }
                else
                {
                    MarkUnknown(destinationOffset + first, end - first);
                }
            }
            foreach ((int offset, PointerChange value) in child.pointers)
            {
                int target = checked(destinationOffset + offset);
                SavePointer(target);
                pointers[target] = value;
            }
        }
        catch
        {
            for (int index = mergeBytes.Count - 1; index >= 0; index--)
            {
                ByteUndo undo = mergeBytes[index];
                present[undo.Offset] = undo.Present;
                data[undo.Offset] = undo.Data;
                known[undo.Offset] = undo.Known;
                dataStore[undo.Offset] = undo.DataStore;
            }
            foreach (PointerUndo undo in mergePointers)
            {
                if (undo.Present)
                {
                    pointers[undo.Offset] = undo.Value;
                }
                else
                {
                    pointers.Remove(undo.Offset);
                }
            }
            dirty.RemoveRange(originalDirtyCount, dirty.Count - originalDirtyCount);
            extent = originalExtent;
            for (int index = originalByteUndoCount; index < byteUndo.Count; index++)
            {
                checkpointSaved[byteUndo[index].Offset] = false;
            }
            for (int index = originalPointerUndoCount; index < pointerUndo.Count; index++)
            {
                pointerSaved.Remove(pointerUndo[index].Offset);
            }
            byteUndo.RemoveRange(originalByteUndoCount, byteUndo.Count - originalByteUndoCount);
            pointerUndo.RemoveRange(originalPointerUndoCount, pointerUndo.Count - originalPointerUndoCount);
            throw;
        }
        finally
        {
            mergingChild = false;
            mergeBytes.Clear();
            mergePointers.Clear();
            mergePointerKeys.Clear();
        }
    }
}
