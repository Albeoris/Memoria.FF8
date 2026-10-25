using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect251Controller
{
    public static NativeBattleEffect251Frame Initialize(NativeBattleEffect251Frame frame, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.Memory.Arena is null)
        {
            throw new InvalidDataException("Effect251 requires its inactive physical banks and current COMMON owner.");
        }
        uint parameters = 0x1D99A78;
        NativeBattleEffectArena parameterBytes = inputs.ReadPhysical(frame, parameters, 8);
        uint group = Dword251(parameterBytes, 4);
        uint target = Dword251(inputs.ReadPhysical(frame, group, 12), 8);
        byte sourceActor = parameterBytes.Read(0, 1)[0];
        byte targetActor = inputs.ReadPhysical(frame, target, 1).Read(0, 1)[0];
        frame = inputs.Execute(frame, new NativeBattleEffect251UploadTim505E30(EmbeddedTimAddress));
        frame = frame with { State = frame.State.BindOriginalScriptSources(resources).WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, parameters).WriteDword(CasterGlobal, ModelAddress(sourceActor)).WriteDword(TargetGlobal, ModelAddress(targetActor)) };
        NativeBattleCureFamilyPool shape = Pools[0];
        NativeBattleEffectTaskPool root = InitializePayload(shape, frame.State.ReadBank(0));
        (root, int slot) = root.Append(RootCallback);
        root = NativeBattleEffectTaskMemory.WriteBankSpan(root, slot * shape.Stride + 12, NativeBattleEffectArena.Clear(), 4);
        frame = frame with { State = frame.State.DetachBank(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor } };
        return inputs.Execute(frame, new NativeBattleEffect251RegisterCamera5099A0(CameraAddress));
    }

    public static NativeBattleEffect251Frame Advance(NativeBattleEffect251Frame frame, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        (frame, int survivors) = AdvanceDescriptor(frame, RootDescriptor, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            if (frame.Effect.Pools.Any(pool => pool.DescriptorAddress == shape.Descriptor))
            {
                frame = RetireCommonPool(frame, shape.Descriptor);
            }
        }
        NativeBattleEffectTaskPool root = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == RootDescriptor);
        frame = frame with { State = frame.State.AttachBank(0, NativeBattleEffectTaskMemory.CaptureBank(root)), Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
        return frame;
    }

    public static (NativeBattleEffect251Frame Frame, int Survivors) AdvanceDescriptor(NativeBattleEffect251Frame frame, uint descriptor, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        int index = Enumerable.Range(0, frame.Effect.Pools.Length).SingleOrDefault(index => frame.Effect.Pools[index].DescriptorAddress == descriptor, -1);
        if (index < 0)
        {
            NativeBattleEffectArena queue = frame.State.ReadCommonQueue(descriptor);
            if (Dword251(queue, 0) != 0 || Dword251(queue, 4) != 0)
            {
                throw new InvalidDataException("Effect251 detached descriptor has an unreachable nonempty queue.");
            }
            return (frame, 0);
        }
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            int current = PoolIndex(frame, descriptor);
            frame = WithPool(frame, current, pool);
            NativeBattleEffect251Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[PoolIndex(frame, descriptor)], advanced.Result);
        });
        return (WithPool(frame, PoolIndex(frame, descriptor), step.Pool), step.Survivors);
    }

    public static NativeBattleEffect251Step AdvanceTask(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot4580(frame, address, resources, inputs),
            MainCallback => AdvanceMain5000(frame, address, resources, inputs),
            BurstCallback => AdvanceBurst4890(frame, address, inputs),
            _ => NativeBattleEffect251Lower.Advance(frame, address, resources, inputs)
        };
    }

    public static NativeBattleEffect251Frame InitializeCommonPool(NativeBattleEffect251Frame frame, uint descriptor)
    {
        NativeBattleCureFamilyPool relative = CommonPools.Single(pool => pool.Descriptor == descriptor);
        NativeBattleCureFamilyPool shape = relative with { Bank = checked(frame.State.ReadDword(ArenaGlobal) + relative.Bank) };
        if (frame.Effect.Pools.Any(pool => pool.DescriptorAddress == descriptor || (ulong)pool.BankAddress < (ulong)shape.Bank + (uint)(shape.Stride * shape.Capacity) && (ulong)shape.Bank < (ulong)pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length)))
        {
            throw new InvalidDataException("Effect251 pool initialization would duplicate its physical bank owner.");
        }
        NativeBattleEffectTaskPool initialized = InitializePayload(shape, ReadBytes(frame, shape.Bank, shape.Stride * shape.Capacity));
        return frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [initialized]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(initialized) }, State = frame.State.DetachCommonQueue(descriptor) };
    }

    public static NativeBattleEffect251Frame RetireCommonPool(NativeBattleEffect251Frame frame, uint descriptor)
    {
        NativeBattleEffectArena queue = ReadBytes(frame, descriptor, 8);
        NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
        return frame with { Memory = transfer.Memory, Effect = transfer.Effect, State = frame.State.AttachCommonQueue(descriptor, queue) };
    }

    public static NativeBattleEffect251Frame BindProgramme(NativeBattleEffect251Frame frame, uint destination, uint source, int clearLength, NativeBattleEffect251Resources resources)
    {
        return NativeBattleEffect251Programme.Initialize(frame, destination, source, clearLength, resources);
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

    public static uint ModelAddress(int actor)
    {
        if ((uint)actor >= 7)
        {
            throw new NotSupportedException("Effect251 requires an actual physical actor selector.");
        }
        return checked(0x1D972C0u + (uint)(actor * 156));
    }

    public static int Actor(uint address)
    {
        long offset = (long)address - 0x1D972C0;
        return offset >= 0 && offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Effect251 requires an actual physical model pointer.");
    }

    private static int PoolIndex(NativeBattleEffect251Frame frame, uint descriptor)
    {
        return Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == descriptor);
    }

    private static void RequireBinding(NativeBattleEffect251Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect251 requires its selected native binding.");
        }
    }
}
