using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect70Controller
{
    public static NativeBattleEffect70Frame Initialize(NativeBattleEffect70Frame frame, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect70 requires its previous physical task banks to be released.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect70 requires its executed current TIM initializer.");
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, checked(0x20DFAB8u + (uint)offset)).WriteDword(ArenaGlobal, 0x20DFAB8).WriteDword(ParametersGlobal, 0x1D99A78).WriteModelAddress(CasterGlobal, Parameters(frame).Source, inputs.ReadPackets(frame).ModelAliases) };
        frame = inputs.Execute(frame, new NativeBattleEffect70UploadTim(frame.State.ReadDword(InitializerGlobal)));
        NativeBattleCureFamilyPool shape = Pools[0];
        NativeBattleEffectTaskPool root = PreparePool(shape, frame.State.ReadRootBank());
        (root, int slot) = root.Append(0x6DD970);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0));
        return frame with { State = frame.State.DetachRootBank(), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleEffect70Frame Advance(NativeBattleEffect70Frame frame, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 3) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect70 requires its exclusive root and current COMMON task banks.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool shape = PoolShape(frame, index);
            pool.Validate();
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect70 task bank differs from its original physical descriptor.");
            }
        }
        (frame, int survivors) = Scan(frame, 0, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.Head is not null)
            {
                throw new InvalidDataException("Effect70 returned while a native task list is still reachable.");
            }
            if (index == 0)
            {
                frame = frame with { State = frame.State.AttachRootBank(NativeBattleEffectTaskMemory.CaptureBank(pool)) };
            }
            else if (NativeBattleEffectTaskMemory.ArenaOffset(pool) is int offset)
            {
                NativeBattleEffectArena arena = NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectTaskMemory.CaptureBank(pool), 0, frame.Memory.Arena!, offset, pool.Stride * pool.Slots.Length);
                frame = frame with { Memory = frame.Memory with { Arena = arena } };
            }
        }
        return frame with { State = frame.State with { ExtendedRecipients = null }, Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static NativeBattleEffect70Step AdvanceTask(NativeBattleEffect70Frame frame, uint address, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs) => Read(frame, address).Callback switch
    {
        0x6DD970 => AdvanceRoot(frame, address, resources, inputs),
        0x6DDD50 => AdvanceMain(frame, address, resources, inputs),
        0x6DFF00 => AdvanceReturn(frame, address, resources, inputs),
        0x6E0100 => AdvanceLaunch(frame, address, resources, inputs),
        0x6E02E0 => AdvanceGround(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect70 reached an unsupported original task callback.")
    };

    public static NativeBattleEffect70Frame InitializeCommonPool(NativeBattleEffect70Frame frame, int index)
    {
        if (index is < 1 or > 2 || index > frame.Effect.Pools.Length)
        {
            throw new InvalidDataException("Effect70 COMMON pool initialization changed its native descriptor order.");
        }
        NativeBattleCureFamilyPool shape = PoolShape(frame, index);
        int bytes = shape.Stride * shape.Capacity;
        NativeBattleEffectArena retained;
        if (index < frame.Effect.Pools.Length)
        {
            retained = NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools[index]);
        }
        else
        {
            retained = index == 2 && frame.State.ExtendedRecipients is not null
                ? NativeBattleEffectArena.Clear().MarkUnknown(0, bytes)
                : ReadBytes(frame, shape.Bank, bytes);
        }
        NativeBattleEffectTaskPool pool = PreparePool(shape, retained);
        ImmutableArray<NativeBattleEffectTaskPool> pools = index == frame.Effect.Pools.Length ? frame.Effect.Pools.Add(pool) : frame.Effect.Pools.SetItem(index, pool);
        return frame with { Effect = frame.Effect with { Pools = pools }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]) };
    }

    private static NativeBattleCureFamilyPool PoolShape(NativeBattleEffect70Frame frame, int index)
    {
        NativeBattleCureFamilyPool shape = Pools[index];
        return index == 2 && frame.State.ExtendedRecipients is { } targets
            ? shape with { Bank = targets.SecondaryBank, Capacity = targets.SecondaryCapacity }
            : shape;
    }

    public static (NativeBattleEffect70Frame Frame, uint Address) Append(NativeBattleEffect70Frame frame, int index, uint callback)
    {
        (NativeBattleEffectTaskPool pool, int slot) = frame.Effect.Pools[index].Append(callback);
        return (frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } }, checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffect70Frame ClearSecondaryLinks(NativeBattleEffect70Frame frame)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[2].UpdateLinks(null, null);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, pool) } };
    }

    private static NativeBattleEffectTaskPool PreparePool(NativeBattleCureFamilyPool shape, NativeBattleEffectArena retained)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
        for (int index = 0; index < pool.Slots.Length; index++)
        {
            pool = pool.WriteSlot(index, CopyTask(pool.Slots[index], 12, Slice(retained, index * pool.Stride + 12, pool.Stride - 12), pool.Stride - 12));
        }
        return pool;
    }

    private static (NativeBattleEffect70Frame Frame, int Survivors) Scan(NativeBattleEffect70Frame frame, int index, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        NativeBattleEffect70Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(index, pool) } };
            NativeBattleEffect70Step advanced = AdvanceTask(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            current = advanced.Frame;
            return new(current.Effect.Pools[index], advanced.Result);
        });
        return (current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(index, step.Pool) } }, step.Survivors);
    }

    internal static BattleModelOwner ReadRecipient(NativeBattleEffect70Frame frame, uint address)
    {
        if (frame.State.ExtendedRecipients is { } targets && Contains(targets.Address, targets.Length, address, 4))
        {
            return targets.Records.ReadExecutionReference<BattleExecutionReference.Model>(checked((int)(address - targets.Address)), 4).Owner;
        }
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (!Contains(pool.BankAddress, pool.Stride * pool.Slots.Length, address, 4))
            {
                continue;
            }
            int offset = checked((int)(address - pool.BankAddress));
            return pool.Slots[offset / pool.Stride].ReadExecutionReference<BattleExecutionReference.Model>(offset % pool.Stride, 4).Owner;
        }
        throw new InvalidDataException("Effect70 recipient record is absent from its task bank.");
    }

    private static void RequireBinding(NativeBattleEffect70Frame frame)
    {
        if (frame.Effect.RequestedEffect != 70 || frame.Effect.Binding.Entry != 0x6DD8D0 || frame.Effect.Binding.Initializer != 0x6DD8E0)
        {
            throw new InvalidDataException("Effect70 requires its original selected entry and initializer.");
        }
    }
}
