namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSigh46PacketStep(NativeBattleEffectMemory Memory, NativeBattleSigh46State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleSigh46PacketDelivery
{
    public static NativeBattleSigh46PacketStep Deliver(NativeBattleEffectMemory memory, NativeBattleSigh46State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags)
    {
        if (cursorGlobal is not (NativeBattleSigh46Profile.PrimaryCursorGlobal or NativeBattleSigh46Profile.SecondaryCursorGlobal))
        {
            throw new InvalidDataException("Sigh packets require one of their actual cursor globals.");
        }
        uint address = state.ReadDword(cursorGlobal);
        if (address < NativeBattleEffectNodeMemory.ArenaAddress || address - NativeBattleEffectNodeMemory.ArenaAddress > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Sigh packet cursor leaves its loaded TIM arena.");
        }
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), ordering, batch, flags);
        return new(written.Memory, state.WriteDword(cursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), written.Ordering);
    }
}
