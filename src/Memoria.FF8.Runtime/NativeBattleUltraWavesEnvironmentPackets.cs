using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleUltraWavesEnvironmentPackets
{
    public static NativeBattleGfGeometryBatch Environment(ImmutableArray<byte> data, int allocation, int writtenEnd, int bucket)
    {
        int length = 4 + data[3] * 4;
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(index * 4))).ToImmutableArray();
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
        Array.Fill(mask, NativeBattleGfPacketByteState.Known, 3, writtenEnd - 3);
        byte[] stores = new byte[allocation];
        data.AsSpan().CopyTo(stores);
        return new([new(0, stores.ToImmutableArray(), mask.ToImmutableArray(), new(bucket, words, [0, 0, 0, 0]))], allocation);
    }
}
