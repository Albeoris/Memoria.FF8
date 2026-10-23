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
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(process, ordering, batch, flags);
        return new(memory with { PrimitiveProcess = written.Process }, state, written.Ordering);
    }
}
