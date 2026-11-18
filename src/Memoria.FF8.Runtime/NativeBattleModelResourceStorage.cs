using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

internal readonly record struct NativeBattleModelResourceSegment(int Offset, ImmutableArray<byte> Source, int SourceOffset, int Length)
{
    internal ReadOnlyMemory<byte> Data => Source.AsMemory().Slice(SourceOffset, Length);
}

/// <summary>Immutable resource slices and copy-on-write patches. No retained contiguous bank image or parent publication is needed.</summary>
internal sealed record NativeBattleModelResourceStorage
{
    private sealed record MaterializedSegments(ImmutableArray<NativeBattleModelResourceSegment> Value);
    private sealed record ArrayPublication(object Identity);
    private static readonly ConditionalWeakTable<object, MaterializedSegments> Materializations = new();
    private static readonly ConditionalWeakTable<object, ArrayPublication> ArrayPublications = new();
    private static readonly IComparer<NativeBattleModelResourceSegment> OffsetComparer = Comparer<NativeBattleModelResourceSegment>.Create(static (left, right) => left.Offset.CompareTo(right.Offset));
    private readonly ImmutableList<NativeBattleModelResourceSegment>? layout;
    private readonly ImmutableArray<NativeBattleModelResourceSegment> supplied;
    private readonly object? publicationIdentity;
    private readonly bool valid;
    private readonly bool canonical;
    private readonly long extent;

    public NativeBattleModelResourceStorage(ImmutableArray<NativeBattleModelResourceSegment> segments)
    {
        Segments = segments;
    }

    private NativeBattleModelResourceStorage(ImmutableList<NativeBattleModelResourceSegment> layout)
    {
        this.layout = layout;
        supplied = default;
        publicationIdentity = new object();
        valid = true;
        canonical = true;
        extent = layout.IsEmpty ? 0 : (long)layout[^1].Offset + layout[^1].Length;
    }

    public ImmutableArray<NativeBattleModelResourceSegment> Segments
    {
        get
        {
            if (layout is null || !supplied.IsDefault)
            {
                return supplied;
            }
            if (Materializations.TryGetValue(publicationIdentity!, out MaterializedSegments? materialized))
            {
                return materialized.Value;
            }
            return Materializations.GetValue(publicationIdentity!, _ =>
            {
                NativeBattleModelResourceSegment[] segments = new NativeBattleModelResourceSegment[layout.Count];
                layout.CopyTo(segments);
                ArrayPublications.Add(segments, new(publicationIdentity!));
                return new(ImmutableCollectionsMarshal.AsImmutableArray(segments));
            }).Value;
        }
        init
        {
            ImmutableArray<NativeBattleModelResourceSegment> segments = value;
            supplied = segments;
            object? suppliedIdentity = ImmutableCollectionsMarshal.AsArray(segments);
            publicationIdentity = suppliedIdentity is not null && ArrayPublications.TryGetValue(suppliedIdentity, out ArrayPublication? publication) ? publication.Identity : suppliedIdentity;
            layout = null;
            valid = false;
            canonical = false;
            extent = 0;
            if (segments.IsDefault)
            {
                return;
            }
            layout = ImmutableList.CreateRange(segments);
            valid = true;
            canonical = true;
            long previousEnd = 0;
            NativeBattleModelResourceSegment previous = default;
            for (int index = 0; index < segments.Length; index++)
            {
                NativeBattleModelResourceSegment segment = segments[index];
                if (segment.Source.IsDefault || segment.Length <= 0 || segment.Offset < previousEnd || segment.SourceOffset < 0 || segment.SourceOffset > segment.Source.Length - segment.Length)
                {
                    valid = false;
                }
                if (index != 0 && CanMerge(previous, segment))
                {
                    canonical = false;
                }
                previousEnd = (long)segment.Offset + segment.Length;
                previous = segment;
            }
            extent = previousEnd;
        }
    }

    internal bool IsEmpty => layout is null || layout.IsEmpty;
    internal int Extent => checked((int)extent);

    public void Deconstruct(out ImmutableArray<NativeBattleModelResourceSegment> segments)
    {
        segments = Segments;
    }

    public bool Equals(NativeBattleModelResourceStorage? other)
    {
        return other is not null && ReferenceEquals(publicationIdentity, other.publicationIdentity);
    }

    public override int GetHashCode()
    {
        return unchecked(EqualityComparer<Type>.Default.GetHashCode(EqualityContract) * -1521134295 + (publicationIdentity is null ? 0 : RuntimeHelpers.GetHashCode(publicationIdentity)));
    }

    internal static NativeBattleModelResourceStorage FromKnownRanges(ImmutableArray<NativeBattleEffectMemoryRegion> ranges)
    {
        return new(ranges.IsDefault ? default : ranges.Select(range => new NativeBattleModelResourceSegment(range.Offset, range.Data, 0, range.Data.Length)).ToImmutableArray());
    }

    internal void Validate(int capacity)
    {
        if (layout is null || capacity < 0)
        {
            throw new InvalidDataException("Native model storage requires explicit resource sections and a bounded address view.");
        }
        if (!valid || extent > capacity)
        {
            throw new InvalidDataException("Native model resource sections overlap or exceed their source or address view.");
        }
    }

    internal byte[] Read(int offset, int length, int capacity)
    {
        Validate(capacity);
        RequireRange(offset, length, capacity);
        int observed = offset;
        int firstIndex = Math.Max(0, LowerBound(offset) - 1);
        for (int index = firstIndex; index < layout!.Count; index++)
        {
            NativeBattleModelResourceSegment segment = layout[index];
            if (segment.Offset >= offset + length)
            {
                break;
            }
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
        for (int index = firstIndex; index < layout!.Count; index++)
        {
            NativeBattleModelResourceSegment segment = layout[index];
            if (segment.Offset >= offset + length)
            {
                break;
            }
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
        return Replace(offset, bytes.Length, [new(offset, ImmutableCollectionsMarshal.AsImmutableArray(bytes.ToArray()), 0, bytes.Length)]);
    }

    internal NativeBattleModelResourceStorage Bind(int offset, ImmutableArray<byte> source, int sourceOffset, int length, int capacity)
    {
        Validate(capacity);
        RequireRange(offset, length, capacity);
        if (source.IsDefault || sourceOffset < 0 || sourceOffset > source.Length - length)
        {
            throw new InvalidDataException("Native model resource binding exceeds its immutable source.");
        }
        return length == 0 ? this : Replace(offset, length, [new(offset, source, sourceOffset, length)]);
    }

    internal NativeBattleModelResourceStorage Copy(int source, int destination, int length, int capacity)
    {
        Validate(capacity);
        RequireRange(source, length, capacity);
        RequireRange(destination, length, capacity);
        if (length == 0 || source == destination)
        {
            return this;
        }
        ImmutableArray<NativeBattleModelResourceSegment>.Builder moved = ImmutableArray.CreateBuilder<NativeBattleModelResourceSegment>();
        foreach (NativeBattleModelResourceSegment segment in layout!)
        {
            int first = Math.Max(source, segment.Offset);
            int end = Math.Min(source + length, segment.Offset + segment.Length);
            if (end > first)
            {
                moved.Add(new(destination + (first - source), segment.Source, segment.SourceOffset + first - segment.Offset, end - first));
            }
        }
        // Gaps in the source replace known destination bytes with unknown bytes.
        return Replace(destination, length, moved.ToImmutable().AsSpan());
    }

    internal ImmutableArray<NativeBattleEffectMemoryRegion> CaptureKnownRanges(int capacity)
    {
        Validate(capacity);
        ImmutableArray<NativeBattleModelResourceSegment> segments = Segments;
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder ranges = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        for (int index = 0; index < segments.Length;)
        {
            NativeBattleModelResourceSegment first = segments[index++];
            int end = first.Offset + first.Length;
            int after = index;
            while (after < segments.Length && segments[after].Offset == end)
            {
                end += segments[after++].Length;
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
                    NativeBattleModelResourceSegment segment = segments[index];
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
        int firstIndex = Math.Max(0, LowerBound(offset) - 1);
        for (int index = firstIndex; index < layout!.Count; index++)
        {
            NativeBattleModelResourceSegment segment = layout[index];
            if (segment.Offset >= offset + bytes.Length)
            {
                break;
            }
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

    private NativeBattleModelResourceStorage Replace(int offset, int length, ReadOnlySpan<NativeBattleModelResourceSegment> replacements)
    {
        // Supplied layouts retain their boundaries until a replacement first normalizes adjacent source slices.
        if (!canonical)
        {
            return new(LayoutReplacement(layout!, offset, length, replacements));
        }
        // In a canonical layout, only the replaced interval and its neighbors can coalesce.
        int first = LowerBound(offset);
        int after = LowerBound(offset + length);
        if (first != 0)
        {
            first--;
        }
        if (after < layout!.Count)
        {
            after++;
        }
        ImmutableList<NativeBattleModelResourceSegment> window = layout.GetRange(first, after - first);
        ImmutableArray<NativeBattleModelResourceSegment> changed = LayoutReplacement(window, offset, length, replacements);
        return new(layout.RemoveRange(first, after - first).InsertRange(first, changed));
    }

    private int LowerBound(int offset)
    {
        int index = layout!.BinarySearch(new(offset, default, 0, 0), OffsetComparer);
        return index < 0 ? ~index : index;
    }

    private static ImmutableArray<NativeBattleModelResourceSegment> LayoutReplacement(ImmutableList<NativeBattleModelResourceSegment> source, int offset, int length, ReadOnlySpan<NativeBattleModelResourceSegment> replacements)
    {
        int count = LayoutReplacement(source, offset, length, replacements, null);
        NativeBattleModelResourceSegment[] result = new NativeBattleModelResourceSegment[count];
        LayoutReplacement(source, offset, length, replacements, result);
        return ImmutableCollectionsMarshal.AsImmutableArray(result);
    }

    private static int LayoutReplacement(ImmutableList<NativeBattleModelResourceSegment> source, int offset, int length, ReadOnlySpan<NativeBattleModelResourceSegment> replacements, NativeBattleModelResourceSegment[]? destination)
    {
        int end = offset + length;
        bool inserted = false;
        SegmentLayout result = new(destination);
        foreach (NativeBattleModelResourceSegment segment in source)
        {
            int segmentEnd = segment.Offset + segment.Length;
            if (segmentEnd <= offset)
            {
                result.Append(segment);
                continue;
            }
            if (!inserted)
            {
                if (segment.Offset < offset)
                {
                    result.Append(segment with { Length = offset - segment.Offset });
                }
                foreach (NativeBattleModelResourceSegment replacement in replacements)
                {
                    result.Append(replacement);
                }
                inserted = true;
            }
            if (segment.Offset >= end)
            {
                result.Append(segment);
            }
            else if (segmentEnd > end)
            {
                result.Append(segment with { Offset = end, SourceOffset = segment.SourceOffset + end - segment.Offset, Length = segmentEnd - end });
            }
        }
        if (!inserted)
        {
            foreach (NativeBattleModelResourceSegment replacement in replacements)
            {
                result.Append(replacement);
            }
        }
        return result.Count;
    }

    private static bool CanMerge(NativeBattleModelResourceSegment previous, NativeBattleModelResourceSegment next)
    {
        return previous.Offset + previous.Length == next.Offset && previous.Source.Equals(next.Source) && previous.SourceOffset + previous.Length == next.SourceOffset;
    }

    private struct SegmentLayout(NativeBattleModelResourceSegment[]? destination)
    {
        private NativeBattleModelResourceSegment previous;
        internal int Count { get; private set; }

        internal void Append(NativeBattleModelResourceSegment segment)
        {
            if (Count > 0 && CanMerge(previous, segment))
            {
                previous = previous with { Length = previous.Length + segment.Length };
                if (destination is not null)
                {
                    destination[Count - 1] = previous;
                }
                return;
            }
            previous = segment;
            if (destination is not null)
            {
                destination[Count] = segment;
            }
            Count++;
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
