using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect31Controller
{
    public static NativeBattleEffect31Frame Initialize(NativeBattleEffect31Frame frame, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        frame = BindOriginalSources(frame, resources);
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect31 entry requires its inactive original task banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect31 requires its executed initializer allocation.");
        uint source = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, source).WriteDword(0x26D33D8, 0).WriteDword(0x26D10A4, 0) };
        frame = InitializePool(frame, 0);
        (frame, uint root) = AppendHierarchy(frame, 0, RootCallback, 0x64, 0, inputs);
        NativeBattleModulePoolTask task = Read(frame, root).WriteDword(12, 0x1D99A78);
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleEffectParameters parameters = Parameters(frame);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, task.ReadDword(12), unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        byte count = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(count - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        frame = Write(frame, root, task.WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target).WriteByte(0x29, 0).WriteWord(0x5A, count).WriteWord(0x58, groups).WriteByte(0x2F, maximum));
        if ((parameters.Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect31RegisterCamera5099A0(CameraAddress));
            frame = inputs.Execute(frame, new NativeBattleEffect31UploadTim505E30(source));
        }
        frame = frame with { State = frame.State.WriteDword(0x26D2DA4, source).WriteDword(0x26D219C, checked(source + 0xD000)).WriteDword(0x26D2DA0, checked(source + 0xD000)).WriteDword(AllocationCursorGlobal, checked(source + 0x1A000)).WriteDword(0x26D2198, checked(source + 0x1A000)) };
        for (int index = 1; index < Pools.Length; index++)
        {
            frame = InitializePool(frame, index);
        }
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = RootDescriptor } };
    }

    public static NativeBattleEffect31Frame BindOriginalSources(NativeBattleEffect31Frame frame, NativeBattleEffect31Resources resources)
    {
        if (frame.State.StaticSourcesBound)
        {
            return frame;
        }
        if (!frame.State.ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect31 requires its explicit original source publication before unknown process history can continue.");
        }
        NativeBattleFileStaging staging = RequireStaging(frame);
        foreach ((uint address, ImmutableArray<byte> data) in resources.StaticFronts)
        {
            bool[] known = staging.ReadSnapshot(address, data.Length).Known;
            if (known.All(value => value))
            {
                continue;
            }
            if (known.Any(value => value))
            {
                throw new NotSupportedException("Effect31 cannot initialize an original source over partially observed current bytes.");
            }
            staging = staging.Write(address, data.AsSpan());
        }
        return frame with { Staging = staging, State = frame.State with { StaticSourcesBound = true } };
    }

    private static NativeBattleEffect31Frame InitializePool(NativeBattleEffect31Frame frame, int index)
    {
        NativeBattleCureFamilyPool shape = Pools[index];
        NativeBattleEffectArena retained = frame.State.ReadBank(index);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int at = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, at, CopyBytes(retained, at, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return frame with { State = frame.State.DetachBank(index).DetachQueue(shape.Descriptor), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
    }

    public static NativeBattleEffect31Frame Advance(NativeBattleEffect31Frame frame, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.ReturnedDescriptor != RootDescriptor || frame.Effect.Pools.Length != Pools.Length)
        {
            throw new InvalidDataException("Effect31 requires its seven original task descriptors.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect31 physical task pool dimensions changed.");
            }
        }
        (frame, int survivors) = Scan(frame, 0, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint head = pool.Head is int headSlot ? checked(pool.BankAddress + (uint)(headSlot * pool.Stride)) : 0;
            uint tail = pool.Tail is int tailSlot ? checked(pool.BankAddress + (uint)(tailSlot * pool.Stride)) : 0;
            NativeBattleEffectArena queue = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(head)).Write(4, BitConverter.GetBytes(tail));
            frame = frame with { State = frame.State.AttachBank(index, NativeBattleEffectTaskMemory.CaptureBank(pool)).AttachQueue(pool.DescriptorAddress, queue) };
        }
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static NativeBattleEffect31Step AdvanceTask(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        0x86CD70 => AdvanceFade(frame, address, false, inputs),
        0x8734A0 => AdvanceFade(frame, address, true, inputs),
        0x86CE90 => AdvanceTarget(frame, address, inputs),
        0x86CF60 => AdvanceOverlay(frame, address, resources, inputs),
        0x86D100 => AdvanceEmpty(frame, address),
        0x86D1D0 => NativeBattleEffect31Nodes.Advance(frame, address, resources, inputs),
        0x871F60 or 0x872170 or 0x8723E0 or 0x872920 or 0x872B10 or 0x873010 or 0x8730C0 or 0x8731D0 => NativeBattleEffect31Actors.Advance(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect31 callback requires its selected current leaf implementation.")
    };

    public static (NativeBattleEffect31Frame Frame, int Survivors) Scan(NativeBattleEffect31Frame frame, int index, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleEffect31Frame current = frame;
        NativeBattleEffectHierarchyStep result = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(index, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            NativeBattleEffect31Step step = AdvanceTask(current, address, resources, inputs);
            current = step.Frame;
            return new(new NativeBattleEffectHierarchy(current.Effect.Pools), step.Result);
        });
        return (current with { Effect = current.Effect with { Pools = result.Hierarchy.Pools } }, result.Survivors);
    }

    public static (NativeBattleEffect31Frame Frame, uint Address) AppendHierarchy(NativeBattleEffect31Frame frame, int poolIndex, uint callback, int length, uint parent, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(poolIndex, callback, length, parent, (pointer, header, contact) => NativeBattleCureController.SelectActors(Parameters(frame), inputs.ReadPackets(frame), pointer, header, contact));
        if (appended.Address == 0)
        {
            throw new InvalidDataException("Effect31 exhausted a mandatory hierarchy allocation.");
        }
        return (frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    public static NativeBattleEffect31Step Finish(NativeBattleEffect31Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        frame = Write(frame, address, task);
        if ((task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0)
        {
            return new(frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } }, 2);
        }
        return new(frame, 0);
    }

    public static int NativePhase(NativeBattleModulePoolTask task, int maximum)
    {
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (phase < 0 || phase > maximum)
        {
            throw new InvalidDataException("Effect31 signed task state leaves its literal dispatch table.");
        }
        return phase;
    }

    public static NativeBattleEffect31Frame IncrementPhase(NativeBattleEffect31Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    public static NativeBattleEffect31Frame MarkComplete(NativeBattleEffect31Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1)));

    private static void RequireBinding(NativeBattleEffect31Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Parameters is null)
        {
            throw new InvalidDataException("Effect31 requires its own binding and native parameters.");
        }
    }
}
