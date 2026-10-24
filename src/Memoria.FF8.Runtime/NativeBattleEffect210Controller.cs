using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Controller
{
    public static NativeBattleEffect210Frame Initialize(NativeBattleEffect210Frame frame, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect210 entry requires its inactive banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect210 requires its current parameters.");
        frame = inputs.Execute(frame, new NativeBattleEffect210UploadTim505E30(TimAddress));
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, unchecked(0x1D972C0u + (uint)(parameters.Actor78 * 156))) };
        NativeBattleEffectTaskPool root = InitializePayload(Pools[0], frame.State.ReadBank(0));
        (root, int slot) = root.Append(RootCallback);
        root = NativeBattleEffectTaskMemory.WriteBankSpan(root, slot * root.Stride + 12, NativeBattleEffectArena.Clear(), 4);
        frame = frame with { State = frame.State.DetachBank(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor } };
        return inputs.Execute(frame, new NativeBattleEffect210RegisterCamera5099A0(CameraAddress));
    }

    private static NativeBattleEffectTaskPool InitializePayload(NativeBattleCureFamilyPool shape, NativeBattleEffectArena bank)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int offset = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(bank, offset, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return pool;
    }

    public static NativeBattleEffect210Frame InitializeCommonPool(NativeBattleEffect210Frame frame, uint descriptor)
    {
        if (frame.Effect.Pools.Any(pool => pool.DescriptorAddress == descriptor))
        {
            frame = DetachCommonPool(frame, descriptor, false);
        }
        NativeBattleCureFamilyPool relative = CommonPools.Single(shape => shape.Descriptor == descriptor);
        NativeBattleCureFamilyPool shape = relative with { Bank = checked(frame.State.ReadDword(ArenaGlobal) + relative.Bank) };
        if (frame.Effect.Pools.Any(pool => (ulong)shape.Bank < (ulong)pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (ulong)pool.BankAddress < (ulong)shape.Bank + (uint)(shape.Stride * shape.Capacity)))
        {
            throw new InvalidDataException("Effect210 COMMON pool initialization overlaps another active physical owner.");
        }
        NativeBattleEffectTaskPool current = InitializePayload(shape, ReadBytes(frame, shape.Bank, shape.Stride * shape.Capacity));
        return frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [current]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(current) }, State = frame.State.DetachCommonQueue(descriptor) };
    }

    public static NativeBattleEffect210Frame DetachCommonPool(NativeBattleEffect210Frame frame, uint descriptor, bool clearQueue)
    {
        NativeBattleEffectTaskPool? pool = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == descriptor);
        if (pool is null)
        {
            return clearQueue ? frame with { State = frame.State.WriteView(descriptor, NativeBattleEffectArena.Clear(), 8) } : frame;
        }
        NativeBattleEffectArena queue = clearQueue ? NativeBattleEffectArena.Clear() : ReadBytes(frame, descriptor, 8);
        NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
        return frame with { Memory = transfer.Memory, Effect = transfer.Effect, State = frame.State.AttachCommonQueue(descriptor, queue) };
    }

    public static NativeBattleEffect210Frame Advance(NativeBattleEffect210Frame frame, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        (frame, int survivors) = AdvancePool(frame, RootDescriptor, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            frame = DetachCommonPool(frame, shape.Descriptor, false);
        }
        NativeBattleEffectTaskPool root = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == RootDescriptor);
        return frame with { State = frame.State.AttachBank(0, NativeBattleEffectTaskMemory.CaptureBank(root)), Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static (NativeBattleEffect210Frame Frame, int Survivors) AdvancePool(NativeBattleEffect210Frame frame, uint descriptor, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        int index = Enumerable.Range(0, frame.Effect.Pools.Length).SingleOrDefault(index => frame.Effect.Pools[index].DescriptorAddress == descriptor, -1);
        if (index < 0)
        {
            NativeBattleEffectArena queue = frame.State.ReadCommonQueue(descriptor);
            if (BitConverter.ToUInt32(queue.Read(0, 4)) != 0 || BitConverter.ToUInt32(queue.Read(4, 4)) != 0)
            {
                throw new NotSupportedException("Effect210 inactive queue contains a live physical task.");
            }
            return (frame, 0);
        }
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect210Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[index], advanced.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect210Step AdvanceTask(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot6F6890(frame, address, resources, inputs),
        MainCallback => AdvanceMain6F7310(frame, address, resources, inputs),
        0x6F9AA0 => NativeBattleEffect210Programme.Advance9AA0(frame, address, resources, inputs),
        _ => NativeBattleEffect210Lower.Advance(frame, address, resources, inputs)
    };

    public static int Actor(uint pointer)
    {
        long offset = (long)pointer - 0x1D972C0;
        return offset >= 0 && offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Effect210 requires an actual physical model pointer.");
    }

    private static void RequireBinding(NativeBattleEffect210Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect210 requires its selected entry.");
        }
    }
}
