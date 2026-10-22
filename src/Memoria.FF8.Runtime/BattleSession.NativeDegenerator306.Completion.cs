namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeDegenerator306Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleDegenerator306Profile.RootBank);
        if (delta >= 32 || delta % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleDegenerator306Profile.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = Complete(detached) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleDegenerator306Profile.Entry } current ? current : throw new NotSupportedException("Degenerator completion requires its retained physical root task.");
            updated = graphics with { OriginalEffect = Complete(effect) };
        }
        return true;

        NativeBattleOriginalEffectState Complete(NativeBattleOriginalEffectState effect)
        {
            int poolIndex = -1;
            for (int index = 0; index < effect.Pools.Length; index++)
            {
                if (effect.Pools[index].BankAddress == NativeBattleDegenerator306Profile.RootBank)
                {
                    poolIndex = index;
                    break;
                }
            }
            if (poolIndex < 0 || effect.Pools[poolIndex].Stride != 16 || effect.Pools[poolIndex].Slots.Length != 2)
            {
                throw new InvalidDataException("Degenerator completion requires its actual two-slot root pool.");
            }
            NativeBattleEffectTaskPool pool = effect.Pools[poolIndex];
            int slot = checked((int)(delta / 16));
            return effect with { Pools = effect.Pools.SetItem(poolIndex, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(14, value)) }) };
        }
    }
}
