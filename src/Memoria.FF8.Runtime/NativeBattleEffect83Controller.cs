using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect83Controller
{
    public static NativeBattleEffect83Frame Initialize(NativeBattleEffect83Frame frame, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.BankStorage.Any(bank => bank is null))
        {
            throw new InvalidDataException("Effect83 entry requires its exclusive inactive fixed banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect83 requires its actual executed initializer result.");
        uint source = checked(0x20DFAB8u + (uint)offset);
        NativeBattleEffect83State state = frame.State.BindNodeSource(resources).WriteDword(InitializerGlobal, source).WriteDword(0x25CFA48, 0).WriteDword(0x25CA854, 0).WriteDword(0x25CF3EC, source).WriteDword(0x25CED2C, checked(source + 0x8000)).WriteDword(0x25CF3E8, checked(source + 0x8000)).WriteDword(0x25CFA4C, checked(source + 0x10000)).WriteDword(0x25CED28, checked(source + 0x10000));
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>(Pools.Length);
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
            NativeBattleEffectArena retained = state.ReadBank(index);
            for (int slot = 0; slot < shape.Capacity; slot++)
            {
                NativeBattleModulePoolTask task = CopyTask(pool.Slots[slot], 12, Slice(retained, slot * shape.Stride + 12, shape.Stride - 12), shape.Stride - 12);
                pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
            }
            pools.Add(pool);
            state = state.DetachBank(index);
        }
        frame = frame with { State = state, Effect = frame.Effect with { Pools = pools.ToImmutable(), ReturnedDescriptor = RootDescriptor } };
        (frame, uint root) = AppendHierarchy(frame, 0, RootCallback, 100, 0, inputs);
        frame = Write(frame, root, NativeBattleEffectRootTask.Initialize(Read(frame, root), Parameters(frame), inputs.ReadPackets(frame)));
        if ((Parameters(frame).Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect83RegisterCamera(resources.Camera));
            frame = inputs.Execute(frame, new NativeBattleEffect83UploadTim(source));
        }
        return frame;
    }

    public static NativeBattleEffect83Frame Advance(NativeBattleEffect83Frame frame, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != Pools.Length || frame.State.BankStorage.Any(bank => bank is not null))
        {
            throw new InvalidDataException("Effect83 requires its exclusive active fixed hierarchy banks.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect83 active hierarchy differs from its physical native descriptors.");
            }
        }
        (frame, int survivors) = Scan(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        if (survivors != 0)
        {
            return frame;
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.Head is not null)
            {
                throw new InvalidDataException("Effect83 root retired before its actual child banks.");
            }
            frame = frame with { State = frame.State.AttachBank(index, NativeBattleEffectTaskMemory.CaptureBank(pool)) };
        }
        return frame with { Effect = frame.Effect with { Pools = [] } };
    }

    public static NativeBattleEffect83Step AdvanceTask(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        0x797A90 => AdvanceMain(frame, address, resources, inputs),
        0x797BF0 => AdvanceDriver(frame, address, resources, inputs),
        0x797ED0 => AdvanceNode(frame, address, resources, inputs),
        0x79D860 => AdvanceStageTransition(frame, address, inputs),
        0x79D9B0 => AdvanceTargetBody(frame, address, inputs),
        0x79E690 => AdvanceModel(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect83 reached an unsupported actual hierarchy callback.")
    };

    private static (NativeBattleEffect83Frame Frame, int Survivors) Scan(NativeBattleEffect83Frame frame, int index, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        NativeBattleEffect83Frame current = frame;
        NativeBattleEffectHierarchyStep step = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(index, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            NativeBattleEffect83Step advanced = AdvanceTask(current, address, resources, inputs);
            current = advanced.Frame;
            return new(new(current.Effect.Pools), advanced.Result);
        });
        return (current with { Effect = current.Effect with { Pools = step.Hierarchy.Pools } }, step.Survivors);
    }

    public static NativeBattleEffect83Step Finish(NativeBattleEffect83Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        task = Read(frame, address);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);
    }

    public static NativeBattleEffect83Frame NextPhase(NativeBattleEffect83Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static int Phase(NativeBattleEffect83Frame frame, uint address) => unchecked((sbyte)Read(frame, address).ReadByte(0x29));
    private static NativeBattleEffect83Frame MarkComplete(NativeBattleEffect83Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1)));
    private static void RequireBinding(NativeBattleEffect83Frame frame)
    {
        if (frame.Effect.RequestedEffect != 83 || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect83 requires its original selected native entry.");
        }
    }
}
