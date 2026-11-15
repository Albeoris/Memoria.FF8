using System.Buffers.Binary;
using NodeKey = Memoria.FF8.Runtime.BattleExecutionReference.CinematicNodeKey;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleCinematicNodeKeys
{
    internal static NodeKey Read(NativeBattleEffectArena arena, int offset)
    {
        if (arena.TryReadExecutionReference<NodeKey>(offset, 2, out NodeKey? key))
        {
            return key;
        }
        Span<byte> bytes = stackalloc byte[2];
        arena.ReadInto(offset, bytes);
        return new NodeKey.Literal(BinaryPrimitives.ReadUInt16LittleEndian(bytes));
    }

    internal static NativeBattleEffectArena Write(NativeBattleEffectArena arena, int offset, NodeKey key)
    {
        if (key is NodeKey.Literal literal)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, literal.Value);
            return arena.Write(offset, bytes);
        }
        return arena.WriteExecutionReference(offset, 2, key);
    }

    internal static NodeKey Relative(NativeBattleEffectArena arena, int actorOffset, ushort displacement)
    {
        Span<byte> bytes = stackalloc byte[1];
        Span<bool> known = stackalloc bool[1];
        arena.ReadWithKnownness(actorOffset, bytes, known);
        if (known[0])
        {
            return new NodeKey.Literal(unchecked((ushort)(bytes[0] + displacement)));
        }
        BattleModelOwner owner = arena.ReadExecutionReference<BattleExecutionReference.Model>(actorOffset, 1).Owner;
        return new NodeKey.ActorRelative(owner, displacement);
    }
}
