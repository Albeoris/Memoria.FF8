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

    [Fact]
    public void Supplied_array_identity_and_publication_equality_survive_diagnostic_materialization_and_global_initial_coalescing()
    {
        ImmutableArray<byte> source = Enumerable.Range(0, 32).Select(value => (byte)value).ToImmutableArray();
        ImmutableArray<NativeBattleModelResourceSegment> supplied = [new(0, source, 0, 4), new(4, source, 4, 4), new(16, source, 16, 4), new(20, source, 20, 4)];
        NativeBattleModelResourceStorage original = new(supplied);
        NativeBattleModelResourceStorage equal = new(supplied);
        Assert.True(original.Segments.Equals(supplied));
        Assert.Equal(original, equal);
        Assert.Equal(unchecked(EqualityComparer<Type>.Default.GetHashCode(typeof(NativeBattleModelResourceStorage)) * -1521134295 + supplied.GetHashCode()), original.GetHashCode());
        Assert.NotEqual(original, new NativeBattleModelResourceStorage(supplied.ToArray().ToImmutableArray()));
        ImmutableArray<byte> patch = [99];
        NativeBattleModelResourceStorage changed = original.Bind(12, patch, 0, 1, 32);
        NativeBattleModelResourceStorage expected = PreviousReplace(original, 12, 1, [new(12, patch, 0, 1)]);
        Assert.Equal(expected.Segments.ToArray(), changed.Segments.ToArray());
        Assert.Equal(3, changed.Segments.Length);
        Assert.Equal(4, original.Segments.Length);
        NativeBattleModelResourceStorage next = changed.Bind(13, patch, 0, 1, 32);
        int hash = next.GetHashCode();
        ImmutableArray<NativeBattleModelResourceSegment> materialized = next.Segments;
        Assert.True(materialized.Equals(next.Segments));
        Assert.Equal(hash, next.GetHashCode());
        NativeBattleModelResourceStorage fromMaterialized = new(materialized);
        NativeBattleModelResourceStorage initializedFromMaterialized = next with { Segments = materialized };
        Assert.Equal(next, fromMaterialized);
        Assert.Equal(next, initializedFromMaterialized);
        Assert.Equal(hash, fromMaterialized.GetHashCode());
        Assert.Equal(hash, initializedFromMaterialized.GetHashCode());
        Assert.True(materialized.Equals(fromMaterialized.Segments));
        Assert.Equal(hash, next.GetHashCode());
        NativeBattleModelResourceStorage clone = next with { };
        Assert.Equal(next, clone);
        Assert.True(materialized.Equals(clone.Segments));
        NativeBattleModelResourceStorage rebound = next with { Segments = supplied };
        Assert.Equal(original, rebound);
        Assert.True(supplied.Equals(rebound.Segments));
        NativeBattleModelResourceStorage corrupt = next with { Segments = [new(0, default, 0, 1)] };
        Assert.Throws<InvalidDataException>(() => corrupt.Validate(32));
        Assert.Equal(hash, next.GetHashCode());
        CheckBytes(original, supplied, 32);
    }

    [Fact]
    public void Independent_empty_publications_keep_distinct_identity_and_record_clones_share_their_published_layout()
    {
        ImmutableArray<byte> source = [1];
        NativeBattleModelResourceStorage original = new([new(0, source, 0, 1)]);
        NativeBattleModelResourceStorage first = original.Copy(4, 0, 1, 8);
        NativeBattleModelResourceStorage second = original.Copy(4, 0, 1, 8);
        int hash = first.GetHashCode();
        Assert.Empty(first.Segments);
        Assert.Empty(second.Segments);
        Assert.NotEqual(first, second);
        Assert.False(first.Segments.Equals(second.Segments));
        NativeBattleModelResourceStorage clone = first with { };
        Assert.Equal(first, clone);
        Assert.True(first.Segments.Equals(clone.Segments));
        Assert.Equal(first, new NativeBattleModelResourceStorage(first.Segments));
        Assert.Equal(first, first with { Segments = first.Segments });
        Assert.Equal(hash, first.GetHashCode());
        Assert.Equal(new byte[] { 1 }, original.Read(0, 1, 8));
    }

    [Fact]
    public void Persistent_publications_match_the_previous_layout_through_a_long_retained_patch_and_clear_chain()
    {
        const int capacity = 512;
        ImmutableArray<byte> source = Enumerable.Range(0, capacity).Select(value => (byte)value).ToImmutableArray();
        NativeBattleModelResourceStorage actual = new([new(0, source, 0, capacity)]);
        NativeBattleModelResourceStorage expected = new(actual.Segments);
        List<(NativeBattleModelResourceStorage Storage, ImmutableArray<NativeBattleModelResourceSegment> Expected)> retained = [];
        for (int index = 0; index < 1024; index++)
        {
            int offset = index < capacity ? index : index - capacity;
            ImmutableArray<byte> patch = [index < capacity ? (byte)(source[offset] ^ 0x80) : source[offset]];
            expected = PreviousReplace(expected, offset, 1, [new(offset, patch, 0, 1)]);
            actual = actual.Bind(offset, patch, 0, 1, capacity);
            if ((index & 31) == 0)
            {
                Assert.Equal(expected.Segments.ToArray(), actual.Segments.ToArray());
                retained.Add((actual, expected.Segments));
            }
        }
        Assert.Equal(source.ToArray(), actual.Read(0, capacity, capacity));
        foreach (var publication in retained)
        {
            Assert.Equal(publication.Expected.ToArray(), publication.Storage.Segments.ToArray());
            CheckBytes(publication.Storage, publication.Expected, capacity);
        }
    }

    [Fact]
    public void Invalid_immutable_layouts_and_capacity_changes_are_rejected_before_publication()
    {
        ImmutableArray<byte> source = [1, 2, 3, 4];
        NativeBattleModelResourceStorage[] invalid = [new(default(ImmutableArray<NativeBattleModelResourceSegment>)), new([new(0, default, 0, 1)]), new([new(-1, source, 0, 1)]), new([new(0, source, -1, 1)]), new([new(0, source, 0, -1)]), new([new(0, source, 0, 5)]), new([new(2, source, 0, 2), new(3, source, 0, 1)])];
        foreach (NativeBattleModelResourceStorage storage in invalid)
        {
            Assert.Throws<InvalidDataException>(() => storage.Validate(16));
            Assert.Throws<InvalidDataException>(() => storage.Bind(0, source, 0, 1, 16));
        }
        NativeBattleModelResourceStorage valid = new([new(4, source, 0, 4)]);
        valid.Validate(8);
        Assert.Throws<InvalidDataException>(() => valid.Validate(7));
        Assert.Throws<InvalidDataException>(() => valid.Validate(-1));
        Assert.Equal(8, valid.Extent);
        Assert.Equal(source.ToArray(), valid.Read(4, 4, 8));
    }

    [Fact]
    public void Warm_patch_allocation_grows_with_the_persistent_path_rather_than_the_whole_layout()
    {
        long small = AllocatedFor(512);
        long large = AllocatedFor(8192);
        Assert.True(large < small * 3, $"512 segments allocated {small} bytes; 8192 segments allocated {large} bytes.");

        static long AllocatedFor(int count)
        {
            ImmutableArray<byte> source = new byte[count * 2].ToImmutableArray();
            NativeBattleModelResourceStorage storage = new(Enumerable.Range(0, count).Select(index => new NativeBattleModelResourceSegment(index * 2, source, index * 2, 1)).ToImmutableArray());
            ImmutableArray<byte> patch = [99];
            for (int index = 0; index < 16; index++)
            {
                _ = storage.Bind(count + 1, patch, 0, 1, count * 2);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 128; index++)
            {
                _ = storage.Bind(count + 1, patch, 0, 1, count * 2);
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
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
