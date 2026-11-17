using System.Buffers;
using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>The loaded model maximum bounds the shared per-object eight-byte projection workspace.</summary>
public static class NativeBattleBodyProjectionScratch
{
    public static NativeBattleFileStaging Publish(NativeBattleFileStaging staging, ushort maximum, ReadOnlySpan<NativeBattleProjectedVertex> vertices)
    {
        int capacity = checked(maximum * 8);
        int length = checked(vertices.Length * 8);
        if (length > capacity)
        {
            throw new InvalidDataException("Native body projection exceeds its actual shared scratch allocation.");
        }
        if (length == 0)
        {
            return staging;
        }
        byte[]? rented = null;
        Span<byte> bytes = length <= 512 ? stackalloc byte[length] : (rented = ArrayPool<byte>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            for (int index = 0; index < vertices.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes[(index * 8)..], vertices[index].Screen);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes[(index * 8 + 4)..], vertices[index].DepthAndClip);
            }
            return staging.Write(checked(0x196B018u - (uint)capacity), bytes);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
