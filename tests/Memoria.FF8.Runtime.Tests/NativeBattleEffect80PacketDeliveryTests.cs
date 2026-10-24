using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect80PacketDeliveryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Body_uv_padding_retains_physical_knownness_and_known_high_words(bool quad, bool knownPadding)
    {
        NativeBattleGfGeometryAttempt packet = Packet(quad ? (byte)0x2E : (byte)0x26);
        int[] padding = quad ? [30, 31, 38, 39] : [30, 31];
        ImmutableArray<NativeBattleGfPacketByteState> mask = packet.WriteMask;
        NativeBattlePrimitiveProcess process = Process(packet.PacketLength);
        foreach (int offset in padding)
        {
            mask = mask.SetItem(offset, NativeBattleGfPacketByteState.Untouched);
            if (knownPadding)
            {
                process = process.ApplyCommonWrite(16 + offset, [(byte)(offset % 2 == 0 ? 0xA5 : 0xB6)], [NativeBattleGfPacketByteState.Known]);
            }
        }
        NativeBattleCommonPacketWrite result = NativeBattleCommonPacketDelivery.Deliver(process, NativeBattleLogicalOrdering.Clear(), new([packet with { WriteMask = mask }], packet.PacketLength));
        Assert.Equal(16 + packet.PacketLength, result.Process.ByteCursor);
        foreach (int offset in padding)
        {
            if (knownPadding)
            {
                Assert.Equal((byte)(offset % 2 == 0 ? 0xA5 : 0xB6), result.Process.ReadCommonPacket(16 + offset, 1)[0]);
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => result.Process.ReadCommonPacket(16 + offset, 1));
            }
        }
        NativeBattlePrimitiveProcess restored = JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>(JsonSerializer.Serialize(result.Process))!;
        Assert.Equal(result.Process.ByteCursor, restored.ByteCursor);
        if (knownPadding)
        {
            Assert.Equal(0xB6A5u, Assert.Single(result.Ordering.Packets).Words[7] >> 16);
            if (quad)
            {
                Assert.Equal(0xB6A5u, Assert.Single(result.Ordering.Packets).Words[9] >> 16);
            }
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => restored.ReadCommonPacket(46, 2));
        }
        Assert.Equal(new[] { 111, 222, 333, 444 }, Assert.Single(result.Ordering.Proxies).VertexPayload4.ToArray());
        Assert.Equal(new byte[] { 0x11, 0x22 }, result.Process.ReadCommonPacket(44, 2));
    }

    [Fact]
    public void Trail_late_color_code_unknowns_stay_physical_unknown_without_becoming_gpu_inputs()
    {
        NativeBattleGfGeometryAttempt packet = Packet(0x3E);
        ImmutableArray<NativeBattleGfPacketByteState> mask = packet.WriteMask;
        foreach (int offset in new[] { 19, 31, 43 })
        {
            mask = mask.SetItem(offset, NativeBattleGfPacketByteState.Unknown);
        }
        foreach (int offset in new[] { 38, 39, 50, 51 })
        {
            mask = mask.SetItem(offset, NativeBattleGfPacketByteState.Untouched);
        }
        NativeBattleCommonPacketWrite result = NativeBattleCommonPacketDelivery.Deliver(Process(52), NativeBattleLogicalOrdering.Clear(), new([packet with { WriteMask = mask }], 52));
        foreach (int offset in new[] { 19, 31, 43, 38, 39, 50, 51 })
        {
            Assert.Throws<NotSupportedException>(() => result.Process.ReadCommonPacket(16 + offset, 1));
        }
        Assert.Equal(0x3Eu, Assert.Single(result.Ordering.Packets).Words[1] >> 24);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, result.Process.ReadCommonPacket(32, 3));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(28)]
    public void Consumed_xy_or_low_uv_unknown_still_fails(int offset)
    {
        NativeBattleGfGeometryAttempt packet = Packet(0x26);
        packet = packet with { WriteMask = packet.WriteMask.SetItem(offset, NativeBattleGfPacketByteState.Unknown) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCommonPacketDelivery.Deliver(Process(32), NativeBattleLogicalOrdering.Clear(), new([packet], 32)));
    }

    private static NativeBattlePrimitiveProcess Process(int length) => (NativeBattlePrimitiveProcess.FromColdProcess() with { PacketBank = "off_B6D070", PacketBuffer = 0, ByteCursor = 16 }).MarkCommonUnknown(16, length);
    private static NativeBattleGfGeometryAttempt Packet(byte code)
    {
        int length = code == 0x3E ? 52 : code == 0x2E ? 40 : 32;
        byte[] bytes = new byte[length];
        for (int offset = 0; offset < length; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 0x44332211);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)(length / 4 - 1) << 24);
        bytes[7] = code;
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        return new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, length).ToImmutableArray(), new(17, words, [111, 222, 333, 444]));
    }
}
