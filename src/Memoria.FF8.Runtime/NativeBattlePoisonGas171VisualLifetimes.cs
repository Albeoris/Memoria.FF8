using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattlePoisonGas171VisualLifetimes
{
    private static readonly ConditionalWeakTable<object, long[]> rows = new();

    private static int Index(uint bank, int row) => bank switch
    {
        0x23AE088 => row,
        0x23AD728 => checked(100 + row),
        _ => throw new InvalidDataException("Poison Gas presentation leaves its actual particle bank.")
    };

    internal static void Allocated(NativeBattleModulePoolTask task, uint bank, int row)
    {
        rows.GetValue(task.RequireOwner(), _ => new long[200])[Index(bank, row)]++;
    }

    internal static NativeBattlePilotVisualIdentity Identity(NativeBattleModulePoolTask task, uint bank, int row, int kind, uint source)
    {
        return NativeBattlePilotVisualLifetimes.Identity(171, task, checked(kind * 100 + row)) with { ResourceGeneration = unchecked(((long)source << 32) ^ rows.GetValue(task.RequireOwner(), _ => new long[200])[Index(bank, row)]) };
    }
}
