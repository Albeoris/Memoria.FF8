using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleResonance49PacketStep(NativeBattleEffectMemory Memory, NativeBattleResonance49State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleResonance49PacketDelivery
{
    public const string PacketBank = "26464D8";
    private const uint StaticBase = 0x26464D8;
    private static readonly uint[] StaticPackets = [0x26472D8, 0x2647318, 0x2647820, 0x2647860];

    public static byte[] ReadStatic(NativeBattleResonance49State state, NativeBattlePrimitiveReference reference, int count)
    {
        if (reference.Buffer != 0 || reference.Bank != PacketBank || reference.ByteOffset < 0 || count < 0)
        {
            throw new InvalidDataException("Resonance environment reads require their actual physical packet reference.");
        }
        uint address = checked(StaticBase + (uint)reference.ByteOffset);
        RequireStatic(address, count);
        return state.Read(address, count);
    }

    public static NativeBattleResonance49PacketStep Store(NativeBattleEffectMemory memory, NativeBattleResonance49State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, uint destination, ushort flags)
    {
        RequireStatic(destination, batch.AllocationBytes);
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            RequireStatic(checked(destination + (uint)attempt.ByteOffsetRelative), attempt.PacketLength);
        }
        NativeBattleEffectArenaWriter writer = new(state.Globals);
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        NativeBattleEffectPacketArena.AppendAttempts(writer, links, checked((int)(destination - StaticBase)), batch, flags, bank: PacketBank);
        state = state with { Globals = writer.Freeze() };
        state.Validate();
        return new(memory, state, links.ToImmutable());
    }

    public static NativeBattleResonance49PacketStep Deliver(NativeBattleEffectMemory memory, NativeBattleResonance49State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags)
    {
        if (cursorGlobal == 0x1D8E054)
        {
            return AppendCommon(memory, state, ordering, batch.Attempts.Select(attempt => (attempt, flags)).ToImmutableArray(), batch.AllocationBytes, batch.NativeUnlinkedAllocation);
        }
        if (cursorGlobal is not (0x26464DC or 0x26465DC))
        {
            throw new InvalidDataException("Resonance effect packet cursor is not one of its actual two globals.");
        }
        uint cursor = state.ReadDword(cursorGlobal);
        if (cursor < NativeBattleEffectNodeMemory.ArenaAddress)
        {
            throw new InvalidDataException("Resonance effect cursor leaves its loaded TIM arena.");
        }
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), ordering, batch, flags);
        return new(written.Memory, state.WriteDword(cursorGlobal, checked(cursor + (uint)batch.AllocationBytes)), written.Ordering);
    }

    public static NativeBattleResonance49PacketStep ScreenPair(NativeBattleEffectMemory memory, NativeBattleResonance49State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryAttempt quad, NativeBattleGfGeometryAttempt mode)
    {
        if (quad.PacketLength != 40 || mode.PacketLength != 12 || quad.Primitive is null || mode.Primitive is null)
        {
            throw new InvalidDataException("Resonance screen emission requires its actual forty-byte quad and twelve-byte mode packet.");
        }
        return AppendCommon(memory, state, ordering, [(mode with { ByteOffsetRelative = 40 }, (ushort)0), (quad with { ByteOffsetRelative = 0 }, (ushort)1)], 52, false);
    }

    private static NativeBattleResonance49PacketStep AppendCommon(NativeBattleEffectMemory memory, NativeBattleResonance49State state, NativeBattleLogicalOrdering ordering, ImmutableArray<(NativeBattleGfGeometryAttempt Attempt, ushort Flags)> attempts, int allocationBytes, bool unlinked)
    {
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Resonance screen requires its actual common packet process.");
        process.Validate();
        int cursor = process.ByteCursor ?? throw new NotSupportedException("Resonance screen requires its current common cursor.");
        string bank = process.PacketBank ?? throw new NotSupportedException("Resonance screen requires its current common bank.");
        if (attempts.IsDefault || allocationBytes < 0 || (allocationBytes & 3) != 0 || cursor < 0 || cursor > process.CommonPacketWriteCapacity - allocationBytes)
        {
            throw new InvalidDataException("Resonance common emission leaves its physical allocation.");
        }
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        int acceptedEnd = 0;
        foreach ((NativeBattleGfGeometryAttempt attempt, ushort flags) in attempts)
        {
            if (attempt.Data.IsDefaultOrEmpty || attempt.WriteMask.IsDefault || attempt.Data.Length != attempt.WriteMask.Length || attempt.ByteOffsetRelative < 0 || (attempt.ByteOffsetRelative & 3) != 0 || (attempt.PacketLength & 3) != 0 || cursor + attempt.ByteOffsetRelative > process.CommonPacketWriteCapacity - attempt.PacketLength)
            {
                throw new InvalidDataException("Resonance common emission requires bounded ordered native stores.");
            }
            int address = checked(cursor + attempt.ByteOffsetRelative);
            NativeBattlePrimitiveReference reference = new(bank, process.PacketBuffer, address);
            for (int offset = 0; offset < attempt.PacketLength; offset++)
            {
                if (attempt.WriteMask[offset] != NativeBattleGfPacketByteState.Untouched)
                {
                    links.InvalidatePacketRange(reference with { ByteOffset = address + offset }, 1);
                }
            }
            stores.Add(new(address, attempt.Data, attempt.WriteMask));
            if (attempt.Primitive is not { } primitive)
            {
                continue;
            }
            if ((uint)primitive.Bucket >= NativeBattlePrimitiveOrdering.BattleTableLength || primitive.Words.IsDefaultOrEmpty || primitive.Words.Length < 1 + (primitive.Words[0] >> 24) || primitive.Words.Length * 4 > attempt.PacketLength || attempt.ByteOffsetRelative + attempt.PacketLength > allocationBytes)
            {
                throw new InvalidDataException("Resonance common primitive leaves its actual allocation or ordering table.");
            }
            acceptedEnd = Math.Max(acceptedEnd, attempt.ByteOffsetRelative + attempt.PacketLength);
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(primitive.Bucket, reference, RetainedCommonWords(process, address, attempt, primitive), flags, primitive.VertexPayload4);
            if (linked)
            {
                stores.Add(new(address, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).ToImmutableArray(), NativeBattleCommonPacketArena.KnownLinkMask));
            }
        }
        if (!unlinked && acceptedEnd != allocationBytes || unlinked && acceptedEnd > allocationBytes)
        {
            throw new InvalidDataException("Resonance common allocation differs from its linked and native unlinked stores.");
        }
        process = process.ApplyCommonWrites(stores.ToImmutable()) with { ByteCursor = checked(cursor + allocationBytes) };
        return new(memory with { PrimitiveProcess = process }, state, links.ToImmutable());
    }

    private static ImmutableArray<uint> RetainedCommonWords(NativeBattlePrimitiveProcess process, int address, NativeBattleGfGeometryAttempt attempt, NativeBattleGeometryPrimitive primitive)
    {
        byte[] bytes = new byte[primitive.Words.Length * 4];
        for (int index = 0; index < primitive.Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
        }
        for (int offset = 4; offset < bytes.Length; offset++)
        {
            if (attempt.WriteMask[offset] == NativeBattleGfPacketByteState.Known)
            {
                continue;
            }
            if (attempt.WriteMask[offset] == NativeBattleGfPacketByteState.Unknown)
            {
                if (!UnconsumedScreenByte(bytes, offset))
                {
                    throw new NotSupportedException("Resonance common command consumes an unobserved packet byte.");
                }
                continue;
            }
            try
            {
                bytes[offset] = process.ReadCommonPacket(address + offset, 1)[0];
            }
            catch (NotSupportedException) when (UnconsumedScreenByte(bytes, offset))
            {
                // Raw sixteen-bit screen textures consume neither RGB modulation nor a palette. Their physical bytes remain unobserved.
            }
        }
        return Enumerable.Range(0, primitive.Words.Length).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
    }

    private static bool UnconsumedScreenByte(byte[] bytes, int offset) => bytes.Length == 40 && bytes[7] is 0x2D or 0x2F && (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(22)) & 0x180) == 0x100 && offset is 4 or 5 or 6 or 14 or 15;

    private static void RequireStatic(uint address, int length)
    {
        if (length < 0 || !StaticPackets.Any(start => address >= start && (ulong)address + (uint)length <= (ulong)start + 64))
        {
            throw new InvalidDataException("Resonance static packet leaves its actual retained sixty-four-byte environment owner.");
        }
    }
}
