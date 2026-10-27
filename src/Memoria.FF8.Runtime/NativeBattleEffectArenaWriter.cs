using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

/// <summary>One atomic packet producer's ordered stores, frozen without repeatedly copying its immutable arena.</summary>
internal sealed class NativeBattleEffectArenaWriter
{
    private readonly record struct Region(int Offset, int Length, int? SourceIndex)
    {
        public int End => Offset + Length;
    }

    private readonly NativeBattleEffectArena source;
    internal ImmutableDictionary<int, NativeBattleResourcePointer?>? ResourcePointers { get; private set; }
    private ImmutableDictionary<int, NativeBattleResourcePointer?>? checkpointResourcePointers;
    internal ImmutableArray<NativeBattleTextureFileSource>? TextureFiles { get; private set; }
    private ImmutableArray<NativeBattleTextureFileSource>? checkpointTextureFiles;
    private readonly List<Region> regions;
    private readonly List<NativeBattleEffectUnknownRegion> unknown;
    private readonly List<(int Offset, byte[] Data)> writes = [];
    private readonly NativeBattleEffectArenaTransaction? transaction;
    private byte[]? storage;
    private Region[]? checkpointRegions;
    private NativeBattleEffectUnknownRegion[]? checkpointUnknown;
    private readonly List<(int Offset, byte[] Data)> undo = [];

    public NativeBattleEffectArenaWriter(NativeBattleEffectArena source)
    {
        transaction = source.Transaction;
        source.Validate();
        this.source = source;
        ResourcePointers = source.ResourcePointers;
        TextureFiles = source.CurrentTextureFiles;
        if (transaction is not null)
        {
            regions = [];
            unknown = [];
            return;
        }
        regions = source.Regions.Select((region, index) => new Region(region.Offset, region.Data.Length, index)).ToList();
        unknown = source.UnknownRegions.ToList();
    }

    public NativeBattleEffectArenaWriter Write(int offset, ReadOnlySpan<byte> bytes)
    {
        if (transaction is not null)
        {
            transaction.Write(offset, bytes);
            return this;
        }
        RequireRange(offset, bytes.Length);
        if (bytes.IsEmpty)
        {
            return this;
        }
        if (storage is null)
        {
            writes.Add((offset, NativeBattleTelemetry.Materialize(1015, bytes)));
        }
        else
        {
            EnsureReadThroughCapacity(offset + bytes.Length);
            if (checkpointRegions is not null)
            {
                undo.Add((offset, NativeBattleTelemetry.Materialize(1016, storage.AsSpan(offset, bytes.Length))));
            }
            NativeBattleTelemetry.Copy(1005, bytes, storage.AsSpan(offset));
        }
        InvalidateResourcePointers(offset, bytes.Length);
        int end = offset + bytes.Length;
        int first = 0;
        int upper = regions.Count;
        while (first < upper)
        {
            int middle = first + (upper - first) / 2;
            if (regions[middle].End < offset)
            {
                first = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }
        if (first < regions.Count && regions[first].Offset <= offset && regions[first].End >= end && (first + 1 == regions.Count || regions[first + 1].Offset > end))
        {
            regions[first] = regions[first] with { SourceIndex = null };
        }
        else
        {
            int after = first;
            int mergedFirst = offset;
            int mergedEnd = end;
            while (after < regions.Count && regions[after].Offset <= end)
            {
                mergedFirst = Math.Min(mergedFirst, regions[after].Offset);
                mergedEnd = Math.Max(mergedEnd, regions[after].End);
                after++;
            }
            regions.RemoveRange(first, after - first);
            regions.Insert(first, new(mergedFirst, mergedEnd - mergedFirst, null));
        }
        int firstUnknown = FirstUnknownEndingAt(offset, false);
        int afterUnknown = firstUnknown;
        while (afterUnknown < unknown.Count && unknown[afterUnknown].Offset < end)
        {
            afterUnknown++;
        }
        if (afterUnknown > firstUnknown)
        {
            NativeBattleEffectUnknownRegion left = unknown[firstUnknown];
            NativeBattleEffectUnknownRegion right = unknown[afterUnknown - 1];
            int rightEnd = right.Offset + right.Length;
            if (left.Offset < offset && rightEnd > end && firstUnknown + 1 == afterUnknown)
            {
                unknown[firstUnknown] = new(left.Offset, offset - left.Offset);
                unknown.Insert(firstUnknown + 1, new(end, rightEnd - end));
            }
            else
            {
                bool retainLeft = left.Offset < offset;
                bool retainRight = rightEnd > end;
                int removeFirst = firstUnknown + (retainLeft ? 1 : 0);
                int removeAfter = afterUnknown - (retainRight ? 1 : 0);
                if (retainLeft)
                {
                    unknown[firstUnknown] = new(left.Offset, offset - left.Offset);
                }
                if (retainRight)
                {
                    unknown[afterUnknown - 1] = new(end, rightEnd - end);
                }
                unknown.RemoveRange(removeFirst, removeAfter - removeFirst);
            }
        }
        return this;
    }

    public NativeBattleEffectArenaWriter MarkUnknown(int offset, int length)
    {
        if (transaction is not null)
        {
            transaction.MarkUnknown(offset, length);
            return this;
        }
        RequireRange(offset, length);
        InvalidateResourcePointers(offset, length);
        if (length == 0)
        {
            return this;
        }
        int end = offset + length;
        int first = FirstUnknownEndingAt(offset, true);
        int after = first;
        int mergedFirst = offset;
        int mergedEnd = end;
        while (after < unknown.Count && unknown[after].Offset <= end)
        {
            mergedFirst = Math.Min(mergedFirst, unknown[after].Offset);
            mergedEnd = Math.Max(mergedEnd, unknown[after].Offset + unknown[after].Length);
            after++;
        }
        if (after == first + 1 && unknown[first].Offset == mergedFirst && unknown[first].Length == mergedEnd - mergedFirst)
        {
            return this;
        }
        if (after == first + 1)
        {
            unknown[first] = new(mergedFirst, mergedEnd - mergedFirst);
            return this;
        }
        unknown.RemoveRange(first, after - first);
        unknown.Insert(first, new(mergedFirst, mergedEnd - mergedFirst));
        return this;
    }

    public NativeBattleEffectArena Freeze()
    {
        if (transaction is not null)
        {
            return transaction.View;
        }
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder result = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>(regions.Count);
        foreach (Region region in regions)
        {
            if (region.SourceIndex is int sourceIndex)
            {
                result.Add(source.Regions[sourceIndex]);
                continue;
            }
            byte[] bytes = new byte[region.Length];
            if (storage is not null)
            {
                NativeBattleTelemetry.Copy(1006, storage.AsSpan(region.Offset, region.Length), bytes);
                result.Add(new(region.Offset, ImmutableCollectionsMarshal.AsImmutableArray(bytes)));
                continue;
            }
            foreach (NativeBattleEffectMemoryRegion original in source.Regions)
            {
                int first = Math.Max(region.Offset, original.Offset);
                int end = Math.Min(region.End, original.Offset + original.Data.Length);
                if (end > first)
                {
                    NativeBattleTelemetry.Copy(1007, original.Data.AsSpan().Slice(first - original.Offset, end - first), bytes.AsSpan(first - region.Offset));
                }
            }
            foreach (var write in writes)
            {
                int first = Math.Max(region.Offset, write.Offset);
                int end = Math.Min(region.End, write.Offset + write.Data.Length);
                if (end > first)
                {
                    NativeBattleTelemetry.Copy(1008, write.Data.AsSpan(first - write.Offset, end - first), bytes.AsSpan(first - region.Offset));
                }
            }
            result.Add(new(region.Offset, ImmutableCollectionsMarshal.AsImmutableArray(bytes)));
        }
        return new(result.MoveToImmutable()) { UnknownRegions = unknown.ToImmutableArray(), ResourcePointers = ResourcePointers, TextureFiles = TextureFiles };
    }

    internal void SetTextureFiles(ImmutableArray<NativeBattleTextureFileSource> files) => TextureFiles = files;

    internal void SetResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        ResourcePointers = (ResourcePointers ?? ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty).SetItem(offset, pointer);
    }

    private void InvalidateResourcePointers(int offset, int count)
    {
        if (ResourcePointers is not null && count > 0)
        {
            ResourcePointers = ResourcePointers.RemoveRange(ResourcePointers.Keys.Where(pointer => pointer < (long)offset + count && pointer + 4L > offset));
            if (ResourcePointers.Count == 0)
            {
                ResourcePointers = null;
            }
        }
    }

    internal void BeginReadThrough()
    {
        if (storage is not null || transaction is not null || writes.Count != 0)
        {
            throw new InvalidOperationException("An arena transaction must begin from one immutable source.");
        }
        int extent = source.Regions.IsEmpty ? 1 : source.Regions[^1].Offset + source.Regions[^1].Data.Length;
        storage = ArrayPool<byte>.Shared.Rent(extent);
        NativeBattleTelemetry.ObservePoolRent(storage.Length);
        NativeBattleTelemetry.Clear(1009, storage, 0, storage.Length);
        foreach (NativeBattleEffectMemoryRegion region in source.Regions)
        {
            NativeBattleTelemetry.Copy(1010, region.Data.AsSpan(), storage.AsSpan(region.Offset));
        }
    }

    internal byte[] ReadThrough(int offset, int count)
    {
        RequireRange(offset, count);
        int first = FirstUnknownEndingAt(offset, false);
        if (count > 0 && first < unknown.Count && unknown[first].Offset < offset + count)
        {
            throw new NotSupportedException($"Native effect arena read intersects unresolved physical bytes at arena offset 0x{offset:X} for {count} bytes; unknown padding or pointer data cannot be substituted.");
        }
        EnsureReadThroughCapacity(offset + count);
        return NativeBattleTelemetry.Materialize(1017, storage!.AsSpan(offset, count));
    }

    internal (byte[] Data, bool[] Known) ReadSnapshotThrough(int offset, int count)
    {
        RequireRange(offset, count);
        bool[] known = new bool[count];
        NativeBattleTelemetry.Fill(1051, known, true, 0, count);
        int first = FirstUnknownEndingAt(offset, false);
        for (int index = first; index < unknown.Count && unknown[index].Offset < offset + count; index++)
        {
            NativeBattleEffectUnknownRegion region = unknown[index];
            int start = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + count, region.Offset + region.Length);
            NativeBattleTelemetry.Clear(1011, known, start - offset, end - start);
        }
        EnsureReadThroughCapacity(offset + count);
        byte[] data = NativeBattleTelemetry.Materialize(1018, storage!.AsSpan(offset, count));
        return (data, known);
    }

    private void EnsureReadThroughCapacity(int extent)
    {
        byte[] previous = storage ?? throw new InvalidOperationException("Arena transaction storage is not active.");
        if (extent <= previous.Length)
        {
            return;
        }
        int capacity = Math.Max(extent, Math.Min(NativeBattleEffectArena.Length, previous.Length * 2));
        byte[] grown = ArrayPool<byte>.Shared.Rent(capacity);
        NativeBattleTelemetry.ObservePoolRent(grown.Length);
        NativeBattleTelemetry.Copy(1012, previous.AsSpan(), grown);
        NativeBattleTelemetry.Clear(1013, grown, previous.Length, grown.Length - previous.Length);
        storage = grown;
        ArrayPool<byte>.Shared.Return(previous);
        NativeBattleTelemetry.ObservePoolReturn(previous.Length);
    }

    internal byte[] ReadCurrent(int offset, int count) => transaction is not null ? transaction.Read(offset, count) : ReadThrough(offset, count);

    private int FirstUnknownEndingAt(int offset, bool includeAdjacent)
    {
        int first = 0;
        int after = unknown.Count;
        while (first < after)
        {
            int middle = first + (after - first) / 2;
            NativeBattleEffectUnknownRegion region = unknown[middle];
            int end = region.Offset + region.Length;
            if (end < offset || !includeAdjacent && end == offset)
            {
                first = middle + 1;
            }
            else
            {
                after = middle;
            }
        }
        return first;
    }

    internal void BeginCheckpoint()
    {
        if (checkpointRegions is not null || storage is null)
        {
            throw new InvalidOperationException("Arena callback checkpoints cannot be nested.");
        }
        checkpointResourcePointers = ResourcePointers;
        checkpointTextureFiles = TextureFiles;
        checkpointRegions = regions.ToArray();
        checkpointUnknown = unknown.ToArray();
    }

    internal void CommitCheckpoint()
    {
        checkpointRegions = null;
        checkpointUnknown = null;
        undo.Clear();
    }

    internal void RollbackCheckpoint()
    {
        if (checkpointRegions is null || checkpointUnknown is null || storage is null)
        {
            throw new InvalidOperationException("Arena callback rollback requires its active checkpoint.");
        }
        ResourcePointers = checkpointResourcePointers;
        TextureFiles = checkpointTextureFiles;
        for (int index = undo.Count - 1; index >= 0; index--)
        {
            var write = undo[index];
            NativeBattleTelemetry.Copy(1014, write.Data.AsSpan(), storage.AsSpan(write.Offset));
        }
        regions.Clear();
        regions.AddRange(checkpointRegions);
        unknown.Clear();
        unknown.AddRange(checkpointUnknown);
        CommitCheckpoint();
    }

    internal void EndReadThrough()
    {
        if (storage is { } rented)
        {
            storage = null;
            ArrayPool<byte>.Shared.Return(rented);
            NativeBattleTelemetry.ObservePoolReturn(rented.Length);
        }
    }

    private static void RequireRange(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > NativeBattleEffectArena.Length - length)
        {
            throw new InvalidDataException("Native effect memory access exceeds20DFAB8..21DFAB8.");
        }
    }
}
