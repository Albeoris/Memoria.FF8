using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAngelWing84State([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int AuxiliaryCursor, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] int? Emitters, [property: JsonRequired] int? Geometry, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? NodeData { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectOwnedModelState? BodyModel { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleActionMatrix>? BodyBones { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAngelWing84BodyDrawStorage? BodyDrawStorage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? BodyShaderAngle { get; init; }
    public int Counter25C9F34 { get; init; }
    public int Counter25C3934 { get; init; }
    public ushort? EmitterScanCursor { get; init; }
    public ushort? GeometryScanCursor { get; init; }
    public ushort? EmitterResetWord { get; init; }
    public ushort? GeometryResetWord { get; init; }
    public ushort? PassEmitterCount { get; init; }
    public ushort? PassGeometryCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAngelWing84SceneMemory? Scene { get; init; }
}

public sealed record NativeBattleAngelWing84Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleAngelWing84State State);
public sealed record NativeBattleAngelWing84LeafStep(NativeBattleAngelWing84Frame Frame, int Result);
public sealed record NativeBattleAngelWing84Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleAngelWing84Frame, uint, NativeBattleAngelWing84LeafStep> AdvanceLeaf, Func<NativeBattleAngelWing84Frame, uint, NativeBattleAngelWing84Frame> BeginTextureRelease);
public sealed record NativeBattleAngelWing84TargetInputs(NativeBattleAngelWing84Inputs Controller, Func<NativeBattleAngelWing84Frame, uint, NativeBattleAngelWing84Frame> InitializeScene, Func<NativeBattleAngelWing84Frame, ushort> ReadContactReady, Func<NativeBattleAngelWing84Frame, ushort> ReadSceneComplete, Func<NativeBattleAngelWing84Frame, int, NativeBattleAngelWing84Frame> ApplyContact);

/// <summary>78D7B0/78D940's physical pools and eleven-state root. Private scene callbacks have their own required dispatcher.</summary>
public static class NativeBattleAngelWing84Controller
{
    public const uint Descriptor = 0x25C2588;
    public static ImmutableArray<uint> Descriptors { get; } = [Descriptor, 0x25C90C0, 0x25C2490, 0x25C8E20, 0x25C8378, 0x25C2598, 0x25C3938, 0x25C90B0, 0x25C9ED8];
    public static ImmutableArray<uint> Banks { get; } = [0x25C24C0, 0x25C90D0, 0x25C23B8, 0x25C8390, 0x25C6AE8, 0x25C25A8, 0x25C6828, 0x25C8E30, 0x25C9D80];
    public static ImmutableArray<ushort> Strides { get; } = [100, 88, 72, 676, 2096, 100, 88, 64, 340];
    public static ImmutableArray<ushort> Capacities { get; } = [2, 4, 3, 4, 3, 50, 8, 10, 1];
    private static ImmutableArray<int> ChildOrder { get; } = [1, 2, 8, 3, 5, 6, 4, 7];

    public static NativeBattleAngelWing84Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        RequireBinding(effect);
        packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native78D7B0 requires the actual50AFC0 parameter block.");
        NativeBattleEffectHierarchy hierarchy = new(Enumerable.Range(0, Descriptors.Length).Select(index => NativeBattleEffectTaskPool.Initialize(Descriptors[index], Banks[index], Strides[index], Capacities[index])).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x78D940, 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native78D7B0 requires its original first contact pointer.");
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
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x25C2350)?.ArenaOffset ?? throw new NotSupportedException("Native78D7B0 requires its retained mag083 initializer pointer.");
        if (source > NativeBattleEffectArena.Length - 98304)
        {
            throw new InvalidDataException("Native78D7B0's original double draw bank leaves the effect arena.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native78D7B0 requires the retained texture owner.");
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native78D7B0 requires its loaded effect arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = Descriptor }, new(source, checked(source + 98304), null, null, null, null, null) { Counter25C9F34 = 0, Counter25C3934 = 0 });
    }

    public static NativeBattleAngelWing84Frame Advance(NativeBattleAngelWing84Frame frame, NativeBattleAngelWing84Inputs inputs)
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
        NativeBattleAngelWing84Frame current = frame with { State = frame.State with { CopiedCamera = inputs.Camera } };
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep advanced = new NativeBattleEffectHierarchy(current.Effect.Pools).Advance(0, (hierarchy, address) =>
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != 0x78D940)
            {
                throw new NotSupportedException("Native78D940 main pool contains an unsupported callback.");
            }
            bool odd = (task.ReadByte(92) & 1) != 0;
            current = current with { State = current.State with { PrimaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 0 : 49152)), SecondaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 49152 : 98304)) } };
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
                        int emitters = current.State.AuxiliaryCursor;
                        int geometry = checked(emitters + 432);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native78DB30 requires its retained effect arena.");
                        arena = arena.Write(emitters, new byte[432]);
                        arena = arena.Write(geometry, new byte[26880]);
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Emitters = emitters, Geometry = geometry, AuxiliaryCursor = checked(emitters + 27312), EmitterScanCursor = 0, GeometryScanCursor = 0, EmitterResetWord = 0, GeometryResetWord = 0 } };
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    hierarchy = hierarchy.Write(address, task.WriteByte(99, 1));
                    hierarchy = hierarchy.Append8DC540(1, 0x78DBA0, 88, address, Select).Hierarchy;
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
                    throw new InvalidDataException("Native78D940's signed phase exceeds its eleven callbacks.");
            }
            hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(94, 0));
            current = current with { State = current.State with { PassEmitterCount = 0, PassGeometryCount = 0 } };
            ushort survivors = 0;
            foreach (int pool in ChildOrder)
            {
                NativeBattleEffectHierarchyStep children = hierarchy.Advance(pool, (childHierarchy, childAddress) =>
                {
                    current = current with { Effect = current.Effect with { Pools = childHierarchy.Pools } };
                    NativeBattleAngelWing84LeafStep child = inputs.AdvanceLeaf(current, childAddress);
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
        return current with { Effect = current.Effect with { Pools = advanced.Hierarchy.Pools, ReturnedDescriptor = advanced.Survivors == 0 ? 0u : Descriptor } };
    }

    public static NativeBattleAngelWing84LeafStep AdvanceTarget(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84TargetInputs inputs)
    {
        RequireBinding(frame.Effect);
        RequirePools(frame.Effect);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x78DBA0)
        {
            throw new InvalidDataException("Native78DBA0 requires its actual target task.");
        }
        NativeBattleAngelWing84Inputs controller = inputs.Controller;
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
                hierarchy = hierarchy.Append8DC540(2, 0x78DCE0, 72, address, Select).Hierarchy;
                hierarchy = Next(hierarchy, address);
                break;
            case 1:
                if (inputs.ReadContactReady(frame) == 1)
                {
                    int header = checked(frame.Effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(42)));
                    int first = controller.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native797540 requires its selected group's actual contact pointer.");
                    int contact = checked(first + unchecked((sbyte)task.ReadByte(43)));
                    frame = inputs.ApplyContact(frame, contact);
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
                throw new InvalidDataException("Native78DBA0's signed phase exceeds its five callbacks.");
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
        if (effect.Binding.Effect != 84 || effect.Binding.Entry != 0x78D7B0 || effect.Binding.Initializer != 0x78D790 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Angel Wing controller requires its own original module and parameters.");
        }
    }

    private static void RequirePools(NativeBattleOriginalEffectState effect)
    {
        if (effect.ReturnedDescriptor != Descriptor || effect.Pools.Length != Descriptors.Length)
        {
            throw new InvalidDataException("Native78D940 requires its executed nine-pool entry.");
        }
        for (int index = 0; index < Descriptors.Length; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            if (pool.DescriptorAddress != Descriptors[index] || pool.BankAddress != Banks[index] || pool.Stride != Strides[index] || pool.Slots.Length != Capacities[index])
            {
                throw new InvalidDataException("Native78D940 task pool contradicts its original physical directory.");
            }
        }
    }
}
