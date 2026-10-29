using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleSleep145Presentation
{
    private static readonly ConditionalWeakTable<NativeBattleTaskBankOwner, Dictionary<int, long>> generations = new();
    private static long nextGeneration;

    internal static void Allocated(NativeBattleModulePoolTask root, int slot)
    {
        generations.GetOrCreateValue(root.RequireOwner())[slot] = Interlocked.Increment(ref nextGeneration);
    }

    internal static NativeBattlePilotVisualIdentity Identity(NativeBattleModulePoolTask root, int slot)
    {
        Dictionary<int, long> rows = generations.GetOrCreateValue(root.RequireOwner());
        if (!rows.TryGetValue(slot, out long generation))
        {
            Allocated(root, slot);
            generation = rows[slot];
        }
        return NativeBattlePilotVisualLifetimes.Identity(145, root, slot) with { ResourceGeneration = generation };
    }
}
