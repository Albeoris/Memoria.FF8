namespace Memoria.FF8.Runtime;

/// <summary>Ordered packet stores in the existing571B70 work bank.</summary>
public static class NativeBattleGuardianWorkPacketArena
{
    public const string PacketBank = "209FAB8";

    public static NativeBattleEffectPacketWrite Append(NativeBattleEffectMemory memory, int cursor, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, ushort flags20 = 0)
    {
        NativeBattleGuardianWorkBuffer source = memory.GuardianWorkBuffer ?? throw new NotSupportedException("Native work packets require their retained571B70 memory.");
        source.Validate();
        NativeBattleEffectArenaWriter writer = new(new NativeBattleEffectArena(source.Regions));
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        NativeBattleEffectPacketArena.AppendAttempts(writer, links, cursor, batch, flags20, bank: PacketBank, capacity: NativeBattleGuardianWorkBuffer.Length);
        return new(memory with { GuardianWorkBuffer = new(writer.Freeze().Regions) }, checked(cursor + batch.AllocationBytes), links.ToImmutable());
    }
}
