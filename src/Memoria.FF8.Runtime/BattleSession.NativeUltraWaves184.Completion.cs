namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeUltraWaves184Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint relative = unchecked(address - 0x22BD110u);
        if (relative >= 32 || relative % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5C7FC0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = Write(detached) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5C7FC0 } current ? current : throw new NotSupportedException("Native Ultra Waves texture completion lost its actual task owner.");
            updated = graphics with { OriginalEffect = Write(effect) };
        }
        return true;

        NativeBattleOriginalEffectState Write(NativeBattleOriginalEffectState effect)
        {
            NativeBattleEffectTaskPool root = effect.Pools[0];
            int slot = (int)(relative / 16);
            if (root.BankAddress != 0x22BD110 || root.Slots[slot].Callback != 0x5C9A00)
            {
                throw new InvalidDataException("Native Ultra Waves completion requires its original waiting callback.");
            }
            NativeBattleModulePoolTask task = root.Slots[slot];
            task = task.WriteByte(12 + (2), value);
            root = root.WriteSlot(slot, task);
            return effect with { Pools = effect.Pools.SetItem(0, root) };
        }
    }
}
