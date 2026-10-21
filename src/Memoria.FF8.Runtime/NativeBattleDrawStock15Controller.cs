using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDrawStock15Controller
{
    public static NativeBattleDrawStock15Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleDrawStock15Resources resources)
    {
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 15 || effect.Binding.Entry != NativeBattleDrawStock15Profile.Entry)
        {
            throw new InvalidDataException("Draw Stock entry requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Draw Stock entry requires its captured action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDrawStock15Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8B0A50, 100);
        NativeBattleModulePoolTask task = InitializeRoot(root.Hierarchy.Read(root.Address), parameters, packets);
        hierarchy = root.Hierarchy.Write(root.Address, task);
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == NativeBattleDrawStock15Profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Draw Stock requires its executed initializer resource pointer.");
        if (source < 0 || source > NativeBattleEffectArena.Length - 2 * NativeBattleDrawStock15Profile.DrawStep)
        {
            throw new InvalidDataException("Draw Stock initializer-relative banks leave their original arena.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Draw Stock requires its retained texture registration owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Draw Stock requires its loaded effect arena."), source);
            memory = memory with { Textures = textures };
        }
        NativeBattleDrawStock15State state = new(new(source, null, null, checked(source + 2 * NativeBattleDrawStock15Profile.DrawStep), null));
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x2732CE8 }, random, state);
    }

    public static NativeBattleDrawStock15Frame Advance(NativeBattleDrawStock15Frame frame, NativeBattleDrawStock15Resources resources, NativeBattleDrawStock15Inputs inputs)
    {
        RequireFrame(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        resources.Validate();
        inputs.Camera.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleDrawStock15Frame current = frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep main = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != 0x8B0A50)
            {
                throw new NotSupportedException("Draw Stock root pool contains another callback.");
            }
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            NativeBattleCuragaState nodes = current.State.Nodes;
            NativeBattlePackedMatrix32 copiedCamera = inputs.PackedCamera ?? NativeBattlePackedMatrix32.FromMatrix(inputs.Camera, inputs.CameraPadding18);
            nodes = nodes with { CopiedCamera = copiedCamera.ReadMatrix(), PrimaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? 0 : NativeBattleDrawStock15Profile.DrawStep)), SecondaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? NativeBattleDrawStock15Profile.DrawStep : 2 * NativeBattleDrawStock15Profile.DrawStep)) };
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = copiedCamera }, State = current.State with { Nodes = nodes } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(), inputs.ReadPoseSource);
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                case 1:
                case 5:
                case 7:
                case 8:
                    state = Next(state, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        nodes = current.State.Nodes;
                        int first = nodes.AuxiliaryCursor;
                        int second = checked(first + 0x438);
                        int end = checked(second + 0x6900);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Draw Stock requires its actual auxiliary arena.");
                        arena = arena.Write(first, new byte[0x438]).Write(second, new byte[0x6900]);
                        nodes = nodes with { Auxiliary25F8 = first, Auxiliary16F80 = second, AuxiliaryCursor = end, Counter26D76C0 = 0, Counter26D82E8 = 0, Counter26D82D8 = 0, Counter26D75C4 = 0 };
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Nodes = nodes } };
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = state.Append8DC540(1, 0x8B0C70, 88, address, Select).Hierarchy;
                    state = Next(state, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(0x2A)) < Word(task, 0x58))
                        {
                            task = task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
                            state = state.Write(address, task);
                        }
                        else
                        {
                            state = Next(state, address);
                        }
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
                    throw new InvalidDataException("Draw Stock signed root phase leaves its eleven original handlers.");
            }
            state = state.Write(address, state.Read(address).WriteWord(0x5E, 0));
            current = current with { State = current.State with { Nodes = current.State.Nodes with { PassCount26D82EA = 0, PassCount26D82DA = 0 } } };
            int survivors = 0;
            for (int pool = 1; pool < 5; pool++)
            {
                NativeBattleEffectHierarchyStep children = state.Advance(pool, Child);
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
                state = state.Write(address, state.Read(address).WriteWord(0x5E, (ushort)survivors));
            }
            task = state.Read(address);
            task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = Complete(task);
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        });
        return current with { Effect = current.Effect with { Pools = main.Hierarchy.Pools, ReturnedDescriptor = main.Survivors == 0 ? 0u : 0x2732CE8u } };

        NativeBattleEffectHierarchyCallback Child(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            switch (task.Callback)
            {
                case 0x8B0C70:
                    return Target(state, address);
                case 0x8B0DA0:
                    state = Execute(state, new NativeBattleDrawStock15GenericTask(address));
                    return new(state, Complete(state.Read(address)) ? 2 : 0);
                case 0x8B5AE0:
                case 0x8B5C70:
                case 0x8B6440:
                    NativeBattleDrawStock15ParticleStep step = NativeBattleDrawStock15Particles.Advance(state, address, current.Random, new(parameters.Actor78, Select, inputs.ReadBone241));
                    state = step.Hierarchy;
                    current = current with { Random = step.Random };
                    foreach (NativeBattleDrawStock15ParticleOperation operation in step.Operations)
                    {
                        state = Execute(state, new NativeBattleDrawStock15Particle(operation));
                    }
                    return new(state, step.Result);
                default:
                    throw new NotSupportedException("Draw Stock child pool contains an unsupported original callback.");
            }
        }

        NativeBattleEffectHierarchyCallback Target(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Draw Stock target requires its current pose.");
            NativeBattlePoseSource source = inputs.ReadPoseSource(task.ReadByte(0x2D));
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(sequence.Pose ?? throw new NotSupportedException("Draw Stock target requires its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Draw Stock target requires its current pose frame."), sequence.PoseFlags109));
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    NativeBattleEffectHierarchyAppend generic = state.Append8DC540(2, 0x8B0DA0, 676, address, Select);
                    state = generic.Hierarchy;
                    if (generic.Address == 0)
                    {
                        throw new NotSupportedException("Draw Stock generic allocation returned null before its original stores.");
                    }
                    state = state.Write(generic.Address, state.Read(generic.Address).WriteDword(0x30, 0x17FC68C).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 15));
                    state = state.Append8DC540(3, 0x8B5AE0, 300, address, Select).Hierarchy;
                    state = Next(state, address);
                    break;
                case 1:
                    if (task.ReadWord(0x24) == 15)
                    {
                        uint root = task.ReadDword(0x10);
                        state = state.Write(root, state.Read(root).WriteByte(0x63, 0));
                        state = Next(state, address);
                    }
                    break;
                case 2:
                    if (Word(task, 0x24) >= 15)
                    {
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        int contact = checked((inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Draw Stock requires its selected physical contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        state = Execute(state, new NativeBattleDrawStock15Contact(contact));
                        task = state.Read(address).WriteByte(0x26, (byte)(state.Read(address).ReadByte(0x26) | 1));
                        state = state.Write(address, task);
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Draw Stock signed target phase leaves its four original handlers.");
            }
            if (state.Read(address).ReadWord(0x24) == 0)
            {
                foreach (uint sound in new uint[] { 0x73, 0x74, 0x75 })
                {
                    state = Execute(state, new NativeBattleDrawStock15Sound(new(sound, 0, null) { Pan = 128 }));
                }
            }
            task = state.Read(address);
            task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = Complete(task);
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        }

        NativeBattleEffectHierarchy Execute(NativeBattleEffectHierarchy state, NativeBattleDrawStock15Operation operation)
        {
            current = current with { Effect = current.Effect with { Pools = state.Pools } };
            current = inputs.Execute(current, operation);
            return new(current.Effect.Pools);
        }

        NativeBattleGraphicsModel[] Models()
        {
            return Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        }

        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target)
        {
            return NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        }
    }

    internal static NativeBattleModulePoolTask InitializeRoot(NativeBattleModulePoolTask task, NativeBattleEffectParameters parameters, NativeBattleActionPacketsState packets)
    {
        task = task.WriteDword(0x0C, 0x1D99A78);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, task.ReadDword(0x0C), unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        byte targets = header.ReadByte(0x10);
        byte groups = header.ReadByte(0x11);
        byte maximum = unchecked((byte)(targets - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        return task.WriteByte(0x2C, actors.Actor).WriteByte(0x29, 0).WriteByte(0x2D, actors.Target).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
    }

    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static bool Complete(NativeBattleModulePoolTask task)
    {
        return (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset)
    {
        return unchecked((short)task.ReadWord(offset));
    }

    private static void RequireFrame(NativeBattleDrawStock15Frame frame)
    {
        frame.Effect.Validate();
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 15 || frame.Effect.Binding.Entry != NativeBattleDrawStock15Profile.Entry || frame.Effect.Parameters is null || frame.Effect.ReturnedDescriptor is not (0 or 0x2732CE8) || frame.Effect.Pools.Length != 5)
        {
            throw new InvalidDataException("Draw Stock requires its executed original root controller.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < 5; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleDrawStock15Profile.Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Draw Stock hierarchy contradicts its actual physical pool owners.");
            }
        }
    }
}
