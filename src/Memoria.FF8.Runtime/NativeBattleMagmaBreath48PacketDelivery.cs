namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMagmaBreath48PacketStep(NativeBattleEffectMemory Memory, NativeBattleMagmaBreath48State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleMagmaBreath48PacketDelivery
{
    public static NativeBattleMagmaBreath48PacketStep Deliver(NativeBattleEffectMemory memory, NativeBattleMagmaBreath48State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags)
    {
        int cursor = Cursor(state, cursorGlobal);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, cursor, ordering, batch, flags);
        return new(written.Memory, state.WriteDword(cursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), written.Ordering);
    }

    public static NativeBattleMagmaBreath48PacketStep Submit(NativeBattleEffectMemory memory, NativeBattleMagmaBreath48State state, NativeBattleLogicalOrdering ordering, NativeBattleModulePacketBatch batch)
    {
        const uint cursorGlobal = 0x26481B8;
        int cursor = Cursor(state, cursorGlobal);
        NativeBattleGfGeometryBatch captured = batch.CapturedWrites ?? throw new NotSupportedException("Magma Breath color packets require their original ordered byte stores.");
        if (batch.Primitives.IsDefault || captured.Attempts.IsDefault || captured.Attempts.Length != batch.Primitives.Length || captured.AllocationBytes != batch.AllocationBytes || batch.AllocationBytes < 0 || (batch.AllocationBytes & 3) != 0 || cursor > NativeBattleEffectArena.Length - batch.AllocationBytes)
        {
            throw new InvalidDataException("Magma Breath packet construction requires its complete bounded mixed-insertion batch.");
        }
        int expected = 0;
        for (int index = 0; index < captured.Attempts.Length; index++)
        {
            NativeBattleGfGeometryAttempt attempt = captured.Attempts[index];
            NativeBattleAllocatedPrimitive allocation = batch.Primitives[index];
            if (attempt.ByteOffsetRelative != expected || allocation.ByteOffset != expected || attempt.Primitive is not { } primitive || allocation.Primitive.Bucket != primitive.Bucket || !allocation.Primitive.Words.SequenceEqual(primitive.Words) || !allocation.Primitive.VertexPayload4.SequenceEqual(primitive.VertexPayload4) || expected > batch.AllocationBytes - attempt.PacketLength)
            {
                throw new InvalidDataException("Magma Breath mixed packets must retain their native contiguous insertion order and individual flags.");
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, checked(cursor + expected), ordering, new([attempt with { ByteOffsetRelative = 0 }], attempt.PacketLength), allocation.Flags20);
            memory = written.Memory;
            ordering = written.Ordering;
            expected = checked(expected + attempt.PacketLength);
        }
        if (expected != batch.AllocationBytes)
        {
            throw new InvalidDataException("Magma Breath packet cursor differs from its native written allocation.");
        }
        return new(memory, state.WriteDword(cursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)(cursor + expected))), ordering);
    }

    private static int Cursor(NativeBattleMagmaBreath48State state, uint address)
    {
        if (address is not (0x26481B8 or 0x26481D4))
        {
            throw new InvalidDataException("Magma Breath packet writes require one of its two physical cursor globals.");
        }
        uint cursor = state.ReadDword(address);
        if (cursor < NativeBattleEffectNodeMemory.ArenaAddress || cursor - NativeBattleEffectNodeMemory.ArenaAddress > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Magma Breath packet cursor leaves its loaded TIM arena.");
        }
        return checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress));
    }
}
