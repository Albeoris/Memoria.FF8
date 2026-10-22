using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeltEye158PacketArenaTests
{
    private const uint Base = NativeBattleMeltEye158PacketArena.PacketAddress;
    private const uint Cursor = NativeBattleMeltEye158PacketArena.CursorAddress;

    [Fact]
    public void Alternating_physical_banks_retain_their_actual_packets_and_one_global_cursor()
    {
        NativeBattleMeltEye158State state = NativeBattleMeltEye158State.FromColdProcess().WriteDword(Cursor, Base);
        NativeBattleMeltEye158PacketWrite first = NativeBattleMeltEye158PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220));
        NativeBattleMeltEye158PacketWrite second = NativeBattleMeltEye158PacketArena.Append(first.State.WriteDword(Cursor, Base + 0x8000), first.Ordering, Accepted(0xE1000221));
        Assert.Equal(Base + 0x8008, second.State.ReadDword(Cursor));
        Assert.Equal(new[] { 0, 0x8000 }, second.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.All(second.Ordering.Packets, packet =>
        {
            Assert.Equal("23DB5BC", packet.Reference.Bank);
            Assert.Equal(0, packet.Reference.Buffer);
        });
        Assert.Equal(0xE1000220u, U32(second.State.ReadPacket(Base + 4, 4)));
        Assert.Equal(0xE1000221u, U32(second.State.ReadPacket(Base + 0x8004, 4)));
    }

    [Fact]
    public void Rejected_prefix_applies_known_unknown_and_untouched_stores_without_allocating()
    {
        NativeBattleMeltEye158State state = NativeBattleMeltEye158State.FromColdProcess().WriteDword(Cursor, Base).WritePacket(Base, Enumerable.Repeat((byte)0xA5, 8).ToArray());
        NativeBattleGfGeometryAttempt rejected = new(0, ImmutableArray.Create<byte>(0, 0, 0, 7, 0, 0, 0, 0), [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched], null);
        NativeBattleMeltEye158PacketWrite result = NativeBattleMeltEye158PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), new([rejected], 0));
        Assert.Equal(Base, result.State.ReadDword(Cursor));
        Assert.Equal(0x07000000u, U32(result.State.ReadPacket(Base, 4)));
        Assert.Throws<NotSupportedException>(() => result.State.ReadPacket(Base + 4, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.State.ReadPacket(Base + 6, 2));
        Assert.Empty(result.Ordering.Packets);
    }

    [Fact]
    public void Warm_unknown_bank_and_physical_links_continue_after_independent_JSON_restore()
    {
        NativeBattleMeltEye158State state = NativeBattleMeltEye158State.FromUnknown().WriteDword(Cursor, Base + 0x8000);
        NativeBattleMeltEye158PacketWrite first = NativeBattleMeltEye158PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220));
        NativeBattleMeltEye158PacketWrite restored = JsonSerializer.Deserialize<NativeBattleMeltEye158PacketWrite>(JsonSerializer.Serialize(first))!;
        Assert.Throws<NotSupportedException>(() => restored.State.ReadPacket(Base, 4));
        NativeBattleMeltEye158PacketWrite Next(NativeBattleMeltEye158PacketWrite current) => NativeBattleMeltEye158PacketArena.Append(current.State, current.Ordering, Accepted(0xE1000221));
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
        byte[] lengths = new byte[256];
        lengths[0xE1] = 1;
        NativeBattleMeltEye158PacketWrite next = Next(restored);
        NativeBattleLogicalCommand[] commands = next.Ordering.Traverse(lengths.ToImmutableArray(), (reference, count) => NativeBattleMeltEye158PacketArena.Read(next.State, reference, count)).ToArray();
        Assert.Equal(new uint[] { 0xE1000221, 0xE1000220 }, commands.Select(command => command.Words[0]));
    }

    [Fact]
    public void Physical_span_bounds_cover_both_banks_and_rejected_prefix_stores()
    {
        NativeBattleMeltEye158State state = NativeBattleMeltEye158State.FromColdProcess().WriteDword(Cursor, Base + 0xFFFC);
        Assert.Throws<InvalidDataException>(() => NativeBattleMeltEye158PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
        NativeBattleGfGeometryAttempt rejected = Accepted(0xE1000220).Attempts[0] with { Primitive = null };
        Assert.Throws<InvalidDataException>(() => NativeBattleMeltEye158PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), new([rejected], 0)));
        Assert.Throws<InvalidDataException>(() => NativeBattleMeltEye158PacketArena.Read(state, new("23DB5BC", 1, 0), 4));
        Assert.Throws<InvalidDataException>(() => NativeBattleMeltEye158PacketArena.Read(state, new("23DB5BC", 0, 0xFFFF), 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleMeltEye158PacketArena.Append(state.WriteDword(Cursor, Base + 1), NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
        Assert.Throws<NotSupportedException>(() => NativeBattleMeltEye158PacketArena.Append(NativeBattleMeltEye158State.FromUnknown(), NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
        NativeBattleMeltEye158PacketWrite crossed = NativeBattleMeltEye158PacketArena.Append(state.WriteDword(Cursor, Base + 0x7FFC), NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220));
        Assert.Equal(Base + 0x8004, crossed.State.ReadDword(Cursor));
    }

    private static NativeBattleGfGeometryBatch Accepted(uint command)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), command);
        NativeBattleGeometryPrimitive primitive = new(17, [0x01000000, command], [0, 0, 0, 0]);
        return new([new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), primitive)], 8);
    }

    private static uint U32(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
}
