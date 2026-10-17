using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>88CF70/883110 and their original main/target's physical pools, ordered callbacks and original header/contact selectors.</summary>
public static class NativeBattleRestorationController
{
    public static NativeBattleRestorationFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleRestorationResources resources)
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(resources.Effect);
        effect.Validate();
        packets.Validate();
        RequireResources(effect, resources, profile);
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native88CF70/883110 requires50AFC0 parameters.");
        NativeBattleEffectHierarchy hierarchy = new(Enumerable.Range(0, 4).Select(index => NativeBattleEffectTaskPool.Initialize(profile.Descriptors[index], profile.Banks[index], new ushort[] { 100, 88, 676, 64 }[index], new ushort[] { 2, 4, 3, 10 }[index])).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.MainCallback, 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native88CF70/883110 requires its header's actual contact pointer.");
        byte target = packets.ReadContact(firstContact).Primary.ActorIndex;
        byte targets = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(targets - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteByte(0x29, 0).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, target).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
        hierarchy = hierarchy.Write(root.Address, task);
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Native88CF70/883110 requires its actual initializer pointer, including texture-skip entries.");
        if (source > NativeBattleEffectArena.Length - 2 * profile.DrawBankBytes)
        {
            throw new InvalidDataException("Native88CF70/883110 draw/auxiliary banks leave the571870 arena.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native88CF70/883110 requires the retained5099A0 texture owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native88CF70/883110 requires its loaded571870 arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = profile.Descriptors[0] }, new(source, null, null, checked(source + 2 * profile.DrawBankBytes), null));
    }

    public static NativeBattleRestorationControllerStep Advance(NativeBattleRestorationFrame frame, NativeBattleRestorationControllerInputs inputs, NativeBattleRestorationResources resources, Func<NativeBattleRestorationFrame, uint, NativeBattleRestorationLeafStep>? advanceLeaf = null)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, [], []);
        }
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(resources.Effect);
        RequireResources(frame.Effect, resources, profile);
        RequireController(frame.Effect, profile);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        inputs.Camera.Validate();
        NativeBattleRestorationFrame current = frame with { State = frame.State with { CopiedCamera = inputs.Camera } };
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep main = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.MainCallback)
            {
                throw new NotSupportedException("Native Esuna/Full-life main pool has an unsupported callback.");
            }
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { State = current.State with { PrimaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 0 : profile.DrawBankBytes)), SecondaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? profile.DrawBankBytes : 2 * profile.DrawBankBytes)) } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                case 1:
                case 7:
                case 8:
                    state = Next(state, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        int first = current.State.AuxiliaryCursor;
                        int second = checked(first + profile.EmitterBytes);
                        int end = checked(second + profile.GeometryBytes);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native88D250/8833F0 requires its retained effect arena.");
                        arena = arena.Write(first, new byte[profile.EmitterBytes]);
                        arena = arena.Write(second, new byte[profile.GeometryBytes]);
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Emitters = first, Geometry = second, AuxiliaryCursor = end, EmitterScanCursor = 0, GeometryScanCursor = 0, EmitterResetWord = 0, GeometryResetWord = 0 } };
                        state = state.Append8DC540(3, profile.FadeInCallback, 64, address, Select).Hierarchy;
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = state.Append8DC540(1, profile.TargetCallback, 88, address, Select).Hierarchy;
                    state = Next(state, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(0x2A)) < unchecked((short)task.ReadWord(0x58)))
                        {
                            task = task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
                        }
                        else
                        {
                            task = task.WriteWord(0x60, profile.RootWait).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                        }
                        state = state.Write(address, task);
                    }
                    break;
                case 5:
                    task = task.WriteWord(0x60, unchecked((ushort)(task.ReadWord(0x60) - 1)));
                    state = state.Write(address, task);
                    if (unchecked((short)task.ReadWord(0x60)) <= 0)
                    {
                        state = state.Append8DC540(3, profile.FadeOutCallback, 64, address, Select).Hierarchy;
                        state = Next(state, address);
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        state = Next(state, address);
                    }
                    break;
                case 9:
                    state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    state = Next(state, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Native88D0A0/883240 signed phase exceeds its eleven original callbacks.");
            }
            state = state.Write(address, state.Read(address).WriteWord(0x5E, 0));
            current = current with { State = current.State with { PassEmitterCount = 0, PassGeometryCount = 0 } };
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
                    NativeBattleRestorationLeafStep leaf = (advanceLeaf ?? throw new NotSupportedException($"Native restoration callback0x{childState.Read(childAddress).Callback:X} requires its original private context/rendering contract."))(current, childAddress);
                    current = leaf.Frame;
                    return new(new(current.Effect.Pools), leaf.Result);
                });
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
                state = state.Write(address, state.Read(address).WriteWord(0x5E, (ushort)survivors));
            }
            task = state.Read(address);
            task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        });
        NativeBattleOriginalEffectState effect = current.Effect with { Pools = main.Hierarchy.Pools, ReturnedDescriptor = main.Survivors == 0 ? 0u : profile.Descriptors[0] };
        return new(current with { Effect = effect }, contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleEffectHierarchyCallback AdvanceChild(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.TargetCallback)
            {
                throw new NotSupportedException("Native Esuna/Full-life child pool has an unsupported callback.");
            }
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState pose = model.SequenceState ?? throw new NotSupportedException("Native8DC870 requires the target's current decoded pose.");
            NativeBattlePoseSource source = inputs.ReadPoseSource(task.ReadByte(0x2D));
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(pose.Pose ?? throw new NotSupportedException("Native8DC870 requires its selected pose."), pose.PoseFrame114 ?? throw new NotSupportedException("Native8DC870 requires its decoded frame."), pose.PoseFlags109));
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    NativeBattleEffectHierarchyAppend append = state.Append8DC540(2, profile.GenericCallback, 676, address, Select);
                    state = append.Hierarchy;
                    if (append.Address == 0)
                    {
                        throw new NotSupportedException("Native88D470/883610 returned null before its original field writes.");
                    }
                    state = state.Write(append.Address, state.Read(append.Address).WriteDword(0x30, profile.ExecutableAddress).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 45));
                    state = Next(state, address);
                    break;
                case 1:
                    if (task.ReadWord(0x24) == profile.TargetReleaseCounter)
                    {
                        uint root = task.ReadDword(0x10);
                        state = state.Write(root, state.Read(root).WriteByte(0x63, 0));
                        state = Next(state, address);
                    }
                    break;
                case 2:
                    if (unchecked((short)task.ReadWord(0x24)) >= 30)
                    {
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        int first = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native892E10/8883C0 requires its selected group's contact pointer.");
                        int contact = checked(first + unchecked((sbyte)task.ReadByte(0x2B)));
                        inputs.ApplyContact?.Invoke(contact);
                        contacts.Add(contact);
                        state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Native88D3B0/883550 signed phase exceeds its four original callbacks.");
            }
            task = state.Read(address);
            if (task.ReadWord(0x24) == 0)
            {
                sounds.Add(new(resources.Sound, 0, null) { Pan = 128 });
            }
            task = state.Read(address).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        }
    }

    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static void RequireResources(NativeBattleOriginalEffectState effect, NativeBattleRestorationResources resources, NativeBattleRestorationProfile profile)
    {
        if (effect.Binding.Effect != profile.Effect || effect.Binding.Entry != profile.Entry || effect.Binding.Initializer != profile.Initializer || resources.Effect != profile.Effect)
        {
            throw new InvalidDataException("Native restoration resources require their own original effect/entry/initializer.");
        }
    }

    private static void RequireController(NativeBattleOriginalEffectState effect, NativeBattleRestorationProfile profile)
    {
        effect.Validate();
        if (effect.Parameters is null || effect.ReturnedDescriptor != profile.Descriptors[0] || effect.Pools.Length != 4)
        {
            throw new InvalidDataException("Native restoration main requires its executed original four-pool entry.");
        }
        ushort[] strides = [100, 88, 676, 64];
        int[] counts = [2, 4, 3, 10];
        for (int index = 0; index < 4; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            if (pool.DescriptorAddress != profile.Descriptors[index] || pool.BankAddress != profile.Banks[index] || pool.Stride != strides[index] || pool.Slots.Length != counts[index])
            {
                throw new InvalidDataException("Native restoration pool does not match its selected physical descriptor/bank/stride/count.");
            }
        }
    }
}
