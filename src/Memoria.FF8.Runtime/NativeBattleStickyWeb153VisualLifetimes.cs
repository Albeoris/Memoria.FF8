using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleStickyWeb153VisualLifetimes
{
    private static readonly ConditionalWeakTable<object, long[]> rows = new();

    internal static void Allocated(NativeBattleModulePoolTask task, int row)
    {
        rows.GetValue(task.RequireOwner(), _ => new long[100])[row]++;
    }

    internal static NativeBattlePilotVisualIdentity Identity(NativeBattleModulePoolTask task, int row, int type)
    {
        return NativeBattlePilotVisualLifetimes.Identity(153, task, checked(type * 100 + row)) with { ResourceGeneration = rows.GetValue(task.RequireOwner(), _ => new long[100])[row] };
    }
}
