using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStare45PacketStep(NativeBattleEffectMemory Memory, NativeBattleStare45State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleStare45PacketDelivery
{
    public static NativeBattleStare45PacketStep Deliver(NativeBattleEffectMemory memory, NativeBattleStare45State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags)
    {
        if (cursorGlobal != 0x1D8E054)
        {
            throw new InvalidDataException("Stare drawing requires its actual COMMON primitive cursor.");
        }
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Stare requires its current COMMON primitive process.");
        process.Validate();
        int cursor = process.ByteCursor ?? throw new NotSupportedException("Stare requires its current COMMON cursor.");
        if (batch.Attempts.IsDefault || batch.AllocationBytes < 0 || (batch.AllocationBytes & 3) != 0 || cursor < 0 || cursor > process.CommonPacketWriteCapacity - batch.AllocationBytes)
        {
            throw new InvalidDataException("Stare packet allocation leaves its actual COMMON owner.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = batch.Attempts.ToBuilder();
        Dictionary<int, (byte Value, bool Known)> observed = [];
        for (int index = 0; index < attempts.Count; index++)
        {
            NativeBattleGfGeometryAttempt attempt = attempts[index];
            if (attempt.Data.IsDefaultOrEmpty || attempt.WriteMask.IsDefault || attempt.WriteMask.Length != attempt.Data.Length || attempt.ByteOffsetRelative < 0 || (attempt.ByteOffsetRelative & 3) != 0 || (attempt.PacketLength & 3) != 0 || attempt.ByteOffsetRelative > process.CommonPacketWriteCapacity - cursor - attempt.PacketLength)
            {
                throw new InvalidDataException("Stare requires bounded ordered native packet stores.");
            }
            int address = checked(cursor + attempt.ByteOffsetRelative);
            for (int offset = 0; offset < attempt.PacketLength; offset++)
            {
                switch (attempt.WriteMask[offset])
                {
                    case NativeBattleGfPacketByteState.Known:
                        observed[address + offset] = (attempt.Data[offset], true);
                        break;
                    case NativeBattleGfPacketByteState.Unknown:
                        observed[address + offset] = (attempt.Data[offset], false);
                        break;
                    case NativeBattleGfPacketByteState.Untouched:
                        break;
                    default:
                        throw new InvalidDataException("Stare packet has an unsupported store mask.");
                }
            }
            if (attempt.Primitive is not { } primitive)
            {
                continue;
            }
            if (primitive.Words.IsDefaultOrEmpty || primitive.Words.Length * 4 > attempt.PacketLength)
            {
                throw new InvalidDataException("Stare logical packet leaves its physical stores.");
            }
            byte[] bytes = new byte[primitive.Words.Length * 4];
            for (int word = 0; word < primitive.Words.Length; word++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), primitive.Words[word]);
            }
            for (int offset = 4; offset < bytes.Length; offset++)
            {
                bytes[offset] = attempt.WriteMask[offset] switch
                {
                    NativeBattleGfPacketByteState.Known => attempt.Data[offset],
                    NativeBattleGfPacketByteState.Untouched => ReadObserved(address + offset),
                    NativeBattleGfPacketByteState.Unknown => throw new NotSupportedException("Stare command consumes an unobserved packet byte."),
                    _ => throw new InvalidDataException("Stare packet has an unsupported store mask.")
                };
            }
            ImmutableArray<uint> words = Enumerable.Range(0, primitive.Words.Length).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(word * 4))).ToImmutableArray();
            attempts[index] = attempt with { Primitive = primitive with { Words = words } };
        }
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, ordering, batch with { Attempts = attempts.ToImmutable() }, flags);
        return new(memory with { PrimitiveProcess = written.Process }, state, written.Ordering);

        byte ReadObserved(int address)
        {
            if (observed.TryGetValue(address, out var value))
            {
                return value.Known ? value.Value : throw new NotSupportedException("Stare command consumes an unobserved preceding packet store.");
            }
            return process.ReadCommonPacket(address, 1)[0];
        }
    }
}
