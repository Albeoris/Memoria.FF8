namespace Memoria.FF8.Runtime;

internal partial class NativeBattleEffectNodeMemory
{
    internal void SynchronizePublishedStorage(NativeBattleEffectMemory published)
    {
        NativeBattleEffectArena arena = published.Arena ?? throw new NotSupportedException("Native node synchronization requires its current physical auxiliary owner.");
        (byte[] current, bool[] known) = arena.ReadSnapshot(first, bytes.Length);
        current.CopyTo(bytes, 0);
        for (int index = 0; index < unknown.Length; index++)
        {
            unknown[index] = !known[index];
        }
        memory = published;
        scratch = published.NodeScratch2793E30;
        dirty = false;
    }
}
