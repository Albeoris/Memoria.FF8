namespace Memoria.FF8.Runtime;

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        if (effect.Binding.Entry != 0x6391D0)
        {
            throw new InvalidDataException("Native No Mercy completion requires its original entered module.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools.Single(current => current.DescriptorAddress == Root);
        long delta = (long)address - pool.BankAddress - 14;
        if (delta < 0 || delta % pool.Stride != 0 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new InvalidDataException("Native No Mercy completion requires the original root task BYTE E.");
        }
        int slot = checked((int)(delta / pool.Stride));
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if ((task.Flags & 1) == 0 || task.Callback != 0x63E6F0)
        {
            throw new InvalidDataException("Native No Mercy completion requires its live texture task.");
        }
        task = task.WriteWord(14, (ushort)((task.ReadWord(14) & 0xFF00) | value));
        pool = pool.WriteSlot(slot, task);
        return effect with { Pools = effect.Pools.SetItem(effect.Pools.IndexOf(effect.Pools.Single(current => current.DescriptorAddress == Root)), pool) };
    }
}
