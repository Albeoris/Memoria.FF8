using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The mirror's two-source GTE vertex morph writes XYZ and preserves each destination padding WORD.</summary>
public static class NativeBattleCarbuncle278Morph
{
    public static NativeBattleEffectMemory Morph683CA0(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource first, ImmutableArray<byte> alternate, int ratio)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Carbuncle mirror morph requires its actual effect arena.");
        int count = BinaryPrimitives.ReadInt32LittleEndian(first.Bytes.AsSpan().Slice(4));
        ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices45E9D0(first.Bytes, 8, alternate, 0, count, ratio);
        NativeBattleEffectArenaWriter writer = new(arena);
        for (int index = 0; index < vertices.Length; index++)
        {
            NativeBattleVector vertex = vertices[index];
            byte[] bytes = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, vertex.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), vertex.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), vertex.Z);
            writer.Write(0x2E84 + index * 8, bytes);
        }
        return memory with { Arena = writer.Freeze() };
    }
}
