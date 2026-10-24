using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

internal static class NativeKnownByteRanges
{
    internal static void Validate(ImmutableArray<NativeBattleEffectMemoryRegion> ranges, int capacity = int.MaxValue)
    {
        if (ranges.IsDefault || capacity < 0)
        {
            throw new InvalidDataException("Native retained byte ranges require an explicit known/unknown owner.");
        }
        int previousEnd = 0;
        foreach (NativeBattleEffectMemoryRegion region in ranges)
        {
            if (region is null || region.Data.IsDefaultOrEmpty || region.Offset < previousEnd || region.Offset > capacity - region.Data.Length)
            {
                throw new InvalidDataException("Native retained byte ranges overlap or exceed their address representation.");
            }
            previousEnd = region.Offset + region.Data.Length;
        }
    }

    internal static byte[] Read(ImmutableArray<NativeBattleEffectMemoryRegion> ranges, int offset, int length, int capacity = int.MaxValue)
    {
        Validate(ranges, capacity);
        RequireRange(offset, length, capacity);
        int observed = offset;
        foreach (NativeBattleEffectMemoryRegion region in ranges)
        {
            int first = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + length, region.Offset + region.Data.Length);
            if (end <= first)
            {
                continue;
            }
            if (first != observed)
            {
                throw new NotSupportedException("Native retained memory read reaches unobserved bytes.");
            }
            observed = end;
        }
        if (observed != offset + length)
        {
            throw new NotSupportedException("Native retained memory read reaches unobserved bytes.");
        }
        byte[] bytes = new byte[length];
        foreach (NativeBattleEffectMemoryRegion region in ranges)
        {
            int first = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + length, region.Offset + region.Data.Length);
            if (end > first)
            {
                region.Data.AsSpan().Slice(first - region.Offset, end - first).CopyTo(bytes.AsSpan(first - offset));
            }
        }
        return bytes;
    }

    internal static ImmutableArray<NativeBattleEffectMemoryRegion> Write(ImmutableArray<NativeBattleEffectMemoryRegion> ranges, int offset, ReadOnlySpan<byte> bytes, int capacity = int.MaxValue, int blockLength = int.MaxValue)
    {
        Validate(ranges, capacity);
        RequireRange(offset, bytes.Length, capacity);
        if (bytes.IsEmpty)
        {
            return ranges;
        }
        int writeEnd = offset + bytes.Length;
        int firstIndex = 0;
        while (firstIndex < ranges.Length && (long)ranges[firstIndex].Offset + ranges[firstIndex].Data.Length < offset)
        {
            firstIndex++;
        }
        int afterIndex = firstIndex;
        while (afterIndex < ranges.Length && ranges[afterIndex].Offset <= writeEnd)
        {
            NativeBattleEffectMemoryRegion region = ranges[afterIndex];
            if (region.Offset <= offset && region.Offset + region.Data.Length >= writeEnd && region.Data.AsSpan().Slice(offset - region.Offset, bytes.Length).SequenceEqual(bytes))
            {
                return ranges;
            }
            afterIndex++;
        }
        int first = firstIndex < afterIndex ? Math.Min(offset, ranges[firstIndex].Offset) : offset;
        int end = firstIndex < afterIndex ? Math.Max(writeEnd, ranges[afterIndex - 1].Offset + ranges[afterIndex - 1].Data.Length) : writeEnd;
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder result = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        result.AddRange(ranges.AsSpan(0, firstIndex));
        int sourceIndex = firstIndex;
        // Split only written blocks: eagerly paging untouched images makes every scalar store copy a large region list.
        int firstWriteBlock = offset - offset % blockLength;
        long afterWriteBlock = ((long)writeEnd + blockLength - 1) / blockLength * blockLength;
        for (int start = first; start < end;)
        {
            long nextBoundary = start < firstWriteBlock ? firstWriteBlock : start >= afterWriteBlock ? end : (long)start + blockLength - start % blockLength;
            int length = (int)(Math.Min(end, nextBoundary) - start);
            int blockEnd = start + length;
            while (sourceIndex < afterIndex && ranges[sourceIndex].Offset + ranges[sourceIndex].Data.Length <= start)
            {
                sourceIndex++;
            }
            if (sourceIndex < afterIndex && ranges[sourceIndex].Offset == start && ranges[sourceIndex].Data.Length == length && (blockEnd <= offset || start >= writeEnd))
            {
                result.Add(ranges[sourceIndex]);
            }
            else
            {
                byte[] data = new byte[length];
                for (int index = sourceIndex; index < afterIndex && ranges[index].Offset < blockEnd; index++)
                {
                    NativeBattleEffectMemoryRegion region = ranges[index];
                    int copyStart = Math.Max(start, region.Offset);
                    int copyEnd = Math.Min(blockEnd, region.Offset + region.Data.Length);
                    region.Data.AsSpan().Slice(copyStart - region.Offset, copyEnd - copyStart).CopyTo(data.AsSpan(copyStart - start));
                }
                int writtenStart = Math.Max(start, offset);
                int writtenEnd = Math.Min(blockEnd, writeEnd);
                if (writtenEnd > writtenStart)
                {
                    bytes.Slice(writtenStart - offset, writtenEnd - writtenStart).CopyTo(data.AsSpan(writtenStart - start));
                }
                result.Add(new(start, ImmutableCollectionsMarshal.AsImmutableArray(data)));
            }
            start = blockEnd;
        }
        result.AddRange(ranges.AsSpan(afterIndex, ranges.Length - afterIndex));
        return result.ToImmutable();
    }

    private static void RequireRange(int offset, int length, int capacity)
    {
        if (offset < 0 || length < 0 || offset > capacity - length)
        {
            throw new InvalidDataException("Native retained memory access exceeds its offset representation.");
        }
    }
}
