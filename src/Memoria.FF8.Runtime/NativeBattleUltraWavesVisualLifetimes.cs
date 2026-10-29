using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleUltraWavesVisualLifetimes
{
    private static readonly ConditionalWeakTable<object, long[]> rows = new();

    internal static void Allocated(NativeBattleModulePoolTask task, int row)
    {
        rows.GetValue(task.RequireOwner(), _ => new long[100])[row]++;
    }

    internal static NativeBattlePilotVisualIdentity Identity(NativeBattlePilotVisualIdentity owner, NativeBattleModulePoolTask task, int row)
    {
        return owner with { Primitive = unchecked(owner.Primitive * 397 + row), ResourceGeneration = rows.GetValue(task.RequireOwner(), _ => new long[100])[row] };
    }
}
