using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCureResources(NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleModuleResource> Sprites, uint Sound)
{
    public ushort Effect { get; init; } = 1;
}
public sealed record NativeBattleCureState([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
{
    public int Counter278C7E0 { get; init; }
    public int Counter2783E10 { get; init; }
    public NativeBattleCureActorContext? ActorContext { get; init; }
}

public sealed record NativeBattleCureFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleCureState State);
public sealed record NativeBattleCureControllerInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource)
{
    public Action<int>? ApplyContact { get; init; }
}
public sealed record NativeBattleCureLeafStep(NativeBattleCureFrame Frame, int Result);
public sealed record NativeBattleCureControllerStep(NativeBattleCureFrame Frame, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>8D6A00/8D6B30/8D6CC0, including the original child-pool scheduling and506690 contact point.</summary>
public static class NativeBattleCureController
{
    public static NativeBattleCureFrame Initialize8D6A00(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleCureResources resources)
    {
        effect.Validate();
        packets.Validate();
        NativeBattleCureFamilyProfile profile = NativeBattleCureFamilyProfile.ForEffect(effect.Binding.Effect);
        if (resources.Effect != profile.Effect)
        {
            throw new InvalidDataException("Native Cure family resources belong to another module.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native8D6A00 requires50AFC0 parameters.");
        if (effect.Binding.Entry != profile.Entry)
        {
            throw new InvalidDataException("Native Cure controller requires its original8D6A00 entry.");
        }
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.Callback(0x8D6B30), 100);
        hierarchy = root.Hierarchy;
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78);
        NativeBattleEffectTaskActors actors = SelectActors(parameters, packets, 0x1D99A78, unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        byte targets = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(targets - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        task = task.WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target).WriteByte(0x29, 0).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
        hierarchy = hierarchy.Write(root.Address, task);
        int arenaOffset = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Native8D6A00 requires the observed277AEC0 initializer pointer.");
        if (arenaOffset > NativeBattleEffectArena.Length - 0x20000)
        {
            throw new NotSupportedException("Native8D6A00 draw-buffer bases leave its original effect arena.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native8D6A00 requires the retained5099A0/505E30 texture owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native8D6A00 requires its actual571B80 arena."), arenaOffset);
            memory = memory with { Textures = textures };
        }
        effect = effect with { Pools = hierarchy.Pools, ReturnedDescriptor = profile.Pools[0].Descriptor };
        return new(memory, effect, new(arenaOffset, null, null, null));
    }

    public static NativeBattleCureControllerStep Advance8D6B30(NativeBattleCureFrame frame, NativeBattleCureControllerInputs inputs, NativeBattleCureResources resources, Func<NativeBattleCureFrame, uint, NativeBattleCureLeafStep> advanceLeaf)
    {
        frame.Effect.Validate();
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, [], []);
        }
        RequireController(frame.Effect);
        NativeBattleCureFamilyProfile profile = NativeBattleCureFamilyProfile.ForEffect(frame.Effect.Binding.Effect);
        if (resources.Effect != profile.Effect)
        {
            throw new InvalidDataException("Native Cure family resources belong to another module.");
        }
        inputs.Camera.Validate();
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleCureFrame current = frame;
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep main = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.Callback(0x8D6B30))
            {
                throw new NotSupportedException($"Native Cure main callback{task.Callback:X} is not8D6B30.");
            }
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { State = current.State with { PrimaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 0 : 0x10000)), SecondaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 0x10000 : 0x20000)), CopiedCamera = inputs.Camera } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(inputs), inputs.ReadPoseSource);
            state = state.Write(address, task);
            int phase = unchecked((sbyte)task.ReadByte(0x29));
            switch (phase)
            {
                case 0:
                case 1:
                case 5:
                case 7:
                case 8:
                    state = IncrementPhase(state, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        state = IncrementPhase(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = state.Append8DC540(1, profile.Callback(0x8D6CC0), 88, address, Select).Hierarchy;
                    state = IncrementPhase(state, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        sbyte group = unchecked((sbyte)task.ReadByte(0x2A));
                        if (group < unchecked((short)task.ReadWord(0x58)))
                        {
                            task = task.WriteByte(0x2A, unchecked((byte)(group + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(phase - 1)));
                            state = state.Write(address, task);
                        }
                        else
                        {
                            state = IncrementPhase(state, address);
                        }
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        state = IncrementPhase(state, address);
                    }
                    break;
                case 9:
                    state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    state = IncrementPhase(state, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Native8D6B30 signed phase exceeds its eleven original callbacks.");
            }
            int survivors = 0;
            for (int pool = 1; pool < 4; pool++)
            {
                int selectedPool = pool;
                NativeBattleEffectHierarchyStep children = state.Advance(pool, (childState, childAddress) =>
                {
                    if (selectedPool == 1)
                    {
                        return AdvanceChild(childState, childAddress);
                    }
                    current = current with { Effect = current.Effect with { Pools = childState.Pools } };
                    NativeBattleCureLeafStep leaf = advanceLeaf(current, childAddress);
                    current = leaf.Frame;
                    return new(new(current.Effect.Pools), leaf.Result);
                });
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
            }
            task = state.Read(address);
            task = task.WriteWord(0x5E, (ushort)survivors).WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        });
        current = current with { Effect = current.Effect with { Pools = main.Hierarchy.Pools, ReturnedDescriptor = main.Survivors == 0 ? 0u : profile.Pools[0].Descriptor } };
        return new(current, contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleEffectHierarchyCallback AdvanceChild(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.Callback(0x8D6CC0))
            {
                throw new NotSupportedException($"Native Cure child callback{task.Callback:X} is not8D6CC0.");
            }
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(inputs), inputs.ReadPoseSource);
            int target = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(target);
            NativeBattlePoseSource source = inputs.ReadPoseSource(target);
            NativeBattleSequenceState pose = model.SequenceState ?? throw new NotSupportedException("Native8DC870 requires its target's posed skeleton owner.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(pose.Pose ?? throw new NotSupportedException("Native8DC870 has no selected pose."), pose.PoseFrame114 ?? throw new NotSupportedException("Native8DC870 has no decoded pose frame."), pose.PoseFlags109));
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    state = state.Append8DC540(2, profile.Callback(0x8D6D80), 108, address, Select).Hierarchy;
                    state = IncrementPhase(state, address);
                    break;
                case 1:
                    state = IncrementPhase(state, address);
                    break;
                case 2:
                    if (unchecked((short)task.ReadWord(0x24)) >= 15)
                    {
                        int contact = ContactIndex(parameters, inputs.Packets, unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
                        _ = inputs.Packets.ReadContact(contact);
                        inputs.ApplyContact?.Invoke(contact);
                        contacts.Add(contact);
                        state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                        state = IncrementPhase(state, address);
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Native8D6CC0 signed phase exceeds its four original callbacks.");
            }
            task = state.Read(address);
            if (task.ReadWord(0x24) == 0)
            {
                sounds.Add(new(resources.Sound, 0, null) { Pan = 128 });
            }
            task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        }
    }

    private static NativeBattleEffectHierarchy IncrementPhase(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static NativeBattleGraphicsModel[] Models(NativeBattleCureControllerInputs inputs) => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();

    internal static NativeBattleEffectTaskActors SelectActors(NativeBattleEffectParameters parameters, NativeBattleActionPacketsState packets, uint parameterAddress, sbyte group, sbyte target)
    {
        if (parameterAddress != 0x1D99A78)
        {
            throw new NotSupportedException("Native8DC540 requires its observed50AFC0 parameter pointer.");
        }
        int header = checked(parameters.HeaderIndex7C + group);
        int contact = ContactIndex(parameters, packets, group, target);
        return new(packets.Headers[header].ReadByte(0), packets.ReadContact(contact).Primary.ActorIndex);
    }

    private static int ContactIndex(NativeBattleEffectParameters parameters, NativeBattleActionPacketsState packets, sbyte group, sbyte target)
    {
        int header = checked(parameters.HeaderIndex7C + group);
        if ((uint)header >= packets.Headers.Length)
        {
            throw new NotSupportedException("Native8DC540 signed header reference leaves the retained physical arena.");
        }
        return checked((packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native8DC540 requires its physical contact pointer.")) + target);
    }

    private static void RequireController(NativeBattleOriginalEffectState effect)
    {
        NativeBattleCureFamilyProfile profile = NativeBattleCureFamilyProfile.ForEffect(effect.Binding.Effect);
        if (effect.Binding.Entry != profile.Entry || effect.Parameters is null || effect.ReturnedDescriptor != profile.Pools[0].Descriptor || effect.Pools.Length != 4)
        {
            throw new InvalidDataException("Native Cure family scheduler requires its four initialized original descriptors.");
        }
        new NativeBattleEffectHierarchy(effect.Pools).Validate();
        ImmutableArray<NativeBattleCureFamilyPool> banks = profile.Pools;
        for (int index = 0; index < banks.Length; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            if (pool.DescriptorAddress != banks[index].Descriptor || pool.BankAddress != banks[index].Bank || pool.Stride != banks[index].Stride || pool.Slots.Length != banks[index].Capacity)
            {
                throw new InvalidDataException("Native Cure scheduler lost an original physical bank, stride or capacity.");
            }
        }
    }
}
