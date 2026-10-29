using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleSquallFinisherVisualIdentity
{
    private sealed record Birth(long TaskGeneration, long RowGeneration);
    private static readonly ConditionalWeakTable<NativeBattleTaskBankOwner, Dictionary<uint, Birth>> births = new();

    internal static void Allocated(ushort effect, NativeBattleModulePoolTask task, uint row)
    {
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(effect, task);
        births.GetOrCreateValue(task.RequireOwner())[row] = new(owner.Generation, NativeBattlePilotVisualLifetimes.EpochFor(new object()));
    }

    internal static NativeBattlePilotVisualIdentity Particle(ushort effect, NativeBattleModulePoolTask task, uint row, uint resource)
    {
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(effect, task, unchecked((int)(row * 397 + resource)));
        Dictionary<uint, Birth> rows = births.GetOrCreateValue(task.RequireOwner());
        if (!rows.TryGetValue(row, out Birth? birth) || birth.TaskGeneration != owner.Generation)
        {
            Allocated(effect, task, row);
            birth = rows[row];
        }
        return owner with { ResourceGeneration = birth.RowGeneration };
    }
}
