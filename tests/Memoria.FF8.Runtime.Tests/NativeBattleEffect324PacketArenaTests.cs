using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect324PacketArenaTests
{
    private const uint Base = NativeBattleEffect324PacketArena.FirstAddress;
    private const uint Cursor = NativeBattleEffect324PacketArena.CursorAddress;

    [Fact]
    public void Alternating_physical_banks_retain_their_actual_packets_and_one_global_cursor()
    {
        NativeBattleEffect324State state = NativeBattleEffect324State.FromColdProcess().WriteDword(Cursor, Base);
        NativeBattleEffect324PacketWrite first = NativeBattleEffect324PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220));
        NativeBattleEffect324PacketWrite second = NativeBattleEffect324PacketArena.Append(first.State.WriteDword(Cursor, Base + 0x8000), first.Ordering, Accepted(0xE1000221));
        Assert.Equal(Base + 0x8008, second.State.ReadDword(Cursor));
        Assert.Equal(new[] { 0, 0 }, second.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.Equal(new[] { NativeBattleEffect324PacketArena.FirstBank, NativeBattleEffect324PacketArena.SecondBank }, second.Ordering.Packets.Select(packet => packet.Reference.Bank));
        Assert.All(second.Ordering.Packets, packet => Assert.Equal(0, packet.Reference.Buffer));
        Assert.Equal(0xE1000220u, U32(second.State.Read(Base + 4, 4)));
        Assert.Equal(0xE1000221u, U32(second.State.Read(Base + 0x8004, 4)));
    }

    [Fact]
    public void Rejected_prefix_applies_known_unknown_and_untouched_stores_without_allocating()
    {
        NativeBattleEffect324State state = NativeBattleEffect324State.FromColdProcess().WriteDword(Cursor, Base).Write(Base, Enumerable.Repeat((byte)0xA5, 8).ToArray());
        NativeBattleGfGeometryAttempt rejected = new(0, ImmutableArray.Create<byte>(0, 0, 0, 7, 0, 0, 0, 0), [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched], null);
        NativeBattleEffect324PacketWrite result = NativeBattleEffect324PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), new([rejected], 0));
        Assert.Equal(Base, result.State.ReadDword(Cursor));
        Assert.Equal(0x07000000u, U32(result.State.Read(Base, 4)));
        Assert.Throws<NotSupportedException>(() => result.State.Read(Base + 4, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.State.Read(Base + 6, 2));
        Assert.Empty(result.Ordering.Packets);
    }

    [Fact]
    public void Warm_unknown_bank_and_physical_links_continue_after_independent_JSON_restore()
    {
        NativeBattleEffect324State state = NativeBattleEffect324State.FromUnknown().WriteDword(Cursor, Base + 0x8000);
        NativeBattleEffect324PacketWrite first = NativeBattleEffect324PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220));
        NativeBattleEffect324PacketWrite restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect324PacketWrite>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        Assert.Throws<NotSupportedException>(() => restored.State.Read(Base, 4));
        NativeBattleEffect324PacketWrite Next(NativeBattleEffect324PacketWrite current) => NativeBattleEffect324PacketArena.Append(current.State, current.Ordering, Accepted(0xE1000221));
        var expected = Next(first);
        var actual = Next(restored);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual)));
        byte[] lengths = new byte[256];
        lengths[0xE1] = 1;
        NativeBattleEffect324PacketWrite next = actual;
        NativeBattleLogicalCommand[] commands = next.Ordering.Traverse(lengths.ToImmutableArray(), (reference, count) => NativeBattleEffect324PacketArena.Read(next.State, reference, count)).ToArray();
        Assert.Equal(new uint[] { 0xE1000221, 0xE1000220 }, commands.Select(command => command.Words[0]));
    }

    [Fact]
    public void Physical_span_bounds_cover_both_banks_and_rejected_prefix_stores()
    {
        NativeBattleEffect324State state = NativeBattleEffect324State.FromColdProcess().WriteDword(Cursor, Base + 0xFFFC);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect324PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
        NativeBattleGfGeometryAttempt rejected = Accepted(0xE1000220).Attempts[0] with { Primitive = null };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect324PacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), new([rejected], 0)));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect324PacketArena.Read(state, new(NativeBattleEffect324PacketArena.FirstBank, 1, 0), 4));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect324PacketArena.Read(state, new(NativeBattleEffect324PacketArena.SecondBank, 0, 0x7FFF), 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect324PacketArena.Append(state.WriteDword(Cursor, Base + 1), NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect324PacketArena.Append(NativeBattleEffect324State.FromUnknown(), NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect324PacketArena.Append(state.WriteDword(Cursor, Base + 0x7FFC), NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
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
