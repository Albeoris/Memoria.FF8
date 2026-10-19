using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSandStorm176PacketsTests
{
    [Fact]
    public void Fixed_sparse_slots_keep_rejected_stores_and_do_not_allocate_the_gaps_or_tail()
    {
        byte[] accepted = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(accepted, 0x09000000);
        BinaryPrimitives.WriteUInt32LittleEndian(accepted.AsSpan(4), 0x2E808080);
        ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(accepted.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 40).ToArray();
        NativeBattleGfGeometryAttempt first = new(0xE48, accepted.ToImmutableArray(), mask.ToImmutableArray(), new(17, words, [0, 0, 0, 0]));
        byte[] rejected = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(rejected.AsSpan(4), 0x2E404040);
        Array.Fill(mask, NativeBattleGfPacketByteState.Untouched);
        Array.Fill(mask, NativeBattleGfPacketByteState.Known, 4, 4);
        NativeBattleGfGeometryAttempt last = new(0x5B48, rejected.ToImmutableArray(), mask.ToImmutableArray(), null);
        NativeBattleEffectArena initial = NativeBattleEffectArena.Clear().Write(0x5B48, Enumerable.Repeat((byte)0xA5, 40).ToArray());
        NativeBattleRegionalPacketWrite written = NativeBattleSandStorm176Packets.WriteFunnel(initial, NativeBattleLogicalOrdering.Clear(), new([first, last], 0));
        Assert.Equal(0, written.Cursor);
        Assert.Equal(0x2E404040u, BinaryPrimitives.ReadUInt32LittleEndian(written.Arena.Read(0x5B4C, 4)));
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, written.Arena.Read(0x5B50, 4));
        Assert.Equal(new byte[40], written.Arena.Read(0xE48 + 40, 40));
        Assert.Throws<InvalidDataException>(() => NativeBattleSandStorm176Packets.WriteFunnel(initial, NativeBattleLogicalOrdering.Clear(), new([first], 40)));
    }
}
