namespace Memoria.FF8.Runtime;

/// <summary>The current actor row refreshes its clone descriptors at each native callback boundary.</summary>
internal static class NativeBattleCinematicActorResourceDescriptors
{
    internal static NativeBattleEffectArena Refresh(NativeBattleEffectArena arena, NativeBattleModelCpuResources cpu, NativeBattleModelCpuResourceRow row, uint clone)
    {
        arena = arena.MarkUnknown(NativeBattleWaterExecutionFields.ArenaOffset(clone + 0x10, 12), 12);
        foreach (int field in new[] { 12, 16, 20, 24 })
        {
            NativeBattleResourcePointer pointer = new(row.Require(field), null, row.Lengths.TryGetValue(field, out int length) ? length : null);
            arena = arena.WriteResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(clone + 0x30u + (uint)(field - 12), 4), pointer.Pin(cpu).WithNativeAddressEncoding());
        }
        return arena;
    }
}
