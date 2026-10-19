using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleModulePackets
{
    public static NativeBattleGfGeometryBatch CaptureSprite571DC0(NativeBattleModulePacketBatch batch)
    {
        if (batch.CapturedWrites is { } captured)
        {
            return captured;
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        foreach (NativeBattleAllocatedPrimitive packet in batch.Primitives)
        {
            ImmutableArray<uint> words = packet.Primitive.Words;
            bool environment = words.Length == 2 && words[0] == 0x1000000 && (words[1] >> 24) == 0xE1;
            int length = environment ? 12 : checked(words.Length * 4);
            if (packet.ByteOffset < 0 || packet.ByteOffset > batch.AllocationBytes - length)
            {
                throw new InvalidDataException("Native571DC0 packet leaves its actual reserved extent.");
            }
            byte[] bytes = new byte[length];
            for (int index = 0; index < words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
            }
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, length).Select(index => index < words.Length * 4 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            attempts.Add(new(packet.ByteOffset, bytes.ToImmutableArray(), mask, packet.Primitive));
        }
        return new(attempts.ToImmutable(), batch.AllocationBytes);
    }
}
