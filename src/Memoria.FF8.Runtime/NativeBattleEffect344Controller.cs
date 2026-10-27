using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect344Controller
{
    public static NativeBattleEffect344Frame Initialize(NativeBattleEffect344Frame frame, NativeBattleEffect344Resources resources, NativeBattleEffect344Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.BankStorage.Any(bank => bank is null))
        {
            throw new InvalidDataException("Effect344 requires its inactive physical BSS banks.");
        }
        NativeBattleEffectParameters parameters = Parameters(frame);
        int contact = FirstContact(frame, inputs);
        int target = inputs.ReadPackets(frame).ReadContact(contact).Primary.ActorIndex;
        NativeBattleEffect344State state = frame.State.WriteDword(TargetGlobal, (uint)target).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, parameters.Actor78);
        var pools = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>(2);
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectArena retained = state.ReadBank(index);
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
            for (int slot = 0; slot < pool.Slots.Length; slot++)
            {
                NativeBattleModulePoolTask task = CopyTask(pool.Slots[slot], 12, Slice(retained, slot * shape.Stride + 12, shape.Stride - 12), shape.Stride - 12);
                pool = pool.WriteSlot(slot, task);
            }
            (pool, int allocated) = pool.Append(index == 0 ? 0x62A7A0u : 0x62A6D0u);
            pool = pool.WriteSlot(allocated, pool.Slots[allocated].WriteWord(12, 0));
            pools.Add(pool);
            state = state.DetachBank(index);
        }
        return frame with { State = state, Effect = frame.Effect with { Pools = pools.ToImmutable(), ReturnedDescriptor = Pools[0].Descriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleEffect344Frame Advance(NativeBattleEffect344Frame frame, NativeBattleEffect344Resources resources, NativeBattleEffect344Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2)
        {
            throw new InvalidDataException("Effect344 requires its two physical private pools.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool shape = Pools[index];
            pool.Validate();
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity || frame.State.BankStorage[index] is not null)
            {
                throw new InvalidDataException("Effect344 lost its exclusive actual BSS task bank.");
            }
        }
        (frame, int survivors) = Scan(frame, 0, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.Head is not null)
            {
                throw new InvalidDataException("Effect344 root returned before its child list.");
            }
            frame = frame with { State = frame.State.AttachBank(index, NativeBattleEffectTaskMemory.CaptureBank(pool)) };
        }
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static NativeBattleEffect344Step AdvanceRoot(NativeBattleEffect344Frame frame, uint address, NativeBattleEffect344Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint cursor = (task.ReadByte(12) & 1) == 0 ? 0x24E0BC8u : 0x24E8BC8u;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, cursor) };
        (frame, int survivors) = Scan(frame, 1, inputs);
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect344Step AdvanceChild(NativeBattleEffect344Frame frame, uint address, NativeBattleEffect344Inputs inputs)
    {
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        ushort age = task.ReadWord(12);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect344MusicFade46BBC0(0, 120, 0));
        }
        if (age == 30)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect344StageMusic501A20(0xDEE388, CompletionGlobal));
        }
        if (age == 31 && frame.State.Read(CompletionGlobal, 1)[0] == 0)
        {
            return new(frame, 0);
        }
        if (age == 32)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect344RegisterCamera5099A0(0xE106D8));
            int caster = checked((int)frame.State.ReadDword(CasterGlobal));
            _ = ModelAddress(caster);
            frame = inputs.Execute(frame, new NativeBattleEffect344SetSequence505C00(caster, 20));
        }
        if (age == 34)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect344MusicGain501B10(0, 127));
            frame = inputs.Execute(frame, new NativeBattleEffect344ConsumeFirstContact506690(FirstContact(frame, inputs)));
        }
        task = Read(frame, address);
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        frame = Write(frame, address, task.WriteWord(12, next));
        return new(frame, unchecked((short)next) > 35 ? 2 : 0);
    }

    public static NativeBattleModulePoolTask Read(NativeBattleEffect344Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);

    public static NativeBattleEffect344Frame Write(NativeBattleEffect344Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task.UpdateHeader(task.Flags, task.State, hierarchy.Read(address).Next, task.Callback)).Pools } };
    }

    public static NativeBattleEffect344Frame WriteCompletionByte(NativeBattleEffect344Frame frame, uint address, byte value)
    {
        if (address != CompletionGlobal)
        {
            throw new NotSupportedException("Effect344 completion does not target its actual music BYTE.");
        }
        return frame with { State = frame.State.Write(address, [value]) };
    }

    private static (NativeBattleEffect344Frame Frame, int Survivors) Scan(NativeBattleEffect344Frame frame, int index, NativeBattleEffect344Inputs inputs)
    {
        NativeBattleEffect344Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(index, pool) } };
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            uint callback = pool.Slots[slot].Callback;
            NativeBattleEffect344Step advanced = callback switch
            {
                0x62A7A0 => AdvanceRoot(current, address, inputs),
                0x62A6D0 => AdvanceChild(current, address, inputs),
                _ => throw new NotSupportedException("Effect344 reached an unsupported original callback.")
            };
            current = advanced.Frame;
            return new(current.Effect.Pools[index], advanced.Result);
        });
        return (current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(index, step.Pool) } }, step.Survivors);
    }

    private static int FirstContact(NativeBattleEffect344Frame frame, NativeBattleEffect344Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= packets.Headers.Length)
        {
            throw new InvalidDataException("Effect344 requires its current physical action header.");
        }
        int first = packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Effect344 requires its actual first contact pointer.");
        _ = packets.ReadContact(first);
        return first;
    }

    private static NativeBattleEffectParameters Parameters(NativeBattleEffect344Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect344 requires its actual original parameters.");
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Effect344 requires an actual physical actor index.");
    private static NativeBattleEffectArena Slice(NativeBattleEffectArena source, int offset, int length) => NativeBattleEffectNodeContextMemory.Copy(source, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

    private static NativeBattleModulePoolTask CopyTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }

    private static void RequireBinding(NativeBattleEffect344Frame frame)
    {
        if (frame.Effect.RequestedEffect != 344 || frame.Effect.Binding.Entry != 0x62A650 || frame.Effect.Binding.Initializer != 0x575580)
        {
            throw new InvalidDataException("Effect344 requires its original selected entry and initializer.");
        }
    }
}
