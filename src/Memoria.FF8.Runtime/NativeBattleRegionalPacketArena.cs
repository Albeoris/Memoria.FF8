namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRegionalPacketWrite(NativeBattleEffectArena Arena, NativeBattleLogicalOrdering Ordering, int Cursor);

/// <summary>Ordered packet stores for module-owned executable banks, with the same proxy/link contract as the common arena.</summary>
public static class NativeBattleRegionalPacketArena
{
    public static NativeBattleRegionalPacketWrite Append(NativeBattleEffectArena source, string bank, int cursor, int capacity, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, int trailingBytes = 0)
    {
        if (batch.Attempts.IsDefault || batch.AllocationBytes < 0 || (batch.AllocationBytes & 3) != 0 || cursor < 0 || (cursor & 3) != 0 || capacity > NativeBattleEffectArena.Length || cursor > capacity - batch.AllocationBytes)
        {
            throw new InvalidDataException("Native module packets leave their aligned physical bank.");
        }
        NativeBattleEffectArenaWriter arena = new(source);
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        int accepted = 0;
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            if (attempt.Data.IsDefaultOrEmpty || attempt.WriteMask.IsDefault || attempt.Data.Length != attempt.WriteMask.Length || attempt.ByteOffsetRelative < 0 || (attempt.ByteOffsetRelative & 3) != 0 || (attempt.PacketLength & 3) != 0)
            {
                throw new InvalidDataException("Native module packet attempt lacks its exact ordered stores.");
            }
            int address = checked(cursor + attempt.ByteOffsetRelative);
            if (address > capacity - attempt.PacketLength)
            {
                throw new InvalidDataException("Native module packet attempt exceeds its physical bank.");
            }
            for (int offset = 0; offset < attempt.PacketLength;)
            {
                NativeBattleGfPacketByteState state = attempt.WriteMask[offset];
                int end = offset + 1;
                while (end < attempt.PacketLength && attempt.WriteMask[end] == state)
                {
                    end++;
                }
                if (state != NativeBattleGfPacketByteState.Untouched)
                {
                    links.InvalidatePacketRange(new(bank, 0, address + offset), end - offset);
                }
                arena = state switch
                {
                    NativeBattleGfPacketByteState.Known => arena.Write(address + offset, attempt.Data.AsSpan().Slice(offset, end - offset)),
                    NativeBattleGfPacketByteState.Unknown => arena.MarkUnknown(address + offset, end - offset),
                    NativeBattleGfPacketByteState.Untouched => arena,
                    _ => throw new InvalidDataException("Native module packet has an invalid write mask.")
                };
                offset = end;
            }
            if (attempt.Primitive is { } primitive)
            {
                int end = checked(attempt.ByteOffsetRelative + attempt.PacketLength);
                if (primitive.Words.IsDefaultOrEmpty || primitive.Words.Length < 1 + (primitive.Words[0] >> 24) || primitive.Words.Length * 4 > attempt.PacketLength || end > batch.AllocationBytes)
                {
                    throw new InvalidDataException("Native module linked primitive exceeds its actual allocation.");
                }
                bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
                links.Prepend(primitive.Bucket, new(bank, 0, address), primitive.Words, 0, primitive.VertexPayload4);
                if (linked)
                {
                    arena = arena.Write(address, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
                }
                accepted = Math.Max(accepted, end);
            }
        }
        if (trailingBytes < 0 || (trailingBytes & 3) != 0 || accepted != batch.AllocationBytes - trailingBytes)
        {
            throw new InvalidDataException("Native module accepted extent disagrees with its final packet cursor.");
        }
        return new(arena.Freeze(), links.ToImmutable(), checked(cursor + batch.AllocationBytes));
    }
}
