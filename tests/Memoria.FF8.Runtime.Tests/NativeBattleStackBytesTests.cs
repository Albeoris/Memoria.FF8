using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStackBytesTests
{
    [Fact]
    public void AdjacentStoresRetainTheSamePointerArrayAndPartialOverlapPreservesSurvivorOrder()
    {
        NativeBattleStackBytes original = NativeBattleStackBytes.Unknown("frame", -24, 24)
            .StorePointer(-20, new(NativeBattleStackPointerKind.Frame, "frame", -4), 0x1000, "first-pointer")
            .StorePointer(-12, new(NativeBattleStackPointerKind.Allocation, "temporary", 0), 0x1001, "second-pointer")
            .StorePointer(-4, new(NativeBattleStackPointerKind.Frame, "frame", -8), 0x1002, "third-pointer");
        NativeBattleStackBytes adjacent = original.StoreUInt32(-16, 7, 0x1003, "adjacent-literal").Invalidate(-8, 4, 0x1004, "adjacent-unknown");
        Assert.True(original.Pointers.Equals(adjacent.Pointers));
        Assert.Equal(5, adjacent.Observations.Length);

        NativeBattleStackBytes overwritten = adjacent.StoreRegister32(-13, 0x12345678, 5, 0x1005, "partial-register");
        Assert.Equal(new[] { -20, -4 }, overwritten.Pointers.Select(cell => cell.Offset));
        Assert.Same(original.Pointers[0], overwritten.Pointers[0]);
        Assert.Same(original.Pointers[2], overwritten.Pointers[1]);
        Assert.Equal(3, original.Pointers.Length);
        overwritten.Validate();
        NativeBattleStackBytes restored = JsonSerializer.Deserialize<NativeBattleStackBytes>(JsonSerializer.Serialize(overwritten))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(overwritten), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (restored with { Pointers = original.Pointers }).Validate());
    }

    [Fact]
    public void ReplayDoesNotAllocateAPointerPredicateForEveryObservation()
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("frame", -8, 8);
        for (int index = 0; index < 256; index++)
        {
            stack = stack.StoreUInt32(-8, (uint)index, 0x1000, "repeated-literal");
        }
        stack.Validate();
        stack.Validate();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 8; index++)
        {
            stack.Validate();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 32 * 1024, $"Eight bounded replays allocated {allocated} bytes.");
        Assert.Equal(256, stack.Observations.Length);
        Assert.Equal(255u, stack.ReadUInt32(-8));
    }

    [Fact]
    public void Model_read_requires_actual_prior_glow_store_in_current_queue()
    {
        NativeBattleStackBytes queue = NativeBattleStackBytes.Unknown("current-player-queue", -0x94, 4);
        Assert.Throws<NotSupportedException>(() => queue.ReadUInt32(-0x94));
        queue = queue.StoreUInt32(-0x94, 1, 0x865AA8, "glow-state1");
        Assert.Equal(1u, queue.ReadUInt32(-0x94));
        Assert.True(queue.EqualsUInt32(-0x94, 1));
        Assert.Equal(0x865AA8u, queue.Observations.Single().Instruction);
        Assert.Equal(4, queue.Observations.Single().Length);
        Assert.Throws<NotSupportedException>(() => NativeBattleStackBytes.Unknown("next-player-queue", -0x94, 4).ReadUInt32(-0x94));
    }

    [Fact]
    public void Byte_and_word_writes_preserve_other_known_bytes_and_writer_order()
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("frame", -8, 8).StoreUInt32(-8, 0x11223344, 0x1000, "first");
        stack = stack.StoreLiteral(-7, new byte[] { 0xAA, 0xBB }, 0x1001, "second");
        Assert.Equal(0x11BBAA44u, stack.ReadUInt32(-8));
        Assert.Equal(new long[] { 1, 2 }, stack.Observations.Select(item => item.Order));
        Assert.Equal(new int[] { 4, 2 }, stack.Observations.Select(item => item.Length));
    }

    [Fact]
    public void Texture_rect_pointer_has_native_owner_but_no_fabricated_numeric_address()
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("root", -0x94, 0x98);
        stack = stack.StorePointer(-0x38, new(NativeBattleStackPointerKind.Frame, "root", -0x20), 0x50C842, "texture-copy");
        Assert.True(stack.IsNonzero(-0x38));
        Assert.False(stack.EqualsUInt32(-0x38, 1));
        Assert.Throws<NotSupportedException>(() => stack.ReadUInt32(-0x38));
        Assert.Throws<NotSupportedException>(() => stack.EqualsUInt32(-0x38, 0x1000));
        Assert.Equal(-0x20, stack.Pointers.Single().Pointer.Offset);
    }

    [Fact]
    public void Partial_write_or_unknown_call_invalidates_symbolic_pointer_comparison()
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("root", -8, 8).StorePointer(-8, new(NativeBattleStackPointerKind.Allocation, "temporary76", 0), 0x50F195, "group-fade");
        NativeBattleStackBytes partial = stack.StoreLiteral(-8, new byte[] { 1 }, 0x1000, "byte-store");
        Assert.Empty(partial.Pointers);
        Assert.Throws<NotSupportedException>(() => partial.EqualsUInt32(-8, 1));
        NativeBattleStackBytes unknown = stack.Invalidate(-8, 4, 0x4825C8, "unmodeled-callback");
        Assert.Empty(unknown.Pointers);
        Assert.Throws<NotSupportedException>(() => unknown.IsNonzero(-8));
        Assert.Equal(NativeBattleStackOperation.UnknownRange, unknown.Observations[^1].Operation);
    }

    [Fact]
    public void Unknown_range_is_not_claimed_as_a_native_instruction_store_width()
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("root", -16, 16).Invalidate(-15, 13, 0x4825C8, "unmodeled-callee");
        Assert.Equal(13, stack.Observations.Single().Length);
        Assert.Equal(NativeBattleStackOperation.UnknownRange, stack.Observations.Single().Operation);
        Assert.Throws<InvalidDataException>(() => stack.StoreLiteral(-16, new byte[3], 0x1000, "invalid-store"));
    }

    [Fact]
    public void Json_roundtrip_retains_literal_pointer_and_unknown_provenance()
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("root", -16, 16).StoreUInt32(-16, 1, 0x865AA8, "glow");
        stack = stack.StorePointer(-8, new(NativeBattleStackPointerKind.Frame, "root", -0x20), 0x50C842, "copy");
        stack = stack.Invalidate(-4, 4, 0x4825C8, "unknown");
        string json = JsonSerializer.Serialize(stack);
        NativeBattleStackBytes restored = JsonSerializer.Deserialize<NativeBattleStackBytes>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(1u, restored.ReadUInt32(-16));
        Assert.True(restored.IsNonzero(-8));
        Assert.Throws<NotSupportedException>(() => restored.ReadUInt32(-4));
    }

    [Fact]
    public void Snapshot_cannot_restore_stale_pointer_after_overlapping_invalidation()
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("root", -4, 4).StorePointer(-4, new(NativeBattleStackPointerKind.Frame, "root", -0x20), 0x50C842, "copy");
        NativeBattleStackBytes invalid = stack.Invalidate(-4, 4, 0x4825C8, "unknown");
        Assert.Throws<InvalidDataException>(() => (invalid with { Pointers = stack.Pointers }).Validate());
        Assert.Throws<InvalidDataException>(() => (invalid with { Known = ImmutableArray.Create(true, true, true, true) }).Validate());
    }

    [Fact]
    public void Physical_interval_and_pointer_alignment_are_explicit()
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("root", -4, 4);
        Assert.Throws<InvalidDataException>(() => stack.StoreUInt32(-5, 1, 0x1000, "outside"));
        Assert.Throws<InvalidDataException>(() => stack.StorePointer(-4, new(NativeBattleStackPointerKind.Frame, "root", -3), 0x1000, "unaligned"));
        Assert.Throws<InvalidDataException>(() => stack.StoreUInt32(-4, 1, 0, "missing-writer"));
    }
    [Theory]
    [InlineData(0x00000100u)]
    [InlineData(0x00010000u)]
    [InlineData(0x01000000u)]
    [InlineData(0x80000000u)]
    public void Nonzero_compares_the_complete_native_DWORD(uint value)
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("root", -4, 4).StoreUInt32(-4, value, 0x1000, "literal");
        Assert.True(stack.IsNonzero(-4));
        Assert.False(stack.EqualsUInt32(-4, 1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Either_byte_or_WORD_overwrite_removes_pointer_proof(int width)
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("root", -4, 4).StorePointer(-4, new(NativeBattleStackPointerKind.Frame, "root", -0x20), 0x50C842, "copy");
        stack = stack.StoreLiteral(-4, new byte[width], 0x1000, "partial");
        Assert.Empty(stack.Pointers);
        Assert.Throws<NotSupportedException>(() => stack.IsNonzero(-4));
    }

    [Fact]
    public void Pain_init_and_play_queues_use_actual_stores_without_cross_frame_carry()
    {
        NativeBattleStackBytes init = NativeBattleStackBytes.Unknown("init-player-queue", -0x94, 4);
        init = init.StoreUInt32(-0x94, 0, 0x865A62, "glow-state0-bone-argument");
        init = init.StoreUInt32(-0x94, 0, 0x865E42, "model-state0-bone-argument");
        Assert.Equal(0u, init.ReadUInt32(-0x94));
        NativeBattleStackBytes play = NativeBattleStackBytes.Unknown("play-player-queue", -0x94, 4);
        Assert.Throws<NotSupportedException>(() => play.ReadUInt32(-0x94));
        play = play.StoreUInt32(-0x94, 1, 0x865AA8, "glow-state1");
        Assert.Equal(1u, play.ReadUInt32(-0x94));
        Assert.Equal(1u, play.ReadUInt32(-0x94));
        Assert.Single(play.Observations);
        NativeBattleStackBytes next = NativeBattleStackBytes.Unknown("next-play-player-queue", -0x94, 4);
        Assert.Throws<NotSupportedException>(() => next.ReadUInt32(-0x94));
    }

    [Fact]
    public void Independent_json_restore_has_identical_next_store_order_and_bytes()
    {
        NativeBattleStackBytes first = NativeBattleStackBytes.Unknown("root", -4, 4).StoreUInt32(-4, 1, 0x865AA8, "glow-state1");
        NativeBattleStackBytes second = JsonSerializer.Deserialize<NativeBattleStackBytes>(JsonSerializer.Serialize(first))!;
        first = first.StoreLiteral(-3, new byte[] { 2 }, 0x1000, "next");
        second = second.StoreLiteral(-3, new byte[] { 2 }, 0x1000, "next");
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(2L, second.Observations[^1].Order);
        Assert.Equal(0x201u, second.ReadUInt32(-4));
    }
}
