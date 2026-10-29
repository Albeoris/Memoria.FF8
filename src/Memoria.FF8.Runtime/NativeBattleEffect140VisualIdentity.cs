namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect140VisualIdentity
{
    internal static NativeBattlePilotVisualIdentity Workspace(NativeBattleEffect140Frame frame, uint workspace, uint source, int primitive)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(workspace - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length))
            {
                return NativeBattlePilotVisualLifetimes.Identity(140, pool.Slots[checked((int)(offset / pool.Stride))], primitive) with { ResourceGeneration = ((long)source << 32) | (offset % pool.Stride) };
            }
        }
        throw new InvalidDataException("Effect140 presentation workspace leaves its actual allocated task.");
    }
}
