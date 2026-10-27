using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectArenaWriterTests
{
    [Theory]
    [InlineData(12, 3)]
    [InlineData(5, 10)]
    [InlineData(15, 30)]
    [InlineData(18, 35)]
    [InlineData(10, 50)]
    [InlineData(20, 10)]
    public void Indexed_unknown_updates_preserve_split_clipped_spanning_and_adjacent_store_layouts(int offset, int length)
    {
        NativeBattleEffectArena source = new([new(0, new byte[80].ToImmutableArray())]) { UnknownRegions = [new(10, 10), new(30, 10), new(50, 10)] };
        NativeBattleEffectArena preserved = NativeBattleStorageSnapshots.Capture(source);
        using NativeBattleEffectArenaWriter writer = new(source);
        byte[] bytes = Enumerable.Repeat((byte)7, length).ToArray();
        NativeBattleEffectArena scalar = NativeBattleStorageSnapshots.Restore(preserved).Write(offset, bytes);
        writer.Write(offset, bytes);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(writer.Freeze())), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(scalar)));
        scalar = scalar.MarkUnknown(offset, length);
        writer.MarkUnknown(offset, length);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(writer.Freeze())), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(scalar)));
        scalar = scalar.MarkUnknown(offset, length);
        writer.MarkUnknown(offset, length);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(writer.Freeze())), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(scalar)));
        Assert.Equal(3, preserved.UnknownRegions.Length);
    }

    [Fact]
    public void Contained_stores_retain_fragmented_layout_and_stores_at_adjacent_boundaries_still_merge()
    {
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(2, [3, 4]), new(100, [5, 6, 7, 8]), new(200, [9])]) { UnknownRegions = [new(101, 2)] };
        NativeBattleEffectArena scalar = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source));
        NativeBattleEffectArena preserved = NativeBattleStorageSnapshots.Capture(source);
        using NativeBattleEffectArenaWriter writer = new(source);
        foreach (int offset in new[] { 101, 102, 100, 1, 2, 200 })
        {
            scalar = scalar.Write(offset, [17]);
            writer.Write(offset, [17]);
            Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(writer.Freeze())), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(scalar)));
        }
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, preserved.ReadSnapshot(100, 4).Data);
        Assert.Equal(new bool[] { true, false, false, true }, preserved.ReadSnapshot(100, 4).Known);
    }

    [Fact]
    public void Ordered_packet_stores_match_scalar_arena_bytes_region_layout_and_unknown_spans()
    {
        Random random = new(5731);
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(2, [3, 4]), new(100, [5])]) { UnknownRegions = [new(1, 3), new(128, 16)] };
        string original = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source));
        NativeBattleEffectArena scalar = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source));
        NativeBattleEffectArena preserved = NativeBattleStorageSnapshots.Capture(source);
        using NativeBattleEffectArenaWriter writer = new(source);
        for (int index = 0; index < 400; index++)
        {
            int offset = random.Next(0, 2048);
            int length = random.Next(0, 65);
            if (index % 3 == 0)
            {
                scalar = scalar.MarkUnknown(offset, length);
                writer.MarkUnknown(offset, length);
            }
            else
            {
                byte[] bytes = new byte[length];
                random.NextBytes(bytes);
                scalar = scalar.Write(offset, bytes);
                writer.Write(offset, bytes);
            }
            if (index % 23 == 0)
            {
                Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(writer.Freeze())), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(scalar)));
            }
        }
        NativeBattleEffectArena result = writer.Freeze();
        result.Validate();
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(scalar)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)));
        Assert.Equal(original, JsonSerializer.Serialize(preserved));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))))));
    }

    [Fact]
    public void Frozen_arenas_and_input_bytes_remain_independent_of_later_edits()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(100, [1, 2, 3]);
        NativeBattleEffectArena preserved = NativeBattleStorageSnapshots.Capture(source);
        using NativeBattleEffectArenaWriter writer = new(source);
        byte[] input = [9, 8];
        writer.Write(101, input);
        input[0] = 0;
        NativeBattleEffectArena first = NativeBattleStorageSnapshots.Capture(writer.Freeze());
        writer.Write(100, [7, 6, 5]);
        NativeBattleEffectArena second = writer.Freeze();
        Assert.Equal(new byte[] { 1, 2, 3 }, preserved.Read(100, 3));
        Assert.Equal(new byte[] { 7, 6, 5 }, source.Read(100, 3));
        Assert.Equal(new byte[] { 1, 9, 8 }, first.Read(100, 3));
        Assert.Equal(new byte[] { 7, 6, 5 }, second.Read(100, 3));
        Assert.Throws<InvalidDataException>(() => writer.Write(NativeBattleEffectArena.Length, [1]));
        Assert.Throws<InvalidDataException>(() => writer.MarkUnknown(-1, 1));
    }

    [Fact]
    public void Packet_store_batch_reuses_its_owner_without_copying_the_full_region()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, new byte[0x20000]);
        NativeBattleEffectArena scalar = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source));
        byte[] bytes = [1, 2, 3, 4];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
        {
            scalar = scalar.Write(index * 4, bytes);
        }
        long scalarBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        using NativeBattleEffectArenaWriter writer = new(source);
        for (int index = 0; index < 64; index++)
        {
            writer.Write(index * 4, bytes);
        }
        NativeBattleEffectArena result = writer.Freeze();
        long batchBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(scalar)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)));
        Assert.Same(source, result);
        Assert.True(batchBytes < 0x20000, $"Scalar allocated {scalarBytes} bytes; batch allocated {batchBytes} bytes for a 131072-byte region.");
    }
}
