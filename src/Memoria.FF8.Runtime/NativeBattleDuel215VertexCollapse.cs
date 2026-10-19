using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel215Controller
{
    public static NativeBattleEffectArena CollapseVertices69C5A0(NativeBattleEffectArena arena, int destination, ReadOnlySpan<byte> mesh, int fraction)
    {
        if (mesh.Length < 12)
        {
            throw new InvalidDataException("Native Duel215 collapse requires its actual geometry header.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Slice(4, 4));
        if (count <= 0)
        {
            return arena;
        }
        if (count > (mesh.Length - 12) / 8)
        {
            throw new InvalidDataException("Native Duel215 collapse leaves its selected vertex owner.");
        }
        int cutoff = unchecked(fraction * count) >> 12;
        Span<byte> position = stackalloc byte[6];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> vertex = mesh.Slice(12 + index * 8, 8);
            if (index > cutoff)
            {
                BinaryPrimitives.WriteInt32LittleEndian(position, 0);
                BinaryPrimitives.WriteInt16LittleEndian(position.Slice(4), 0x4000);
                arena = arena.Write(checked(destination + index * 8), position);
                continue;
            }
            if (unchecked((uint)(index - cutoff)) < 0xFFFFFFC0u)
            {
                arena = arena.Write(checked(destination + index * 8), vertex);
                continue;
            }
            int anchorIndex = index / 8 * 8 + 2;
            if (anchorIndex > (mesh.Length - 20) / 8)
            {
                throw new InvalidDataException("Native Duel215 collapse requires its actual eight-vertex anchor group.");
            }
            ReadOnlySpan<byte> anchor = mesh.Slice(12 + anchorIndex * 8, 8);
            int weight = unchecked(64 * (cutoff - index));
            for (int component = 0; component < 2; component++)
            {
                short origin = BinaryPrimitives.ReadInt16LittleEndian(anchor.Slice(component * 2, 2));
                short selected = BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(component * 2, 2));
                short value = unchecked((short)(origin + unchecked(weight * (selected - origin)) / 4096));
                BinaryPrimitives.WriteInt16LittleEndian(position.Slice(component * 2, 2), value);
            }
            vertex.Slice(4, 2).CopyTo(position.Slice(4));
            arena = arena.Write(checked(destination + index * 8), position);
        }
        return arena;
    }
}
