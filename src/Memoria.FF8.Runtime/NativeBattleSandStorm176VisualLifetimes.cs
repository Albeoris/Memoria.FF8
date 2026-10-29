using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleSandStorm176VisualLifetimes
{
    private static readonly ConditionalWeakTable<object, long[]> rows = new();

    internal static void Allocated(NativeBattleModulePoolTask task, int row)
    {
        rows.GetValue(task.RequireOwner(), _ => new long[150])[row]++;
    }

    internal static NativeBattlePilotVisualIdentity Identity(NativeBattleModulePoolTask task, int row, bool landing, uint source)
    {
        return NativeBattlePilotVisualLifetimes.Identity(176, task, checked((landing ? 150 : 0) + row)) with { ResourceGeneration = unchecked(((long)source << 32) ^ rows.GetValue(task.RequireOwner(), _ => new long[150])[row]) };
    }
}
