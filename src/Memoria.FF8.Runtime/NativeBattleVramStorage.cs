using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

/// <summary>An immutable presentation version; only explicitly changed pages are copied at publication.</summary>
internal sealed class NativeBattleVramVersion
{
    internal const int PageLength = 4096;
    private readonly ImmutableArray<NativeBattleEffectMemoryRegion> initial;
    private readonly ImmutableDictionary<int, ImmutableArray<byte>> pages;
    private readonly ImmutableSortedDictionary<int, int> coverage;

    internal NativeBattleVramVersion(ImmutableArray<NativeBattleEffectMemoryRegion> regions)
    {
        NativeBattleEffectArena.CreateSnapshot(regions, []).Validate();
        initial = regions;
        pages = ImmutableDictionary<int, ImmutableArray<byte>>.Empty;
        ImmutableSortedDictionary<int, int> ranges = ImmutableSortedDictionary<int, int>.Empty;
        foreach (NativeBattleEffectMemoryRegion region in regions)
        {
            ranges = AddRange(ranges, region.Offset, region.Data.Length);
        }
        coverage = ranges;
    }

    private NativeBattleVramVersion(NativeBattleVramVersion previous, ImmutableDictionary<int, ImmutableArray<byte>> pages, ImmutableSortedDictionary<int, int> coverage)
    {
        initial = previous.initial;
        this.pages = pages;
        this.coverage = coverage;
    }

    internal void ReadInto(int offset, Span<byte> destination)
    {
        if (offset < 0 || offset > NativeBattleEffectArena.Length - destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        destination.Clear();
        int end = offset + destination.Length;
        foreach (NativeBattleEffectMemoryRegion region in initial)
        {
            int first = Math.Max(offset, region.Offset);
            int last = Math.Min(end, region.Offset + region.Data.Length);
            if (first < last)
            {
                region.Data.AsSpan(first - region.Offset, last - first).CopyTo(destination[(first - offset)..]);
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
            data.AsSpan(first - page * PageLength, last - first).CopyTo(destination[(first - offset)..]);
        }
    }

    internal NativeBattleVramVersion PublishChangedPages(NativeBattleEffectArena arena, HashSet<int> changedPages, List<(int Offset, int Length)> writes)
    {
        if (changedPages.Count == 0)
        {
            return this;
        }
        ImmutableDictionary<int, ImmutableArray<byte>> next = pages;
        foreach (int page in changedPages)
        {
            byte[] bytes = new byte[PageLength];
            arena.ReadInto(page * PageLength, bytes);
            next = next.SetItem(page, ImmutableCollectionsMarshal.AsImmutableArray(bytes));
        }
        ImmutableSortedDictionary<int, int> ranges = coverage;
        foreach ((int offset, int length) in writes)
        {
            ranges = AddRange(ranges, offset, length);
        }
        return new(this, next, ranges);
    }

    internal ImmutableArray<NativeBattleEffectMemoryRegion> CaptureRegions()
    {
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder result = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        foreach ((int offset, int length) in coverage)
        {
            byte[] bytes = new byte[length];
            ReadInto(offset, bytes);
            result.Add(new(offset, ImmutableCollectionsMarshal.AsImmutableArray(bytes)));
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

    internal NativeBattleVramOwner(ImmutableArray<NativeBattleEffectMemoryRegion> regions)
    {
        Arena = new(regions);
        Initial = new(regions);
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
            owner.Arena.RequireRange(offset, bytes.Length);
            owner.Arena.Write(offset, bytes);
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
            int last = (offset + bytes.Length - 1) / NativeBattleVramVersion.PageLength;
            for (int page = offset / NativeBattleVramVersion.PageLength; page <= last; page++)
            {
                pages.Add(page);
            }
        }

        internal NativeBattleVramVersion Commit()
        {
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
