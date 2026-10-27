using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonPacketWriterBoundsTests
{
    [Theory]
    [InlineData(52, 88)]
    [InlineData(52, 104)]
    [InlineData(8, 12)]
    public void Native_allocation_may_reserve_untouched_bytes_after_its_complete_logical_command(int commandBytes, int allocatedBytes)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        int start = process.ByteCursor!.Value;
        byte[] original = Enumerable.Repeat((byte)0xA7, allocatedBytes).ToArray();
        process = process.ApplyCommonWrites([new(start, original.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, allocatedBytes).ToImmutableArray())]);
        NativeBattlePrimitiveProcess preserved = NativeBattleStorageSnapshots.Capture(process);
        byte[] data = new byte[allocatedBytes];
        data[3] = checked((byte)(commandBytes / 4 - 1));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), commandBytes == 8 ? 0xE1000220u : 0x3C808080u);
        ImmutableArray<uint> words = Enumerable.Range(0, commandBytes / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocatedBytes];
        Array.Fill(mask, NativeBattleGfPacketByteState.Known, 0, commandBytes);
        NativeBattleGfGeometryBatch batch = new([new(0, data.ToImmutableArray(), mask.ToImmutableArray(), new(17, words, []))], allocatedBytes);
        NativeBattleLogicalOrdering beforeOrdering = Ordering(process.PacketBank!, process.PacketBuffer);
        NativeBattleCommonPacketWrite result = NativeBattleCommonPacketArena.Append(process, beforeOrdering, batch);
        Assert.Equal(start + allocatedBytes, result.Process.ByteCursor);
        Assert.Equal(original.AsSpan(commandBytes).ToArray(), result.Process.ReadCommonPacket(start + commandBytes, allocatedBytes - commandBytes));
        Assert.Equal(words.Length, Assert.Single(result.Ordering.Packets, packet => packet.Reference.ByteOffset == start).Words.Length);
        if (allocatedBytes - commandBytes >= 8)
        {
            Assert.False(Assert.Single(result.Ordering.Packets, packet => packet.Reference.ByteOffset == start + commandBytes).RequiresPhysicalRead);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(start, original) };
        NativeBattleEffectPacketWrite effect = NativeBattleEffectPacketArena.Append(memory, start, Ordering("20DFAB8", 0), batch);
        Assert.Equal(start + allocatedBytes, effect.Cursor);
        Assert.Equal(original.AsSpan(commandBytes).ToArray(), effect.Memory.Arena!.Read(start + commandBytes, allocatedBytes - commandBytes));
        if (allocatedBytes - commandBytes >= 8)
        {
            Assert.False(Assert.Single(effect.Ordering.Packets, packet => packet.Reference.ByteOffset == start + commandBytes).RequiresPhysicalRead);
        }
        NativeBattlePrimitiveProcess restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(NativeBattleTestSnapshots.Json(result.Process))!;
        Assert.Equal(result.Process.ReadCommonPacket(start, allocatedBytes), restored.ReadCommonPacket(start, allocatedBytes));
        Assert.Equal(original, preserved.ReadCommonPacket(start, allocatedBytes));
        NativeBattleGfGeometryBatch truncated = batch with { Attempts = [batch.Attempts[0] with { Primitive = new(17, words.RemoveAt(words.Length - 1), []) }] };
        Assert.Throws<InvalidDataException>(() => NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), truncated));

        NativeBattleLogicalOrdering Ordering(string bank, byte buffer)
        {
            NativeBattleLogicalOrdering.Builder ordering = new(NativeBattleLogicalOrdering.Clear());
            if (allocatedBytes - commandBytes >= 8)
            {
                ordering.Prepend(17, new(bank, buffer, start + commandBytes), [0x01000000, 0xE1000240], 0, []);
            }
            return ordering.ToImmutable();
        }
    }

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
        NativeBattlePrimitiveProcess cold = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(NativeBattleTestSnapshots.Json(written.Process))!;
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
