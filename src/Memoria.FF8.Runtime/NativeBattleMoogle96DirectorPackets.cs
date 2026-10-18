using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleMoogle96DirectorPackets
{
    public static NativeBattleGfGeometryBatch Batch7327B0()
    {
        ImmutableArray<uint> words = [2, 0xE1000020, 0];
        byte[] data = new byte[12];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 12).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(4385, words, [0, 0, 0, 0]) { PhysicalWriteMask = mask };
        return new([new(0, data.ToImmutableArray(), mask, primitive)], 12);
    }
}
