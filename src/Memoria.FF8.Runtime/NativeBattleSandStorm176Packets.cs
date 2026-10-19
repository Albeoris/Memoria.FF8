namespace Memoria.FF8.Runtime;

public static class NativeBattleSandStorm176Packets
{
    public static NativeBattleRegionalPacketWrite WriteFunnel(NativeBattleEffectArena module, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        if (batch.AllocationBytes != 0)
        {
            throw new InvalidDataException("Native Sand Storm funnel stores target persistent slots without allocating a packet cursor.");
        }
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            NativeBattleGfGeometryAttempt fixedSlot = attempt with { ByteOffsetRelative = 0 };
            NativeBattleGfGeometryBatch single = new([fixedSlot], attempt.Primitive is null ? 0 : attempt.PacketLength);
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(module, "2368580", attempt.ByteOffsetRelative, NativeBattleSandStorm176Controller.ModuleLength, ordering, single);
            module = written.Arena;
            ordering = written.Ordering;
        }
        return new(module, ordering, 0);
    }
}
