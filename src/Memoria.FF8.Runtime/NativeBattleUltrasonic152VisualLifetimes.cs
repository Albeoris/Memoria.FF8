using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleUltrasonic152VisualLifetimes
{
    private static readonly ConditionalWeakTable<object, long[]> generations = new();

    internal static void Allocated(NativeBattleModulePoolTask task, int row)
    {
        long[] rows = generations.GetValue(task.RequireOwner(), _ => new long[100]);
        rows[row]++;
    }

    internal static NativeBattlePilotVisualIdentity Identity(NativeBattleModulePoolTask task, int row, uint type)
    {
        long[] rows = generations.GetValue(task.RequireOwner(), _ => new long[100]);
        return NativeBattlePilotVisualLifetimes.Identity(152, task, checked((int)type * 100 + row)) with { ResourceGeneration = rows[row] };
    }
}
