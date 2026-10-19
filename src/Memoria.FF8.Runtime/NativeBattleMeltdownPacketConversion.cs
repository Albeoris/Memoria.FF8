using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleMeltdownPacketConversion
{
    public static NativeBattleGfGeometryBatch FromSprite(NativeBattleModulePacketBatch batch)
    {
        if (batch.CapturedWrites is not null)
        {
            throw new InvalidDataException("Native Meltdown sprite conversion requires the selected sequence packet producer.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        foreach (NativeBattleAllocatedPrimitive allocation in batch.Primitives)
        {
            NativeBattleGeometryPrimitive primitive = allocation.Primitive;
            int length = primitive.Words.Length * 4;
            bool mode = length == 8 && primitive.Words[1] == 0xE1000220;
            if (!mode && (length != 40 || (primitive.Words[1] >> 24 & 0xFC) != 0x2C))
            {
                throw new NotSupportedException("Native Meltdown sequence reached an unproved packet producer.");
            }
            byte[] data = new byte[mode ? 12 : 40];
            NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, data.Length).ToArray();
            for (int index = 0; index < primitive.Words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), primitive.Words[index]);
            }
            if (mode)
            {
                mask.AsSpan(8, 4).Fill(NativeBattleGfPacketByteState.Untouched);
            }
            else
            {
                mask.AsSpan(30, 2).Fill(NativeBattleGfPacketByteState.Untouched);
                mask.AsSpan(38, 2).Fill(NativeBattleGfPacketByteState.Untouched);
            }
            attempts.Add(new(allocation.ByteOffset, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
        }
        return new(attempts.ToImmutable(), batch.AllocationBytes);
    }
}
