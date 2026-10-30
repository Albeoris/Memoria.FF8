using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleCloudBombVisualLifetimes
{
    private static readonly ConditionalWeakTable<object, long[]> rows = new();

    internal static void Allocated(NativeBattleModulePoolTask task, int row)
    {
        rows.GetValue(task.RequireOwner(), _ => new long[150])[row]++;
    }

    internal static NativeBattlePilotVisualIdentity Identity(ushort effect, NativeBattleModulePoolTask task, int row, uint source)
    {
        return NativeBattlePilotVisualLifetimes.Identity(effect, task, row) with { ResourceGeneration = unchecked(((long)source << 32) ^ rows.GetValue(task.RequireOwner(), _ => new long[150])[row]) };
    }
}
