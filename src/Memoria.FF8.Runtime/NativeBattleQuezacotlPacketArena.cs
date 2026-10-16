using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlPacketWrite(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleLogicalOrdering Ordering);

/// <summary>6C6060 writes252176C's effect-arena packets;45C870 links their portable identities without inventing native pointer bytes.</summary>
public static class NativeBattleQuezacotlPacketArena
{
    public static NativeBattleQuezacotlPacketWrite Append(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, ushort flags20 = 0)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C6060 requires its actual effect-arena packet bytes.");
        int start = effect.DrawArenaOffset ?? throw new NotSupportedException("Native6C6060 requires its executed6C3760 draw-buffer pointer.");
        int cursor = start;
        if (batch.Attempts.IsDefault || batch.AllocationBytes < 0 || (start & 3) != 0)
        {
            throw new InvalidDataException("Native6C6060 packet writes require their ordered attempts and actual aligned cursor.");
        }
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            if (attempt is null || attempt.Data.IsDefaultOrEmpty || attempt.WriteMask.IsDefault || attempt.WriteMask.Length != attempt.Data.Length || attempt.ByteOffsetRelative != cursor - start || (attempt.PacketLength & 3) != 0)
            {
                throw new InvalidDataException("Native7043B0 packet attempt does not match its current physical cursor or byte-write masks.");
            }
            for (int offset = 0; offset < attempt.PacketLength;)
            {
                NativeBattleGfPacketByteState state = attempt.WriteMask[offset];
                int end = offset + 1;
                while (end < attempt.PacketLength && attempt.WriteMask[end] == state)
                {
                    end++;
                }
                switch (state)
                {
                    case NativeBattleGfPacketByteState.Known:
                        arena = arena.Write(checked(cursor + offset), attempt.Data.AsSpan().Slice(offset, end - offset));
                        break;
                    case NativeBattleGfPacketByteState.Unknown:
                        arena = arena.MarkUnknown(checked(cursor + offset), end - offset);
                        break;
                    case NativeBattleGfPacketByteState.Untouched:
                        break;
                    default:
                        throw new InvalidDataException("Native7043B0 packet attempt has an unknown byte-write state.");
                }
                offset = end;
            }
            if (attempt.Primitive is { } primitive)
            {
                if (primitive.Words.Length * 4 != attempt.PacketLength)
                {
                    throw new InvalidDataException("Native accepted packet length differs from its physical write allocation.");
                }
                bool linked = ordering.Proxies.Length < NativeBattlePrimitiveOrdering.ProxyCapacity;
                ordering = ordering.Prepend(primitive.Bucket, new("20DFAB8", 0, cursor), primitive.Words, flags20, primitive.VertexPayload4);
                if (linked)
                {
                    arena = arena.MarkUnknown(cursor, 3);
                }
                cursor = checked(cursor + attempt.PacketLength);
            }
        }
        if (cursor - start != batch.AllocationBytes)
        {
            throw new InvalidDataException("Native7043B0 final cursor differs from its accepted-only allocation size.");
        }
        return new(memory with { Arena = arena }, effect with { DrawArenaOffset = cursor }, ordering);
    }
}
