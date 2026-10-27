using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCurse47UpdateCaster(uint Address) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47UpdateTargetBounds(uint Address) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47InitializeTarget(uint Address) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47Contact(int Index) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47TextureLoad(uint RootAddress, uint SourceAddress) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47QueueFile(NativeBattleCurse47IoRequest Request) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47Inputs(NativeBattleActionPacketsState Packets, Func<NativeBattleCurse47Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleCurse47Frame, NativeBattleCurse47Operation, NativeBattleCurse47Frame> Execute, Func<NativeBattleCurse47Frame, uint, NativeBattleCurse47Step> AdvanceLeaf, uint DirectorSound);

public static class NativeBattleCurse47Controller
{
    public const uint Entry = 0x82A7F0;
    public const uint RootCallback = 0x82A970;
    public const uint TargetCallback = 0x82ABC0;
    private const uint ArenaAddress = 0x20DFAB8;
    private static readonly ImmutableArray<int> ScanOrder = [1, 2, 7, 3, 5, 4, 6];

    public static NativeBattleCurse47Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleCurse47Resources resources, NativeBattleCurse47State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 47 || effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Curse requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Curse requires its captured action parameters.");
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x264B3C8)?.ArenaOffset ?? throw new NotSupportedException("Curse requires its executed initializer TIM pointer.");
        uint address = checked(ArenaAddress + (uint)source);
        NativeBattleCurse47State state = retained.WriteDword(0x264B3C8, address).WriteDword(0x264E69C, address)
            .WriteDword(0x264DFDC, checked(address + 0x4000)).WriteDword(0x264E698, checked(address + 0x4000))
            .WriteDword(0x264EAE8, checked(address + 0x8000)).WriteDword(0x264DFD8, checked(address + 0x8000))
            .WriteDword(0x264EAE4, 0).WriteDword(0x264D82C, 0);
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleCurse47State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets));
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Curse requires its actual texture registration owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Curse requires its loaded effect arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = NativeBattleCurse47State.Pools[0].Descriptor }, random, state);
    }

    public static NativeBattleCurse47Frame Advance(NativeBattleCurse47Frame frame, NativeBattleCurse47Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        ArgumentNullException.ThrowIfNull(inputs.ReadCamera);
        ArgumentNullException.ThrowIfNull(inputs.Execute);
        ArgumentNullException.ThrowIfNull(inputs.AdvanceLeaf);
        inputs.Packets.Validate();
        NativeBattleCurse47Frame current = frame;
        NativeBattleEffectHierarchyStep roots = new NativeBattleEffectHierarchy(current.Effect.Pools).Advance(0, (hierarchy, address) =>
        {
            NativeBattleCurse47Step result = AdvanceRoot(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, address, inputs);
            current = result.Frame;
            return new(new(current.Effect.Pools), result.Result);
        });
        return current with { Effect = current.Effect with { Pools = roots.Hierarchy.Pools, ReturnedDescriptor = roots.Survivors == 0 ? 0u : NativeBattleCurse47State.Pools[0].Descriptor } };
    }

    private static NativeBattleCurse47Step AdvanceRoot(NativeBattleCurse47Frame frame, uint address, NativeBattleCurse47Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != RootCallback)
        {
            throw new NotSupportedException("Curse root bank contains an unsupported callback.");
        }
        bool odd = (task.ReadWord(0x5C) & 1) != 0;
        frame = frame with
        {
            Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadCamera(frame) },
            State = frame.State.WriteDword(0x264EADC, 0x2793E58).WriteDword(0x264B4D8, 0x2793E58)
                .WriteDword(0x264B3CC, frame.State.ReadDword(odd ? 0x264E69Cu : 0x264E698u))
                .WriteDword(0x264B4D4, frame.State.ReadDword(odd ? 0x264DFDCu : 0x264DFD8u))
        };
        frame = inputs.Execute(frame, new NativeBattleCurse47UpdateCaster(address));
        task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
            case 1:
            case 5:
                frame = Next(frame, address);
                break;
            case 2:
                if (task.ReadByte(0x28) == 0)
                {
                    uint emitters = frame.State.ReadDword(0x264EAE8);
                    uint geometry = checked(emitters + 0x438);
                    NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Curse requires its executed effect arena.");
                    arena = arena.Write(ArenaOffset(emitters), new byte[0x438]).Write(ArenaOffset(geometry), new byte[0xB7C0]);
                    frame = frame with
                    {
                        Memory = frame.Memory with { Arena = arena },
                        State = frame.State.WriteDword(0x264E708, emitters).WriteDword(0x264EAE0, geometry).WriteDword(0x264EAE8, checked(geometry + 0xB7C0))
                            .WriteWord(0x264D828, 0).WriteWord(0x264E714, 0).WriteWord(0x264E6A4, 0).WriteWord(0x264B3D0, 0)
                    };
                    frame = Next(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                frame = Append(frame, 1, TargetCallback, 88, address, inputs.Packets);
                frame = Next(frame, address);
                break;
            case 4:
                if (task.ReadByte(0x63) == 0)
                {
                    if (unchecked((sbyte)task.ReadByte(0x2A)) < unchecked((short)task.ReadWord(0x58)))
                    {
                        frame = Write(frame, address, task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, 3));
                    }
                    else
                    {
                        frame = Next(frame, address);
                    }
                }
                break;
            case 6:
                if (task.ReadWord(0x5E) == 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 7:
                frame = inputs.Execute(frame, new NativeBattleCurse47TextureLoad(address, frame.State.ReadDword(0x264B3C8)));
                frame = Next(frame, address);
                break;
            case 8:
                if (task.ReadByte(0x62) != 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 9:
                frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                frame = Next(frame, address);
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Curse root signed phase exceeds its eleven handlers.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(0x264EAC0, 0).WriteWord(0x264E704, 0) };
        foreach (int pool in ScanOrder)
        {
            NativeBattleCurse47Frame current = frame;
            NativeBattleEffectHierarchyStep scanned = new NativeBattleEffectHierarchy(current.Effect.Pools).Advance(pool, (hierarchy, child) =>
            {
                NativeBattleCurse47Step result = AdvanceChild(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, child, inputs);
                current = result.Frame;
                return new(new(current.Effect.Pools), result.Result);
            });
            frame = current with { Effect = current.Effect with { Pools = scanned.Hierarchy.Pools } };
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x5E, unchecked((ushort)(task.ReadWord(0x5E) + scanned.Survivors))));
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    private static NativeBattleCurse47Step AdvanceChild(NativeBattleCurse47Frame frame, uint address, NativeBattleCurse47Inputs inputs)
    {
        switch (Read(frame, address).Callback)
        {
            case TargetCallback:
                return AdvanceTarget(frame, address, inputs);
            case NativeBattleCurse47Director.Callback:
                return NativeBattleCurse47Director.Advance(frame, address, new(inputs.DirectorSound, inputs.Execute));
            case NativeBattleCurse47Io.Callback:
                NativeBattleCurse47IoAdvance step = NativeBattleCurse47Io.AdvanceTask(Read(frame, address), frame.State);
                frame = Write(frame with { State = step.State }, address, step.Task);
                if (step.Request is { } request)
                {
                    frame = inputs.Execute(frame, new NativeBattleCurse47QueueFile(request));
                }
                return Complete(frame, address);
            default:
                return inputs.AdvanceLeaf(frame, address);
        }
    }

    private static NativeBattleCurse47Step AdvanceTarget(NativeBattleCurse47Frame frame, uint address, NativeBattleCurse47Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleCurse47UpdateTargetBounds(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleCurse47InitializeTarget(address));
                frame = frame with { State = NativeBattleCurse47Io.Initialize(frame.State) };
                frame = Append(frame, 7, NativeBattleCurse47Io.Callback, 48, address, inputs.Packets);
                frame = Append(frame, 2, NativeBattleCurse47Director.Callback, 72, address, inputs.Packets);
                frame = Next(frame, address);
                break;
            case 1:
                if (frame.State.SharedWord(0x48) == 1)
                {
                    NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Curse target requires its captured action parameters.");
                    int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                    if ((uint)header >= inputs.Packets.Headers.Length)
                    {
                        throw new InvalidDataException("Curse contact header leaves its actual arena.");
                    }
                    int first = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Curse requires its actual contact pointer.");
                    int contact = checked(first + unchecked((sbyte)task.ReadByte(0x2B)));
                    if ((uint)contact >= NativeBattleActionPacketsState.ContactCapacity)
                    {
                        throw new InvalidDataException("Curse contact leaves its actual captured arena.");
                    }
                    frame = inputs.Execute(frame, new NativeBattleCurse47Contact(contact));
                    frame = Next(frame, address);
                }
                break;
            case 2:
                if (task.ReadByte(0x28) == 0 || frame.State.SharedWord(0x4C) == 1)
                {
                    frame = Next(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                uint root = task.ReadDword(0x10);
                frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                frame = Next(frame, address);
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Curse target signed phase exceeds its five handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleModulePoolTask Read(NativeBattleCurse47Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    public static NativeBattleCurse47Frame Write(NativeBattleCurse47Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task.UpdateHeader(task.Flags, task.State, Read(frame, address).Next, task.Callback)).Pools } };
    public static NativeBattleCurse47Frame Append(NativeBattleCurse47Frame frame, int pool, uint callback, int bytes, uint parent, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Curse child selection requires its captured action parameters.");
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, bytes, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target));
        return frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } };
    }
    public static NativeBattleCurse47Step Finish(NativeBattleCurse47Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        return Complete(frame, address);
    }
    private static NativeBattleCurse47Step Complete(NativeBattleCurse47Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);
    }
    private static NativeBattleCurse47Frame Next(NativeBattleCurse47Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        return Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }
    private static int ArenaOffset(uint address)
    {
        if (address < ArenaAddress || address - ArenaAddress >= NativeBattleEffectArena.Length)
        {
            throw new NotSupportedException("Curse requires a pointer inside its actual effect arena.");
        }
        return checked((int)(address - ArenaAddress));
    }
    private static void Require(NativeBattleCurse47Frame frame)
    {
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 47 || frame.Effect.Binding.Entry != Entry || frame.Effect.Pools.Length != NativeBattleCurse47State.Pools.Length)
        {
            throw new InvalidDataException("Curse requires its executed eight-pool hierarchy.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleCurse47State.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Curse pool layout contradicts its physical descriptor.");
            }
        }
    }
}
