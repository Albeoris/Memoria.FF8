using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Reusable changed-byte journal against persistent staging; no source pages are hydrated into the overlay.</summary>
internal sealed class NativeBattleFileStagingOverlay(NativeBattleFileStagingWorkingOwner source)
{
    private readonly record struct Cell(byte Data, long Sequence);
    private readonly record struct Unknown(uint Address, int Length, long Sequence);
    private readonly record struct Pointer(bool Present, NativeBattleResourcePointer? Value);
    private readonly record struct CellUndo(uint Address, bool Present, Cell Value);
    private readonly record struct PointerUndo(uint Address, bool Present, Pointer Value);
    private readonly NativeBattleStagingDeltaMap<Cell> bytes = new();
    private readonly List<Unknown> unknown = [];
    private readonly List<NativeBattleLoaderZeroRegion> forgottenZeros = [];
    private readonly NativeBattleStagingDeltaMap<Pointer> pointers = new();
    private readonly List<uint> invalidated = [];
    private readonly List<uint> sorted = [];
    private readonly List<CellUndo> byteUndo = [];
    private readonly List<PointerUndo> pointerUndo = [];
    private long sequence;
    private readonly record struct CheckpointMark(int Bytes, int Pointers, int Unknown, int Zeros, long Sequence, uint BaseAddress);
    private readonly List<CheckpointMark> checkpointMarks = [];
    private bool checkpoint => checkpointMarks.Count != 0;
    public int CheckpointDepth => checkpointMarks.Count;

    public uint BaseAddress { get; private set; }
    public ulong KnownExtentEnd
    {
        get
        {
            ulong last = BaseAddress;
            var pages = source.EnumeratePages();
            while (pages.MoveNext())
            {
                var entry = pages.Current;
                for (int index = entry.Value.StoredLength - 1; index >= 0 && (ulong)entry.Key + (uint)index + 1 > last; index--)
                {
                    uint address = checked(entry.Key + (uint)index);
                    if (!bytes.TryGetValue(address, out _) && entry.Value.IsKnown(index, 1) && LastUnknown(address) == 0)
                    {
                        last = (ulong)address + 1;
                        break;
                    }
                }
            }
            foreach (var entry in bytes)
            {
                if (entry.Value.Sequence > LastUnknown(entry.Key))
                {
                    last = Math.Max(last, (ulong)entry.Key + 1);
                }
            }
            return last;
        }
    }

    public void Begin()
    {
        BaseAddress = source.BaseAddress;
    }

    public void Rebase(uint address)
    {
        if (address > BaseAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }
        source.ValidateRebase(Math.Min(address, source.BaseAddress));
        foreach (uint stored in bytes.Keys)
        {
            if ((ulong)stored + 1 - address > int.MaxValue)
            {
                throw new InvalidDataException("Native staging overlay rebase exceeds its offset representation.");
            }
        }
        BaseAddress = address;
    }

    public bool IsKnown(uint address, int length)
    {
        Span<byte> data = stackalloc byte[64];
        Span<bool> known = stackalloc bool[64];
        for (int offset = 0; offset < length;)
        {
            int count = Math.Min(length - offset, data.Length);
            ReadWithKnownness(checked(address + (uint)offset), data[..count], known[..count]);
            if (known[..count].Contains(false))
            {
                return false;
            }
            offset += count;
        }
        return true;
    }

    public void ReadWithKnownness(uint address, Span<byte> data, Span<bool> known)
    {
        source.ReadWithKnownness(address, data, known);
        for (int index = 0; index < data.Length; index++)
        {
            uint current = checked(address + (uint)index);
            bool written = bytes.TryGetValue(current, out Cell cell);
            if (!written && !source.IsExplicitKnown(current) && ForgottenZero(current))
            {
                known[index] = false;
            }
            if (written)
            {
                data[index] = cell.Data;
                known[index] = true;
            }
            long unknownSequence = LastUnknown(current);
            if (unknownSequence > 0 && (!written || unknownSequence > cell.Sequence))
            {
                known[index] = false;
            }
        }
    }

    public void Write(uint address, ReadOnlySpan<byte> input)
    {
        bytes.EnsureCapacity(checked(bytes.Count + input.Length));
        if (checkpoint)
        {
            byteUndo.EnsureCapacity(checked(byteUndo.Count + input.Length));
        }
        InvalidatePointers(address, input.Length);
        long currentSequence = ++sequence;
        for (int index = 0; index < input.Length; index++)
        {
            uint current = checked(address + (uint)index);
            bool present = bytes.TryGetValue(current, out Cell previous);
            if (checkpoint)
            {
                byteUndo.Add(new(current, present, previous));
            }
            bytes[current] = new(input[index], currentSequence);
        }
        NativeBattleTelemetry.Record(1035, NativeBattleTrafficKind.CpuCopyPayload, input.Length);
    }

    public void MarkUnknown(uint address, int length)
    {
        if (length == 0)
        {
            return;
        }
        unknown.EnsureCapacity(checked(unknown.Count + 1));
        InvalidatePointers(address, length);
        unknown.Add(new(address, length, ++sequence));
    }

    public void ForgetLoaderZero(uint address, int length)
    {
        if (length != 0)
        {
            forgottenZeros.Add(new(address, length));
        }
    }

    public bool TryGetPointer(uint address, out NativeBattleResourcePointer? pointer)
    {
        if (pointers.TryGetValue(address, out Pointer change))
        {
            pointer = change.Value;
            return change.Present;
        }
        return source.TryGetPointer(address, out pointer);
    }

    public void SetPointerMetadata(uint address, NativeBattleResourcePointer? pointer)
    {
        StorePointer(address, new(true, pointer));
    }

    public void InvalidatePointers(uint address, int length)
    {
        if (length == 0)
        {
            return;
        }
        invalidated.Clear();
        var originals = source.EnumeratePointers();
        while (originals.MoveNext())
        {
            uint current = originals.Current.Key;
            if (Overlaps(current, address, length))
            {
                invalidated.Add(current);
            }
        }
        foreach (uint current in pointers.Keys)
        {
            if (Overlaps(current, address, length) && !invalidated.Contains(current))
            {
                invalidated.Add(current);
            }
        }
        if (checkpoint)
        {
            pointerUndo.EnsureCapacity(checked(pointerUndo.Count + invalidated.Count));
        }
        foreach (uint current in invalidated)
        {
            StorePointer(current, new(false, null));
        }
    }

    public IEnumerable<KeyValuePair<uint, NativeBattleResourcePointer?>> EnumerateResourcePointers()
    {
        using var originals = source.EnumeratePointers();
        while (originals.MoveNext())
        {
            var entry = originals.Current;
            if (pointers.TryGetValue(entry.Key, out Pointer change))
            {
                if (change.Present)
                {
                    yield return new(entry.Key, change.Value);
                }
            }
            else
            {
                yield return entry;
            }
        }
        foreach (var entry in pointers)
        {
            if (entry.Value.Present && !source.TryGetPointer(entry.Key, out _))
            {
                yield return new(entry.Key, entry.Value.Value);
            }
        }
    }

    public ImmutableDictionary<uint, NativeBattleResourcePointer?> CapturePointers()
    {
        var result = source.CapturePointers().ToBuilder();
        foreach (var entry in pointers)
        {
            if (entry.Value.Present)
            {
                result[entry.Key] = entry.Value.Value;
            }
            else
            {
                result.Remove(entry.Key);
            }
        }
        return result.ToImmutable();
    }

    public bool IntersectsLoaderZero(uint address, int length)
    {
        if (!source.IntersectsLoaderZero(address, length))
        {
            return false;
        }
        // Only diagnostic callers use this predicate on a view; byte reads apply exact exclusions.
        for (int index = 0; index < length; index++)
        {
            uint current = checked(address + (uint)index);
            if (source.IntersectsLoaderZero(current, 1) && LastUnknown(current) == 0 && !ForgottenZero(current))
            {
                return true;
            }
        }
        return false;
    }

    public void BeginCheckpoint()
    {
        if (checkpoint)
        {
            throw new InvalidOperationException("Native staging overlay checkpoints cannot be nested.");
        }
        BeginNestedCheckpoint();
    }

    public int BeginNestedCheckpoint()
    {
        checkpointMarks.Add(new(byteUndo.Count, pointerUndo.Count, unknown.Count, forgottenZeros.Count, sequence, BaseAddress));
        return checkpointMarks.Count;
    }

    public void CommitCheckpoint()
    {
        RequireDepth(1);
        CommitNestedCheckpoint(1);
    }

    public void CommitNestedCheckpoint(int depth)
    {
        RequireDepth(depth);
        checkpointMarks.RemoveAt(checkpointMarks.Count - 1);
        if (!checkpoint)
        {
            byteUndo.Clear();
            pointerUndo.Clear();
        }
    }

    public void RollbackCheckpoint()
    {
        RequireDepth(1);
        RollbackNestedCheckpoint(1);
    }

    public void RollbackNestedCheckpoint(int depth)
    {
        RequireDepth(depth);
        CheckpointMark mark = checkpointMarks[^1];
        for (int index = byteUndo.Count - 1; index >= mark.Bytes; index--)
        {
            CellUndo entry = byteUndo[index];
            if (entry.Present)
            {
                bytes[entry.Address] = entry.Value;
            }
            else
            {
                bytes.Remove(entry.Address);
            }
        }
        for (int index = pointerUndo.Count - 1; index >= mark.Pointers; index--)
        {
            PointerUndo entry = pointerUndo[index];
            if (entry.Present)
            {
                pointers[entry.Address] = entry.Value;
            }
            else
            {
                pointers.Remove(entry.Address);
            }
        }
        byteUndo.RemoveRange(mark.Bytes, byteUndo.Count - mark.Bytes);
        pointerUndo.RemoveRange(mark.Pointers, pointerUndo.Count - mark.Pointers);
        unknown.RemoveRange(mark.Unknown, unknown.Count - mark.Unknown);
        forgottenZeros.RemoveRange(mark.Zeros, forgottenZeros.Count - mark.Zeros);
        sequence = mark.Sequence;
        BaseAddress = mark.BaseAddress;
        CommitNestedCheckpoint(depth);
    }

    public void Publish()
    {
        if (checkpoint)
        {
            throw new InvalidOperationException("Native staging publication requires a committed overlay boundary.");
        }
        source.BeginCheckpoint();
        try
        {
            source.Rebase(Math.Min(BaseAddress, source.BaseAddress));
            foreach (NativeBattleLoaderZeroRegion range in forgottenZeros)
            {
                source.ForgetLoaderZero(range.Address, range.Length);
            }
            foreach (Unknown range in unknown)
            {
                source.MarkUnknown(range.Address, range.Length);
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
                    uint current = sorted[index++];
                    Cell cell = bytes[current];
                    data[count] = cell.Data;
                    known[count] = LastUnknown(current) < cell.Sequence;
                    count++;
                }
                while (count < data.Length && index < sorted.Count && (ulong)first + (uint)count == sorted[index]);
                source.WriteWithKnownness(first, data[..count], known[..count]);
            }
            foreach (var entry in pointers)
            {
                source.SetPointerMetadata(entry.Key, entry.Value.Present, entry.Value.Value);
            }
            source.CommitCheckpoint();
        }
        catch
        {
            if (source.HasCheckpoint)
            {
                source.RollbackCheckpoint();
            }
            throw;
        }
        Reset();
        Begin();
    }

    public void Reset()
    {
        long cleared = bytes.Clear();
        unknown.Clear();
        forgottenZeros.Clear();
        cleared += pointers.Clear();
        invalidated.Clear();
        sorted.Clear();
        byteUndo.Clear();
        pointerUndo.Clear();
        checkpointMarks.Clear();
        sequence = 0;
        NativeBattleTelemetry.Record(1035, NativeBattleTrafficKind.ZeroFill, cleared);
    }

    private long LastUnknown(uint address)
    {
        for (int index = unknown.Count - 1; index >= 0; index--)
        {
            Unknown range = unknown[index];
            if (address >= range.Address && (ulong)address - range.Address < (uint)range.Length)
            {
                return range.Sequence;
            }
        }
        return 0;
    }

    public ImmutableArray<NativeBattleLoaderZeroRegion> CaptureLoaderZeros()
    {
        List<NativeBattleLoaderZeroRegion> ranges = [.. source.CaptureLoaderZeros()];
        foreach (Unknown range in unknown)
        {
            RemoveZero(ranges, range.Address, range.Length);
        }
        foreach (NativeBattleLoaderZeroRegion range in forgottenZeros)
        {
            RemoveZero(ranges, range.Address, range.Length);
        }
        return ranges.ToImmutableArray();
    }

    private bool ForgottenZero(uint address)
    {
        foreach (NativeBattleLoaderZeroRegion range in forgottenZeros)
        {
            if (address >= range.Address && (ulong)address - range.Address < (uint)range.Length)
            {
                return true;
            }
        }
        return false;
    }

    private static void RemoveZero(List<NativeBattleLoaderZeroRegion> ranges, uint address, int length)
    {
        ulong end = (ulong)address + (uint)length;
        for (int index = 0; index < ranges.Count;)
        {
            NativeBattleLoaderZeroRegion range = ranges[index];
            ulong last = (ulong)range.Address + (uint)range.Length;
            if (range.Address >= end)
            {
                break;
            }
            if (last <= address)
            {
                index++;
                continue;
            }
            ranges.RemoveAt(index);
            if (range.Address < address)
            {
                ranges.Insert(index++, new(range.Address, checked((int)(address - range.Address))));
            }
            if (last > end)
            {
                ranges.Insert(index++, new(checked((uint)end), checked((int)(last - end))));
            }
        }
    }

    private void StorePointer(uint address, Pointer pointer)
    {
        bool present = pointers.TryGetValue(address, out Pointer previous);
        if (checkpoint)
        {
            pointerUndo.Add(new(address, present, previous));
        }
        pointers[address] = pointer;
    }

    private static bool Overlaps(uint pointer, uint address, int length) => pointer < (ulong)address + (uint)length && (ulong)pointer + 4 > address;

    private void RequireDepth(int depth)
    {
        if (depth <= 0 || checkpointMarks.Count != depth)
        {
            throw new InvalidOperationException("Native staging requires its current innermost overlay checkpoint.");
        }
    }
}
