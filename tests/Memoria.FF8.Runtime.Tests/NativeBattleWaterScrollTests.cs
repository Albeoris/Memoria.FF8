using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterScrollTests
{
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(255, 255, 2)]
    [InlineData(-1, 255, 2)]
    [InlineData(-256, 0, 1)]
    public void Signed_scroll_uses_bitmask_and_emits_bottom_then_top(short scroll, int row, int count)
    {
        var fixture = Source(128, 256);
        NativeBattleWaterScrollStep step = NativeBattleWaterScroll.DrawA9C140(fixture.Profile, fixture.Files, new(0, 512, 256), scroll, 0, Ignore);
        Assert.Equal(count, step.Writes.Length);
        Assert.Equal((byte)count, step.Counter43);
        Assert.Equal(0x100 + 256 * row, step.Writes[0].Upload.Offset);
        Assert.Equal(new NativeBattleEffectTextureRectangle(512, 256, 128, (short)(256 - row)), step.Writes[0].Upload.Rectangle);
        if (row != 0)
        {
            Assert.Equal(0x100, step.Writes[1].Upload.Offset);
            Assert.Equal(new NativeBattleEffectTextureRectangle(512, (short)(512 - row), 128, (short)row), step.Writes[1].Upload.Rectangle);
        }
        Assert.All(step.Writes, write => Assert.Equal(0, write.Upload.SourceIndex));
    }

    [Fact]
    public void Ring_counter_wraps_and_full_RECT_writes_preserve_other_retained_slots_in_submission_order()
    {
        var fixture = Source(2, 4);
        byte[] ring = Enumerable.Range(0, 128).Select(index => unchecked((byte)(index + 11))).ToArray();
        byte[] before = ring.ToArray();
        List<int> submitted = [];
        byte counter = 0xFE;
        void Submit(NativeBattleWaterRectangleWrite write)
        {
            Assert.Equal(submitted.Count == 0 ? 14 : submitted.Count == 1 ? 15 : 0, write.RingIndex);
            Assert.Equal(unchecked((byte)(counter + 1)), write.NextCounter);
            counter = write.NextCounter;
            write.RectangleBytes.CopyTo(ring, write.RingIndex * 8);
            submitted.Add(write.RingIndex);
        }
        NativeBattleWaterScrollStep first = NativeBattleWaterScroll.DrawA9C140(fixture.Profile, fixture.Files, new(0, -2, 32767), 1, counter, Submit);
        Assert.Equal((byte)0, first.Counter43);
        NativeBattleWaterScrollStep second = NativeBattleWaterScroll.DrawA9C140(fixture.Profile, fixture.Files, new(0, 9, 12), 0, first.Counter43, Submit);
        Assert.Equal((byte)1, second.Counter43);
        Assert.Equal(new[] { 14, 15, 0 }, submitted);
        Assert.Equal(before.Skip(8).Take(104), ring.Skip(8).Take(104));
        Assert.Equal((short)-32766, BinaryPrimitives.ReadInt16LittleEndian(ring.AsSpan(15 * 8 + 2)));
        Assert.All(first.Writes.Concat(second.Writes), write => Assert.Equal(8, write.RectangleBytes.Length));
    }

    [Fact]
    public void Arena_queue_keeps_late_pixel_source_and_cold_drain_after_current_source_mutation()
    {
        var fixture = Source(2, 4);
        const int file0 = 0x2000;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(file0, fixture.Files[0].Data.AsSpan());
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleWaterScrollStep step = NativeBattleWaterScroll.DrawA9C140(fixture.Profile, fixture.Files, new(0, 10, 20), 1, 7, write => textures = NativeBattleWaterScroll.EnqueueArena505DF0(textures, arena, file0, 0x50000, write));
        Assert.Equal((byte)9, step.Counter43);
        Assert.Equal(2, textures.Count);
        Assert.All(textures.Slots.Take(2), slot => Assert.Null(slot!.Source));
        arena = arena.Write(file0 + 0x104, [0xAA, 0xBB]);
        NativeBattleEffectTextureState restoredTextures = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(textures))!;
        NativeBattleEffectArena restoredArena = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(arena))!;
        NativeBattleEffectTextureDrain current = textures.Drain505D20(arena);
        NativeBattleEffectTextureDrain cold = restoredTextures.Drain505D20(restoredArena);
        Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(cold));
        Assert.Equal(new byte[] { 0xAA, 0xBB }, current.Uploads[0].Bytes.Take(2));
        Assert.Equal((short)20, current.Uploads[0].Y);
        Assert.Equal((short)23, current.Uploads[1].Y);
        Assert.Equal(fixture.Files[0].Data.Skip(0x100).Take(4), current.Uploads[1].Bytes);
    }

    [Fact]
    public void First_enqueue_occurs_before_second_ring_allocation_and_global_queue_limit_is_not_hidden()
    {
        var fixture = Source(2, 4);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, fixture.Files[0].Data.AsSpan());
        NativeBattleEffectTextureRectangle rectangle = new(0, 0, 1, 1);
        for (int index = 0; index < 30; index++)
        {
            textures = textures.EnqueueArenaRectangle505DF0(rectangle, arena, 0x100);
        }
        List<NativeBattleWaterRectangleWrite> observed = [];
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterScroll.DrawA9C140(fixture.Profile, fixture.Files, new(0, 0, 0), 1, 4, write =>
        {
            Assert.Equal(observed.Count == 0 ? 30 : 31, textures.Count);
            observed.Add(write);
            textures = NativeBattleWaterScroll.EnqueueArena505DF0(textures, arena, 0, 0x50000, write);
        }));
        Assert.Equal(2, observed.Count);
        Assert.Equal(new[] { 4, 5 }, observed.Select(write => write.RingIndex));
        Assert.Equal(31, textures.Count);
    }

    [Fact]
    public void Bitwise_scroll_is_not_remainder_and_missing_source_or_nonpositive_extent_fail()
    {
        var fixture = Source(2, 6);
        NativeBattleWaterScrollStep step = NativeBattleWaterScroll.DrawA9C140(fixture.Profile, fixture.Files, new(0, 0, 0), 2, 0, Ignore);
        Assert.Single(step.Writes);
        Assert.Equal((short)6, step.Writes[0].Upload.Rectangle.Height);
        var empty = Source(0, 4);
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterScroll.DrawA9C140(empty.Profile, empty.Files, new(0, 0, 0), 0, 0, Ignore));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterScroll.DrawA9C140(fixture.Profile, fixture.Files, new(-1, 0, 0), 0, 0, Ignore));
        ImmutableArray<NativeBattleEffectHeapAllocation> shortSource = fixture.Files.SetItem(0, new("current", fixture.Files[0].Data.Take(0x108).ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterScroll.DrawA9C140(fixture.Profile, shortSource, new(0, 0, 0), 1, 0, Ignore));
    }

    private static void Ignore(NativeBattleWaterRectangleWrite write)
    {
    }

    private static (NativeBattleWater222Profile Profile, ImmutableArray<NativeBattleEffectHeapAllocation> Files) Source(short width, short height)
    {
        byte[] tables = new byte[0x70];
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(4), width);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(6), height);
        tables[80] = 0x80;
        byte[] file = new byte[0x100 + 2 * width * height];
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x14), 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x40), 0xC0);
        for (int index = 0x100; index < file.Length; index++)
        {
            file[index] = unchecked((byte)index);
        }
        return (new(new("tables", tables.ToImmutableArray()), new("initialization", new byte[0x1C].ToImmutableArray()), new("owners", new byte[0x14].ToImmutableArray())), [new("current0", file.ToImmutableArray()), new("current1", [])]);
    }
}
