using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect345Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Controller
{
    public static NativeBattleEffect345Frame Initialize(NativeBattleEffect345Frame frame, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        frame = BindOriginalSources(frame, resources);
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect345 entry requires its inactive original banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect345 requires its executed initializer allocation.");
        uint source = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, source).WriteDword(0x255EA8C, 0).WriteDword(0x255775C, 0) };
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>(Pools.Length);
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectArena retained = frame.State.ReadBank(index);
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
            for (int slot = 0; slot < shape.Capacity; slot++)
            {
                int at = slot * shape.Stride + 12;
                pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, at, CopyBytes(retained, at, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
            }
            pools.Add(pool);
            frame = frame with { State = frame.State.DetachBank(index) };
        }
        frame = frame with { Effect = frame.Effect with { Pools = pools.ToImmutable(), ReturnedDescriptor = RootDescriptor } };
        (frame, uint address) = AppendHierarchy(frame, 0, RootCallback, 100, 0, inputs);
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(12, 0x1D99A78);
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(Parameters(frame), packets, task.ReadDword(12), unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[Parameters(frame).HeaderIndex7C];
        byte count = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(count - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        frame = Write(frame, address, task.WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target).WriteByte(0x29, 0).WriteWord(0x5A, count).WriteWord(0x58, groups).WriteByte(0x2F, maximum));
        if ((Parameters(frame).Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect345RegisterCamera5099A0(NativeBattleEffect345Resources.CameraAddress));
            frame = inputs.Execute(frame, new NativeBattleEffect345UploadTim505E30(source));
        }
        return frame with { State = frame.State.WriteDword(0x255DC0C, source).WriteDword(0x255CD64, checked(source + 0xE000)).WriteDword(0x255DC08, checked(source + 0xE000)).WriteDword(0x255CD60, checked(source + 0x1C000)).WriteDword(AllocationCursorGlobal, checked(source + 0x1C000)) };
    }

    public static NativeBattleEffect345Frame BindOriginalSources(NativeBattleEffect345Frame frame, NativeBattleEffect345Resources resources)
    {
        frame = frame with { State = frame.State.BindNodeSources(resources) };
        if (frame.State.OwnedModelSourceBound)
        {
            return frame;
        }
        if (!frame.State.ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect345 requires an explicit original source owner before binding unknown process memory.");
        }
        NativeBattleFileStaging staging = RequireStaging(frame);
        foreach ((uint address, uint value) in resources.ColdPointerSeeds)
        {
            staging = BindOnce(staging, address, BitConverter.GetBytes(value));
        }
        staging = BindOnce(staging, resources.Model.Address, resources.Model.Bytes.AsSpan());
        return frame with { Staging = staging, State = frame.State with { OwnedModelSourceBound = true } };
    }

    private static NativeBattleFileStaging BindOnce(NativeBattleFileStaging staging, uint address, ReadOnlySpan<byte> source)
    {
        bool[] known = staging.ReadSnapshot(address, source.Length).Known;
        if (known.All(value => value))
        {
            return staging;
        }
        if (known.Any(value => value))
        {
            throw new NotSupportedException("Effect345 cannot seed a partially observed original source over retained current bytes.");
        }
        return staging.Write(address, source);
    }

    public static NativeBattleEffect345Frame Advance(NativeBattleEffect345Frame frame, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.ReturnedDescriptor != RootDescriptor || frame.Effect.Pools.Length != Pools.Length)
        {
            throw new InvalidDataException("Effect345 requires its eight original task descriptors.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect345 physical pool dimensions changed.");
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

    public static NativeBattleEffect345Step AdvanceTask(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs) => Read(frame, address).Callback switch
    {
        0x705C00 => AdvanceRoot(frame, address, resources, inputs),
        0x705E50 => AdvanceTarget(frame, address, resources, inputs),
        0x705FF0 => AdvanceSetup(frame, address, resources, inputs),
        0x706130 => AdvanceModel(frame, address, resources, inputs),
        0x706A80 => AdvanceParticles(frame, address, resources, inputs),
        0x70C720 => AdvanceFade(frame, address, false, inputs),
        0x70C9E0 => AdvanceBurst(frame, address, resources, inputs),
        0x70CD70 => AdvanceMotes(frame, address, resources, inputs),
        0x70D0E0 => AdvanceFade(frame, address, true, inputs),
        _ => throw new NotSupportedException("Effect345 callback leaves its selected hierarchy.")
    };

    public static (NativeBattleEffect345Frame Frame, int Survivors) Scan(NativeBattleEffect345Frame frame, int index, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleEffect345Frame current = frame;
        NativeBattleEffectHierarchyStep result = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(index, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            NativeBattleEffect345Step step = AdvanceTask(current, address, resources, inputs);
            current = step.Frame;
            return new(new NativeBattleEffectHierarchy(current.Effect.Pools), step.Result);
        });
        return (current with { Effect = current.Effect with { Pools = result.Hierarchy.Pools } }, result.Survivors);
    }

    private static NativeBattleEffect345Step Finish(NativeBattleEffect345Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(0x24, unchecked((ushort)(Read(frame, address).ReadWord(0x24) + 1)));
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
            throw new InvalidDataException("Effect345 signed task phase leaves its original literal dispatch table.");
        }
        return phase;
    }

    private static NativeBattleEffect345Frame IncrementPhase(NativeBattleEffect345Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static NativeBattleEffect345Frame MarkComplete(NativeBattleEffect345Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1)));
    private static void RequireBinding(NativeBattleEffect345Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Parameters is null)
        {
            throw new InvalidDataException("Effect345 requires its own binding and current native parameters.");
        }
    }
}
