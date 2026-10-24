using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect54Controller
{
    public static NativeBattleEffect54Frame Initialize(NativeBattleEffect54Frame frame, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        (NativeBattleEffect54State boundState, NativeBattleFileStaging boundStaging) = frame.State.BindSources(RequireStaging(frame), resources);
        frame = frame with { State = boundState, Staging = boundStaging };
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect54 entry requires inactive fixed banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect54 requires its executed initializer pointer.");
        uint source = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, source).WriteDword(EntryZeroGlobal, 0).WriteDword(EntryControlGlobal, 0) };
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>(4);
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectArena retained = frame.State.ReadBank(index);
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
            for (int slot = 0; slot < shape.Capacity; slot++)
            {
                int start = slot * shape.Stride + 12;
                pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, start, CopyBytes(retained, start, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
            }
            pools.Add(pool);
            frame = frame with { State = frame.State.DetachBank(index) };
        }
        frame = frame with { Effect = frame.Effect with { Pools = pools.ToImmutable(), ReturnedDescriptor = RootDescriptor } };
        (frame, uint address) = AppendHierarchy(frame, 0, RootCallback, 100, 0, inputs);
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(12, 0x1D99A78);
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(Parameters(frame), packets, 0x1D99A78, unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[Parameters(frame).HeaderIndex7C];
        byte count = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(count - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        task = task.WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target).WriteByte(0x29, 0).WriteWord(0x5A, count).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
        frame = Write(frame, address, task);
        if ((Parameters(frame).Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect54RegisterCamera5099A0(CameraAddress));
            frame = inputs.Execute(frame, new NativeBattleEffect54UploadTim505E30(source));
        }
        return frame with { State = frame.State.WriteDword(OddPrimaryPacketGlobal, source).WriteDword(EvenPrimaryPacketGlobal, checked(source + 0x8000)).WriteDword(OddSecondaryPacketGlobal, checked(source + 0x8000)).WriteDword(EvenSecondaryPacketGlobal, checked(source + 0x10000)).WriteDword(AllocationCursorGlobal, checked(source + 0x10000)) };
    }

    public static NativeBattleEffect54Frame Advance(NativeBattleEffect54Frame frame, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 4 || frame.Effect.ReturnedDescriptor != RootDescriptor)
        {
            throw new InvalidDataException("Effect54 requires its four original fixed task pools.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect54 fixed pool dimensions changed.");
            }
        }
        (frame, int survivors) = Scan(frame, 0, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            frame = frame with { State = frame.State.AttachBank(index, NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools[index])) };
        }
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static NativeBattleEffect54Step AdvanceTask(NativeBattleEffect54Frame frame, uint address, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        DriverCallback => AdvanceDriver(frame, address, resources, inputs),
        ModelCallback => AdvanceModel(frame, address, resources, inputs),
        FadeInCallback => AdvanceFade(frame, address, false, inputs),
        FadeOutCallback => AdvanceFade(frame, address, true, inputs),
        _ => throw new NotSupportedException("Effect54 callback is outside its selected hierarchy.")
    };

    public static (NativeBattleEffect54Frame Frame, int Survivors) Scan(NativeBattleEffect54Frame frame, int index, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        NativeBattleEffect54Frame current = frame;
        NativeBattleEffectHierarchyStep step = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(index, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            NativeBattleEffect54Step result = AdvanceTask(current, address, resources, inputs);
            current = result.Frame;
            return new(new NativeBattleEffectHierarchy(current.Effect.Pools), result.Result);
        });
        return (current with { Effect = current.Effect with { Pools = step.Hierarchy.Pools } }, step.Survivors);
    }

    private static NativeBattleEffect54Step Complete(NativeBattleEffect54Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if ((task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Complete8DC530(address).Pools } }, 2);
        }
        return new(frame, 0);
    }

    private static NativeBattleEffect54Frame IncrementPhase(NativeBattleEffect54Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static void RequireBinding(NativeBattleEffect54Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Parameters is null)
        {
            throw new InvalidDataException("Effect54 requires its own binding and native action parameters.");
        }
    }
}
