namespace Memoria.FF8.Runtime;

public static class NativeBattleDoomtrain191Finish
{
    public static NativeBattleDoomtrain191LeafStep Advance641E60(NativeBattleDoomtrain191Frame frame, int slot, Func<NativeBattleDoomtrain191Frame, uint, uint, NativeBattleDoomtrain191Frame> request)
    {
        NativeBattleEffectTaskPool pool = Pool(frame.Effect);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x641E60)
        {
            throw new InvalidDataException("Native Doomtrain finish requires its actual task.");
        }
        if (task.ReadWord(0xC) == 1)
        {
            uint destination = unchecked((frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain finish requires its retained arena pointer.")).ArenaAddress + 0x10000);
            frame = request(frame, destination, unchecked(pool.BankAddress + (uint)(slot * pool.Stride) + 14));
        }
        pool = Pool(frame.Effect);
        task = pool.Slots[slot];
        int result = task.ReadWord(0xE) == 0 ? 0 : 2;
        pool = pool with { Slots = pool.Slots.SetItem(slot, task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)))) };
        return new(frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame.Effect)), pool) } }, result);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = Pool(effect);
        uint relative = unchecked(address - pool.BankAddress);
        if (relative >= pool.Slots.Length * pool.Stride || relative % pool.Stride != 14)
        {
            throw new InvalidDataException("Native Doomtrain finish completion leaves its physical task WORD.");
        }
        int slot = checked((int)(relative / pool.Stride));
        NativeBattleModulePoolTask task = pool.Slots[slot];
        pool = pool with { Slots = pool.Slots.SetItem(slot, task.WriteWord(14, (ushort)((task.ReadWord(14) & 0xFF00) | value))) };
        return effect with { Pools = effect.Pools.SetItem(effect.Pools.IndexOf(Pool(effect)), pool) };
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleOriginalEffectState effect) => effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
}
