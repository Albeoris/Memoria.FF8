using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuraSceneResource(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleCuraResources(NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleModuleResource> Sprites, uint Sound)
{
    public ushort Effect { get; init; } = 25;
    public ImmutableArray<NativeBattleCuraSceneResource> Scenes { get; init; } = [];
    public ImmutableArray<ImmutableArray<short>> EmissionSchedules { get; init; } = [];
}
public sealed record NativeBattleCuraState([property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int OddDrawBase26FD114, [property: JsonRequired] int EvenDrawBase26FD110)
{
    public NativeBattleCureActorContext? ActorContext { get; init; }
}
public sealed record NativeBattleCuraFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleCuraState State);
public sealed record NativeBattleCuraControllerInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource)
{
    public Action<int>? ApplyContact { get; init; }
}
public sealed record NativeBattleCuraLeafStep(NativeBattleCuraFrame Frame, int Result);
public sealed record NativeBattleCuraControllerStep(NativeBattleCuraFrame Frame, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>889D60/889E40/889F40's actual Cura pools, controller phases and506690 contact producer.</summary>
public static class NativeBattleCuraController
{
    public static NativeBattleCuraFrame Initialize889D60(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleCuraResources resources)
    {
        effect.Validate();
        packets.Validate();
        NativeBattleCuraFamilyProfile profile = NativeBattleCuraFamilyProfile.ForEffect(effect.Binding.Effect);
        if (resources.Effect != profile.Effect)
        {
            throw new InvalidDataException("Native Cura family resources belong to another module.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native889D60 requires50AFC0 parameters.");
        if (effect.Binding.Entry != profile.Entry)
        {
            throw new InvalidDataException("Native Cura requires the actual889D60 entry.");
        }
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.Callback(0x889E40), 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native889D60 requires its header's original contact pointer.");
        byte target = packets.ReadContact(firstContact).Primary.ActorIndex;
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteByte(0x2D, target).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        hierarchy = hierarchy.Write(root.Address, task);
        if ((parameters.Flags79 & 1) == 0)
        {
            int textureOffset = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Native889D60 requires the actual26E5908 initializer resource pointer.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native889D60 requires the retained5099A0/505E30 texture owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native889D60 requires the loaded571870 arena."), textureOffset);
            memory = memory with { Textures = textures };
        }
        //571B50 is literally MOV EAX,20DFAB8;RET. These are arena offsets, independent of the allocator cursor.
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = profile.Pools[0].Descriptor }, new(null, 0, 0x12000));
    }

    public static NativeBattleCuraControllerStep Advance889E40(NativeBattleCuraFrame frame, NativeBattleCuraControllerInputs inputs, NativeBattleCuraResources resources, Func<NativeBattleCuraFrame, uint, NativeBattleCuraLeafStep> advanceLeaf)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, [], []);
        }
        RequireController(frame.Effect);
        NativeBattleCuraFamilyProfile profile = NativeBattleCuraFamilyProfile.ForEffect(frame.Effect.Binding.Effect);
        if (resources.Effect != profile.Effect)
        {
            throw new InvalidDataException("Native Cura family resources belong to another module.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleCuraFrame current = frame;
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep main = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.Callback(0x889E40))
            {
                throw new NotSupportedException("Native Cura main task has an unsupported original callback.");
            }
            current = current with { State = current.State with { PrimaryDrawCursor = (task.ReadWord(0x5C) & 1) != 0 ? current.State.OddDrawBase26FD114 : current.State.EvenDrawBase26FD110 } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    state = IncrementPhase(state, address);
                    break;
                case 1:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = state.Append8DC540(1, profile.Callback(0x889F40), 88, address, Select).Hierarchy;
                    state = IncrementPhase(state, address);
                    break;
                case 2:
                    if (task.ReadByte(0x63) == 0)
                    {
                        sbyte group = unchecked((sbyte)task.ReadByte(0x2A));
                        task = group < unchecked((short)task.ReadWord(0x58)) ? task.WriteByte(0x2A, unchecked((byte)(group + 1))).WriteByte(0x29, 1) : task.WriteByte(0x29, 3);
                        state = state.Write(address, task);
                    }
                    break;
                case 3:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 4));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Native889E40 signed phase exceeds its five original callbacks.");
            }
            int survivors = 0;
            for (int pool = 1; pool < 4; pool++)
            {
                int selected = pool;
                NativeBattleEffectHierarchyStep children = state.Advance(pool, (childState, childAddress) =>
                {
                    if (selected == 1)
                    {
                        return AdvanceChild(childState, childAddress);
                    }
                    current = current with { Effect = current.Effect with { Pools = childState.Pools } };
                    NativeBattleCuraLeafStep leaf = advanceLeaf(current, childAddress);
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
        NativeBattleOriginalEffectState effect = current.Effect with { Pools = main.Hierarchy.Pools, ReturnedDescriptor = main.Survivors == 0 ? 0u : profile.Pools[0].Descriptor };
        return new(current with { Effect = effect }, contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleEffectHierarchyCallback AdvanceChild(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.Callback(0x889F40))
            {
                throw new NotSupportedException("Native Cura child task has an unsupported original callback.");
            }
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattlePoseSource source = inputs.ReadPoseSource(task.ReadByte(0x2D));
            NativeBattleSequenceState pose = model.SequenceState ?? throw new NotSupportedException("Native Cura bounds require the current body pose.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(pose.Pose ?? throw new NotSupportedException("Native Cura bounds require a selected body pose."), pose.PoseFrame114 ?? throw new NotSupportedException("Native Cura bounds require the current pose frame."), pose.PoseFlags109));
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    Spawn(3, 0x88A070, 132);
                    Spawn(2, 0x88A740, 200, 0);
                    Spawn(2, 0x88B530, 200);
                    Spawn(2, 0x88B810, 200);
                    Spawn(2, 0x88B3D0, 200);
                    state = IncrementPhase(state, address);
                    break;
                case 1:
                    Spawn(2, 0x88A740, 200, 0x800);
                    Spawn(2, 0x88BBD0, 200);
                    state = IncrementPhase(state, address);
                    break;
                case 2:
                    Spawn(2, 0x88A740, 200, 0xC00);
                    state = IncrementPhase(state, address);
                    break;
                case 3:
                    Spawn(2, 0x88A740, 200, 0xE00);
                    state = IncrementPhase(state, address);
                    break;
                case 4:
                    if (unchecked((short)task.ReadWord(0x24)) > 11)
                    {
                        Spawn(2, 0x88CC70, 200);
                        state = IncrementPhase(state, address);
                    }
                    break;
                case 5:
                    if (unchecked((short)task.ReadWord(0x24)) > 30)
                    {
                        sbyte group = unchecked((sbyte)task.ReadByte(0x2A));
                        int headerIndex = checked(parameters.HeaderIndex7C + group);
                        int contact = inputs.Packets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Native88CEA0 requires the selected header's actual first contact pointer.");
                        inputs.ApplyContact?.Invoke(contact);
                        contacts.Add(contact);
                        task = state.Read(address).WriteByte(0x26, (byte)(state.Read(address).ReadByte(0x26) | 1));
                        state = state.Write(address, task);
                        state = IncrementPhase(state, address);
                    }
                    break;
                case 6:
                    break;
                default:
                    throw new InvalidDataException("Native889F40 signed phase exceeds its seven original callbacks.");
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

            void Spawn(int pool, uint callback, int bytes, ushort? rotation = null)
            {
                NativeBattleEffectHierarchyAppend append = state.Append8DC540(pool, profile.Callback(callback), bytes, address, Select);
                state = append.Hierarchy;
                if (rotation is ushort value)
                {
                    if (append.Address == 0)
                    {
                        throw new NotSupportedException("Native Cura allocation returned null before its required C4 write.");
                    }
                    state = state.Write(append.Address, state.Read(append.Address).WriteWord(0xC4, value));
                }
            }
        }
    }

    private static NativeBattleEffectHierarchy IncrementPhase(NativeBattleEffectHierarchy state, uint address)
    {
        NativeBattleModulePoolTask task = state.Read(address);
        return state.Write(address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static void RequireController(NativeBattleOriginalEffectState effect)
    {
        NativeBattleCuraFamilyProfile profile = NativeBattleCuraFamilyProfile.ForEffect(effect.Binding.Effect);
        if (effect.Binding.Entry != profile.Entry || effect.Parameters is null || effect.ReturnedDescriptor != profile.Pools[0].Descriptor || effect.Pools.IsDefault || effect.Pools.Length != 4)
        {
            throw new InvalidDataException("Native Cura family requires its executed original pool owners.");
        }
        new NativeBattleEffectHierarchy(effect.Pools).Validate();
        ImmutableArray<NativeBattleCureFamilyPool> banks = profile.Pools;
        for (int index = 0; index < banks.Length; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            if (pool.DescriptorAddress != banks[index].Descriptor || pool.BankAddress != banks[index].Bank || pool.Stride != banks[index].Stride || pool.Slots.Length != banks[index].Capacity)
            {
                throw new InvalidDataException("Native Cura task bank differs from its889D60 initialization.");
            }
        }
    }
}
