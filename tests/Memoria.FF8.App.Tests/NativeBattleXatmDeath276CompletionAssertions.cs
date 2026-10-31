using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleXatmDeath276CompletionAssertions
{
    internal static bool PoolsReleased(ImmutableArray<NativeBattleEffectTaskPool> pools, ImmutableArray<NativeBattleXatmDeath276Table> tables)
    {
        uint[] descriptors = [0x2508318, 0x25082A8, 0x25082B8, 0x25082C8, 0x25082D8, 0x25082E8, 0x25082F8, 0x2508308];
        if (pools.IsDefault || pools.Length != descriptors.Length || tables.IsDefault)
        {
            return false;
        }
        for (int index = 0; index < pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = pools[index];
            if (pool.DescriptorAddress != descriptors[index])
            {
                return false;
            }
            if (index != 6)
            {
                if (pool.Head is not null || pool.Tail is not null || pool.Slots.Any(task => task.Flags != 0))
                {
                    return false;
                }
                continue;
            }
            foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Flags != 0))
            {
                if (task.Flags != 1 || task.Callback != 0x685CF0)
                {
                    return false;
                }
                NativeBattleXatmDeath276Table? table = tables.SingleOrDefault(item => item.Address == task.ReadDword(16));
                if (table is null || table.Data.IsDefault || table.Data.Length < 0x16 || table.Data[0x15] == 0)
                {
                    return false;
                }
                uint tick = task.ReadDword(12);
                if (tick > int.MaxValue || tick <= table.Data[0] || tick <= table.Data[2] || tick <= table.Data[8] || table.Data[3] != 255 && tick <= table.Data[3])
                {
                    return false;
                }
            }
        }
        return true;
    }
}
