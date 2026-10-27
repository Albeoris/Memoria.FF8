using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNeedlesFamilyPacketTests
{
    [Theory]
    [InlineData(177)]
    [InlineData(178)]
    public void Fixed_packet_banks_keep_independent_unknown_bytes_physical_links_and_JSON_continuations(ushort effect)
    {
        NativeBattleNeedlesFamilyState state = NativeBattleNeedlesFamilyState.FromUnknown(effect);
        NativeBattleNeedlesFamilyProfile profile = state.Profile;
        state = state.WriteDword(profile.CursorGlobal, profile.PacketBase + NativeBattleNeedlesFamilyProfile.PacketStep);
        NativeBattleNeedlesFamilyPacketWrite first = NativeBattleNeedlesFamilyPacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220));
        Assert.Equal(profile.PacketBase + NativeBattleNeedlesFamilyProfile.PacketStep + 8, first.State.ReadDword(profile.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => first.State.ReadPacket(profile.PacketBase, 4));
        NativeBattleNeedlesFamilyPacketWrite restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleNeedlesFamilyPacketWrite>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleNeedlesFamilyPacketWrite Next(NativeBattleNeedlesFamilyPacketWrite current) => NativeBattleNeedlesFamilyPacketArena.Append(current.State, current.Ordering, Accepted(0xE1000221));
        var direct = Next(first);
        var next = Next(restored);
        Assert.Equal(NativeBattleTestSnapshots.Json(direct), NativeBattleTestSnapshots.Json(next));
        byte[] lengths = new byte[256];
        lengths[0xE1] = 1;
        Assert.Equal(new uint[] { 0xE1000221, 0xE1000220 }, next.Ordering.Traverse(lengths.ToImmutableArray(), (reference, count) => NativeBattleNeedlesFamilyPacketArena.Read(next.State, reference, count)).Select(command => command.Words[0]));
        Assert.All(next.Ordering.Packets, packet => Assert.Equal(NativeBattleNeedlesFamilyPacketArena.Bank(effect), packet.Reference.Bank));
        Assert.Throws<InvalidDataException>(() => NativeBattleNeedlesFamilyPacketArena.Read(next.State, new(NativeBattleNeedlesFamilyPacketArena.Bank(effect == 177 ? 178 : 177), 0, 0), 4));
    }

    [Theory]
    [InlineData(177)]
    [InlineData(178)]
    public void Rejected_prefix_stores_retain_unknown_and_untouched_bytes_and_obey_the_complete_physical_bound(ushort effect)
    {
        NativeBattleNeedlesFamilyState state = NativeBattleNeedlesFamilyState.FromColdProcess(effect);
        uint address = state.Profile.PacketBase;
        state = state.WriteDword(state.Profile.CursorGlobal, address).WritePacket(address, Enumerable.Repeat((byte)0xA5, 8).ToArray());
        NativeBattleGfGeometryAttempt prefix = new(0, ImmutableArray.Create<byte>(0, 0, 0, 7, 0, 0, 0, 0), [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched], null);
        NativeBattleNeedlesFamilyPacketWrite result = NativeBattleNeedlesFamilyPacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), new([prefix], 0));
        Assert.Equal(address, result.State.ReadDword(state.Profile.CursorGlobal));
        Assert.Equal(0x07000000u, BinaryPrimitives.ReadUInt32LittleEndian(result.State.ReadPacket(address, 4)));
        Assert.Throws<NotSupportedException>(() => result.State.ReadPacket(address + 4, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.State.ReadPacket(address + 6, 2));
        Assert.Empty(result.Ordering.Packets);
        state = state.WriteDword(state.Profile.CursorGlobal, address + NativeBattleNeedlesFamilyProfile.PacketBytes - 4);
        Assert.Throws<InvalidDataException>(() => NativeBattleNeedlesFamilyPacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), new([prefix], 0)));
        Assert.Throws<InvalidDataException>(() => NativeBattleNeedlesFamilyPacketArena.Append(state, NativeBattleLogicalOrdering.Clear(), Accepted(0xE1000220)));
    }

    private static NativeBattleGfGeometryBatch Accepted(uint command)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), command);
        NativeBattleGeometryPrimitive primitive = new(17, [0x01000000, command], [0, 0, 0, 0]);
        return new([new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), primitive)], 8);
    }
}
