using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>6C68F0's ordered scratch, common heap and effect arena stores use their distinct physical cursors.</summary>
public static class NativeBattleQuezacotlSkinArena
{
    public static NativeBattleQuezacotlPacketWrite Append(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleLogicalOrdering ordering, NativeBattleQuezacotlSkinBatch batch)
    {
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native6C6770 requires its retained common packet process.");
        int common = process.ByteCursor ?? throw new NotSupportedException("Native6C6770 requires its allocated common packet cursor.");
        string bank = process.PacketBank ?? throw new NotSupportedException("Native6C6770 requires its allocated common packet bank.");
        int draw = effect.DrawArenaOffset ?? throw new NotSupportedException("Native6C6770 requires its actual effect draw cursor.");
        int capacity = process.CommonPacketWriteCapacity;
        if (batch.Writes.IsDefault || batch.CommonAllocationBytes < 0 || batch.EffectAllocationBytes < 0 || (batch.CommonAllocationBytes & 3) != 0 || (batch.EffectAllocationBytes & 3) != 0 || common > capacity - batch.CommonAllocationBytes || draw > NativeBattleEffectArena.Length - batch.EffectAllocationBytes)
        {
            throw new InvalidDataException("Native6C6770 final physical packet cursors leave their actual allocated banks.");
        }
        foreach (NativeBattleQuezacotlSkinWrite write in batch.Writes)
        {
            NativeBattleGfGeometryAttempt attempt = write.Attempt;
            if (attempt.ByteOffsetRelative < 0 || attempt.Data.IsDefaultOrEmpty || attempt.WriteMask.IsDefault || attempt.WriteMask.Length != attempt.Data.Length)
            {
                throw new InvalidDataException("Native6C6770 requires each original ordered store mask.");
            }
            if (write.Bank == NativeBattleQuezacotlSkinBank.Common)
            {
                int address = checked(common + attempt.ByteOffsetRelative);
                NativeBattleLogicalOrdering.Builder links = new(ordering);
                links.InvalidatePacketRange(new(bank, process.PacketBuffer, address), attempt.PacketLength);
                process = process.ApplyCommonWrite(address, attempt.Data, attempt.WriteMask);
                if (attempt.Primitive is { } primitive)
                {
                    if (attempt.ByteOffsetRelative > batch.CommonAllocationBytes - attempt.PacketLength || primitive.Words.Length * 4 != attempt.PacketLength)
                    {
                        throw new InvalidDataException("Native6C6770 linked common packet leaves its accepted allocation.");
                    }
                    bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
                    links.Prepend(primitive.Bucket, new(bank, process.PacketBuffer, address), primitive.Words, 0, primitive.VertexPayload4);
                    if (linked)
                    {
                        process = process.ApplyCommonWrite(address, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).ToImmutableArray(), [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known]);
                    }
                }
                ordering = links.ToImmutable();
            }
            else
            {
                bool scratch = write.Bank == NativeBattleQuezacotlSkinBank.VertexScratch;
                if (!scratch && write.Bank != NativeBattleQuezacotlSkinBank.Effect || scratch && attempt.Primitive is not null)
                {
                    throw new InvalidDataException("Native6C6770 has an invalid physical scratch or packet bank.");
                }
                if (!scratch && attempt.Primitive is not null && attempt.ByteOffsetRelative > batch.EffectAllocationBytes - attempt.PacketLength)
                {
                    throw new InvalidDataException("Native6C6770 linked effect packet leaves its accepted allocation.");
                }
                int address = checked((scratch ? 0x8D0 : draw) + attempt.ByteOffsetRelative);
                NativeBattleGfGeometryAttempt local = attempt with { ByteOffsetRelative = 0 };
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, address, ordering, new([local], attempt.Primitive is null ? 0 : attempt.PacketLength));
                memory = written.Memory;
                ordering = written.Ordering;
            }
        }
        process = process with { ByteCursor = checked(common + batch.CommonAllocationBytes) };
        process.Validate();
        return new(memory with { PrimitiveProcess = process }, effect with { DrawArenaOffset = checked(draw + batch.EffectAllocationBytes) }, ordering);
    }
}
