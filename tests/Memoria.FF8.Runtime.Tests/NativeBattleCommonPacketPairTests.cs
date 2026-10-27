using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonPacketPairTests
{
    [Fact]
    public void Cold_process_JSON_does_not_evaluate_or_persist_derived_packet_capacities()
    {
        NativeBattlePrimitiveProcess cold = NativeBattlePrimitiveProcess.FromColdProcess();
        Assert.Null(cold.PacketBank);
        string json = NativeBattleTestSnapshots.Json(cold);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(NativeBattlePrimitiveProcess.CommonPacketNominalCapacity), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(NativeBattlePrimitiveProcess.CommonPacketWriteCapacity), out _));
        NativeBattlePrimitiveProcess restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(json)!;
        Assert.Null(restored.PacketBank);
        Assert.Null(restored.ByteCursor);
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
    }

    [Theory]
    [InlineData("off_B6D070", 114688)]
    [InlineData("off_B6D094", 147456)]
    public void Cross_slice_write_has_one_physical_alias_and_keeps_original_slice_sizes(string bank, int stride)
    {
        NativeBattlePrimitiveProcess source = Process(bank);
        NativeBattlePrimitiveProcess result = source.ApplyCommonWrite(stride - 2, [1, 2, 3, 4], Known(4));
        Assert.Equal(stride, result.CommonPacketNominalCapacity);
        Assert.Equal(stride * 2, result.CommonPacketWriteCapacity);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.ReadCommonPacket(stride - 2, 4));
        Assert.Equal(new byte[] { 3, 4 }, (result with { PacketBuffer = 1 }).ReadCommonPacket(0, 2));
        Assert.Equal(stride, (result with { PacketBuffer = 1 }).CommonPacketWriteCapacity);
        Assert.Throws<NotSupportedException>(() => result.ReadCommonPacket(stride - 3, 1));
        Assert.Throws<NotSupportedException>(() => result.ReadCommonPacket(stride + 2, 1));
        Assert.Null(source.CommonPacketMemory);
        result.ValidateCommonPacketMemory();
        NativeBattleCommonPacketMemory owners = result.CommonPacketMemory!;
        Assert.Null(bank == "off_B6D070" ? owners.Bank094Buffer0 : owners.Bank070Buffer0);
        Assert.Null(bank == "off_B6D070" ? owners.Bank094Buffer1 : owners.Bank070Buffer1);
    }

    [Theory]
    [InlineData("off_B6D070", 114688)]
    [InlineData("off_B6D094", 147456)]
    public void Cross_slice_masks_and_ordered_batch_writes_preserve_unknown_and_untouched_bytes(string bank, int stride)
    {
        NativeBattlePrimitiveProcess source = Process(bank).ApplyCommonWrite(stride - 2, [1, 2, 3, 4], Known(4));
        ImmutableArray<NativeBattleCommonPacketStore> writes =
        [
            new(stride - 2, [10, 20, 30, 40], [NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched]),
            new(stride - 1, [50, 60], Known(2)),
            new(stride + 1, [0], [NativeBattleGfPacketByteState.Unknown])
        ];
        NativeBattlePrimitiveProcess preserved = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source));
        NativeBattlePrimitiveProcess result = source.ApplyCommonWrites(writes);
        Assert.Equal(new byte[] { 1, 50, 60 }, result.ReadCommonPacket(stride - 2, 3));
        Assert.Throws<NotSupportedException>(() => result.ReadCommonPacket(stride + 1, 1));
        Assert.Throws<NotSupportedException>(() => (result with { PacketBuffer = 1 }).ReadCommonPacket(1, 1));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, preserved.ReadCommonPacket(stride - 2, 4));
        NativeBattlePrimitiveProcess sequential = preserved;
        foreach (NativeBattleCommonPacketStore write in writes)
        {
            sequential = sequential.ApplyCommonWrite(write.Offset, write.Data, write.Mask);
        }
        Assert.Equal(NativeBattleTestSnapshots.Json(sequential.CommonPacketMemory), NativeBattleTestSnapshots.Json(result.CommonPacketMemory));
        NativeBattlePrimitiveProcess unknown = result.MarkCommonUnknown(stride - 1, 2);
        Assert.Throws<NotSupportedException>(() => unknown.ReadCommonPacket(stride - 1, 1));
        Assert.Throws<NotSupportedException>(() => (unknown with { PacketBuffer = 1 }).ReadCommonPacket(0, 1));
    }

    [Theory]
    [InlineData("off_B6D070", 114688, 0)]
    [InlineData("off_B6D070", 114688, 1)]
    [InlineData("off_B6D094", 147456, 0)]
    [InlineData("off_B6D094", 147456, 1)]
    public void Whole_batch_is_validated_before_either_original_slice_is_written(string bank, int stride, byte buffer)
    {
        NativeBattlePrimitiveProcess source = Process(bank) with { PacketBuffer = buffer };
        int remaining = (2 - buffer) * stride;
        Assert.Throws<InvalidDataException>(() => source.ApplyCommonWrites([new(0, [1], Known(1)), new(remaining - 1, [2, 3], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched])]));
        Assert.Throws<InvalidDataException>(() => source.MarkCommonUnknown(remaining, 1));
        Assert.Throws<InvalidDataException>(() => source.ReadCommonPacket(remaining, 1));
        Assert.Null(source.CommonPacketMemory);
        NativeBattlePrimitiveProcess tail = source.ApplyCommonWrite(remaining - 1, [7], Known(1));
        Assert.Equal(new byte[] { 7 }, tail.ReadCommonPacket(remaining - 1, 1));
    }

    [Fact]
    public void Untouched_second_slice_is_not_materialized_and_reset_one_observes_cross_write_after_JSON()
    {
        NativeBattlePrimitiveProcess source = Process("off_B6D070");
        NativeBattlePrimitiveProcess one = source.ApplyCommonWrite(114687, [7, 8], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched]);
        Assert.Null(one.CommonPacketMemory!.Bank070Buffer1);
        Assert.Throws<NotSupportedException>(() => one.ReadCommonPacket(114688, 1));
        NativeBattlePrimitiveProcess written = one.ApplyCommonWrite(114688, [9, 10], Known(2));
        NativeBattlePrimitiveProcess restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(NativeBattleTestSnapshots.Json(written))!;
        NativeBattlePrimitiveProcess reset = (restored with { Buffer80 = 1 }).Reset500900(0);
        Assert.Equal((byte)1, reset.PacketBuffer);
        Assert.Equal(new byte[] { 9, 10 }, reset.ReadCommonPacket(0, 2));
        reset = reset.ApplyCommonWrite(1, [11], Known(1));
        Assert.Equal(new byte[] { 9, 11 }, (reset with { PacketBuffer = 0 }).ReadCommonPacket(114688, 2));
        Assert.Equal(new byte[] { 9, 10 }, written.ReadCommonPacket(114688, 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void COMMON_alias_overwrite_invalidates_prior_OT_reference_and_physical_JSON_traversal(bool reverse)
    {
        NativeBattlePrimitiveReference first = reverse ? new("off_B6D070", 1, 16) : new("off_B6D070", 0, 114704);
        NativeBattlePrimitiveReference alias = reverse ? new("off_B6D070", 0, 114704) : new("off_B6D070", 1, 16);
        NativeBattleLogicalOrdering.Builder links = new(NativeBattleLogicalOrdering.Clear());
        ImmutableArray<uint> words = [0x03000000, 0x60000001, 0x00010002, 0x00030004];
        links.Prepend(17, first, words, 1, []);
        byte[] bytes = new byte[16];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), index == 0 ? words[0] | links.LastHeaderLink & 0xFFFFFF : words[index]);
        }
        NativeBattlePrimitiveProcess process = (Process(first.Bank) with { PacketBuffer = first.Buffer }).ApplyCommonWrite(first.ByteOffset, bytes.ToImmutableArray(), Known(16));
        byte[] color = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(color, 0x60000055);
        process = (process with { PacketBuffer = alias.Buffer }).ApplyCommonWrite(alias.ByteOffset + 4, color.ToImmutableArray(), Known(4));
        links.InvalidatePacketRange(alias with { ByteOffset = alias.ByteOffset + 4 }, 4);
        NativeBattleLogicalOrdering ordering = links.ToImmutable();
        links.Dispose();
        Assert.True(Assert.Single(ordering.Packets).RequiresPhysicalRead);
        ordering = NativeBattleTestSnapshots.RestoreJson<NativeBattleLogicalOrdering>(NativeBattleTestSnapshots.Json(ordering))!;
        process = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(NativeBattleTestSnapshots.Json(process))!;
        byte[] lengths = new byte[256];
        lengths[0x60] = 3;
        NativeBattleLogicalCommand command = Assert.Single(ordering.Traverse(lengths.ToImmutableArray(), (reference, count) => (process with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count)));
        Assert.Equal(0x60000055u, command.Words[0]);
        NativeBattleLogicalOrdering aliasPacketIdentity = NativeBattleOrderingTestFixture.Frozen(ordering, data => data with { Packets = data.Packets.SetItem(0, data.Packets[0] with { Reference = alias }) });
        Assert.Single(aliasPacketIdentity.Traverse(lengths.ToImmutableArray(), (reference, count) => (process with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count)));
    }

    [Fact]
    public void Other_packet_owners_remain_distinct_and_nominal_remaining_uses_unsigned_underflow()
    {
        NativeBattlePrimitiveReference privateReference = new("20DFAB8", 1, 114704);
        Assert.Same(privateReference, NativeBattlePrimitiveProcess.NormalizeCommonReference(privateReference));
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(17, privateReference, [0x01000000, 0xE1000000], 1, []);
        NativeBattleLogicalOrdering.Builder builder = new(ordering);
        builder.InvalidatePacketRange(new("20DFAB8", 0, 114704), 8);
        Assert.False(Assert.Single(builder.ToImmutable().Packets).RequiresPhysicalRead);
        Assert.Equal(uint.MaxValue, (Process("off_B6D070") with { ByteCursor = 114689 }).NativeRemaining500820());
    }

    private static NativeBattlePrimitiveProcess Process(string bank) => NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = bank };
    private static ImmutableArray<NativeBattleGfPacketByteState> Known(int count) => Enumerable.Repeat(NativeBattleGfPacketByteState.Known, count).ToImmutableArray();
}
