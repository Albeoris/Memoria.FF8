namespace Memoria.FF8.Runtime;

internal static class NativeBattleDiablos325VisualGeometry
{
    internal static NativeBattlePilotVisualIdentity? Identity(NativeBattleDiablos325Frame frame, int primitive)
    {
        NativeBattleEffectTaskPool? pool = frame.Effect.Pools.FirstOrDefault(pool => pool.DescriptorAddress == NativeBattleDiablos325Construction.TimelineDescriptor) ?? frame.Effect.Pools.FirstOrDefault();
        return pool is null ? null : NativeBattlePilotVisualLifetimes.Identity(325, pool.Slots[0], primitive);
    }
}
