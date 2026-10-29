namespace Memoria.FF8.Runtime;

internal static class NativeBattleRinoa89Visuals
{
    internal static NativeBattlePilotVisualIdentity Task(NativeBattleRinoa89Frame frame, uint descriptor, int slot = 0, int primitive = 0)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == descriptor);
        return NativeBattlePilotVisualLifetimes.Identity(89, pool.Slots[slot], primitive);
    }

    internal static NativeBattlePilotVisualIdentity Row(NativeBattleEffectArena arena, int offset, int index) => NativeBattlePilotVisualLifetimes.Identity(89, arena, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset), index);
}
