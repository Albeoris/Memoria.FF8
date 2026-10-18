using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonPacketWriterBoundsTests
{
    [Theory]
    [InlineData("off_B6D070", 0x1C000)]
    [InlineData("off_B6D094", 0x24000)]
    public void Accepted_common_packet_can_span_existing_slices_and_restore_the_same_physical_alias(string bank, int stride)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = bank, ByteCursor = stride - 4 };
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), Batch());
        Assert.Equal(stride + 4, written.Process.ByteCursor);
        written.Process.Validate();
        byte[] actual = written.Process.ReadCommonPacket(stride - 4, 8);
        Assert.Equal(0xE1000220u, BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(4)));
        Assert.Equal(actual.AsSpan(4).ToArray(), (written.Process with { PacketBuffer = 1, ByteCursor = 0 }).ReadCommonPacket(0, 4));
        NativeBattlePrimitiveProcess cold = JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>(JsonSerializer.Serialize(written.Process))!;
        Assert.Equal(actual, cold.ReadCommonPacket(stride - 4, 8));
        Assert.Null(process.CommonPacketMemory);
    }

    [Theory]
    [InlineData("off_B6D070", 0)]
    [InlineData("off_B6D070", 1)]
    [InlineData("off_B6D094", 0)]
    [InlineData("off_B6D094", 1)]
    public void Packet_append_still_rejects_the_end_of_the_existing_physical_pair_atomically(string bank, byte buffer)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = bank, PacketBuffer = buffer };
        process = process with { ByteCursor = process.CommonPacketWriteCapacity - 4 };
        Assert.Throws<InvalidDataException>(() => NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), Batch()));
        Assert.Null(process.CommonPacketMemory);
    }

    [Theory]
    [InlineData(-244, 244u)]
    [InlineData(-240, 240u)]
    [InlineData(0, 0u)]
    [InlineData(4, 0xFFFFFFFCu)]
    public void Native_remaining_preserves_the_nominal_end_and_unsigned_underflow(int offsetFromNominal, uint expected)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        process = process with { ByteCursor = process.CommonPacketNominalCapacity + offsetFromNominal };
        process.Validate();
        Assert.Equal(expected, process.NativeRemaining500820());
    }

    private static NativeBattleGfGeometryBatch Batch()
    {
        NativeBattleGeometryPrimitive primitive = new(17, [0x01000000, 0xE1000220], []);
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, primitive.Words[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), primitive.Words[1]);
        return new([new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), primitive)], 8);
    }
}
