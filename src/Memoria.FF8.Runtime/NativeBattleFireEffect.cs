namespace Memoria.FF8.Runtime;

/// <summary>The original6298A0 physical pool setup and62A610 scheduler, independent of its unported draw callbacks.</summary>
public static partial class NativeBattleFireEffect
{
    public static NativeBattleOriginalEffectState InitializePools(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        effect.Validate();
        packets.Validate();
        if (effect.Binding.Entry != 0x6298A0)
        {
            throw new ArgumentException("Native Fire pools require the actual6298A0 module entry.", nameof(effect));
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native Fire requires the50AFC0 parameter block.");
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x24CA828, 0x24CA818, 16, 1);
        (main, int mainSlot) = main.Append(0x62A610);
        main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x24CA838, 0x24CA848, 36, 150);
        bool multiple = header.ReadByte(16) > 1;
        (children, int childSlot) = children.Append(multiple ? 0x629990u : 0x62A380u);
        NativeBattleModulePoolTask child = children.Slots[childSlot].WriteWord(12, 0).WriteWord(14, 0).WriteWord(34, 0);
        if (!multiple)
        {
            int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Fire requires its actual physical contact pointer even for a zero-count header.");
            child = child.WriteWord(32, packets.ReadContact(first).Primary.ActorIndex);
        }
        children = children.WriteSlot(childSlot, child);
        return effect with { Pools = [main, children], ReturnedDescriptor = main.DescriptorAddress, FireModelCompletion = System.Collections.Immutable.ImmutableArray.Create(1, 1, 1, 1, 1, 1, 1) };
    }

    public static NativeBattleOriginalEffectState Advance(NativeBattleOriginalEffectState effect, Func<NativeBattleEffectTaskPool, int, uint, NativeBattleEffectTaskCallback> childCallback)
    {
        effect.Validate();
        if (effect.ReturnedDescriptor == 0)
        {
            return effect;
        }
        if (effect.Binding.Entry != 0x6298A0 || effect.ReturnedDescriptor != 0x24CA828 || effect.Pools.Length != 2 || effect.Pools[0].DescriptorAddress != 0x24CA828 || effect.Pools[0].BankAddress != 0x24CA818 || effect.Pools[0].Stride != 16 || effect.Pools[0].Slots.Length != 1 || effect.Pools[1].DescriptorAddress != 0x24CA838 || effect.Pools[1].BankAddress != 0x24CA848 || effect.Pools[1].Stride != 36 || effect.Pools[1].Slots.Length != 150)
        {
            throw new InvalidDataException("Native62A610 requires its original1x16 and150x36 physical task banks.");
        }
        NativeBattleEffectTaskPool children = effect.Pools[1];
        uint? drawBuffer = effect.FireDrawBuffer;
        NativeBattleEffectTaskStep main = effect.Pools[0].Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x62A610)
            {
                throw new NotSupportedException($"Native Fire main callback{task.Callback:X} is unsupported.");
            }
            ushort counter = task.ReadWord(12);
            uint buffer = (counter & 1) != 0 ? 0x24D5D6Cu : 0x24CBD6Cu;
            drawBuffer = buffer;
            NativeBattleEffectTaskStep step = children.Advance((bank, index) => childCallback(bank, index, buffer));
            children = step.Pool;
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return new(pool.WriteSlot(slot, task), step.Survivors == 0 ? 2 : 0);
        });
        return effect with { Pools = [main.Pool, children], FireDrawBuffer = drawBuffer, ReturnedDescriptor = main.Survivors == 0 ? 0 : main.Pool.DescriptorAddress };
    }
}
