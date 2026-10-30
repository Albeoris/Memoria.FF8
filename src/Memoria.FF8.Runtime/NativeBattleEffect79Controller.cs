using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect79Controller
{
    public static NativeBattleEffect79Frame Initialize(NativeBattleEffect79Frame frame, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.FixedBanks.Values.Any(bank => bank is null))
        {
            throw new InvalidDataException("Effect79 entry requires its exclusive inactive fixed banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect79 requires its actual executed initializer result.");
        uint source = checked(0x20DFAB8u + (uint)offset);
        NativeBattleEffect79State state = frame.State.BindProgram(resources).WriteDword(InitializerGlobal, source).WriteDword(0x25EB3B4, 0).WriteDword(0x25E8954, 0).WriteDword(0x25EA344, source).WriteDword(0x25E973C, checked(source + 0x8000)).WriteDword(0x25EA340, checked(source + 0x8000)).WriteDword(0x25EB3B8, checked(source + 0x10000)).WriteDword(0x25E9738, checked(source + 0x10000));
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>(Pools.Length);
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
            NativeBattleEffectArena retained = state.ReadBank(index);
            for (int slot = 0; slot < shape.Capacity; slot++)
            {
                NativeBattleModulePoolTask task = CopyTask(pool.Slots[slot], 12, Slice(retained, slot * shape.Stride + 12, shape.Stride - 12), shape.Stride - 12);
                pool = pool.WriteSlot(slot, task);
            }
            pools.Add(pool);
            state = state.DetachBank(index);
        }
        frame = frame with { State = state, Effect = frame.Effect with { Pools = pools.ToImmutable(), ReturnedDescriptor = RootDescriptor } };
        (frame, uint root) = AppendHierarchy(frame, 0, RootCallback, 100, 0, inputs);
        frame = Write(frame, root, NativeBattleEffectRootTask.Initialize(Read(frame, root), Parameters(frame), inputs.ReadPackets(frame)));
        if ((Parameters(frame).Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect79RegisterCamera(resources.Camera));
            frame = inputs.Execute(frame, new NativeBattleEffect79UploadTim(source));
        }
        return frame;
    }

    public static NativeBattleEffect79Frame Advance(NativeBattleEffect79Frame frame, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != Pools.Length || frame.State.FixedBanks.Values.Any(bank => bank is not null))
        {
            throw new InvalidDataException("Effect79 requires its exclusive active fixed hierarchy banks.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect79 active hierarchy differs from its physical native descriptors.");
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
                throw new InvalidDataException("Effect79 root retired before its actual child banks.");
            }
            frame = frame with { State = frame.State.AttachBank(index, NativeBattleEffectTaskMemory.CaptureBank(pool)) };
        }
        return frame with { Effect = frame.Effect with { Pools = [] } };
    }

    public static NativeBattleEffect79Step AdvanceTask(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        if (inputs.CapturePresentation)
        {
            inputs = inputs with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(79, Read(frame, address)) };
        }
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            0x7B4FF0 => AdvanceMain(frame, address, resources, inputs),
            0x7B55B0 => AdvanceDriver(frame, address, resources, inputs),
            0x7B51C0 => AdvanceIo7B51C0(frame, address, resources, inputs),
            0x7B82C0 => AdvanceNode(frame, address, resources, inputs),
            0x7B5B80 or 0x7B80B0 or 0x7B81C0 => AdvanceProgramme(frame, address, resources, inputs),
            0x7BCF10 => AdvanceBody(frame, address, resources, inputs),
            0x7BCD50 => AdvanceParticle7BCD50(frame, address, resources, inputs),
            0x7BC610 => AdvanceParticle7BC610(frame, address, resources, inputs),
            0x7BC840 => AdvanceParticle7BC840(frame, address, resources, inputs),
            0x7BBC20 => AdvanceParticle7BBC20(frame, address, resources, inputs),
            0x7BBDF0 => AdvanceParticle7BBDF0(frame, address, resources, inputs),
            0x7BCB40 => AdvanceParticle7BCB40(frame, address, resources, inputs),
            0x7BCC80 => AdvanceParticle7BCC80(frame, address, resources, inputs),
            0x7BCE40 => AdvanceParticle7BCE40(frame, address, resources, inputs),
            0x7BDAD0 => AdvanceParticle7BDAD0(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Effect79 reached an unsupported actual hierarchy callback.")
        };
    }

    private static (NativeBattleEffect79Frame Frame, int Survivors) Scan(NativeBattleEffect79Frame frame, int index, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleEffect79Frame current = frame;
        NativeBattleEffectHierarchyStep step = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(index, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            NativeBattleEffect79Step advanced = AdvanceTask(current, address, resources, inputs);
            current = advanced.Frame;
            return new(new(current.Effect.Pools), advanced.Result);
        });
        return (current with { Effect = current.Effect with { Pools = step.Hierarchy.Pools } }, step.Survivors);
    }

    public static NativeBattleEffect79Step Finish(NativeBattleEffect79Frame frame, uint address)
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

    public static NativeBattleEffect79Frame NextPhase(NativeBattleEffect79Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static int Phase(NativeBattleEffect79Frame frame, uint address) => unchecked((sbyte)Read(frame, address).ReadByte(0x29));
    private static NativeBattleEffect79Frame MarkComplete(NativeBattleEffect79Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1)));
    private static void RequireBinding(NativeBattleEffect79Frame frame)
    {
        if (frame.Effect.RequestedEffect != 79 || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect79 requires its original selected native entry.");
        }
    }
}
