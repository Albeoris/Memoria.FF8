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
    private readonly List<Region> regions;
    private readonly List<NativeBattleEffectUnknownRegion> unknown;
    private readonly List<(int Offset, byte[] Data)> writes = [];

    public NativeBattleEffectArenaWriter(NativeBattleEffectArena source)
    {
        source.Validate();
        this.source = source;
        regions = source.Regions.Select((region, index) => new Region(region.Offset, region.Data.Length, index)).ToList();
        unknown = source.UnknownRegions.ToList();
    }

    public NativeBattleEffectArenaWriter Write(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireRange(offset, bytes.Length);
        if (bytes.IsEmpty)
        {
            return this;
        }
        writes.Add((offset, bytes.ToArray()));
        int end = offset + bytes.Length;
        int first = regions.FindIndex(region => region.End >= offset);
        if (first < 0)
        {
            first = regions.Count;
        }
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
        for (int index = 0; index < unknown.Count;)
        {
            NativeBattleEffectUnknownRegion region = unknown[index];
            int regionEnd = region.Offset + region.Length;
            if (region.Offset >= end || regionEnd <= offset)
            {
                index++;
                continue;
            }
            unknown.RemoveAt(index);
            if (region.Offset < offset)
            {
                unknown.Insert(index++, new(region.Offset, offset - region.Offset));
            }
            if (regionEnd > end)
            {
                unknown.Insert(index++, new(end, regionEnd - end));
            }
        }
        return this;
    }

    public NativeBattleEffectArenaWriter MarkUnknown(int offset, int length)
    {
        RequireRange(offset, length);
        if (length == 0)
        {
            return this;
        }
        int end = offset + length;
        int first = unknown.FindIndex(region => region.Offset + region.Length >= offset);
        if (first < 0)
        {
            first = unknown.Count;
        }
        int after = first;
        int mergedFirst = offset;
        int mergedEnd = end;
        while (after < unknown.Count && unknown[after].Offset <= end)
        {
            mergedFirst = Math.Min(mergedFirst, unknown[after].Offset);
            mergedEnd = Math.Max(mergedEnd, unknown[after].Offset + unknown[after].Length);
            after++;
        }
        unknown.RemoveRange(first, after - first);
        unknown.Insert(first, new(mergedFirst, mergedEnd - mergedFirst));
        return this;
    }

    public NativeBattleEffectArena Freeze()
    {
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder result = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>(regions.Count);
        foreach (Region region in regions)
        {
            if (region.SourceIndex is int sourceIndex)
            {
                result.Add(source.Regions[sourceIndex]);
                continue;
            }
            byte[] bytes = new byte[region.Length];
            foreach (NativeBattleEffectMemoryRegion original in source.Regions)
            {
                int first = Math.Max(region.Offset, original.Offset);
                int end = Math.Min(region.End, original.Offset + original.Data.Length);
                if (end > first)
                {
                    original.Data.AsSpan().Slice(first - original.Offset, end - first).CopyTo(bytes.AsSpan(first - region.Offset));
                }
            }
            foreach (var write in writes)
            {
                int first = Math.Max(region.Offset, write.Offset);
                int end = Math.Min(region.End, write.Offset + write.Data.Length);
                if (end > first)
                {
                    write.Data.AsSpan(first - write.Offset, end - first).CopyTo(bytes.AsSpan(first - region.Offset));
                }
            }
            result.Add(new(region.Offset, ImmutableCollectionsMarshal.AsImmutableArray(bytes)));
        }
        return new(result.MoveToImmutable()) { UnknownRegions = unknown.ToImmutableArray() };
    }

    private static void RequireRange(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > NativeBattleEffectArena.Length - length)
        {
            throw new InvalidDataException("Native effect memory access exceeds20DFAB8..21DFAB8.");
        }
    }
}
