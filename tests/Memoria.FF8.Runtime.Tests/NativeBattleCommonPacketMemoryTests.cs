using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonPacketMemoryTests
{
    private static NativeBattlePrimitiveProcess Process => NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();

    [Fact]
    public void First_observed_write_initializes_only_active_bank_with_every_other_byte_unknown()
    {
        NativeBattlePrimitiveProcess initial = Process;
        NativeBattlePrimitiveProcess written = initial.ApplyCommonWrite(14, [0, 7], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known]);
        Assert.Null(initial.CommonPacketMemory);
        NativeBattleCommonPacketMemory memory = Assert.IsType<NativeBattleCommonPacketMemory>(written.CommonPacketMemory);
        Assert.Null(memory.Bank070Buffer1);
        Assert.Null(memory.Bank094Buffer0);
        Assert.Null(memory.Bank094Buffer1);
        Assert.Equal(new byte[] { 0, 7 }, written.ReadCommonPacket(14, 2));
        Assert.Throws<NotSupportedException>(() => written.ReadCommonPacket(13, 2));
        Assert.Throws<NotSupportedException>(() => written.ReadCommonPacket(16, 1));
        Assert.Equal(new[] { new NativeBattleEffectUnknownRegion(0, 14), new NativeBattleEffectUnknownRegion(16, 114672) }, memory.Bank070Buffer0!.UnknownRegions);
        written.Validate();
    }

    [Fact]
    public void Byte_masks_preserve_untouched_values_and_make_only_explicit_known_runs_readable()
    {
        NativeBattlePrimitiveProcess process = Process.ApplyCommonWrite(10, [1, 2, 3, 4], Known(4));
        NativeBattlePrimitiveProcess rewritten = process.ApplyCommonWrite(10, [11, 22, 33, 44], [NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched]);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, process.ReadCommonPacket(10, 4));
        Assert.Equal(new byte[] { 1 }, rewritten.ReadCommonPacket(10, 1));
        Assert.Throws<NotSupportedException>(() => rewritten.ReadCommonPacket(11, 1));
        Assert.Equal(new byte[] { 33, 4 }, rewritten.ReadCommonPacket(12, 2));
        NativeBattlePrimitiveProcess resolved = rewritten.ApplyCommonWrite(11, [0], Known(1));
        Assert.Equal(new byte[] { 1, 0, 33, 4 }, resolved.ReadCommonPacket(10, 4));
    }

    [Fact]
    public void Rejected_packet_prefix_retains_prior_tail_and_never_advances_the_cursor()
    {
        NativeBattlePrimitiveProcess process = Process.ApplyCommonWrite(0, Enumerable.Range(1, 40).Select(value => (byte)value).ToImmutableArray(), Known(40));
        byte[] prefix = new byte[40];
        prefix[0] = 9;
        prefix[14] = 0x54;
        prefix[15] = 0x3F;
        NativeBattleGfPacketByteState[] masks = new NativeBattleGfPacketByteState[40];
        masks[0] = NativeBattleGfPacketByteState.Known;
        masks[14] = NativeBattleGfPacketByteState.Known;
        masks[15] = NativeBattleGfPacketByteState.Known;
        NativeBattlePrimitiveProcess rejected = process.ApplyCommonWrite(0, prefix.ToImmutableArray(), masks.ToImmutableArray());
        Assert.Equal(0, rejected.ByteCursor);
        Assert.Equal(new byte[] { 9, 2, 3 }, rejected.ReadCommonPacket(0, 3));
        Assert.Equal(new byte[] { 0x54, 0x3F, 17, 18 }, rejected.ReadCommonPacket(14, 4));
        Assert.Equal(new byte[] { 39, 40 }, rejected.ReadCommonPacket(38, 2));
    }

    [Fact]
    public void Both_bank_names_and_both_buffer_indices_have_independent_retained_storage()
    {
        NativeBattlePrimitiveProcess process = Process;
        for (byte buffer = 0; buffer < 2; buffer++)
        {
            process = process with { PacketBank = "off_B6D070", PacketBuffer = buffer };
            process = process.ApplyCommonWrite(0, [(byte)(10 + buffer)], Known(1));
            process = process with { PacketBank = "off_B6D094" };
            process = process.ApplyCommonWrite(0, [(byte)(20 + buffer)], Known(1));
        }
        for (byte buffer = 0; buffer < 2; buffer++)
        {
            Assert.Equal(new byte[] { (byte)(10 + buffer) }, (process with { PacketBank = "off_B6D070", PacketBuffer = buffer }).ReadCommonPacket(0, 1));
            Assert.Equal(new byte[] { (byte)(20 + buffer) }, (process with { PacketBank = "off_B6D094", PacketBuffer = buffer }).ReadCommonPacket(0, 1));
        }
    }

    [Fact]
    public void Initialization_and_both_frame_reset_modes_retain_heap_bytes_and_unknown_ranges()
    {
        NativeBattlePrimitiveProcess process = Process.ApplyCommonWrite(100, [7, 8], Known(2));
        NativeBattleCommonPacketMemory memory = process.CommonPacketMemory!;
        NativeBattlePrimitiveProcess initialized = process.Initialize500400();
        NativeBattlePrimitiveProcess ordinary = initialized.Reset500900(0);
        NativeBattlePrimitiveProcess alternate = ordinary.Reset500900(0x80);
        Assert.Same(memory, initialized.CommonPacketMemory);
        Assert.Same(memory, ordinary.CommonPacketMemory);
        Assert.Same(memory, alternate.CommonPacketMemory);
        Assert.Equal(new byte[] { 7, 8 }, ordinary.ReadCommonPacket(100, 2));
        Assert.Throws<NotSupportedException>(() => alternate.ReadCommonPacket(100, 2));
        Assert.Equal(new byte[] { 7, 8 }, alternate.Reset500900(0).ReadCommonPacket(100, 2));
    }

    [Fact]
    public void Missing_old_owner_and_unobserved_banks_are_strict_while_unobserved_writes_remain_unknown()
    {
        NativeBattlePrimitiveProcess process = JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>("{\"TexturePage\":0,\"Buffer80\":0,\"PacketBank\":\"off_B6D070\",\"PacketBuffer\":0,\"ByteCursor\":0}")!;
        Assert.Null(process.CommonPacketMemory);
        Assert.Throws<NotSupportedException>(() => process.ReadCommonPacket(0, 1));
        NativeBattlePrimitiveProcess unknown = process.MarkCommonUnknown(10, 8);
        NativeBattleEffectArena arena = unknown.CommonPacketMemory!.Bank070Buffer0!;
        Assert.Empty(arena.Regions);
        Assert.Equal(new[] { new NativeBattleEffectUnknownRegion(0, 114688) }, arena.UnknownRegions);
        Assert.Throws<NotSupportedException>(() => unknown.ReadCommonPacket(10, 1));
        Assert.Throws<NotSupportedException>(() => (unknown with { PacketBuffer = 1 }).ReadCommonPacket(10, 1));
    }

    [Fact]
    public void Empty_and_entirely_untouched_writes_do_not_establish_a_memory_owner()
    {
        NativeBattlePrimitiveProcess process = Process;
        Assert.Same(process, process.ApplyCommonWrite(0, [], []));
        Assert.Same(process, process.ApplyCommonWrite(0, [1, 2], [NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched]));
        Assert.Same(process, process.MarkCommonUnknown(114688, 0));
        Assert.Null(process.CommonPacketMemory);
    }

    [Theory]
    [InlineData("off_B6D070", 229376, 0)]
    [InlineData("off_B6D070", 114688, 1)]
    [InlineData("off_B6D094", 294912, 0)]
    [InlineData("off_B6D094", 147456, 1)]
    public void Physical_capacity_is_validated_before_any_mask_run_even_when_excess_bytes_are_untouched(string bank, int capacity, byte buffer)
    {
        NativeBattlePrimitiveProcess process = Process with { PacketBank = bank, PacketBuffer = buffer };
        NativeBattlePrimitiveProcess last = process.ApplyCommonWrite(capacity - 1, [7], Known(1));
        Assert.Equal(new byte[] { 7 }, last.ReadCommonPacket(capacity - 1, 1));
        Assert.Throws<InvalidDataException>(() => process.ApplyCommonWrite(capacity - 1, [7, 8], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched]));
        Assert.Throws<InvalidDataException>(() => process.MarkCommonUnknown(capacity, 1));
        Assert.Throws<InvalidDataException>(() => last.ReadCommonPacket(capacity, 1));
        Assert.Throws<InvalidDataException>(() => process.MarkCommonUnknown(-1, 0));
        Assert.Null(process.CommonPacketMemory);
    }

    [Fact]
    public void Validation_rejects_oversized_sparse_regions_and_oversized_unknown_ranges_in_inactive_banks()
    {
        NativeBattleEffectArena known = new([new(114688, [1])]);
        NativeBattlePrimitiveProcess process = Process with { CommonPacketMemory = new(null, known, null, null) };
        Assert.Throws<InvalidDataException>(process.ValidateCommonPacketMemory);
        Assert.Throws<InvalidDataException>(process.Validate);
        NativeBattleEffectArena unknown = new([]) { UnknownRegions = [new(147456, 1)] };
        Assert.Throws<InvalidDataException>(() => (Process with { CommonPacketMemory = new(null, null, unknown, null) }).ValidateCommonPacketMemory());
    }

    [Fact]
    public void Invalid_masks_and_default_collections_fail_without_establishing_known_bytes()
    {
        NativeBattlePrimitiveProcess process = Process;
        Assert.Throws<InvalidDataException>(() => process.ApplyCommonWrite(0, default, []));
        Assert.Throws<InvalidDataException>(() => process.ApplyCommonWrite(0, [], default));
        Assert.Throws<InvalidDataException>(() => process.ApplyCommonWrite(0, [1], []));
        Assert.Throws<InvalidDataException>(() => process.ApplyCommonWrite(0, [1], [(NativeBattleGfPacketByteState)255]));
        Assert.Null(process.CommonPacketMemory);
        Assert.Throws<NotSupportedException>(() => NativeBattlePrimitiveProcess.FromColdProcess().ApplyCommonWrite(0, [1], Known(1)));
    }

    [Fact]
    public void Common_heap_rejects_implicit_cleared_holes_even_when_the_generic_arena_accepts_them()
    {
        NativeBattlePrimitiveProcess source = Process.ApplyCommonWrite(100, [7, 8], Known(2));
        NativeBattleCommonPacketMemory owner = source.CommonPacketMemory!;
        NativeBattleEffectArena malformed = owner.Bank070Buffer0! with { UnknownRegions = [] };
        malformed.Validate();
        NativeBattlePrimitiveProcess process = source with { CommonPacketMemory = owner with { Bank070Buffer0 = malformed } };
        Assert.Throws<InvalidDataException>(process.ValidateCommonPacketMemory);
        Assert.Throws<InvalidDataException>(() => process.ReadCommonPacket(100, 2));
        NativeBattlePrimitiveProcess cleared = Process with { CommonPacketMemory = new(NativeBattleEffectArena.Clear(), null, null, null) };
        Assert.Throws<InvalidDataException>(cleared.ValidateCommonPacketMemory);
    }

    [Fact]
    public void Fully_explicit_known_capacity_and_overlapping_unknown_regions_are_valid()
    {
        NativeBattleEffectArena full = new([new(0, new byte[114688].ToImmutableArray())]);
        NativeBattlePrimitiveProcess process = Process with { CommonPacketMemory = new(full, null, null, null) };
        process.ValidateCommonPacketMemory();
        Assert.Equal(new byte[] { 0, 0 }, process.ReadCommonPacket(114686, 2));
        process = process.MarkCommonUnknown(100, 8);
        process.ValidateCommonPacketMemory();
        Assert.Throws<NotSupportedException>(() => process.ReadCommonPacket(100, 1));
        Assert.Equal(new byte[] { 0 }, process.ReadCommonPacket(108, 1));
    }

    [Fact]
    public void JSON_round_trip_preserves_four_bank_identity_known_zeros_and_unknown_link_bytes()
    {
        NativeBattlePrimitiveProcess source = Process.ApplyCommonWrite(0, [0, 1, 2, 9], Known(4)).MarkCommonUnknown(0, 3);
        source = source with { PacketBank = "off_B6D094", PacketBuffer = 1 };
        source = source.ApplyCommonWrite(100, [0, 6], Known(2));
        NativeBattlePrimitiveProcess restored = JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>(JsonSerializer.Serialize(source))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(restored));
        Assert.Equal(new byte[] { 0, 6 }, restored.ReadCommonPacket(100, 2));
        restored = restored with { PacketBank = "off_B6D070", PacketBuffer = 0 };
        Assert.Throws<NotSupportedException>(() => restored.ReadCommonPacket(0, 1));
        Assert.Equal(new byte[] { 9 }, restored.ReadCommonPacket(3, 1));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleCommonPacketMemory>("{\"Bank070Buffer0\":null}"));
    }

    private static ImmutableArray<NativeBattleGfPacketByteState> Known(int length)
    {
        return Enumerable.Repeat(NativeBattleGfPacketByteState.Known, length).ToImmutableArray();
    }
}
