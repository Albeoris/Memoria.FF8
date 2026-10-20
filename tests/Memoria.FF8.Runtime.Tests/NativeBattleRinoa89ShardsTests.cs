using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89ShardsTests
{
    [Fact]
    public void Shard_rotation_uses_saturated_GTE_IR_stores_and_ignores_unconsumed_padding()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        short[] matrix = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096];
        byte[] bytes = new byte[18];
        for (int index = 0; index < matrix.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), matrix[index]);
        }
        arena = arena.Write(30468, bytes).MarkUnknown(30486, 2);
        arena = arena.Write(24228 + 6, BitConverter.GetBytes((ushort)16)).Write(24228 + 16, BitConverter.GetBytes(0x20DFAB8u + 30468)).Write(24228 + 20, BitConverter.GetBytes((short)20000));
        arena = arena.MarkUnknown(24228 + 38, 2).MarkUnknown(24228 + 50, 2);
        NativeBattleRinoa89Frame frame = new(new(arena, 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0), null!, NativeBattleFileStaging.Unknown(), new(1));
        frame = NativeBattleRinoa89Controller.AdvanceShards6FD100(frame, 1);
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(24228 + 20, 2)));
        Assert.False(frame.Memory.Arena.ReadSnapshot(24228 + 38, 2).Known[0]);
        Assert.Single(NativeBattleRinoa89ShardDrawing.Draw6FCFD0(frame.Memory.Arena).Attempts);
    }

    [Fact]
    public void Rejected_shard_still_writes_native_UV_and_XY_at_the_unadvanced_packet_cursor()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(24228 + 6, BitConverter.GetBytes((ushort)16)).Write(24228, BitConverter.GetBytes((short)-1000));
        NativeBattleGfGeometryBatch packets = NativeBattleRinoa89ShardDrawing.Draw6FCFD0(arena);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(packets.Attempts);
        Assert.Equal(0, packets.AllocationBytes);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[28]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
    }
}
