using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

internal readonly record struct NativeBattleModelResourceSegment(int Offset, ImmutableArray<byte> Source, int SourceOffset, int Length)
{
    internal ReadOnlyMemory<byte> Data => Source.AsMemory().Slice(SourceOffset, Length);
}

/// <summary>Immutable resource slices and copy-on-write patches. No retained contiguous bank image or parent publication is needed.</summary>
internal sealed record NativeBattleModelResourceStorage(ImmutableArray<NativeBattleModelResourceSegment> Segments)
{
    internal static NativeBattleModelResourceStorage FromKnownRanges(ImmutableArray<NativeBattleEffectMemoryRegion> ranges)
    {
        return new(ranges.IsDefault ? default : ranges.Select(range => new NativeBattleModelResourceSegment(range.Offset, range.Data, 0, range.Data.Length)).ToImmutableArray());
    }

    internal void Validate(int capacity)
    {
        if (Segments.IsDefault || capacity < 0)
        {
            throw new InvalidDataException("Native model storage requires explicit resource sections and a bounded address view.");
        }
        int previousEnd = 0;
        foreach (NativeBattleModelResourceSegment segment in Segments)
        {
            if (segment.Source.IsDefault || segment.Length <= 0 || segment.Offset < previousEnd || segment.Offset > capacity - segment.Length || segment.SourceOffset < 0 || segment.SourceOffset > segment.Source.Length - segment.Length)
            {
                throw new InvalidDataException("Native model resource sections overlap or exceed their source or address view.");
            }
            previousEnd = segment.Offset + segment.Length;
        }
    }

    internal byte[] Read(int offset, int length, int capacity)
    {
        Validate(capacity);
        RequireRange(offset, length, capacity);
        int observed = offset;
        foreach (NativeBattleModelResourceSegment segment in Segments)
        {
            int first = Math.Max(offset, segment.Offset);
            int end = Math.Min(offset + length, segment.Offset + segment.Length);
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
        foreach (NativeBattleModelResourceSegment segment in Segments)
        {
            int first = Math.Max(offset, segment.Offset);
            int end = Math.Min(offset + length, segment.Offset + segment.Length);
            if (end > first)
            {
                NativeBattleTelemetry.Copy(1019, segment.Source.AsSpan(segment.SourceOffset + first - segment.Offset, end - first), bytes.AsSpan(first - offset));
            }
        }
        return bytes;
    }

    internal NativeBattleModelResourceStorage Write(int offset, ReadOnlySpan<byte> bytes, int capacity)
    {
        Validate(capacity);
        RequireRange(offset, bytes.Length, capacity);
        if (bytes.IsEmpty || Matches(offset, bytes))
        {
            return this;
        }
        return Replace(offset, ImmutableCollectionsMarshal.AsImmutableArray(bytes.ToArray()), 0, bytes.Length);
    }

    internal NativeBattleModelResourceStorage Bind(int offset, ImmutableArray<byte> source, int sourceOffset, int length, int capacity)
    {
        Validate(capacity);
        RequireRange(offset, length, capacity);
        if (source.IsDefault || sourceOffset < 0 || sourceOffset > source.Length - length)
        {
            throw new InvalidDataException("Native model resource binding exceeds its immutable source.");
        }
        return length == 0 ? this : Replace(offset, source, sourceOffset, length);
    }

    internal ImmutableArray<NativeBattleEffectMemoryRegion> CaptureKnownRanges(int capacity)
    {
        Validate(capacity);
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder ranges = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        for (int index = 0; index < Segments.Length;)
        {
            NativeBattleModelResourceSegment first = Segments[index++];
            int end = first.Offset + first.Length;
            int after = index;
            while (after < Segments.Length && Segments[after].Offset == end)
            {
                end += Segments[after++].Length;
            }
            if (index == after && first.SourceOffset == 0 && first.Length == first.Source.Length)
            {
                ranges.Add(new(first.Offset, first.Source));
            }
            else
            {
                byte[] bytes = new byte[end - first.Offset];
                first.Data.Span.CopyTo(bytes);
                for (; index < after; index++)
                {
                    NativeBattleModelResourceSegment segment = Segments[index];
                    segment.Data.Span.CopyTo(bytes.AsSpan(segment.Offset - first.Offset));
                }
                ranges.Add(new(first.Offset, ImmutableCollectionsMarshal.AsImmutableArray(bytes)));
            }
            index = after;
        }
        return ranges.ToImmutable();
    }

    private bool Matches(int offset, ReadOnlySpan<byte> bytes)
    {
        int observed = offset;
        foreach (NativeBattleModelResourceSegment segment in Segments)
        {
            int first = Math.Max(offset, segment.Offset);
            int end = Math.Min(offset + bytes.Length, segment.Offset + segment.Length);
            if (end <= first)
            {
                continue;
            }
            if (first != observed || !segment.Source.AsSpan(segment.SourceOffset + first - segment.Offset, end - first).SequenceEqual(bytes.Slice(first - offset, end - first)))
            {
                return false;
            }
            observed = end;
        }
        return observed == offset + bytes.Length;
    }

    private NativeBattleModelResourceStorage Replace(int offset, ImmutableArray<byte> source, int sourceOffset, int length)
    {
        int end = offset + length;
        bool inserted = false;
        ImmutableArray<NativeBattleModelResourceSegment>.Builder result = ImmutableArray.CreateBuilder<NativeBattleModelResourceSegment>();
        foreach (NativeBattleModelResourceSegment segment in Segments)
        {
            int segmentEnd = segment.Offset + segment.Length;
            if (segmentEnd <= offset)
            {
                Append(segment);
                continue;
            }
            if (!inserted)
            {
                if (segment.Offset < offset)
                {
                    Append(segment with { Length = offset - segment.Offset });
                }
                Append(new(offset, source, sourceOffset, length));
                inserted = true;
            }
            if (segment.Offset >= end)
            {
                Append(segment);
            }
            else if (segmentEnd > end)
            {
                Append(segment with { Offset = end, SourceOffset = segment.SourceOffset + end - segment.Offset, Length = segmentEnd - end });
            }
        }
        if (!inserted)
        {
            Append(new(offset, source, sourceOffset, length));
        }
        return new(result.ToImmutable());

        void Append(NativeBattleModelResourceSegment segment)
        {
            if (result.Count > 0 && result[^1] is var previous && previous.Offset + previous.Length == segment.Offset && previous.Source.Equals(segment.Source) && previous.SourceOffset + previous.Length == segment.SourceOffset)
            {
                result[^1] = previous with { Length = previous.Length + segment.Length };
                return;
            }
            result.Add(segment);
        }
    }

    private static void RequireRange(int offset, int length, int capacity)
    {
        if (offset < 0 || length < 0 || offset > capacity - length)
        {
            throw new InvalidDataException("Native model resource access exceeds its bounded address view.");
        }
    }
}
