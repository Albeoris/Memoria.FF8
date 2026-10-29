using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleGastricJuice169VisualLifetimes
{
    private static readonly ConditionalWeakTable<object, long[]> generations = new();

    internal static void Allocated(NativeBattleModulePoolTask task, int row)
    {
        generations.GetValue(task.RequireOwner(), _ => new long[100])[row]++;
    }

    internal static NativeBattlePilotVisualIdentity Identity(NativeBattleModulePoolTask task, int row, bool splash)
    {
        long generation = generations.GetValue(task.RequireOwner(), _ => new long[100])[row];
        return NativeBattlePilotVisualLifetimes.Identity(169, task, (splash ? 100 : 0) + row) with { ResourceGeneration = generation };
    }
}
