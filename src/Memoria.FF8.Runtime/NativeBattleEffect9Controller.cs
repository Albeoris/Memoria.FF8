using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect9Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect9State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleEffect9Step(NativeBattleEffect9Frame Frame, int Result);
public abstract record NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9UpdateCaster(uint Address) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9UpdateTarget(uint Address) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9InitializeControl(uint Address) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9Contact(uint Address) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9Sound(NativeBattleModuleSound Sound) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9TextureLoad(uint Address, uint Source) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9Inputs(NativeBattleActionPacketsState Packets, Func<NativeBattleEffect9Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleEffect9Frame, NativeBattleEffect9Operation, NativeBattleEffect9Frame> Execute, Func<NativeBattleEffect9Frame, uint, NativeBattleEffect9Step> AdvanceLeaf, uint DirectorSound);

public static partial class NativeBattleEffect9Controller
{
    public const uint Entry = 0x8C0380;
    public const uint RootCallback = 0x8C0500;
    public const uint TargetCallback = 0x8C06D0;
    private const uint ArenaAddress = 0x20DFAB8;
    private static readonly ImmutableArray<int> ScanOrder = [1, 2, 7, 4, 5, 3, 6];

    public static NativeBattleEffect9Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleEffectHeapAllocation camera, NativeBattleEffect9State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        if (effect.Binding.Effect != 9 || effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Effect9 requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Effect9 requires its captured action parameters.");
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x274EFE8)?.ArenaOffset ?? throw new NotSupportedException("Effect9 requires its executed initializer TIM pointer.");
        uint address = checked(ArenaAddress + (uint)source);
        NativeBattleEffect9State state = retained.WriteDword(0x274EFE8, address).WriteDword(0x27584C8, 0).WriteDword(0x274F620, 0)
            .WriteDword(0x27575C4, address).WriteDword(0x27571B4, checked(address + 0x10000)).WriteDword(0x27575C0, checked(address + 0x10000))
            .WriteDword(0x27584CC, checked(address + 0x20000)).WriteDword(0x27571B0, checked(address + 0x20000));
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleEffect9State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets));
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Effect9 requires its texture registration owner.");
            textures = textures.Register5099A0(camera);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Effect9 requires its loaded arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = NativeBattleEffect9State.Pools[0].Descriptor }, random, state);
    }

    public static NativeBattleEffect9Frame Advance(NativeBattleEffect9Frame frame, NativeBattleEffect9Inputs inputs)
    {
        RequireRoot(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleEffect9Frame current = frame;
        NativeBattleEffectHierarchyStep roots = new NativeBattleEffectHierarchy(current.Effect.Pools).Advance(0, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            NativeBattleEffect9Step step = AdvanceRoot(current, address, inputs);
            current = step.Frame;
            return new(new(current.Effect.Pools), step.Result);
        });
        return current with { Effect = current.Effect with { Pools = roots.Hierarchy.Pools, ReturnedDescriptor = roots.Survivors == 0 ? 0u : NativeBattleEffect9State.Pools[0].Descriptor } };
    }

    private static NativeBattleEffect9Step AdvanceRoot(NativeBattleEffect9Frame frame, uint address, NativeBattleEffect9Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != RootCallback)
        {
            throw new NotSupportedException("Effect9 root bank contains an unsupported callback.");
        }
        bool odd = (task.ReadByte(0x5C) & 1) != 0;
        frame = frame with
        {
            Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadCamera(frame) },
            State = frame.State.WriteDword(0x27584C4, 0x2793E58).WriteDword(0x274F150, 0x2793E58)
                .WriteDword(0x274EFEC, frame.State.ReadDword(odd ? 0x27575C4u : 0x27575C0u))
                .WriteDword(0x274F14C, frame.State.ReadDword(odd ? 0x27571B4u : 0x27571B0u))
        };
        frame = inputs.Execute(frame, new NativeBattleEffect9UpdateCaster(address));
        task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
            case 1:
            case 5:
                frame = NextPhase(frame, address);
                break;
            case 2:
                if (task.ReadByte(0x28) == 0)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                frame = Append(frame, 1, TargetCallback, 88, address, inputs.Packets).Frame;
                frame = NextPhase(frame, address);
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
                        frame = NextPhase(frame, address);
                    }
                }
                break;
            case 6:
                if (task.ReadWord(0x5E) == 0)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 7:
                frame = inputs.Execute(frame, new NativeBattleEffect9TextureLoad(address, frame.State.ReadDword(0x274EFE8)));
                frame = NextPhase(frame, address);
                break;
            case 8:
                if (task.ReadByte(0x62) != 0)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 9:
                frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                frame = NextPhase(frame, address);
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Effect9 signed root phase exceeds its eleven handlers.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        foreach (int pool in ScanOrder)
        {
            NativeBattleEffect9Frame current = frame;
            NativeBattleEffectHierarchyStep scanned = new NativeBattleEffectHierarchy(current.Effect.Pools).Advance(pool, (hierarchy, child) =>
            {
                current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
                NativeBattleEffect9Step step = Read(current, child).Callback switch
                {
                    TargetCallback => AdvanceTarget(current, child, inputs),
                    0x8C0800 => AdvanceControl(current, child, inputs),
                    _ => inputs.AdvanceLeaf(current, child)
                };
                current = step.Frame;
                return new(new(current.Effect.Pools), step.Result);
            });
            frame = current with { Effect = current.Effect with { Pools = scanned.Hierarchy.Pools } };
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x5E, unchecked((ushort)(task.ReadWord(0x5E) + scanned.Survivors))));
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    internal static NativeBattleModulePoolTask Read(NativeBattleEffect9Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);

    internal static NativeBattleEffect9Frame Write(NativeBattleEffect9Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(address, task with { Next = hierarchy.Read(address).Next });
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    }

    internal static NativeBattleEffect9Step Finish(NativeBattleEffect9Frame frame, uint address)
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

    internal static NativeBattleEffect9Step Append(NativeBattleEffect9Frame frame, int pool, uint callback, int length, uint parent, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect9 child selection requires its actual action parameters.");
        NativeBattleEffectHierarchyAppend append = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, length, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target));
        return new(frame with { Effect = frame.Effect with { Pools = append.Hierarchy.Pools } }, checked((int)append.Address));
    }

    internal static NativeBattleEffect9Frame NextPhase(NativeBattleEffect9Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        return Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static void RequireRoot(NativeBattleEffect9Frame frame)
    {
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 9 || frame.Effect.Binding.Entry != Entry || frame.Effect.Pools.Length != NativeBattleEffect9State.Pools.Length)
        {
            throw new InvalidDataException("Effect9 requires its executed eight-bank hierarchy.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleEffect9State.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect9 task bank contradicts its physical descriptor.");
            }
        }
    }
}
