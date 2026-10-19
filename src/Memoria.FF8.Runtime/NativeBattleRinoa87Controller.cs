using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleRinoa87State State);
public sealed record NativeBattleRinoa87LeafStep(NativeBattleRinoa87Frame Frame, int Result);
public sealed record NativeBattleRinoa87Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleRinoa87Frame, uint, NativeBattleRinoa87LeafStep> AdvanceLeaf, Func<NativeBattleRinoa87Frame, uint, NativeBattleRinoa87Frame> BeginTextureRelease);
public sealed record NativeBattleRinoa87TargetInputs(NativeBattleRinoa87Inputs Controller, Func<NativeBattleRinoa87Frame, uint, NativeBattleRinoa87Frame> InitializeScene, Func<NativeBattleRinoa87Frame, ushort> ReadContactReady, Func<NativeBattleRinoa87Frame, ushort> ReadSceneComplete, Func<NativeBattleRinoa87Frame, int, NativeBattleRinoa87Frame> ApplyContact);

/// <summary>773870/773A10's physical pools, node workspaces and eleven-state root.</summary>
public static class NativeBattleRinoa87Controller
{
    public const uint Descriptor = 0x25AD090;
    public static ImmutableArray<uint> Descriptors { get; } = [Descriptor, 0x25B37D8, 0x25ACF98, 0x25B3538, 0x25B2A90, 0x25AD0A0, 0x25AECD8, 0x25B37C8, 0x25B4620, 0x25B4B10];
    public static ImmutableArray<uint> Banks { get; } = [0x25ACFC8, 0x25B37E8, 0x25ACEC0, 0x25B2AA8, 0x25B1C68, 0x25AD0B0, 0x25AECE8, 0x25B3548, 0x25B44A8, 0x25B4648];
    public static ImmutableArray<ushort> Strides { get; } = [100, 88, 72, 676, 604, 144, 152, 64, 376, 48];
    public static ImmutableArray<ushort> Capacities { get; } = [2, 4, 3, 4, 6, 50, 80, 10, 1, 2];
    private static ImmutableArray<int> ChildOrder { get; } = [1, 2, 9, 8, 3, 5, 6, 4, 7];

    public static NativeBattleRinoa87Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        RequireBinding(effect);
        NativeBattleRinoa87State retained = memory.Rinoa87 ?? throw new NotSupportedException("Native Rinoa87 requires its original retained image-data owner; explicitly migrate the selected standalone.");
        packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native773870 requires the actual50AFC0 parameter block.");
        NativeBattleEffectHierarchy hierarchy = new(Enumerable.Range(0, Descriptors.Length).Select(index => NativeBattleEffectTaskPool.Initialize(Descriptors[index], Banks[index], Strides[index], Capacities[index])).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x773A10, 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native773870 requires its original first contact pointer.");
        byte target = packets.ReadContact(first).Primary.ActorIndex;
        byte targets = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(targets - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(12, 0x1D99A78).WriteByte(41, 0).WriteByte(44, header.ReadByte(0)).WriteByte(45, target).WriteWord(90, targets).WriteWord(88, groups).WriteByte(47, maximum);
        hierarchy = hierarchy.Write(root.Address, task);
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x25ACEB0)?.ArenaOffset ?? throw new NotSupportedException("Native773870 requires its retained mag086 initializer pointer.");
        if (source > NativeBattleEffectArena.Length - 81920)
        {
            throw new InvalidDataException("Native773870's original double draw bank leaves the effect arena.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native773870 requires the retained texture owner.");
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native773870 requires its loaded effect arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = Descriptor }, retained with { SourceArenaOffset = source, AuxiliaryCursor = checked(source + 81920), PrimaryDrawCursor = null, SecondaryDrawCursor = null, CopiedCamera = null, Counter25B4B4C = 0, Counter25AECD4 = 0 });
    }

    public static NativeBattleRinoa87Frame Advance(NativeBattleRinoa87Frame frame, NativeBattleRinoa87Inputs inputs)
    {
        RequireBinding(frame.Effect);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        RequirePools(frame.Effect);
        inputs.Packets.Validate();
        inputs.Camera.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleRinoa87Frame current = frame with { State = frame.State with { CopiedCamera = inputs.Camera } };
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep advanced = new NativeBattleEffectHierarchy(current.Effect.Pools).Advance(0, (hierarchy, address) =>
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != 0x773A10)
            {
                throw new NotSupportedException("Native773A10 main pool contains an unsupported callback.");
            }
            bool odd = (task.ReadByte(92) & 1) != 0;
            current = current with { State = current.State with { PrimaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 0 : 40960)), SecondaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 40960 : 81920)) } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
            hierarchy = hierarchy.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                case 1:
                case 5:
                    hierarchy = Next(hierarchy, address);
                    break;
                case 2:
                    if (task.ReadByte(40) == 0)
                    {
                        int emitter = current.State.AuxiliaryCursor;
                        int geometry = checked(emitter + 2160);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native773C00 requires its actual effect arena.");
                        arena = arena.Write(emitter, new byte[2160]).Write(geometry, new byte[33600]);
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { EmitterOffset = emitter, GeometryOffset = geometry, AuxiliaryCursor = checked(geometry + 33600), Word25AECD0 = 0, Word25B4640 = 0, Word25B4444 = 0, Word25ACEB8 = 0 } };
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    hierarchy = hierarchy.Write(address, task.WriteByte(99, 1));
                    hierarchy = hierarchy.Append8DC540(1, 0x773C70, 88, address, Select).Hierarchy;
                    hierarchy = Next(hierarchy, address);
                    break;
                case 4:
                    if (task.ReadByte(99) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(42)) >= unchecked((short)task.ReadWord(88)))
                        {
                            hierarchy = Next(hierarchy, address);
                        }
                        else
                        {
                            hierarchy = hierarchy.Write(address, task.WriteByte(42, unchecked((byte)(task.ReadByte(42) + 1))).WriteByte(46, unchecked((byte)(task.ReadByte(46) + 1))).WriteByte(41, unchecked((byte)(task.ReadByte(41) - 1))));
                        }
                    }
                    break;
                case 6:
                    if (task.ReadWord(94) == 0)
                    {
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 7:
                    current = inputs.BeginTextureRelease(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, address);
                    hierarchy = Next(new(current.Effect.Pools), address);
                    break;
                case 8:
                    if (task.ReadByte(98) != 0)
                    {
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 9:
                    hierarchy = hierarchy.Write(address, task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    hierarchy = Next(hierarchy, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Native773A10's signed phase exceeds its eleven callbacks.");
            }
            hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(94, 0));
            current = current with { State = current.State with { Word25B4B08 = 0, Word25B4630 = 0 } };
            ushort survivors = 0;
            foreach (int pool in ChildOrder)
            {
                NativeBattleEffectHierarchyStep children = hierarchy.Advance(pool, (childHierarchy, childAddress) =>
                {
                    current = current with { Effect = current.Effect with { Pools = childHierarchy.Pools } };
                    NativeBattleRinoa87LeafStep child = inputs.AdvanceLeaf(current, childAddress);
                    current = child.Frame;
                    return new(new(current.Effect.Pools), child.Result);
                });
                hierarchy = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
                hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(94, survivors));
            }
            task = hierarchy.Read(address);
            task = task.WriteWord(92, unchecked((ushort)(task.ReadWord(92) + 1))).WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
            hierarchy = hierarchy.Write(address, task);
            bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
            return new(complete ? hierarchy.Complete8DC530(address) : hierarchy, complete ? 2 : 0);
        });
        return current with { Memory = current.Memory with { Rinoa87 = current.State }, Effect = current.Effect with { Pools = advanced.Hierarchy.Pools, ReturnedDescriptor = advanced.Survivors == 0 ? 0u : Descriptor } };
    }

    public static NativeBattleRinoa87LeafStep AdvanceTarget(NativeBattleRinoa87Frame frame, uint address, NativeBattleRinoa87TargetInputs inputs)
    {
        RequireBinding(frame.Effect);
        RequirePools(frame.Effect);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x773C70)
        {
            throw new InvalidDataException("Native773C70 requires its actual target task.");
        }
        NativeBattleRinoa87Inputs controller = inputs.Controller;
        task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Enumerable.Range(0, 7).Select(controller.ReadModel).ToArray(), controller.ReadPoseSource);
        NativeBattleGraphicsModel model = controller.ReadModel(task.ReadByte(45));
        NativeBattlePoseSource source = controller.ReadPoseSource(task.ReadByte(45));
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native8DC870 requires the target's actual selected pose.");
        NativeBattleDecodedPose pose = source.Sample(sequence.Pose ?? throw new NotSupportedException("Native8DC870 requires its pose index."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native8DC870 requires its decoded frame."), sequence.PoseFlags109);
        task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
        hierarchy = hierarchy.Write(address, task);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                frame = inputs.InitializeScene(frame, address);
                hierarchy = new(frame.Effect.Pools);
                NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
                NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, controller.Packets, pointer, group, target);
                hierarchy = hierarchy.Append8DC540(9, 0x773E30, 48, address, Select).Hierarchy;
                hierarchy = hierarchy.Append8DC540(2, 0x774240, 72, address, Select).Hierarchy;
                hierarchy = Next(hierarchy, address);
                break;
            case 1:
                if (inputs.ReadContactReady(frame) == 1)
                {
                    int header = checked(frame.Effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(42)));
                    NativeBattleActionHeaderSlot group = controller.Packets.Headers[header];
                    int first = group.FirstContactIndex ?? throw new NotSupportedException("Native77F0D0 requires its selected group's actual contact pointer.");
                    int selectedContact = checked(first + unchecked((sbyte)task.ReadByte(43)));
                    frame = inputs.ApplyContact(frame, selectedContact);
                    hierarchy = Next(new(frame.Effect.Pools), address);
                }
                break;
            case 2:
                if (task.ReadByte(40) == 0 || inputs.ReadSceneComplete(frame) == 1)
                {
                    hierarchy = Next(hierarchy, address);
                }
                break;
            case 3:
                hierarchy = hierarchy.Write(address, task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                uint parent = task.ReadDword(16);
                hierarchy = hierarchy.Write(parent, hierarchy.Read(parent).WriteByte(99, 0));
                hierarchy = Next(hierarchy, address);
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Native773C70's signed phase exceeds its five callbacks.");
        }
        task = hierarchy.Read(address);
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);
    }

    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1))));
    }

    private static void RequireBinding(NativeBattleOriginalEffectState effect)
    {
        effect.Validate();
        if (effect.Binding.Effect != 87 || effect.Binding.Entry != 0x773870 || effect.Binding.Initializer != 0x773850 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Rinoa87 controller requires its own original module and parameters.");
        }
    }

    private static void RequirePools(NativeBattleOriginalEffectState effect)
    {
        if (effect.ReturnedDescriptor != Descriptor || effect.Pools.Length != Descriptors.Length)
        {
            throw new InvalidDataException("Native773A10 requires its executed ten-pool entry.");
        }
        for (int index = 0; index < Descriptors.Length; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            if (pool.DescriptorAddress != Descriptors[index] || pool.BankAddress != Banks[index] || pool.Stride != Strides[index] || pool.Slots.Length != Capacities[index])
            {
                throw new InvalidDataException("Native773A10 task pool contradicts its original physical directory.");
            }
        }
    }
}
