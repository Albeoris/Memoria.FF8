using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDoom8Controller
{
    public const uint RootCallback = 0x8C4B30;
    public const uint TargetCallback = 0x8C4D90;
    public const uint IoCallback = 0x8C4F60;
    private const uint ArenaAddress = 0x20DFAB8;
    private static readonly ImmutableArray<int> ScanOrder = [1, 2, 9, 8, 3, 5, 6, 4, 7];

    public static NativeBattleDoom8Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleDoom8Resources resources, NativeBattleDoom8State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 8 || effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Doom requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Doom requires its captured action parameters.");
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2758500)?.ArenaOffset ?? throw new NotSupportedException("Doom requires its executed initializer TIM pointer.");
        uint address = checked(ArenaAddress + (uint)source);
        NativeBattleDoom8State state = retained.WriteDword(0x2758500, address).WriteDword(0x275E82C, address)
            .WriteDword(0x275DF4C, checked(address + 0x8000)).WriteDword(0x275E828, checked(address + 0x8000))
            .WriteDword(0x275F9D8, checked(address + 0x10000)).WriteDword(0x275DF48, checked(address + 0x10000))
            .WriteDword(0x275F9D4, 0).WriteDword(0x275ABDC, 0);
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets));
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Doom requires its texture registration owner.");
            textures = textures.Register5099A0(resources.Camera);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Doom requires its loaded arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = NativeBattleDoom8State.Pools[0].Descriptor }, random, state);
    }

    public static NativeBattleDoom8Frame Advance(NativeBattleDoom8Frame frame, NativeBattleDoom8Inputs inputs)
    {
        RequireRoot(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        ArgumentNullException.ThrowIfNull(inputs.ReadCamera);
        ArgumentNullException.ThrowIfNull(inputs.Execute);
        ArgumentNullException.ThrowIfNull(inputs.AdvanceLeaf);
        inputs.Packets.Validate();
        NativeBattleDoom8Frame current = frame;
        NativeBattleEffectHierarchyStep roots = new NativeBattleEffectHierarchy(current.Effect.Pools).Advance(0, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            NativeBattleDoom8Step result = AdvanceRoot(current, address, inputs);
            current = result.Frame;
            return new(new(current.Effect.Pools), result.Result);
        });
        return current with { Effect = current.Effect with { Pools = roots.Hierarchy.Pools, ReturnedDescriptor = roots.Survivors == 0 ? 0u : NativeBattleDoom8State.Pools[0].Descriptor } };
    }

    private static NativeBattleDoom8Step AdvanceRoot(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != RootCallback)
        {
            throw new NotSupportedException("Doom root pool contains an unsupported callback.");
        }
        bool odd = (task.ReadWord(0x5C) & 1) != 0;
        frame = frame with
        {
            Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadCamera(frame) },
            State = frame.State.WriteDword(0x275F92C, 0x2793E58).WriteDword(0x2758888, 0x2793E58)
                .WriteDword(0x2758504, frame.State.ReadDword(odd ? 0x275E82Cu : 0x275E828u))
                .WriteDword(0x2758884, frame.State.ReadDword(odd ? 0x275DF4Cu : 0x275DF48u))
        };
        frame = inputs.Execute(frame, new NativeBattleDoom8UpdateCaster(address));
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
                    uint emitters = frame.State.ReadDword(0x275F9D8);
                    uint geometry = checked(emitters + 1080);
                    NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Doom requires its actual effect arena.");
                    arena = arena.Write(ArenaOffset(emitters), new byte[1080]).Write(ArenaOffset(geometry), new byte[26880]);
                    frame = frame with
                    {
                        Memory = frame.Memory with { Arena = arena },
                        State = frame.State.WriteDword(0x275F3EC, emitters).WriteDword(0x275F980, geometry).WriteDword(0x275F9D8, checked(geometry + 26880))
                            .WriteWord(0x275ABD8, 0).WriteWord(0x275F508, 0).WriteWord(0x275F238, 0).WriteWord(0x2758508, 0)
                    };
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                frame = Append(frame, 1, TargetCallback, 88, address, inputs.Packets);
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
                frame = inputs.Execute(frame, new NativeBattleDoom8TextureLoad(address, frame.State.ReadDword(0x2758500)));
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
                throw new InvalidDataException("Doom root signed phase exceeds its eleven handlers.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(0x275F910, 0).WriteWord(0x275F3E8, 0) };
        foreach (int pool in ScanOrder)
        {
            NativeBattleDoom8Frame current = frame;
            NativeBattleEffectHierarchyStep scanned = new NativeBattleEffectHierarchy(current.Effect.Pools).Advance(pool, (hierarchy, child) =>
            {
                current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
                NativeBattleDoom8Step result = AdvanceChild(current, child, inputs);
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

    private static NativeBattleDoom8Step AdvanceChild(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            TargetCallback => AdvanceTarget(frame, address, inputs),
            DirectorCallback => NativeBattleDoom8Director.Advance(frame, address, new(inputs.DirectorSound, inputs.Execute)),
            IoCallback => AdvanceIo(frame, address, inputs),
            0x8CC1E0 or 0x8CC2D0 => NativeBattleDoom8StageDepth.Advance(frame, address, inputs.Execute),
            _ => inputs.AdvanceLeaf(frame, address)
        };
    }

    private static NativeBattleDoom8Step AdvanceIo(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8Inputs inputs)
    {
        NativeBattleProfiledIoAdvance result = NativeBattleDoom8Io.Advance8C4F60(Read(frame, address), frame.State.ReadIo());
        frame = Write(frame with { State = frame.State.WithIo(result.State) }, address, result.Task);
        if (result.Request is { } request)
        {
            frame = inputs.Execute(frame, new NativeBattleDoom8QueueFile(request.Opcode, request.FileId, request.Index, request.DestinationAddress, request.DeclaredLength, request.CompletionCallback));
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            NativeBattleEffectHierarchy hierarchy = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        }
        return new(frame, complete ? 2 : 0);
    }

    private static NativeBattleDoom8Frame Append(NativeBattleDoom8Frame frame, int pool, uint callback, int length, uint parent, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Doom child selection requires its actual action parameters.");
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, length, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target));
        return frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } };
    }

    private static NativeBattleDoom8Frame NextPhase(NativeBattleDoom8Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        return Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static int ArenaOffset(uint address)
    {
        if (address < ArenaAddress || address - ArenaAddress >= NativeBattleEffectArena.Length)
        {
            throw new NotSupportedException("Doom requires a pointer inside its actual effect arena.");
        }
        return checked((int)(address - ArenaAddress));
    }

    private static void RequireRoot(NativeBattleDoom8Frame frame)
    {
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != Entry || frame.Effect.Pools.Length != NativeBattleDoom8State.Pools.Length)
        {
            throw new InvalidDataException("Doom requires its executed ten-pool hierarchy.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleDoom8State.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Doom pool layout contradicts its physical descriptors.");
            }
        }
    }
}
