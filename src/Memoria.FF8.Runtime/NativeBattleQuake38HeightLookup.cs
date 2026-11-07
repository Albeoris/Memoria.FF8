using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38HeightLookup
{
    public static short Read8D9C76(NativeBattleQuake38State state, ImmutableArray<NativeBattleEffectTaskPool> pools, byte zone)
    {
        uint address = checked(0x278C912u + (uint)zone * 16);
        if (zone <= 10)
        {
            return state.Word(address);
        }
        if (!pools.IsDefault)
        {
            foreach (NativeBattleEffectTaskPool pool in pools)
            {
                if (pool.BankAddress != 0x278C9B8)
                {
                    continue;
                }
                if (pool.DescriptorAddress != 0x278E538 || pool.Stride != 176 || pool.Slots.Length != 40)
                {
                    throw new InvalidDataException("Native Quake height lookup requires its original debris task bank dimensions.");
                }
                return NativeBattleEffectTaskMemory.CaptureBank(pool).ReadInt16(checked((int)(address - pool.BankAddress)));
            }
        }
        throw new NotSupportedException("Native Quake height lookup requires its current physical debris task bank.");
    }
}
