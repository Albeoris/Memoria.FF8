using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResonance49PacketDeliveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Screen_pair_links_mode_first_but_retains_quad_partial_bytes_and_advances_fifty_two(bool retained)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 16 };
        if (retained)
        {
            process = process.ApplyCommonWrite(16, Enumerable.Repeat((byte)0xA5, 52).ToImmutableArray(), Known(52));
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = process };
        NativeBattleResonance49State state = NativeBattleResonance49State.FromColdProcess();
        NativeBattleGfGeometryAttempt quad = ScreenQuad();
        NativeBattleGfGeometryAttempt mode = Packet(12, 0x02000000, 0xE1000020, 2);
        NativeBattleResonance49PacketStep result = NativeBattleResonance49PacketDelivery.ScreenPair(memory, state, NativeBattleLogicalOrdering.Clear(), quad, mode);
        NativeBattlePrimitiveProcess output = result.Memory.PrimitiveProcess!;
        Assert.Equal(68, output.ByteCursor);
        Assert.Same(memory.Arena, result.Memory.Arena);
        Assert.Same(state, result.State);
        Assert.Equal(new[] { 56, 16 }, result.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.Equal(new ushort[] { 0, 1 }, result.Ordering.Proxies.Select(proxy => proxy.Flags20));
        Assert.Empty(result.Ordering.Proxies[1].VertexPayload4);
        Assert.Equal(0xE1000020u, U32(output.ReadCommonPacket(60, 4)));
        Assert.Equal((byte)0x2D, output.ReadCommonPacket(23, 1)[0]);
        if (retained)
        {
            Assert.Equal(new byte[] { 0xA5 }, output.ReadCommonPacket(20, 1));
            Assert.Equal(new byte[] { 0xA5 }, output.ReadCommonPacket(30, 1));
            Assert.Equal(0xA5u, result.Ordering.Packets[1].Words[1] & 0xFF);
            Assert.Equal(0xA5u, (result.Ordering.Packets[1].Words[3] >> 16) & 0xFF);
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => output.ReadCommonPacket(20, 1));
            Assert.Throws<NotSupportedException>(() => output.ReadCommonPacket(30, 1));
        }
        NativeBattleResonance49PacketStep restored = JsonSerializer.Deserialize<NativeBattleResonance49PacketStep>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restored));
        Assert.Equal(68, restored.Memory.PrimitiveProcess!.ByteCursor);
    }

    [Fact]
    public void Static_packets_use_four_exact_state_owners_and_preserve_unwritten_suffix()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleResonance49State state = NativeBattleResonance49State.FromUnknown();
        foreach (uint address in new uint[] { 0x26472D8, 0x2647318, 0x2647820, 0x2647860 })
        {
            NativeBattleResonance49PacketStep result = NativeBattleResonance49PacketDelivery.Store(memory, state, NativeBattleLogicalOrdering.Clear(), new([StaticPacket()], 64), address, 0);
            Assert.Same(memory, result.Memory);
            Assert.Equal(0xE1000020u, result.State.ReadDword(address + 4));
            Assert.Throws<NotSupportedException>(() => result.State.ReadDword(address + 8));
            NativeBattlePrimitiveReference reference = new(NativeBattleResonance49PacketDelivery.PacketBank, 0, checked((int)(address - 0x26464D8)));
            Assert.Equal(result.State.Read(address, 8), NativeBattleResonance49PacketDelivery.ReadStatic(result.State, reference, 8));
            Assert.Throws<InvalidDataException>(() => NativeBattleResonance49PacketDelivery.ReadStatic(result.State, reference with { Buffer = 1 }, 4));
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleResonance49PacketDelivery.Store(memory, state, NativeBattleLogicalOrdering.Clear(), new([StaticPacket()], 64), 0x26472DC, 0));
    }

    [Theory]
    [InlineData(0x26464DCu)]
    [InlineData(0x26465DCu)]
    public void Programme_cursor_updates_only_its_TIM_arena_owner(uint cursor)
    {
        const uint start = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0, new byte[64]), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() };
        NativeBattleResonance49State state = NativeBattleResonance49State.FromColdProcess().WriteDword(cursor, start + 16);
        NativeBattleResonance49PacketStep result = NativeBattleResonance49PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), new([Packet(8, 0x01000000, 0xE1000020, 0)], 8), cursor, 0);
        Assert.Equal(start + 24, result.State.ReadDword(cursor));
        Assert.Same(memory.PrimitiveProcess, result.Memory.PrimitiveProcess);
        Assert.Equal(0xE1000020u, U32(result.Memory.Arena!.Read(20, 4)));
        Assert.Equal("20DFAB8", Assert.Single(result.Ordering.Packets).Reference.Bank);
        Assert.Throws<InvalidDataException>(() => NativeBattleResonance49PacketDelivery.Deliver(memory, state, NativeBattleLogicalOrdering.Clear(), new([], 0), cursor + 1, 0));
    }

    [Fact]
    public void Common_emission_rejects_missing_cursor_and_overflow_without_mutating_source()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleResonance49State state = NativeBattleResonance49State.FromColdProcess();
        NativeBattleGfGeometryAttempt quad = Packet(40, 0x09000000, 0x2D000000, 0);
        NativeBattleGfGeometryAttempt mode = Packet(12, 0x02000000, 0xE1000020, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleResonance49PacketDelivery.ScreenPair(memory, state, NativeBattleLogicalOrdering.Clear(), quad, mode));
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBuffer = 1, ByteCursor = 114688 - 48 };
        Assert.Throws<InvalidDataException>(() => NativeBattleResonance49PacketDelivery.ScreenPair(memory with { PrimitiveProcess = process }, state, NativeBattleLogicalOrdering.Clear(), quad, mode));
        Assert.Null(process.CommonPacketMemory);
    }

    [Theory]
    [InlineData(8, 0x0119, false)]
    [InlineData(4, 0x0019, false)]
    [InlineData(8, 0x0119, true)]
    public void Unknown_consumed_coordinates_or_palette_texture_modulation_are_not_normalized(int unknownOffset, ushort page, bool writtenUnknown)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() };
        NativeBattleGfGeometryAttempt quad = ScreenQuad(page);
        quad = quad with { WriteMask = quad.WriteMask.SetItem(unknownOffset, writtenUnknown ? NativeBattleGfPacketByteState.Unknown : NativeBattleGfPacketByteState.Untouched) };
        Assert.Throws<NotSupportedException>(() => NativeBattleResonance49PacketDelivery.ScreenPair(memory, NativeBattleResonance49State.FromColdProcess(), NativeBattleLogicalOrdering.Clear(), quad, Packet(12, 0x02000000, 0xE1000020, 0)));
    }

    private static NativeBattleGfGeometryAttempt ScreenQuad(ushort page = 0x0119)
    {
        NativeBattleGfGeometryAttempt packet = Packet(40, 0x09000000, 0x2D000000, 1);
        byte[] data = packet.Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(22), page);
        ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
        return packet with { Data = data.ToImmutableArray(), WriteMask = Known(40).SetItem(4, NativeBattleGfPacketByteState.Untouched).SetItem(14, NativeBattleGfPacketByteState.Untouched), Primitive = new(17, words, []) };
    }

    private static NativeBattleGfGeometryAttempt StaticPacket()
    {
        NativeBattleGfGeometryAttempt packet = Packet(64, 0x01000000, 0xE1000020, 0);
        return packet with { WriteMask = Known(8).AddRange(Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 56)), Primitive = new(17, [0x01000000, 0xE1000020], [0, 0, 0, 0]) };
    }

    private static NativeBattleGfGeometryAttempt Packet(int length, uint header, uint command, byte fill)
    {
        byte[] bytes = Enumerable.Repeat(fill, length).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, header);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), command);
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        return new(0, bytes.ToImmutableArray(), Known(length), new(17, words, [0, 0, 0, 0]));
    }

    private static ImmutableArray<NativeBattleGfPacketByteState> Known(int count) => Enumerable.Repeat(NativeBattleGfPacketByteState.Known, count).ToImmutableArray();
    private static uint U32(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
}
