namespace Memoria.FF8.Runtime;

/// <summary>Reusable unpublished byte/knowledge/provenance changes over one retained authority.</summary>
internal sealed class NativeBattleArenaOverlay
{
    private readonly record struct PointerChange(bool Present, NativeBattleResourcePointer? Value);
    private readonly record struct ByteUndo(int Offset, bool Present, byte Data, bool Known, bool DataStore);
    private readonly record struct PointerUndo(int Offset, bool Present, PointerChange Value);
    private NativeBattleEffectArena? source;
    private byte[] data = [];
    private bool[] known = [];
    private bool[] present = [];
    private bool[] dataStore = [];
    private readonly NativeBattleArenaCoverage dataRanges = new();
    private readonly NativeBattleArenaCoverage unknownRanges = new();
    private bool[] checkpointSaved = [];
    private readonly List<int> dirty = [];
    private readonly Dictionary<int, PointerChange> pointers = [];
    private readonly List<int> pointerScratch = [];
    private readonly List<ByteUndo> byteUndo = [];
    private readonly List<PointerUndo> pointerUndo = [];
    private readonly HashSet<int> pointerSaved = [];
    private bool checkpoint;
    private bool publicationNested;
    private int checkpointDirtyCount;
    private int checkpointExtent;
    private int extent;

    internal NativeBattleEffectArena Source
    {
        get
        {
            NativeBattleEffectArena owner = source ?? throw new ObjectDisposedException(nameof(NativeBattleArenaOverlay));
            owner.Validate();
            return owner;
        }
    }
    internal bool HasPendingChanges => checkpoint || dirty.Count != 0 || pointers.Count != 0;
    internal int StoredLength => Math.Max(Source.CommittedStoredLength, extent);

    internal void ValidateExplicitCoverage(int capacity)
    {
        NativeBattleEffectArena owner = Source;
        owner.ValidateExtent(capacity);
        if (StoredLength > capacity)
        {
            throw new InvalidDataException("Native arena overlay exceeds its bounded packet storage.");
        }
        NativeBattleArenaCoverage? classified = owner.Classification;
        if (classified?.Prefix == capacity)
        {
            return;
        }
        int range = 0;
        int covered = 0;
        while (covered < capacity)
        {
            while (classified is not null && range < classified.Ranges.Count && classified.Ranges[range].End <= covered)
            {
                range++;
            }
            if (classified is not null && range < classified.Ranges.Count && classified.Ranges[range].Offset <= covered)
            {
                covered = classified.Ranges[range].End;
            }
            else if (covered < present.Length && present[covered])
            {
                covered++;
            }
            else
            {
                throw new InvalidDataException("Native packet storage has unclassified implicit cleared holes.");
            }
        }
    }

    internal NativeBattleArenaRanges DataRanges()
    {
        dataRanges.Ranges.Clear();
        foreach (NativeBattleEffectStorageRange range in Source.EnumerateDataRanges(true))
        {
            dataRanges.Add(range.Offset, range.Length);
        }
        foreach (int offset in dirty)
        {
            if (dataStore[offset])
            {
                dataRanges.Add(offset, 1);
            }
        }
        return new(dataRanges);
    }

    internal NativeBattleArenaRanges UnknownRanges()
    {
        unknownRanges.Ranges.Clear();
        foreach (NativeBattleEffectStorageRange range in Source.EnumerateUnknownRanges(true))
        {
            unknownRanges.Add(range.Offset, range.Length);
        }
        foreach (int offset in dirty)
        {
            if (known[offset])
            {
                unknownRanges.Remove(offset, 1);
            }
            else
            {
                unknownRanges.Add(offset, 1);
            }
        }
        return new(unknownRanges);
    }

    internal void Begin(NativeBattleEffectArena owner)
    {
        if (source is not null)
        {
            throw new InvalidOperationException("Native arena overlay already has an owner.");
        }
        source = owner;
    }

    internal void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> destinationKnown)
    {
        Source.RequireRange(offset, destination.Length);
        if (destination.Length != destinationKnown.Length)
        {
            throw new ArgumentException("Native arena data and knownness destinations must have equal lengths.", nameof(destinationKnown));
        }
        if (AllPresent(offset, destination.Length))
        {
            NativeBattleTelemetry.Copy(1002, data.AsSpan(offset, destination.Length), destination);
            NativeBattleTelemetry.Copy(1002, known.AsSpan(offset, destination.Length), destinationKnown);
            return;
        }
        Source.ReadCommittedWithKnownness(offset, destination, destinationKnown);
        for (int index = 0; index < destination.Length && offset + index < present.Length; index++)
        {
            int current = offset + index;
            if (present[current])
            {
                destination[index] = data[current];
                destinationKnown[index] = known[current];
            }
        }
    }

    internal void ReadInto(int offset, Span<byte> destination)
    {
        Source.RequireRange(offset, destination.Length);
        if (AllPresent(offset, destination.Length))
        {
            if (known.AsSpan(offset, destination.Length).Contains(false))
            {
                throw new NotSupportedException("Native arena overlay read intersects unresolved physical bytes.");
            }
            NativeBattleTelemetry.Copy(1002, data.AsSpan(offset, destination.Length), destination);
            return;
        }
        bool touched = false;
        for (int index = 0; index < destination.Length && offset + index < present.Length; index++)
        {
            touched |= present[offset + index];
        }
        if (!touched)
        {
            Source.ReadCommittedInto(offset, destination);
            return;
        }
        Span<byte> scratch = stackalloc byte[256];
        Span<bool> flags = stackalloc bool[256];
        for (int cursor = 0; cursor < destination.Length;)
        {
            int count = Math.Min(scratch.Length, destination.Length - cursor);
            ReadWithKnownness(offset + cursor, scratch[..count], flags[..count]);
            if (flags[..count].Contains(false))
            {
                throw new NotSupportedException("Native arena overlay read intersects unresolved physical bytes.");
            }
            cursor += count;
        }
        for (int cursor = 0; cursor < destination.Length;)
        {
            int count = Math.Min(flags.Length, destination.Length - cursor);
            ReadWithKnownness(offset + cursor, destination.Slice(cursor, count), flags[..count]);
            cursor += count;
        }
    }

    internal void Write(int offset, ReadOnlySpan<byte> bytes)
    {
        Source.RequireRange(offset, bytes.Length);
        if (bytes.IsEmpty)
        {
            return;
        }
        PrepareWrite(offset, bytes.Length);
        InvalidatePointers(offset, bytes.Length);
        for (int index = 0; index < bytes.Length; index++)
        {
            int current = offset + index;
            SaveByte(current);
            data[current] = bytes[index];
            dataStore[current] = true;
            known[current] = true;
            MarkPresent(current);
        }
        extent = Math.Max(extent, offset + bytes.Length);
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, bytes.Length);
    }

    internal void WriteWithKnownness(int offset, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> flags)
    {
        Source.RequireRange(offset, bytes.Length);
        if (bytes.Length != flags.Length)
        {
            throw new ArgumentException("Native data and knownness inputs must have equal lengths.", nameof(flags));
        }
        if (bytes.IsEmpty)
        {
            return;
        }
        PrepareWrite(offset, bytes.Length);
        InvalidatePointers(offset, bytes.Length);
        for (int index = 0; index < bytes.Length; index++)
        {
            int current = offset + index;
            SaveByte(current);
            data[current] = bytes[index];
            dataStore[current] = true;
            known[current] = flags[index];
            MarkPresent(current);
        }
        extent = Math.Max(extent, offset + bytes.Length);
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, bytes.Length);
    }

    internal void WriteRetainedBytes(int offset, ReadOnlySpan<byte> bytes)
    {
        NativeBattleEffectArena owner = Source;
        owner.RequireRange(offset, bytes.Length);
        if (bytes.IsEmpty)
        {
            return;
        }
        PrepareWrite(offset, bytes.Length);
        InvalidatePointers(offset, bytes.Length);
        for (int index = 0; index < bytes.Length; index++)
        {
            int current = offset + index;
            bool previousKnown = present[current] ? known[current] : owner.Working.IsKnown(owner.WorkingOffset + current, 1);
            SaveByte(current);
            data[current] = bytes[index];
            dataStore[current] = true;
            known[current] = previousKnown;
            MarkPresent(current);
        }
        extent = Math.Max(extent, offset + bytes.Length);
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, bytes.Length);
    }

    internal void MarkUnknown(int offset, int length)
    {
        Source.RequireRange(offset, length);
        if (length == 0)
        {
            return;
        }
        PrepareWrite(offset, length);
        InvalidatePointers(offset, length);
        for (int index = 0; index < length; index++)
        {
            SaveByte(offset + index);
        }
        for (int index = 0; index < length;)
        {
            if (present[offset + index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < length && !present[offset + index])
            {
                index++;
            }
            Source.ReadCommittedWithKnownness(offset + first, data.AsSpan(offset + first, index - first), known.AsSpan(offset + first, index - first));
        }
        for (int index = 0; index < length; index++)
        {
            int current = offset + index;
            known[current] = false;
            MarkPresent(current);
        }
        extent = Math.Max(extent, offset + length);
    }

    internal bool TryGetResourcePointer(int offset, out NativeBattleResourcePointer? pointer)
    {
        Source.RequireRange(offset, 4);
        if (pointers.TryGetValue(offset, out PointerChange changed))
        {
            pointer = changed.Value;
            return changed.Present;
        }
        return Source.TryGetCommittedResourcePointer(offset, out pointer);
    }

    internal IEnumerable<KeyValuePair<int, NativeBattleResourcePointer?>> EnumerateResourcePointers()
    {
        foreach ((int offset, NativeBattleResourcePointer? pointer) in Source.EnumerateResourcePointers())
        {
            if (!pointers.TryGetValue(offset, out PointerChange changed))
            {
                yield return new(offset, pointer);
            }
            else if (changed.Present)
            {
                yield return new(offset, changed.Value);
            }
        }
        foreach ((int offset, PointerChange changed) in pointers)
        {
            if (changed.Present && !Source.TryGetCommittedResourcePointer(offset, out _))
            {
                yield return new(offset, changed.Value);
            }
        }
    }

    internal void SetResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        Source.RequireRange(offset, 4);
        NativeBattleEffectArena.ValidatePointer(pointer);
        SavePointer(offset);
        pointers[offset] = new(true, pointer);
    }

    internal void BeginCheckpoint()
    {
        _ = Source;
        if (checkpoint)
        {
            throw new InvalidOperationException("Native arena overlay checkpoints cannot be nested.");
        }
        checkpoint = true;
        checkpointDirtyCount = dirty.Count;
        checkpointExtent = extent;
    }

    internal void CommitCheckpoint()
    {
        RequireCheckpoint();
        checkpoint = false;
        foreach (ByteUndo undo in byteUndo)
        {
            checkpointSaved[undo.Offset] = false;
        }
        byteUndo.Clear();
        pointerUndo.Clear();
        pointerSaved.Clear();
    }

    internal void RollbackCheckpoint()
    {
        RequireCheckpoint();
        for (int index = byteUndo.Count - 1; index >= 0; index--)
        {
            ByteUndo undo = byteUndo[index];
            present[undo.Offset] = undo.Present;
            data[undo.Offset] = undo.Data;
            known[undo.Offset] = undo.Known;
            dataStore[undo.Offset] = undo.DataStore;
        }
        for (int index = pointerUndo.Count - 1; index >= 0; index--)
        {
            PointerUndo undo = pointerUndo[index];
            if (undo.Present)
            {
                pointers[undo.Offset] = undo.Value;
            }
            else
            {
                pointers.Remove(undo.Offset);
            }
        }
        dirty.RemoveRange(checkpointDirtyCount, dirty.Count - checkpointDirtyCount);
        extent = checkpointExtent;
        CommitCheckpoint();
    }

    internal NativeBattleWorkingMemory Kernel => Source.Working;

    internal NativeBattleEffectArena Commit()
    {
        if (dirty.Count == 0 && pointers.Count == 0)
        {
            return Source;
        }
        PrepareCommit();
        NativeBattleWorkingMemory memory = Kernel;
        BeginPublication();
        try
        {
            ApplyPrepared();
            PublishPrepared();
        }
        catch
        {
            RollbackPublication();
            throw;
        }
        CommitPublication();
        return Source;
    }

    internal void BeginPublication()
    {
        publicationNested = Kernel.HasCheckpoint;
        if (publicationNested)
        {
            Kernel.BeginNestedCheckpoint();
        }
        else
        {
            Kernel.BeginCheckpoint();
        }
    }

    internal void CommitPublication()
    {
        if (publicationNested)
        {
            Kernel.CommitNestedCheckpoint();
        }
        else
        {
            Kernel.CommitCheckpoint();
        }
    }

    internal void RollbackPublication()
    {
        if (publicationNested)
        {
            Kernel.RollbackNestedCheckpoint();
        }
        else
        {
            Kernel.RollbackCheckpoint();
        }
    }

    internal void PrepareCommit()
    {
        NativeBattleEffectArena owner = Source;
        if (checkpoint)
        {
            throw new InvalidOperationException("Native arena overlay publication requires a committed overlay checkpoint.");
        }
        int pointerStores = 0;
        foreach (PointerChange pointer in pointers.Values)
        {
            if (pointer.Present)
            {
                NativeBattleEffectArena.ValidatePointer(pointer.Value);
                pointerStores++;
            }
        }
        dirty.Sort();
        int runs = 0;
        int last = -2;
        foreach (int index in dirty)
        {
            if (index != last + 1 || known[index] != known[last] || dataStore[index] != dataStore[last])
            {
                runs++;
            }
            last = index;
        }
        owner.PrepareCoverage(checked(runs + pointerStores));
    }

    internal void ApplyPrepared()
    {
        NativeBattleEffectArena owner = Source;
        NativeBattleWorkingMemory memory = owner.Working;
        memory.ReserveCheckpointCapacity(checked(dirty.Count + pointers.Count * 4), checked(memory.ResourcePointerCount + pointers.Count * 2));
        for (int index = 0; index < dirty.Count;)
        {
            int first = dirty[index++];
            int end = first + 1;
            while (index < dirty.Count && dirty[index] == end)
            {
                index++;
                end++;
            }
            memory.WriteWithKnownness(owner.WorkingOffset + first, data.AsSpan(first, end - first), known.AsSpan(first, end - first));
        }
        foreach ((int offset, PointerChange pointer) in pointers)
        {
            if (pointer.Present)
            {
                memory.SetResourcePointer(owner.WorkingOffset + offset, pointer.Value);
            }
        }
    }

    internal void PublishPrepared()
    {
        NativeBattleEffectArena owner = Source;
        owner.EnlistRangeMetadata();
        for (int index = 0; index < dirty.Count;)
        {
            int first = dirty[index++];
            int end = first + 1;
            while (index < dirty.Count && dirty[index] == end)
            {
                index++;
                end++;
            }
            owner.RecordCoverage(first, end - first);
            for (int cursor = first; cursor < end;)
            {
                bool value = known[cursor];
                int start = cursor++;
                while (cursor < end && known[cursor] == value)
                {
                    cursor++;
                }
                owner.RecordKnowledge(start, cursor - start, value);
            }
            for (int cursor = first; cursor < end;)
            {
                if (!dataStore[cursor])
                {
                    cursor++;
                    continue;
                }
                int start = cursor++;
                while (cursor < end && dataStore[cursor])
                {
                    cursor++;
                }
                owner.RecordDataCoverage(start, cursor - start);
            }
        }
        foreach ((int offset, PointerChange pointer) in pointers)
        {
            if (pointer.Present)
            {
                owner.RecordCoverage(offset, 4);
                bool numeric = (pointer.Value?.StagingAddress ?? pointer.Value?.Cpu?.PhysicalAddress) is not null;
                if (numeric)
                {
                    owner.RecordDataCoverage(offset, 4);
                }
                owner.RecordKnowledge(offset, 4, numeric);
            }
        }
        ClearChanges();
    }

    internal void Reset()
    {
        ClearChanges();
        source = null;
    }

    private void PrepareWrite(int offset, int length)
    {
        int required = offset + length;
        if (required > data.Length)
        {
            int capacity = Math.Min(Source.Bound, Math.Max(required, Math.Max(64, data.Length * 2)));
            Array.Resize(ref data, capacity);
            Array.Resize(ref known, capacity);
            Array.Resize(ref present, capacity);
            Array.Resize(ref dataStore, capacity);
            Array.Resize(ref checkpointSaved, capacity);
        }
        dirty.EnsureCapacity(checked(dirty.Count + length));
        if (checkpoint)
        {
            byteUndo.EnsureCapacity(checked(byteUndo.Count + length));
        }
    }

    private bool AllPresent(int offset, int length)
    {
        if (offset > present.Length - length)
        {
            return false;
        }
        return !present.AsSpan(offset, length).Contains(false);
    }

    private void SaveByte(int offset)
    {
        if (checkpoint && !checkpointSaved[offset])
        {
            checkpointSaved[offset] = true;
            byteUndo.Add(new(offset, present[offset], data[offset], known[offset], dataStore[offset]));
        }
    }

    private void MarkPresent(int offset)
    {
        if (!present[offset])
        {
            present[offset] = true;
            dirty.Add(offset);
        }
    }

    private void SavePointer(int offset)
    {
        if (checkpoint && pointerSaved.Add(offset))
        {
            bool present = pointers.TryGetValue(offset, out PointerChange value);
            pointerUndo.Add(new(offset, present, value));
        }
    }

    private void InvalidatePointers(int offset, int length)
    {
        pointerScratch.Clear();
        NativeBattleEffectArena owner = Source;
        using var retained = owner.Working.EnumerateResourcePointers();
        while (retained.MoveNext())
        {
            int key = retained.Current.Key - owner.WorkingOffset;
            if (key >= 0 && key <= owner.Bound - 4 && key < offset + length && key + 4 > offset)
            {
                pointerScratch.Add(key);
            }
        }
        foreach ((int key, PointerChange value) in pointers)
        {
            if (value.Present && key < offset + length && key + 4 > offset && !pointerScratch.Contains(key))
            {
                pointerScratch.Add(key);
            }
        }
        pointers.EnsureCapacity(pointers.Count + pointerScratch.Count);
        if (checkpoint)
        {
            pointerUndo.EnsureCapacity(pointerUndo.Count + pointerScratch.Count);
        }
        foreach (int key in pointerScratch)
        {
            SavePointer(key);
            pointers[key] = new(false, null);
        }
    }

    private void ClearChanges()
    {
        foreach (int index in dirty)
        {
            present[index] = false;
            dataStore[index] = false;
        }
        dirty.Clear();
        foreach (ByteUndo undo in byteUndo)
        {
            checkpointSaved[undo.Offset] = false;
        }
        pointers.Clear();
        pointerScratch.Clear();
        byteUndo.Clear();
        pointerUndo.Clear();
        pointerSaved.Clear();
        extent = 0;
        checkpoint = false;
    }

    private void RequireCheckpoint()
    {
        _ = Source;
        if (!checkpoint)
        {
            throw new InvalidOperationException("Native arena overlay has no current checkpoint.");
        }
    }
}
