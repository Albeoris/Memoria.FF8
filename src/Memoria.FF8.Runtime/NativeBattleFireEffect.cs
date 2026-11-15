using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The original6298A0 physical pool setup and62A610 scheduler, independent of its unported draw callbacks.</summary>
public static partial class NativeBattleFireEffect
{
    internal const uint Entry = 0x6298A0;
    private const ushort NativeChildCapacity = 150;
    internal const ushort ChildStride = 36;

    public static int RequiredChildCapacity(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        effect.Validate(packets.Roster);
        packets.Validate();
        BattleActionHeaderReference first = effect.Parameters?.Header ?? throw new InvalidDataException("Hosted Fire requires its selected action header.");
        int last = packets.ReadHeader(first).ReadRepeatModeMinusOne();
        int capacity = 0;
        for (int offset = 0; offset <= last; offset++)
        {
            NativeBattleActionHeaderSlot header = packets.ReadHeader(first.Advance(offset));
            int count = header.ReadContactCount();
            // Each emitter appends one seed per occurrence (four in the single-target presenter). A seed can append fourteen children.
            capacity = checked(capacity + 1 + 15 * (count > 1 ? count : 4));
        }
        if (capacity > ushort.MaxValue)
        {
            throw new InvalidDataException("Hosted Fire child allocation bound exceeds the task-pool slot domain.");
        }
        return Math.Max((int)NativeChildCapacity, capacity);
    }

    public static NativeBattleOriginalEffectState InitializePools(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, uint? hostedChildrenAddress = null)
    {
        effect.Validate(packets.Roster);
        packets.Validate();
        int childCapacity = hostedChildrenAddress.HasValue ? RequiredChildCapacity(effect, packets) : NativeChildCapacity;
        if (effect.Binding.Entry != Entry)
        {
            throw new ArgumentException("Native Fire pools require the actual6298A0 module entry.", nameof(effect));
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native Fire requires the50AFC0 parameter block.");
        NativeBattleActionHeaderSlot header = packets.ReadHeader(parameters.Header);
        uint childBank = 0x24CA848;
        if (hostedChildrenAddress is uint address)
        {
            if (!NativeBattlePresentationTaskStorage.IsHostedBank(address, ChildStride, childCapacity))
            {
                throw new InvalidDataException("Hosted Fire requires an owned virtual child task span.");
            }
            childBank = address;
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x24CA828, 0x24CA818, 16, 1);
        (main, int mainSlot) = main.Append(0x62A610);
        main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x24CA838, childBank, ChildStride, checked((ushort)childCapacity));
        bool multiple = header.ReadContactCount() > 1;
        (children, int childSlot) = children.Append(multiple ? 0x629990u : 0x62A380u);
        NativeBattleModulePoolTask child = children.Slots[childSlot].WriteWord(12, 0).WriteWord(14, 0).WriteWord(34, 0);
        if (!multiple)
        {
            WriteSingleRecipient(child, packets, header.FirstContact);
        }
        children = children.WriteSlot(childSlot, child);
        return effect with { Pools = [main, children], ReturnedDescriptor = main.DescriptorAddress, FireModelCompletion = packets.Roster.Owners.ToImmutableDictionary(owner => owner, _ => 1) };
    }

    public static NativeBattleOriginalEffectState Advance(NativeBattleOriginalEffectState effect, Func<NativeBattleEffectTaskPool, int, uint, NativeBattleEffectTaskCallback> childCallback)
    {
        effect.Validate();
        if (effect.ReturnedDescriptor == 0)
        {
            return effect;
        }
        if (effect.Binding.Entry != 0x6298A0 || effect.ReturnedDescriptor != 0x24CA828 || effect.Pools.Length != 2 || effect.Pools[0].DescriptorAddress != 0x24CA828 || effect.Pools[0].BankAddress != 0x24CA818 || effect.Pools[0].Stride != 16 || effect.Pools[0].Slots.Length != 1 || effect.Pools[1].DescriptorAddress != 0x24CA838 || effect.Pools[1].Stride != ChildStride)
        {
            throw new InvalidDataException("Native62A610 requires its original1x16 and150x36 physical task banks.");
        }
        NativeBattleEffectTaskPool children = effect.Pools[1];
        if (children.BankAddress == 0x24CA848
            ? children.Slots.Length != NativeChildCapacity
            : children.Slots.Length < NativeChildCapacity || !NativeBattlePresentationTaskStorage.IsHostedBank(children.BankAddress, ChildStride, children.Slots.Length))
        {
            throw new InvalidDataException("Native62A610 child task storage disagrees with its presentation owner.");
        }
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

    private static void WriteSingleRecipient(NativeBattleModulePoolTask task, NativeBattleActionPacketsState packets, BattleContactCursor first)
    {
        BattleModelOwner recipient = packets.ReadContact(first).Primary.Recipient;
        task.WriteExecutionReference(32, 2, new BattleExecutionReference.Model(recipient), packets.ModelAliases.EncodeOperand(recipient, 2));
    }
}
