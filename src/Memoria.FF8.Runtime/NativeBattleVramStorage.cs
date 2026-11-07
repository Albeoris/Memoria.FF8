using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

/// <summary>An immutable presentation version; only explicitly changed pages are copied at publication.</summary>
internal sealed class NativeBattleVramVersion
{
    internal const int PageLength = 512;
    private readonly ImmutableArray<NativeBattleEffectMemoryRegion> initial;
    private readonly ImmutableDictionary<int, ImmutableArray<byte>> pages;
    private readonly ImmutableDictionary<int, ImmutableArray<bool>> unknownPages;
    private readonly ImmutableSortedDictionary<int, int> coverage;

    internal NativeBattleVramVersion(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown = default)
    {
        if (unknown.IsDefault)
        {
            unknown = [];
        }
        NativeBattleEffectArena.CreateSnapshot(regions, unknown).Validate();
        initial = regions;
        pages = ImmutableDictionary<int, ImmutableArray<byte>>.Empty;
        Dictionary<int, bool[]> masks = [];
        foreach (NativeBattleEffectUnknownRegion region in unknown)
        {
            for (int offset = region.Offset; offset < region.Offset + region.Length; offset++)
            {
                int page = offset / PageLength;
                if (!masks.TryGetValue(page, out bool[]? mask))
                {
                    mask = new bool[PageLength];
                    Array.Fill(mask, true);
                    masks.Add(page, mask);
                }
                mask[offset % PageLength] = false;
            }
        }
        unknownPages = masks.ToImmutableDictionary(entry => entry.Key, entry => ImmutableCollectionsMarshal.AsImmutableArray(entry.Value));
        ImmutableSortedDictionary<int, int> ranges = ImmutableSortedDictionary<int, int>.Empty;
        foreach (NativeBattleEffectMemoryRegion region in regions)
        {
            ranges = AddRange(ranges, region.Offset, region.Data.Length);
        }
        coverage = ranges;
    }

    private NativeBattleVramVersion(NativeBattleVramVersion previous, ImmutableDictionary<int, ImmutableArray<byte>> pages, ImmutableSortedDictionary<int, int> coverage, ImmutableDictionary<int, ImmutableArray<bool>> unknownPages)
    {
        initial = previous.initial;
        this.pages = pages;
        this.coverage = coverage;
        this.unknownPages = unknownPages;
    }

    internal void ReadInto(int offset, Span<byte> destination)
    {
        RequireKnown(offset, destination.Length);
        ReadRawInto(offset, destination);
    }

    internal void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        if (destination.Length != known.Length)
        {
            throw new ArgumentException("VRAM data and knownness extents must match.");
        }
        ReadRawInto(offset, destination);
        known.Fill(true);
        int end = offset + known.Length;
        for (int page = offset / PageLength; page * PageLength < end; page++)
        {
            if (unknownPages.TryGetValue(page, out ImmutableArray<bool> mask))
            {
                int first = Math.Max(offset, page * PageLength);
                int last = Math.Min(end, (page + 1) * PageLength);
                mask.AsSpan(first - page * PageLength, last - first).CopyTo(known[(first - offset)..]);
            }
        }
    }

    private void RequireKnown(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > NativeBattleEffectArena.Length - length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        if (unknownPages.Count == 0)
        {
            return;
        }
        int end = offset + length;
        for (int page = offset / PageLength; page * PageLength < end; page++)
        {
            if (unknownPages.TryGetValue(page, out ImmutableArray<bool> mask))
            {
                int first = Math.Max(offset, page * PageLength);
                int last = Math.Min(end, (page + 1) * PageLength);
                if (mask.AsSpan(first - page * PageLength, last - first).Contains(false))
                {
                    throw new NotSupportedException("Native VRAM read intersects unknown physical bytes.");
                }
            }
        }
    }

    private void ReadRawInto(int offset, Span<byte> destination)
    {
        if (offset < 0 || offset > NativeBattleEffectArena.Length - destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        destination.Clear();
        NativeBattleTelemetry.Record(1242, NativeBattleTrafficKind.ZeroFill, destination.Length);
        int end = offset + destination.Length;
        foreach (NativeBattleEffectMemoryRegion region in initial)
        {
            int first = Math.Max(offset, region.Offset);
            int last = Math.Min(end, region.Offset + region.Data.Length);
            if (first < last)
            {
                NativeBattleTelemetry.Copy<byte>(1243, region.Data.AsSpan(first - region.Offset, last - first), destination[(first - offset)..]);
            }
        }
        for (int page = offset / PageLength; page * PageLength < end; page++)
        {
            if (!pages.TryGetValue(page, out ImmutableArray<byte> data))
            {
                continue;
            }
            int first = Math.Max(offset, page * PageLength);
            int last = Math.Min(end, (page + 1) * PageLength);
            NativeBattleTelemetry.Copy<byte>(1244, data.AsSpan(first - page * PageLength, last - first), destination[(first - offset)..]);
        }
    }

    internal ushort ReadWord(int offset)
    {
        RequireKnown(offset, 2);
        if (pages.TryGetValue(offset / PageLength, out ImmutableArray<byte> page) && offset % PageLength <= PageLength - 2)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(page.AsSpan(offset % PageLength, 2));
        }
        return (ushort)(ReadByte(offset) | ReadByte(offset + 1) << 8);
    }

    private byte ReadByte(int offset)
    {
        if (pages.TryGetValue(offset / PageLength, out ImmutableArray<byte> page))
        {
            return page[offset % PageLength];
        }
        int first = 0;
        int last = initial.Length - 1;
        while (first <= last)
        {
            int middle = first + (last - first) / 2;
            NativeBattleEffectMemoryRegion region = initial[middle];
            if (offset < region.Offset)
            {
                last = middle - 1;
            }
            else if (offset >= region.Offset + region.Data.Length)
            {
                first = middle + 1;
            }
            else
            {
                return region.Data[offset - region.Offset];
            }
        }
        return 0;
    }

    internal NativeBattleVramVersion PublishChangedPages(NativeBattleEffectArena arena, HashSet<int> changedPages, List<(int Offset, int Length)> writes)
    {
        ImmutableSortedDictionary<int, int> ranges = coverage;
        foreach ((int offset, int length) in writes)
        {
            ranges = AddRange(ranges, offset, length);
        }
        if (changedPages.Count == 0 && ReferenceEquals(ranges, coverage))
        {
            return this;
        }
        if (changedPages.Count == 0)
        {
            return new(this, pages, ranges, unknownPages);
        }
        ImmutableDictionary<int, ImmutableArray<byte>>.Builder next = pages.ToBuilder();
        ImmutableDictionary<int, ImmutableArray<bool>>.Builder masks = unknownPages.ToBuilder();
        foreach (int page in changedPages)
        {
            byte[] bytes = new byte[PageLength];
            bool[] known = new bool[PageLength];
            arena.ReadWithKnownness(page * PageLength, bytes, known);
            next[page] = ImmutableCollectionsMarshal.AsImmutableArray(bytes);
            if (known.AsSpan().Contains(false))
            {
                masks[page] = ImmutableCollectionsMarshal.AsImmutableArray(known);
            }
            else
            {
                masks.Remove(page);
            }
        }
        return new(this, next.ToImmutable(), ranges, masks.ToImmutable());
    }

    internal ImmutableArray<NativeBattleEffectMemoryRegion> CaptureRegions()
    {
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder result = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        foreach ((int offset, int length) in coverage)
        {
            byte[] bytes = new byte[length];
            ReadRawInto(offset, bytes);
            result.Add(new(offset, ImmutableCollectionsMarshal.AsImmutableArray(bytes)));
        }
        return result.ToImmutable();
    }

    internal ImmutableArray<NativeBattleEffectUnknownRegion> CaptureUnknownRegions()
    {
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder result = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        int start = -1;
        int end = -1;
        foreach (int page in unknownPages.Keys.Order())
        {
            ImmutableArray<bool> mask = unknownPages[page];
            for (int lane = 0; lane < PageLength; lane++)
            {
                if (mask[lane])
                {
                    continue;
                }
                int offset = page * PageLength + lane;
                if (offset != end)
                {
                    if (start >= 0)
                    {
                        result.Add(new(start, end - start));
                    }
                    start = offset;
                }
                end = offset + 1;
            }
        }
        if (start >= 0)
        {
            result.Add(new(start, end - start));
        }
        return result.ToImmutable();
    }

    private static ImmutableSortedDictionary<int, int> AddRange(ImmutableSortedDictionary<int, int> ranges, int offset, int length)
    {
        if (length == 0)
        {
            return ranges;
        }
        int end = checked(offset + length);
        List<int>? remove = null;
        foreach ((int first, int count) in ranges)
        {
            if (first > end)
            {
                break;
            }
            if (first + count < offset)
            {
                continue;
            }
            if (first <= offset && first + count >= end)
            {
                return ranges;
            }
            offset = Math.Min(offset, first);
            end = Math.Max(end, first + count);
            (remove ??= []).Add(first);
        }
        if (remove is not null)
        {
            ranges = ranges.RemoveRange(remove);
        }
        return ranges.SetItem(offset, end - offset);
    }
}

/// <summary>One physical VRAM authority with a journaled immutable publication cursor.</summary>
internal sealed class NativeBattleVramOwner : INativeBattleCheckpointParticipant
{
    internal readonly NativeBattleEffectArena Arena;
    internal NativeBattleVramVersion Initial { get; }
    internal NativeBattleVramVersion Current { get; private set; }
    private readonly List<NativeBattleVramVersion> marks = [];

    internal NativeBattleVramOwner(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown = default)
    {
        if (unknown.IsDefault)
        {
            unknown = [];
        }
        Arena = new(regions) { UnknownRegions = unknown };
        Initial = new(regions, unknown);
        Current = Initial;
    }

    internal Mutation BeginMutation(NativeBattleVramVersion version)
    {
        if (!ReferenceEquals(Current, version))
        {
            throw new InvalidOperationException("Historical texture versions must be explicitly restored before mutation.");
        }
        return new(this, version);
    }

    void INativeBattleCheckpointParticipant.BeginCheckpoint() => marks.Add(Current);
    void INativeBattleCheckpointParticipant.CommitCheckpoint() => marks.RemoveAt(marks.Count - 1);
    void INativeBattleCheckpointParticipant.RollbackCheckpoint()
    {
        Current = marks[^1];
        marks.RemoveAt(marks.Count - 1);
    }

    internal sealed class Mutation : IDisposable
    {
        private readonly NativeBattleVramOwner owner;
        private readonly NativeBattleVramVersion previous;
        private readonly bool nested;
        private readonly HashSet<int> pages = [];
        private readonly List<(int Offset, int Length)> writes = [];
        private bool complete;
        internal NativeBattleEffectArena Arena => owner.Arena;

        internal Mutation(NativeBattleVramOwner owner, NativeBattleVramVersion previous)
        {
            this.owner = owner;
            this.previous = previous;
            NativeBattleWorkingMemory storage = owner.Arena.Working;
            NativeBattleTaskOperation.EnlistRetained(storage);
            nested = storage.HasCheckpoint;
            if (nested)
            {
                storage.BeginNestedCheckpoint();
            }
            else
            {
                storage.BeginCheckpoint();
            }
            storage.EnlistCheckpointParticipant(owner);
        }

        internal void Write(int offset, ReadOnlySpan<byte> bytes)
        {
            WriteWithKnownness(offset, bytes, default);
        }

        internal void WriteWithKnownness(int offset, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
        {
            RequireActive();
            owner.Arena.RequireRange(offset, bytes.Length);
            owner.Arena.RequireWritable();
            if (!known.IsEmpty && known.Length != bytes.Length)
            {
                throw new ArgumentException("VRAM data and knownness extents must match.");
            }
            bool unchanged = HasSameKnownBytes(offset, bytes, known);
            if (known.IsEmpty)
            {
                owner.Arena.Write(offset, bytes);
            }
            else
            {
                owner.Arena.WriteWithKnownness(offset, bytes, known);
            }
            if (bytes.Length == 0)
            {
                return;
            }
            if (writes.Count != 0 && writes[^1].Offset + writes[^1].Length == offset)
            {
                (int first, int count) = writes[^1];
                writes[^1] = (first, count + bytes.Length);
            }
            else
            {
                writes.Add((offset, bytes.Length));
            }
            if (unchanged)
            {
                return;
            }
            int last = (offset + bytes.Length - 1) / NativeBattleVramVersion.PageLength;
            for (int page = offset / NativeBattleVramVersion.PageLength; page <= last; page++)
            {
                pages.Add(page);
            }
        }

        internal NativeBattleVramVersion Commit()
        {
            RequireActive();
            NativeBattleVramVersion version = previous.PublishChangedPages(owner.Arena, pages, writes);
            owner.Current = version;
            if (nested)
            {
                owner.Arena.Working.CommitNestedCheckpoint();
            }
            else
            {
                owner.Arena.Working.CommitCheckpoint();
            }
            complete = true;
            return version;
        }

        private bool HasSameKnownBytes(int offset, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> sourceKnown)
        {
            Span<byte> previousBytes = stackalloc byte[256];
            Span<bool> known = stackalloc bool[256];
            for (int first = 0; first < bytes.Length; first += previousBytes.Length)
            {
                int count = Math.Min(previousBytes.Length, bytes.Length - first);
                owner.Arena.ReadWithKnownness(offset + first, previousBytes[..count], known[..count]);
                bool sameKnownness = sourceKnown.IsEmpty ? !known[..count].Contains(false) : known[..count].SequenceEqual(sourceKnown.Slice(first, count));
                if (!sameKnownness || !previousBytes[..count].SequenceEqual(bytes.Slice(first, count)))
                {
                    return false;
                }
            }
            return true;
        }

        private void RequireActive()
        {
            if (complete)
            {
                throw new InvalidOperationException("A completed VRAM mutation cannot write or publish another version.");
            }
        }

        public void Dispose()
        {
            if (complete)
            {
                return;
            }
            if (nested)
            {
                owner.Arena.Working.RollbackNestedCheckpoint();
            }
            else
            {
                owner.Arena.Working.RollbackCheckpoint();
            }
            complete = true;
        }
    }
}
