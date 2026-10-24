namespace Memoria.FF8.Runtime;

public static class NativeBattleCommonPacketDelivery
{
    public static NativeBattleCommonPacketWrite Deliver(NativeBattlePrimitiveProcess process, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, ushort flags = 0)
    {
        process.Validate();
        int cursor = process.ByteCursor ?? throw new NotSupportedException("Native COMMON delivery requires its current packet cursor.");
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, cursor, process.CommonPacketWriteCapacity, address =>
        {
            (byte[] bytes, bool[] known) = process.ReadCommonPacketSnapshot(address, 1);
            return (bytes[0], known[0]);
        });
        return NativeBattleCommonPacketArena.Append(process, ordering, resolved, flags);
    }
}
