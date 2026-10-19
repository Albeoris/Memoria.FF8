using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffectPacketArena
{
    /// <summary>Apply ordered native stores and links, including rejected attempts and nonsequential E1/draw packet insertion.</summary>
    public static NativeBattleEffectPacketWrite Append(NativeBattleEffectMemory memory, int cursor, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, ushort flags20 = 0, int? moduleDraw = null)
    {
        NativeBattleEffectArenaWriter arena = new(memory.Arena ?? throw new NotSupportedException("Native effect packet attempts require their actual571870 arena bytes."));
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        AppendAttempts(arena, links, cursor, batch, flags20, moduleDraw);
        return new(memory with { Arena = arena.Freeze() }, checked(cursor + batch.AllocationBytes), links.ToImmutable());
    }

    public static NativeBattleEffectPacketWrite AppendPrivate(NativeBattleEffectMemory memory, int cursor, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, uint orderingAddress, ushort flags20 = 0, int? moduleDraw = null)
    {
        NativeBattleEffectArena source = memory.Arena ?? throw new NotSupportedException("Native private packets require their actual effect arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena view = owned?.View ?? source;
        NativeBattleEffectArenaWriter arena = new(view);
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        AppendAttempts(arena, links, cursor, batch, flags20, moduleDraw, orderingAddress);
        return new(memory with { Arena = owned?.Freeze() ?? view }, checked(cursor + batch.AllocationBytes), links.ToImmutable());
    }

    internal static void AppendAttempts(NativeBattleEffectArenaWriter arena, NativeBattleLogicalOrdering.Builder links, int cursor, NativeBattleGfGeometryBatch batch, ushort flags20 = 0, int? moduleDraw = null, uint? privateOrderingAddress = null)
    {
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
            links.InvalidatePacketRange(new("20DFAB8", 0, address), attempt.PacketLength);
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
                //8DD260's first extended E1 occupies12 allocation bytes but only two logical command DWORDs; its final four bytes are untouched padding.
                if (primitive.Words.IsDefaultOrEmpty || primitive.Words.Length < 1 + (primitive.Words[0] >> 24) || primitive.Words.Length * 4 > attempt.PacketLength || end > batch.AllocationBytes)
                {
                    throw new InvalidDataException("Native linked effect packet leaves its accepted allocation.");
                }
                acceptedEnd = Math.Max(acceptedEnd, end);
                bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
                int? rootOffset = null;
                uint proxyAddress = 0x1C48828u + (uint)(24 * links.ProxyCount);
                if (privateOrderingAddress is uint orderingBase)
                {
                    if (linked)
                    {
                        uint root = unchecked(orderingBase + (uint)(4 * primitive.Bucket));
                        rootOffset = NativeBattleWaterExecutionFields.ArenaOffset(root, 4);
                        uint previous = BinaryPrimitives.ReadUInt32LittleEndian(arena.ReadCurrent(rootOffset.Value, 4));
                        links.PrependAddressed(previous, new("20DFAB8", 0, address), primitive.Words, flags20, primitive.VertexPayload4, moduleDraw);
                    }
                }
                else
                {
                    links.Prepend(primitive.Bucket, new("20DFAB8", 0, address), primitive.Words, flags20, primitive.VertexPayload4, moduleDraw);
                }
                if (linked)
                {
                    arena = arena.Write(address, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
                    if (rootOffset is int root)
                    {
                        arena = arena.Write(root, BitConverter.GetBytes(proxyAddress));
                    }
                }
            }
        }
        if (acceptedEnd != batch.AllocationBytes)
        {
            throw new InvalidDataException("Native accepted effect packet extent differs from its original final cursor.");
        }
    }
}
