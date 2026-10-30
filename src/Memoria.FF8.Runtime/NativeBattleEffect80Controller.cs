using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Controller
{
    public static NativeBattleEffect80Frame Initialize(NativeBattleEffect80Frame frame, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.FixedBanks.Values.Any(bank => bank is null))
        {
            throw new InvalidDataException("Effect80 entry requires its exclusive inactive fixed banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect80 requires its actual executed initializer result.");
        uint source = checked(0x20DFAB8u + (uint)offset);
        NativeBattleEffect80State state = frame.State.WriteDword(InitializerGlobal, source).WriteDword(0x25DF300, 0).WriteDword(0x25DCF34, 0).WriteDword(0x25DE424, source).WriteDword(0x25DD81C, checked(source + 0x8000)).WriteDword(0x25DE420, checked(source + 0x8000)).WriteDword(NextArenaGlobal, checked(source + 0x10000)).WriteDword(0x25DD818, checked(source + 0x10000));
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
        frame = frame with { State = state, Effect = frame.Effect with { Pools = pools.ToImmutable(), ReturnedDescriptor = Pools[0].Descriptor } };
        (frame, uint root) = AppendHierarchy(frame, 0, RootCallback, 100, 0, inputs);
        frame = Write(frame, root, NativeBattleEffectRootTask.Initialize(Read(frame, root), Parameters(frame), inputs.ReadPackets(frame)));
        if ((Parameters(frame).Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect80RegisterCamera(resources.Camera));
            frame = inputs.Execute(frame, new NativeBattleEffect80UploadTim(source));
        }
        return frame;
    }

    public static NativeBattleEffect80Frame Advance(NativeBattleEffect80Frame frame, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != Pools.Length || frame.State.FixedBanks.Values.Any(bank => bank is not null))
        {
            throw new InvalidDataException("Effect80 requires its exclusive active fixed hierarchy banks.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect80 active hierarchy differs from its physical native descriptors.");
            }
        }
        (frame, int survivors) = Scan(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : Pools[0].Descriptor } };
        if (survivors != 0)
        {
            return frame;
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.Head is not null)
            {
                throw new InvalidDataException("Effect80 root retired before its actual child banks.");
            }
            frame = frame with { State = frame.State.AttachBank(index, NativeBattleEffectTaskMemory.CaptureBank(pool)) };
        }
        return frame with { Effect = frame.Effect with { Pools = [] } };
    }

    public static NativeBattleEffect80Step AdvanceTask(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        if (inputs.CapturePresentation)
        {
            inputs = inputs with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(80, Read(frame, address)) };
        }
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot(frame, address, resources, inputs),
            DriverCallback => AdvanceDriver(frame, address, resources, inputs),
            ControlCallback => AdvanceControl(frame, address, resources, inputs),
            0x7ABF90 or 0x7B4540 => AdvanceAct(frame, address, resources, inputs),
            0x7AE690 => AdvancePointEmitter(frame, address, inputs),
            0x7AEFA0 => AdvancePuffEmitter(frame, address, inputs),
            0x7AE7D0 => NativeBattleEffect80Particles.Advance7AE7D0(frame, address, resources, inputs),
            0x7AF030 => NativeBattleEffect80Particles.Advance7AF030(frame, address, resources, inputs),
            0x7AF1D0 => AdvanceStageRise(frame, address, inputs),
            0x7AF2C0 => AdvanceStageFall(frame, address, inputs),
            0x7AF390 => AdvanceBody(frame, address, resources, inputs),
            0x7B0AE0 => AdvanceNode(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Effect80 reached an unsupported actual hierarchy callback.")
        };
    }

    private static (NativeBattleEffect80Frame Frame, int Survivors) Scan(NativeBattleEffect80Frame frame, int index, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleEffect80Frame current = frame;
        NativeBattleEffectHierarchyStep step = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(index, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            NativeBattleEffect80Step advanced = AdvanceTask(current, address, resources, inputs);
            current = advanced.Frame;
            return new(new(current.Effect.Pools), advanced.Result);
        });
        return (current with { Effect = current.Effect with { Pools = step.Hierarchy.Pools } }, step.Survivors);
    }

    public static NativeBattleEffect80Step Finish(NativeBattleEffect80Frame frame, uint address)
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

    public static NativeBattleEffect80Frame NextPhase(NativeBattleEffect80Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    public static int Phase(NativeBattleEffect80Frame frame, uint address) => unchecked((sbyte)Read(frame, address).ReadByte(0x29));
    public static NativeBattleEffect80Frame MarkComplete(NativeBattleEffect80Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1)));
    private static void RequireBinding(NativeBattleEffect80Frame frame)
    {
        if (frame.Effect.RequestedEffect != 80 || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect80 requires its original selected native entry.");
        }
    }
}
