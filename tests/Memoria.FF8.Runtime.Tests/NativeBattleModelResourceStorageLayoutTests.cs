using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelResourceStorageLayoutTests
{
    [Fact]
    public void Overlap_gaps_adjacent_sources_and_random_copies_match_previous_layout_and_preserve_prior_owners()
    {
        const int capacity = 128;
        ImmutableArray<byte> first = Enumerable.Range(0, capacity).Select(value => (byte)value).ToImmutableArray();
        ImmutableArray<byte> second = Enumerable.Range(0, capacity).Select(value => (byte)(255 - value)).ToImmutableArray();
        NativeBattleModelResourceStorage actual = new([]);
        NativeBattleModelResourceStorage expected = new([]);
        List<(NativeBattleModelResourceStorage Storage, ImmutableArray<NativeBattleModelResourceSegment> Segments)> retained = [];
        Bind(8, first, 0, 16);
        Bind(24, first, 16, 16);
        Assert.Single(actual.Segments);
        Bind(64, second, 8, 24);
        Bind(16, second, 0, 56);
        Copy(0, 12, 80);
        Copy(12, 0, 80);
        Bind(0, first, 0, capacity);
        Assert.Single(actual.Segments);
        Random random = new(314);
        for (int index = 0; index < 256; index++)
        {
            int length = random.Next(1, 49);
            int destination = random.Next(capacity - length + 1);
            int source = random.Next(capacity - length + 1);
            if ((index & 1) == 0)
            {
                Bind(destination, (index & 2) == 0 ? first : second, source, length);
            }
            else
            {
                Copy(source, destination, length);
            }
        }
        foreach ((NativeBattleModelResourceStorage storage, ImmutableArray<NativeBattleModelResourceSegment> segments) in retained)
        {
            Assert.Equal(segments.ToArray(), storage.Segments.ToArray());
            CheckBytes(storage, segments, capacity);
        }

        void Check()
        {
            actual.Validate(capacity);
            Assert.Equal(expected.Segments.ToArray(), actual.Segments.ToArray());
            CheckBytes(actual, expected.Segments, capacity);
            retained.Add((actual, expected.Segments));
        }

        void Bind(int destination, ImmutableArray<byte> source, int sourceOffset, int length)
        {
            expected = PreviousReplace(expected, destination, length, [new(destination, source, sourceOffset, length)]);
            actual = actual.Bind(destination, source, sourceOffset, length, capacity);
            Check();
        }

        void Copy(int source, int destination, int length)
        {
            ImmutableArray<NativeBattleModelResourceSegment>.Builder moved = ImmutableArray.CreateBuilder<NativeBattleModelResourceSegment>();
            foreach (NativeBattleModelResourceSegment segment in expected.Segments)
            {
                int firstByte = Math.Max(source, segment.Offset);
                int end = Math.Min(source + length, segment.Offset + segment.Length);
                if (end > firstByte)
                {
                    moved.Add(new(destination + firstByte - source, segment.Source, segment.SourceOffset + firstByte - segment.Offset, end - firstByte));
                }
            }
            if (source != destination)
            {
                expected = PreviousReplace(expected, destination, length, moved.ToImmutable().AsSpan());
            }
            actual = actual.Copy(source, destination, length, capacity);
            Check();
        }
    }

    [Fact]
    public void Invalid_replacements_leave_the_original_storage_and_its_sources_unchanged()
    {
        ImmutableArray<byte> source = [1, 2, 3, 4];
        NativeBattleModelResourceStorage storage = new([new(4, source, 0, 4)]);
        Assert.Throws<InvalidDataException>(() => storage.Bind(0, source, 2, 4, 16));
        Assert.Throws<InvalidDataException>(() => storage.Copy(4, 15, 4, 16));
        Assert.Throws<InvalidDataException>(() => storage.Write(15, new byte[4], 16));
        Assert.Equal(new NativeBattleModelResourceSegment(4, source, 0, 4), Assert.Single(storage.Segments));
        Assert.Equal(source.ToArray(), storage.Read(4, 4, 16));
        Assert.Throws<NotSupportedException>(() => storage.Read(3, 2, 16));
    }

    [Fact]
    public void Fragmented_replacement_allocates_one_layout_instead_of_builder_growth_and_export_copies()
    {
        const int capacity = 8192;
        ImmutableArray<byte> source = Enumerable.Range(0, capacity).Select(value => (byte)value).ToImmutableArray();
        NativeBattleModelResourceStorage storage = new(Enumerable.Range(0, capacity / 2).Select(index => new NativeBattleModelResourceSegment(index * 2, source, index * 2, 1)).ToImmutableArray());
        NativeBattleModelResourceSegment[] replacement = [new(2001, source, 1000, 1)];
        for (int index = 0; index < 8; index++)
        {
            _ = PreviousReplace(storage, 2001, 1, replacement);
            _ = storage.Bind(2001, source, 1000, 1, capacity);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 32; index++)
        {
            _ = PreviousReplace(storage, 2001, 1, replacement);
        }
        long previous = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 32; index++)
        {
            _ = storage.Bind(2001, source, 1000, 1, capacity);
        }
        long current = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(current < previous / 2, $"Exact layouts allocated {current} bytes; previous layouts allocated {previous} bytes.");
        Assert.Equal(PreviousReplace(storage, 2001, 1, replacement).Segments.ToArray(), storage.Bind(2001, source, 1000, 1, capacity).Segments.ToArray());
    }

    private static void CheckBytes(NativeBattleModelResourceStorage storage, ImmutableArray<NativeBattleModelResourceSegment> expected, int capacity)
    {
        for (int offset = 0; offset < capacity; offset++)
        {
            NativeBattleModelResourceSegment? containing = null;
            foreach (NativeBattleModelResourceSegment segment in expected)
            {
                if (offset >= segment.Offset && offset < segment.Offset + segment.Length)
                {
                    containing = segment;
                    break;
                }
            }
            if (containing is { } known)
            {
                Assert.Equal(known.Source[known.SourceOffset + offset - known.Offset], Assert.Single(storage.Read(offset, 1, capacity)));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => storage.Read(offset, 1, capacity));
            }
        }
    }

    private static NativeBattleModelResourceStorage PreviousReplace(NativeBattleModelResourceStorage storage, int offset, int length, ReadOnlySpan<NativeBattleModelResourceSegment> replacements)
    {
        int end = offset + length;
        bool inserted = false;
        ImmutableArray<NativeBattleModelResourceSegment>.Builder result = ImmutableArray.CreateBuilder<NativeBattleModelResourceSegment>();
        foreach (NativeBattleModelResourceSegment segment in storage.Segments)
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
                foreach (NativeBattleModelResourceSegment replacement in replacements)
                {
                    Append(replacement);
                }
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
            foreach (NativeBattleModelResourceSegment replacement in replacements)
            {
                Append(replacement);
            }
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
}
