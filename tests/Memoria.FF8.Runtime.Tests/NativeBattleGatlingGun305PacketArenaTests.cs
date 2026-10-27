using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGatlingGun305PacketArenaTests
{
    private const uint Base = NativeBattleGatlingGun305PacketArena.PacketAddress;
    private const uint Cursor = NativeBattleGatlingGun305PacketArena.CursorAddress;

    [Fact]
    public void Alternating_physical_banks_retain_their_actual_packets_and_one_global_cursor()
    {
        NativeBattleGatlingGun305State state = NativeBattleGatlingGun305State.FromColdProcess().WriteDword(Cursor, Base);
        NativeBattleGatlingGun305PacketWrite first = NativeBattleGatlingGun305PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220));
        NativeBattleGatlingGun305PacketWrite second = NativeBattleGatlingGun305PacketArena.Append(first.State.WriteDword(Cursor, Base + 0x8000), first.Ordering, Accepted(0xE1000221));
        Assert.Equal(Base + 0x8008, second.State.ReadDword(Cursor));
        Assert.Equal(new[] { 0, 0x8000 }, second.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.All(second.Ordering.Packets, packet =>
        {
            Assert.Equal("2247790", packet.Reference.Bank);
            Assert.Equal(0, packet.Reference.Buffer);
        });
        Assert.Equal(0xE1000220u, U32(second.State.ReadPacket(Base + 4, 4)));
        Assert.Equal(0xE1000221u, U32(second.State.ReadPacket(Base + 0x8004, 4)));
    }

    [Fact]
    public void Rejected_prefix_applies_known_unknown_and_untouched_stores_without_allocating()
    {
        NativeBattleGatlingGun305State state = NativeBattleGatlingGun305State.FromColdProcess().WriteDword(Cursor, Base).WritePacket(Base, Enumerable.Repeat((byte)0xA5, 8).ToArray());
        NativeBattleGfGeometryAttempt rejected = new(0, ImmutableArray.Create<byte>(0, 0, 0, 7, 0, 0, 0, 0), [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched], null);
        NativeBattleGatlingGun305PacketWrite result = NativeBattleGatlingGun305PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), new([rejected], 0));
        Assert.Equal(Base, result.State.ReadDword(Cursor));
        Assert.Equal(0x07000000u, U32(result.State.ReadPacket(Base, 4)));
        Assert.Throws<NotSupportedException>(() => result.State.ReadPacket(Base + 4, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.State.ReadPacket(Base + 6, 2));
        Assert.Empty(result.Ordering.Packets);
    }

    [Fact]
    public void Warm_unknown_bank_and_physical_links_continue_after_independent_JSON_restore()
    {
        NativeBattleGatlingGun305State state = NativeBattleGatlingGun305State.FromUnknown().WriteDword(Cursor, Base + 0x8000);
        NativeBattleGatlingGun305PacketWrite first = NativeBattleGatlingGun305PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220));
        NativeBattleGatlingGun305PacketWrite restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGatlingGun305PacketWrite>(NativeBattleTestSnapshots.Json(first))!;
        Assert.Throws<NotSupportedException>(() => restored.State.ReadPacket(Base, 4));
        NativeBattleGatlingGun305PacketWrite Next(NativeBattleGatlingGun305PacketWrite current) => NativeBattleGatlingGun305PacketArena.Append(current.State, current.Ordering, Accepted(0xE1000221));
        var direct = Next(first);
        var next = Next(restored);
        Assert.Equal(NativeBattleTestSnapshots.Json(direct), NativeBattleTestSnapshots.Json(next));
        byte[] lengths = new byte[256];
        lengths[0xE1] = 1;
        NativeBattleLogicalCommand[] commands = next.Ordering.Traverse(lengths.ToImmutableArray(), (reference, count) => NativeBattleGatlingGun305PacketArena.Read(next.State, reference, count)).ToArray();
        Assert.Equal(new uint[] { 0xE1000221, 0xE1000220 }, commands.Select(command => command.Words[0]));
    }

    [Fact]
    public void Physical_span_bounds_cover_both_banks_and_rejected_prefix_stores()
    {
        NativeBattleGatlingGun305State state = NativeBattleGatlingGun305State.FromColdProcess().WriteDword(Cursor, Base + 0xFFFC);
        Assert.Throws<InvalidDataException>(() => NativeBattleGatlingGun305PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
        NativeBattleGfGeometryAttempt rejected = Accepted(0xE1000220).Attempts[0] with { Primitive = null };
        Assert.Throws<InvalidDataException>(() => NativeBattleGatlingGun305PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), new([rejected], 0)));
        Assert.Throws<InvalidDataException>(() => NativeBattleGatlingGun305PacketArena.Read(state, new("2247790", 1, 0), 4));
        Assert.Throws<InvalidDataException>(() => NativeBattleGatlingGun305PacketArena.Read(state, new("2247790", 0, 0xFFFF), 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleGatlingGun305PacketArena.Append(state.WriteDword(Cursor, Base + 1), NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
        Assert.Throws<NotSupportedException>(() => NativeBattleGatlingGun305PacketArena.Append(NativeBattleGatlingGun305State.FromUnknown(), NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
        NativeBattleGatlingGun305PacketWrite crossed = NativeBattleGatlingGun305PacketArena.Append(state.WriteDword(Cursor, Base + 0x7FFC), NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220));
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
