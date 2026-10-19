using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectArenaWriterTests
{
    [Fact]
    public void Contained_stores_retain_fragmented_layout_and_stores_at_adjacent_boundaries_still_merge()
    {
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(2, [3, 4]), new(100, [5, 6, 7, 8]), new(200, [9])]) { UnknownRegions = [new(101, 2)] };
        NativeBattleEffectArena scalar = source;
        NativeBattleEffectArenaWriter writer = new(source);
        foreach (int offset in new[] { 101, 102, 100, 1, 2, 200 })
        {
            scalar = scalar.Write(offset, [17]);
            writer.Write(offset, [17]);
            Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(writer.Freeze()));
        }
        Assert.Equal(4, source.Regions.Length);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, source.Regions[2].Data.ToArray());
    }

    [Fact]
    public void Ordered_packet_stores_match_scalar_arena_bytes_region_layout_and_unknown_spans()
    {
        Random random = new(5731);
        NativeBattleEffectArena source = new([new(0, [1, 2]), new(2, [3, 4]), new(100, [5])]) { UnknownRegions = [new(1, 3), new(128, 16)] };
        string original = JsonSerializer.Serialize(source);
        NativeBattleEffectArena scalar = source;
        NativeBattleEffectArenaWriter writer = new(source);
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
                Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(writer.Freeze()));
            }
        }
        NativeBattleEffectArena result = writer.Freeze();
        result.Validate();
        Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(result));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(result))));
    }

    [Fact]
    public void Frozen_arenas_and_input_bytes_remain_independent_of_later_edits()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(100, [1, 2, 3]);
        NativeBattleEffectArenaWriter writer = new(source);
        byte[] input = [9, 8];
        writer.Write(101, input);
        input[0] = 0;
        NativeBattleEffectArena first = writer.Freeze();
        writer.Write(100, [7, 6, 5]);
        NativeBattleEffectArena second = writer.Freeze();
        Assert.Equal(new byte[] { 1, 2, 3 }, source.Read(100, 3));
        Assert.Equal(new byte[] { 1, 9, 8 }, first.Read(100, 3));
        Assert.Equal(new byte[] { 7, 6, 5 }, second.Read(100, 3));
        Assert.Throws<InvalidDataException>(() => writer.Write(NativeBattleEffectArena.Length, [1]));
        Assert.Throws<InvalidDataException>(() => writer.MarkUnknown(-1, 1));
    }

    [Fact]
    public void Packet_store_batch_avoids_repeated_full_region_copies()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, new byte[0x20000]);
        NativeBattleEffectArena scalar = source;
        byte[] bytes = [1, 2, 3, 4];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
        {
            scalar = scalar.Write(index * 4, bytes);
        }
        long scalarBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattleEffectArenaWriter writer = new(source);
        for (int index = 0; index < 64; index++)
        {
            writer.Write(index * 4, bytes);
        }
        NativeBattleEffectArena result = writer.Freeze();
        long batchBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(result));
        Assert.True(batchBytes < scalarBytes / 8, $"Scalar allocated {scalarBytes} bytes; batch allocated {batchBytes} bytes.");
    }
}
