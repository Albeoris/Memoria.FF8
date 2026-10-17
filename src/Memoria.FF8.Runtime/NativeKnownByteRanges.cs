using System.Collections.Immutable;

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

    internal static ImmutableArray<NativeBattleEffectMemoryRegion> Write(ImmutableArray<NativeBattleEffectMemoryRegion> ranges, int offset, ReadOnlySpan<byte> bytes, int capacity = int.MaxValue)
    {
        Validate(ranges, capacity);
        RequireRange(offset, bytes.Length, capacity);
        if (bytes.IsEmpty)
        {
            return ranges;
        }
        int writeEnd = offset + bytes.Length;
        NativeBattleEffectMemoryRegion[] overlaps = ranges.Where(region => region.Offset <= writeEnd && region.Offset + region.Data.Length >= offset).ToArray();
        int first = overlaps.Select(region => region.Offset).Append(offset).Min();
        int end = overlaps.Select(region => region.Offset + region.Data.Length).Append(writeEnd).Max();
        byte[] data = new byte[end - first];
        foreach (NativeBattleEffectMemoryRegion region in overlaps)
        {
            region.Data.AsSpan().CopyTo(data.AsSpan(region.Offset - first));
        }
        bytes.CopyTo(data.AsSpan(offset - first));
        return ranges.Except(overlaps).Append(new(first, data.ToImmutableArray())).OrderBy(region => region.Offset).ToImmutableArray();
    }

    private static void RequireRange(int offset, int length, int capacity)
    {
        if (offset < 0 || length < 0 || offset > capacity - length)
        {
            throw new InvalidDataException("Native retained memory access exceeds its offset representation.");
        }
    }
}
