using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa86Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleRinoa86IoResources Io, NativeBattleEffectOwnedModelResource Model, uint DefaultColor44, NativeBattleEffectHeapAllocation ActorOverride, NativeBattleEffectHeapAllocation SceneTim)
{
    public ImmutableArray<NativeBattleGfAnimationSource> Programs { get; init; }
    public ImmutableDictionary<uint, ImmutableArray<byte>> Meshes { get; init; } = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty;
    public ImmutableArray<byte> Sprite { get; init; }
    public short GroundReferenceZ { get; init; }
    public ImmutableDictionary<uint, uint> Sounds { get; init; } = ImmutableDictionary<uint, uint>.Empty;
}
public sealed record NativeBattleRinoa86State([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int AuxiliaryCursor, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
{
    [JsonRequired]
    public ImmutableArray<byte> MorphScratch { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> BodyProjected { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa86SceneMemory? Scene { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa86IoState? Io { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectOwnedModelState? BodyModel { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleActionMatrix>? BodyBones { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? RetainedScript { get; init; }
    public int Counter25BC218 { get; init; }
    public int Counter25B7080 { get; init; }
}

public sealed record NativeBattleRinoa86Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleRinoa86State State);
public sealed record NativeBattleRinoa86LeafStep(NativeBattleRinoa86Frame Frame, int Result);
public sealed record NativeBattleRinoa86Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleRinoa86Frame, uint, NativeBattleRinoa86LeafStep> AdvanceLeaf, Func<NativeBattleRinoa86Frame, uint, NativeBattleRinoa86Frame> BeginTextureRelease);
public sealed record NativeBattleRinoa86TargetInputs(NativeBattleRinoa86Inputs Controller, Func<NativeBattleRinoa86Frame, uint, NativeBattleRinoa86Frame> InitializeScene, Func<NativeBattleRinoa86Frame, ushort> ReadContactReady, Func<NativeBattleRinoa86Frame, ushort> ReadSceneComplete, Func<NativeBattleRinoa86Frame, int, NativeBattleRinoa86Frame> ApplyContact);

/// <summary>77F250/77F3E0's physical pools and eleven-state root. Private scene callbacks have their own required dispatcher.</summary>
public static class NativeBattleRinoa86Controller
{
    public const uint Descriptor = 0x25B4EA0;
    public static ImmutableArray<uint> Descriptors { get; } = [Descriptor, 0x25BB0C0, 0x25B4DA8, 0x25BAE18, 0x25B4EB0, 0x25B7088, 0x25BB0B0, 0x25BBEE8, 0x25BC1E0];
    public static ImmutableArray<uint> Banks { get; } = [0x25B4DD8, 0x25BB0D0, 0x25B4CD0, 0x25B9438, 0x25B4EC0, 0x25B7098, 0x25BAE30, 0x25BBD88, 0x25BBF00];
    public static ImmutableArray<ushort> Strides { get; } = [100, 88, 72, 1104, 144, 152, 64, 352, 48];
    public static ImmutableArray<ushort> Capacities { get; } = [2, 4, 3, 6, 60, 60, 10, 1, 2];
    private static ImmutableArray<int> ChildOrder { get; } = [1, 2, 8, 7, 4, 5, 3, 6];

    public static NativeBattleRinoa86Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleRinoa86Resources resources)
    {
        RequireBinding(effect);
        NativeBattleRinoa86State retained = memory.Rinoa86 ?? throw new NotSupportedException("Native Rinoa86 requires its original retained image-data owner; explicitly migrate the selected standalone.");
        packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native77F250 requires the actual50AFC0 parameter block.");
        NativeBattleEffectHierarchy hierarchy = new(Enumerable.Range(0, Descriptors.Length).Select(index => NativeBattleEffectTaskPool.Initialize(Descriptors[index], Banks[index], Strides[index], Capacities[index])).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x77F3E0, 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native77F250 requires its original first contact pointer.");
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
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x25B4CC8)?.ArenaOffset ?? throw new NotSupportedException("Native77F250 requires its retained mag085 initializer pointer.");
        if (source > NativeBattleEffectArena.Length - 114688)
        {
            throw new InvalidDataException("Native77F250's original double draw bank leaves the effect arena.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native77F250 requires the retained texture owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native77F250 requires its loaded effect arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = Descriptor }, new(source, checked(source + 114688), null, null, null) { Counter25BC218 = 0, Counter25B7080 = 0, RetainedScript = retained.Io?.Script ?? retained.RetainedScript, BodyBones = retained.BodyBones, Scene = retained.Scene, Io = retained.Io, MorphScratch = retained.MorphScratch, BodyProjected = retained.BodyProjected });
    }

    public static NativeBattleRinoa86Frame Advance(NativeBattleRinoa86Frame frame, NativeBattleRinoa86Inputs inputs)
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
        NativeBattleRinoa86Frame current = frame with { State = frame.State with { CopiedCamera = inputs.Camera } };
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep advanced = new NativeBattleEffectHierarchy(current.Effect.Pools).Advance(0, (hierarchy, address) =>
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != 0x77F3E0)
            {
                throw new NotSupportedException("Native77F3E0 main pool contains an unsupported callback.");
            }
            bool odd = (task.ReadByte(92) & 1) != 0;
            current = current with { State = current.State with { PrimaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 0 : 57344)), SecondaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 57344 : 114688)) } };
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
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    hierarchy = hierarchy.Write(address, task.WriteByte(99, 1));
                    hierarchy = hierarchy.Append8DC540(1, 0x77F5B0, 88, address, Select).Hierarchy;
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
                    throw new InvalidDataException("Native77F3E0's signed phase exceeds its eleven callbacks.");
            }
            hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(94, 0));
            ushort survivors = 0;
            foreach (int pool in ChildOrder)
            {
                NativeBattleEffectHierarchyStep children = hierarchy.Advance(pool, (childHierarchy, childAddress) =>
                {
                    current = current with { Effect = current.Effect with { Pools = childHierarchy.Pools } };
                    NativeBattleRinoa86LeafStep child = inputs.AdvanceLeaf(current, childAddress);
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
        return current with { Memory = current.Memory with { Rinoa86 = current.State }, Effect = current.Effect with { Pools = advanced.Hierarchy.Pools, ReturnedDescriptor = advanced.Survivors == 0 ? 0u : Descriptor } };
    }

    public static NativeBattleRinoa86LeafStep AdvanceTarget(NativeBattleRinoa86Frame frame, uint address, NativeBattleRinoa86TargetInputs inputs)
    {
        RequireBinding(frame.Effect);
        RequirePools(frame.Effect);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x77F5B0)
        {
            throw new InvalidDataException("Native77F5B0 requires its actual target task.");
        }
        NativeBattleRinoa86Inputs controller = inputs.Controller;
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
                hierarchy = hierarchy.Append8DC540(8, 0x77F780, 48, address, Select).Hierarchy;
                hierarchy = hierarchy.Append8DC540(2, 0x77FBF0, 72, address, Select).Hierarchy;
                hierarchy = Next(hierarchy, address);
                break;
            case 1:
                if (inputs.ReadContactReady(frame) == 1)
                {
                    int header = checked(frame.Effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(42)));
                    NativeBattleActionHeaderSlot group = controller.Packets.Headers[header];
                    int first = group.FirstContactIndex ?? throw new NotSupportedException("Native7842F0 requires its selected group's actual contact pointer.");
                    for (int contact = 0; contact < group.ReadByte(16); contact++)
                    {
                        frame = inputs.ApplyContact(frame, checked(first + contact));
                    }
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
                throw new InvalidDataException("Native77F5B0's signed phase exceeds its five callbacks.");
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
        if (effect.Binding.Effect != 86 || effect.Binding.Entry != 0x77F250 || effect.Binding.Initializer != 0x77F230 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Rinoa86 controller requires its own original module and parameters.");
        }
    }

    private static void RequirePools(NativeBattleOriginalEffectState effect)
    {
        if (effect.ReturnedDescriptor != Descriptor || effect.Pools.Length != Descriptors.Length)
        {
            throw new InvalidDataException("Native77F3E0 requires its executed nine-pool entry.");
        }
        for (int index = 0; index < Descriptors.Length; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            if (pool.DescriptorAddress != Descriptors[index] || pool.BankAddress != Banks[index] || pool.Stride != Strides[index] || pool.Slots.Length != Capacities[index])
            {
                throw new InvalidDataException("Native77F3E0 task pool contradicts its original physical directory.");
            }
        }
    }
}
