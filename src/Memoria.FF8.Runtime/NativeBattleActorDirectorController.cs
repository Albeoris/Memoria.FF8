using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleActorDirectorController
{
    public static NativeBattleActorDirectorFrame Initialize(NativeBattleActorDirectorFrame frame, NativeBattleActorDirectorResources resources, NativeBattleActionPacketsState packets)
    {
        frame.Effect.Validate();
        packets.Validate();
        resources.Validate();
        frame.State.Validate();
        NativeBattleActorDirectorProfile profile = frame.Profile;
        if (resources.Effect != profile.Effect || frame.State.Effect != profile.Effect || frame.Effect.Binding.Entry != profile.Entry || frame.Effect.Binding.Initializer != profile.Initializer)
        {
            throw new InvalidDataException("The actor director entry requires its original module profile.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("The actor director requires its captured parameters.");
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(0, profile.Callback(0x8AB290), 100);
        NativeBattleModulePoolTask task = append.Hierarchy.Read(append.Address);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, 0x1D99A78, unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        byte targets = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(targets - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        task = task.WriteDword(0x0C, 0x1D99A78).WriteByte(0x29, 0).WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
        hierarchy = append.Hierarchy.Write(append.Address, task);
        int source = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("The actor director requires its executed initializer pointer.");
        if (source < 0 || source > NativeBattleEffectArena.Length - 0x10000)
        {
            throw new InvalidDataException("The actor director draw banks leave the current arena.");
        }
        NativeBattleEffectMemory memory = frame.Memory;
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("The actor director requires its retained texture owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("The actor director requires its loaded texture arena."), source);
            memory = memory with { Textures = textures };
        }
        NativeBattleCuragaState generic = frame.State.Generic with { SourceArenaOffset = source, AuxiliaryCursor = checked(source + 0x10000), Counter26D82F8 = 0, Counter26D76C4 = 0 };
        return (frame with { Memory = memory, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = profile.Pools[0].Descriptor } }).WithState(frame.State with { Generic = generic });
    }

    public static NativeBattleActorDirectorFrame Advance(NativeBattleActorDirectorFrame frame, NativeBattleActorDirectorResources resources, NativeBattleActorDirectorInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleActorDirectorProfile profile = frame.Profile;
        if (resources.Effect != profile.Effect || frame.State.Effect != profile.Effect || frame.Effect.Pools.Length != 6 || frame.Effect.ReturnedDescriptor != profile.Pools[0].Descriptor)
        {
            throw new InvalidDataException("The actor director callback requires its executed original pools and resources.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("The actor director lost its captured parameters.");
        for (int index = 0; index < profile.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = profile.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("The actor director task bank contradicts its original physical profile.");
            }
        }
        NativeBattleActorDirectorFrame current = frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.Callback(0x8AB290))
            {
                throw new InvalidDataException("The actor director root requires its original callback.");
            }
            int source = current.State.Generic.SourceArenaOffset;
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            NativeBattlePackedMatrix32 copiedCamera = inputs.PackedCamera ?? NativeBattlePackedMatrix32.FromMatrix(inputs.Camera, inputs.CopiedCameraPadding);
            current = (current with { Memory = current.Memory with { SharedEffectView2793E58 = copiedCamera } }).WithState(current.State with { PrimaryDrawCursor = checked(source + (odd ? 0 : 0x8000)), SecondaryDrawCursor = checked(source + (odd ? 0x8000 : 0x10000)), Generic = current.State.Generic with { CopiedCamera = copiedCamera.ReadMatrix() } });
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(), inputs.ReadPoseSource);
            state = state.Write(address, task);
            switch (Phase(task))
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
                        int emitters = current.State.Generic.AuxiliaryCursor;
                        int geometry = checked(emitters + profile.EmitterBytes);
                        int end = checked(geometry + profile.GeometryBytes);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("The actor director requires its current node arena.");
                        arena = arena.Write(emitters, new byte[profile.EmitterBytes]).Write(geometry, new byte[profile.GeometryBytes]);
                        current = (current with { Memory = current.Memory with { Arena = arena } }).WithState(current.State with { Generic = current.State.Generic with { Auxiliary25F8 = emitters, Auxiliary16F80 = geometry, AuxiliaryCursor = end, Counter26D76C0 = 0, Counter26D82E8 = 0, Counter26D82D8 = 0, Counter26D75C4 = 0 } });
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = Add(state, 1, 0x8AB4C0, 88, address).Hierarchy;
                    state = Next(state, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(0x2A)) < Word(task, 0x58))
                        {
                            state = state.Write(address, task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1))));
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
                    state = Done(state, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("The actor director root leaves its original phase table.");
            }
            current = current.WithState(current.State with { Generic = current.State.Generic with { PassCount26D82EA = 0, PassCount26D82DA = 0 } });
            ushort survivors = 0;
            for (int pool = 1; pool < 6; pool++)
            {
                NativeBattleEffectHierarchyStep children = state.Advance(pool, Child);
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
            }
            task = state.Read(address);
            state = state.Write(address, task.WriteWord(0x5E, survivors).WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))));
            return Finish(state, address);
        });
        return current with { Effect = current.Effect with { Pools = roots.Hierarchy.Pools, ReturnedDescriptor = roots.Survivors == 0 ? 0u : profile.Pools[0].Descriptor } };

        NativeBattleEffectHierarchyCallback Child(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            switch (profile.CanonicalCallback(task.Callback))
            {
                case 0x8AB4C0:
                    task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(), inputs.ReadPoseSource);
                    int actor = task.ReadByte(0x2D);
                    NativeBattlePoseSource source = inputs.ReadPoseSource(actor);
                    NativeBattleSequenceState pose = inputs.ReadModel(actor).SequenceState ?? throw new NotSupportedException("The actor director target requires its actual pose.");
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(pose.Pose ?? throw new NotSupportedException("The actor director target lost its pose selector."), pose.PoseFrame114 ?? throw new NotSupportedException("The actor director target lost its pose frame."), pose.PoseFlags109));
                    state = state.Write(address, task);
                    switch (Phase(task))
                    {
                        case 0:
                            current = current.WithState(InitializeShared(current.State, task, inputs));
                            state = Add(state, 2, 0x8AB600, 72, address).Hierarchy;
                            state = Next(state, address);
                            break;
                        case 1:
                            if (current.State.Word(0x48) == 1)
                            {
                                int header = parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
                                int first = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("The actor director contact requires its physical header.");
                                Emit(ref state, new NativeBattleActorDirectorContact(first + unchecked((sbyte)task.ReadByte(0x2B))));
                                state = Next(state, address);
                            }
                            break;
                        case 2:
                            if (task.ReadByte(0x28) == 0 || current.State.Word(0x4C) == 1)
                            {
                                state = Next(state, address);
                            }
                            break;
                        case 3:
                            uint root = task.ReadDword(0x10);
                            state = state.Write(root, state.Read(root).WriteByte(0x63, 0));
                            state = Done(state, address);
                            break;
                        case 4:
                            break;
                        default:
                            throw new InvalidDataException("The actor director target leaves its original phase table.");
                    }
                    return Finish(state, address);
                case 0x8AB600:
                    current = current.WithState(current.State.WithWord(0x46, current.State.Word(0x46) + 1));
                    if (current.State.Word(0x40) != current.State.Word(0x42))
                    {
                        current = current.WithState(current.State.WithWord(0x40, current.State.Word(0x42)).WithWord(0x46, 0));
                        if (current.State.Word(0x40) == 1)
                        {
                            NativeBattleEffectHierarchyAppend generic = Add(state, 3, 0x8AB890, 676, address);
                            state = generic.Hierarchy.Write(generic.Address, generic.Hierarchy.Read(generic.Address).WriteDword(0x30, profile.SourceAddress).WriteWord(0x298, 4).WriteWord(0x29C, 0).WriteWord(0x29E, 15));
                            state = Add(state, 4, 0x8B0660, 140, address).Hierarchy;
                            Emit(ref state, new NativeBattleActorDirectorGenericTask(generic.Address));
                        }
                    }
                    if (current.State.Word(0x42) != current.State.Word(0x44))
                    {
                        current = current.WithState(current.State.WithWord(0x42, current.State.Word(0x44)));
                    }
                    task = state.Read(address);
                    switch (Phase(task))
                    {
                        case 0:
                            if (RequestPhase(1))
                            {
                                state = Next(state, address);
                            }
                            break;
                        case 1:
                            if (current.State.Word(0x40) >= 1)
                            {
                                Emit(ref state, new NativeBattleActorDirectorSound(new(resources.Sound, 0, null) { Pan = 128 }));
                                state = Next(state, address);
                            }
                            break;
                        case 2:
                            if (current.State.Word(0x46) >= 40)
                            {
                                current = current.WithState(current.State.WithWord(0x48, 1));
                                state = Next(state, address);
                            }
                            break;
                        case 3:
                            if (RequestPhase(2))
                            {
                                state = Next(state, address);
                            }
                            break;
                        case 4:
                            state = Done(state, address);
                            break;
                        case 5:
                            break;
                        default:
                            throw new InvalidDataException("The actor director leaves its original phase table.");
                    }
                    return Finish(state, address);
                case 0x8AB890:
                    NativeBattleActorDirectorLeafStep step = inputs.AdvanceGeneric(current with { Effect = current.Effect with { Pools = state.Pools } }, address);
                    current = step.Frame;
                    return new(new(current.Effect.Pools), step.Result);
                case 0x8B0660:
                    switch (Phase(task))
                    {
                        case 0:
                            state = state.Write(address, task.WriteWord(0x1C, 0));
                            Emit(ref state, new NativeBattleActorDirectorStage(0, true));
                            state = Next(state, address);
                            break;
                        case 1:
                            short up = unchecked((short)(Word(task, 0x1C) + 512));
                            if (up >= 2048)
                            {
                                up = 2048;
                                state = Next(state, address);
                            }
                            state = state.Write(address, state.Read(address).WriteWord(0x1C, unchecked((ushort)up)));
                            Emit(ref state, new NativeBattleActorDirectorStage(up, false));
                            break;
                        case 2:
                            if (Word(task, 0x24) >= 40)
                            {
                                state = Next(state, address);
                            }
                            break;
                        case 3:
                            short down = unchecked((short)(Word(task, 0x1C) - 256));
                            if (down <= 0)
                            {
                                down = 0;
                                state = Done(state, address);
                            }
                            state = state.Write(address, state.Read(address).WriteWord(0x1C, unchecked((ushort)down)));
                            Emit(ref state, new NativeBattleActorDirectorStage(down, false));
                            break;
                        case 4:
                            break;
                        default:
                            throw new InvalidDataException("The actor director stage task leaves its original phase table.");
                    }
                    return Finish(state, address);
                default:
                    throw new NotSupportedException("The actor director encountered an unsupported original callback.");
            }
        }

        bool RequestPhase(short requested)
        {
            if (current.State.Word(0x44) != current.State.Word(0x40) || current.State.Word(0x40) != requested - 1)
            {
                return false;
            }
            current = current.WithState(current.State.WithWord(0x44, requested));
            return true;
        }

        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleEffectHierarchyAppend Add(NativeBattleEffectHierarchy state, int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend result = state.Append8DC540(pool, profile.Callback(callback), size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            return result.Address != 0 ? result : throw new NotSupportedException("The actor director requires its original mandatory child allocation.");
        }
        void Emit(ref NativeBattleEffectHierarchy state, NativeBattleActorDirectorOperation operation)
        {
            current = inputs.Execute(current with { Effect = current.Effect with { Pools = state.Pools } }, operation);
            state = new(current.Effect.Pools);
        }
    }

    public static NativeBattleActorDirectorState InitializeShared(NativeBattleActorDirectorState retained, NativeBattleModulePoolTask task, NativeBattleActorDirectorInputs inputs)
    {
        NativeBattleModelTransform actor = inputs.ReadModel(task.ReadByte(0x2C)).Transform ?? throw new NotSupportedException("The actor director requires its actual source position.");
        NativeBattleModelTransform target = inputs.ReadModel(task.ReadByte(0x2D)).Transform ?? throw new NotSupportedException("The actor director requires its actual target position.");
        NativeBattleActorDirectorState state = retained with { Shared84 = new byte[84].ToImmutableArray() };
        state = state.WithWord(0, actor.Position28.X).WithWord(2, actor.Position28.Y).WithWord(4, actor.Position28.Z).WithWord(6, actor.Vertical34).WithWord(8, target.Position28.X).WithWord(10, target.Position28.Y).WithWord(12, target.Position28.Z).WithWord(14, target.Vertical34);
        short dx = unchecked((short)(target.Position28.X - actor.Position28.X));
        short dz = unchecked((short)(target.Position28.Z - actor.Position28.Z));
        int angle = NativePrecision53.RoundInteger(Math.Atan2(dx, dz) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        return state.WithWord(0x4A, (angle - 0x800) & 0xFFF);
    }

    private static int Phase(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy state, uint address) => state.Write(address, state.Read(address).WriteByte(0x29, unchecked((byte)(state.Read(address).ReadByte(0x29) + 1))));
    private static NativeBattleEffectHierarchy Done(NativeBattleEffectHierarchy state, uint address) => Next(state.Write(address, state.Read(address).WriteByte(0x26, (byte)(state.Read(address).ReadByte(0x26) | 1))), address);
    private static NativeBattleEffectHierarchyCallback Finish(NativeBattleEffectHierarchy state, uint address)
    {
        NativeBattleModulePoolTask task = state.Read(address).WriteWord(0x24, unchecked((ushort)(state.Read(address).ReadWord(0x24) + 1)));
        state = state.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
    }
}
