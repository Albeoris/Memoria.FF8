namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffectPacketArena
{
    /// <summary>Apply ordered native stores and links, including rejected attempts and nonsequential E1/draw packet insertion.</summary>
    public static NativeBattleEffectPacketWrite Append(NativeBattleEffectMemory memory, int cursor, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, ushort flags20 = 0)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native effect packet attempts require their actual571870 arena bytes.");
        if (batch.Attempts.IsDefault || batch.AllocationBytes < 0 || (batch.AllocationBytes & 3) != 0 || cursor < 0 || (cursor & 3) != 0 || cursor > NativeBattleEffectArena.Length - batch.AllocationBytes)
        {
            throw new InvalidDataException("Native effect packet attempts require their original ordered stores/links and aligned allocation.");
        }
        int acceptedEnd = 0;
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            if (attempt is null || attempt.Data.IsDefaultOrEmpty || attempt.WriteMask.IsDefault || attempt.WriteMask.Length != attempt.Data.Length || attempt.ByteOffsetRelative < 0 || (attempt.ByteOffsetRelative & 3) != 0 || (attempt.PacketLength & 3) != 0)
            {
                throw new InvalidDataException("Native effect packet attempt lacks its actual bounded stores or link address.");
            }
            int address = checked(cursor + attempt.ByteOffsetRelative);
            if (address > NativeBattleEffectArena.Length - attempt.PacketLength)
            {
                throw new InvalidDataException("Native effect packet stores leave the original1MB arena.");
            }
            for (int offset = 0; offset < attempt.PacketLength;)
            {
                NativeBattleGfPacketByteState state = attempt.WriteMask[offset];
                int end = offset + 1;
                while (end < attempt.PacketLength && attempt.WriteMask[end] == state)
                {
                    end++;
                }
                arena = state switch
                {
                    NativeBattleGfPacketByteState.Known => arena.Write(checked(address + offset), attempt.Data.AsSpan().Slice(offset, end - offset)),
                    NativeBattleGfPacketByteState.Unknown => arena.MarkUnknown(checked(address + offset), end - offset),
                    NativeBattleGfPacketByteState.Untouched => arena,
                    _ => throw new InvalidDataException("Native effect packet attempt has an unknown byte-write state.")
                };
                offset = end;
            }
            if (attempt.Primitive is { } primitive)
            {
                int end = checked(attempt.ByteOffsetRelative + attempt.PacketLength);
                if (primitive.Words.Length * 4 != attempt.PacketLength || end > batch.AllocationBytes)
                {
                    throw new InvalidDataException("Native linked effect packet leaves its accepted allocation.");
                }
                acceptedEnd = Math.Max(acceptedEnd, end);
                bool linked = ordering.Proxies.Length < NativeBattlePrimitiveOrdering.ProxyCapacity;
                ordering = ordering.Prepend(primitive.Bucket, new("20DFAB8", 0, address), primitive.Words, flags20, primitive.VertexPayload4);
                if (linked)
                {
                    arena = arena.MarkUnknown(address, 3);
                }
            }
        }
        if (acceptedEnd != batch.AllocationBytes)
        {
            throw new InvalidDataException("Native accepted effect packet extent differs from its original final cursor.");
        }
        return new(memory with { Arena = arena }, checked(cursor + batch.AllocationBytes), ordering);
    }
}
