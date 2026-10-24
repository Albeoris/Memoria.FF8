using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect68Controller
{
    public static NativeBattleEffect68Frame Initialize(NativeBattleEffect68Frame frame, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect68 entry requires inactive fixed banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect68 requires its executed initializer pointer.");
        uint source = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, source).WriteDword(FirstPacketGlobal, CommonAddress).WriteDword(SecondPacketGlobal, CommonAddress + 0x4000) };
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>(Pools.Length);
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
        frame = frame with { Effect = frame.Effect with { Pools = pools.ToImmutable(), ReturnedDescriptor = Pools[0].Descriptor } };
        (frame, uint root) = AppendHierarchy(frame, 0, 0x7DC280, 0x64, 0, inputs);
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect68 requires its actual first contact pointer.");
        byte target = checked((byte)packets.ReadContact(first).Primary.ActorIndex);
        frame = Write(frame, root, Read(frame, root).WriteDword(12, 0x1D99A78).WriteByte(0x2D, target).WriteWord(0x5A, header.ReadByte(0x10)).WriteWord(0x58, header.ReadByte(0x11)));
        if ((Parameters(frame).Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect68UploadTim505E30(source));
        }
        return frame;
    }

    public static NativeBattleEffect68Frame Advance(NativeBattleEffect68Frame frame, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != Pools.Length)
        {
            throw new InvalidDataException("Effect68 requires its five active fixed banks.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != shape.Descriptor || actual.BankAddress != shape.Bank || actual.Stride != shape.Stride || actual.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect68 active bank dimensions changed.");
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

    public static NativeBattleEffect68Step AdvanceTask(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs) => Read(frame, address).Callback switch
    {
        0x7DC280 => AdvanceRoot(frame, address, resources, inputs),
        0x7DC3A0 => AdvanceDriver(frame, address, resources, inputs),
        0x7DC600 => AdvanceMain(frame, address, resources, inputs),
        _ => NativeBattleEffect68Lower.Advance(frame, address, resources, inputs)
    };

    public static (NativeBattleEffect68Frame Frame, uint Address) AppendHierarchy(NativeBattleEffect68Frame frame, int index, uint callback, int length, uint parent, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(index, callback, length, parent, (pointer, header, contact) => NativeBattleCureController.SelectActors(Parameters(frame), inputs.ReadPackets(frame), pointer, header, contact));
        if (appended.Address == 0)
        {
            throw new NotSupportedException("Effect68 exhausted a required native hierarchy allocation.");
        }
        return (frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    public static (NativeBattleEffect68Frame Frame, int Survivors) Scan(NativeBattleEffect68Frame frame, int index, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleEffectHierarchyStep step = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(index, (hierarchy, address) =>
        {
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
            NativeBattleEffect68Step advanced = AdvanceTask(frame, address, resources, inputs);
            frame = advanced.Frame;
            return new(new(frame.Effect.Pools), advanced.Result);
        });
        return (frame with { Effect = frame.Effect with { Pools = step.Hierarchy.Pools } }, step.Survivors);
    }

    public static NativeBattleEffect68Step Finish(NativeBattleEffect68Frame frame, uint address)
    {
        frame = Write(frame, address, Read(frame, address).WriteWord(0x24, unchecked((ushort)(Read(frame, address).ReadWord(0x24) + 1))));
        NativeBattleModulePoolTask task = Read(frame, address);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);
    }

    public static NativeBattleEffect68Frame NextPhase(NativeBattleEffect68Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    public static NativeBattleEffect68Frame MarkComplete(NativeBattleEffect68Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1)));
    public static NativeBattleEffectParameters Parameters(NativeBattleEffect68Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect68 requires its current parameters.");
    public static int Phase(NativeBattleEffect68Frame frame, uint address) => unchecked((sbyte)Read(frame, address).ReadByte(0x29));
    private static void RequireBinding(NativeBattleEffect68Frame frame)
    {
        if (frame.Effect.RequestedEffect != 68 || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect68 requires its original selected entry.");
        }
    }
}
