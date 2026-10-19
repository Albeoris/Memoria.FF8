namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191EnvironmentServices(Func<uint> ReadFlags, Func<NativeBattleDoomtrain191Frame, short, NativeBattleDoomtrain191Frame> PublishDepth, Func<NativeBattleDoomtrain191Frame, bool, NativeBattleDoomtrain191Frame> HideStage, Func<NativeBattleDoomtrain191Frame, bool, NativeBattleDoomtrain191Frame> HideTargets);

public static class NativeBattleDoomtrain191Environment
{
    public static NativeBattleDoomtrain191Frame Invoke(NativeBattleDoomtrain191Frame frame, uint address, NativeBattleDoomtrain191EnvironmentServices services)
    {
        switch (address)
        {
            case 0x63F7A0:
            case 0x63F850:
                NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
                (pool, int slot) = pool.Append(address == 0x63F7A0 ? 0x63F7D0u : 0x63F880u);
                pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0xC, 0).WriteWord(0xE, 16).WriteWord(0x18, 4096).WriteWord(0x1A, 1)) };
                int index = frame.Effect.Pools.IndexOf(frame.Effect.Pools.Single(current => current.DescriptorAddress == pool.DescriptorAddress));
                return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
            case 0x63F780:
            case 0x63F830:
                return services.HideStage(frame, address == 0x63F780);
            case 0x63F8F0:
            case 0x63F930:
                return services.HideTargets(frame, address == 0x63F930);
            case 0x5713E0:
                return services.PublishDepth(frame, 0);
            default:
                throw new NotSupportedException($"Native Doomtrain environment helper{address:X8} requires its original contract.");
        }
    }

    public static NativeBattleDoomtrain191LeafStep AdvanceFlash(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleDoomtrain191EnvironmentServices services)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback is not (0x63F7D0 or 0x63F880))
        {
            throw new NotSupportedException("Native Doomtrain stage flash requires its selected original callback.");
        }
        short age = unchecked((short)task.ReadWord(0xC));
        short duration = unchecked((short)task.ReadWord(0xE));
        short amount = unchecked((short)task.ReadWord(0x18));
        if (duration == 0)
        {
            throw new NotSupportedException("Native Doomtrain flash reached a zero signed divisor.");
        }
        int depth = unchecked((amount / duration) * age);
        bool fadeOut = task.Callback == 0x63F880;
        frame = services.PublishDepth(frame, unchecked((short)(fadeOut ? amount - depth : depth)));
        if (fadeOut && age == 0 && task.ReadWord(0x1A) != 0)
        {
            frame = services.HideStage(frame, true);
        }
        if ((services.ReadFlags() & 1) != 0)
        {
            return new(frame, 0);
        }
        ushort next = unchecked((ushort)(age + 1));
        pool = frame.Effect.Pools.Single(current => current.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0xC, next)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(frame.Effect.Pools.Single(current => current.DescriptorAddress == pool.DescriptorAddress)), pool) } };
        if (unchecked((short)next) < duration)
        {
            return new(frame, 0);
        }
        if (fadeOut)
        {
            frame = services.PublishDepth(frame, 0);
        }
        else if (task.ReadWord(0x1A) != 0)
        {
            frame = services.HideStage(frame, false);
        }
        return new(frame, 2);
    }
}
