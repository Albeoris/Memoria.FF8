using System.Buffers;
using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>The loaded model maximum bounds the shared per-object eight-byte projection workspace.</summary>
public static class NativeBattleBodyProjectionScratch
{
    internal const uint EndAddress = 0x196B018;
    internal const int BytesPerVertex = 8;

    public static NativeBattleFileStaging Publish(NativeBattleFileStaging staging, ushort maximum, ReadOnlySpan<NativeBattleProjectedVertex> vertices)
    {
        int capacity = checked(maximum * BytesPerVertex);
        int length = checked(vertices.Length * BytesPerVertex);
        if (length > capacity)
        {
            throw new InvalidDataException($"Native body projection uses {vertices.Length} vertices ({maximum} allocated).");
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
                BinaryPrimitives.WriteUInt32LittleEndian(bytes[(index * BytesPerVertex)..], vertices[index].Screen);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes[(index * BytesPerVertex + 4)..], vertices[index].DepthAndClip);
            }
            return staging.Write(checked(EndAddress - (uint)capacity), bytes);
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
