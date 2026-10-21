using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleAquaController
{
    public static NativeBattleAquaFrame Initialize(NativeBattleAquaFrame frame, NativeBattleAquaResources resources, NativeBattleActionPacketsState packets)
    {
        frame.Effect.Validate();
        packets.Validate();
        resources.Validate();
        frame.State.Validate();
        NativeBattleAquaProfile profile = frame.Profile;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("The Aqua entry requires its captured action parameters.");
        if (frame.Effect.Binding.Entry != profile.Entry || resources.Effect != profile.Effect || frame.State.Effect != profile.Effect || frame.State.WaveMesh is null)
        {
            throw new InvalidDataException("The Aqua entry requires its actual module and retained mutable mesh.");
        }
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.Callback(0x808970), 100);
        NativeBattleModulePoolTask task = root.Hierarchy.Read(root.Address);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, 0x1D99A78, unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        byte contacts = header.ReadByte(0x10);
        byte groups = header.ReadByte(0x11);
        byte maximum = unchecked((byte)(contacts - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        task = task.WriteDword(0x0C, 0x1D99A78).WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target).WriteByte(0x29, 0).WriteWord(0x5A, contacts).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
        hierarchy = root.Hierarchy.Write(root.Address, task);
        int source = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("The Aqua entry requires its executed initializer resource pointer.");
        if (source < 0 || source > NativeBattleEffectArena.Length - 0x18000)
        {
            throw new InvalidDataException("The Aqua working buffers leave their original arena.");
        }
        NativeBattleEffectMemory memory = frame.Memory;
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("The Aqua entry requires its retained texture owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("The Aqua entry requires its loaded texture arena."), source);
            memory = memory with { Textures = textures };
        }
        NativeBattleAquaState state = frame.State with { Source = source, PrimaryBuffer = source, AlternateBuffer = checked(source + 0xC000), PrimaryLimit = checked(source + 0xC000), AlternateLimit = checked(source + 0x18000), BufferEnd = checked(source + 0x18000), ResetCounter = 0, ResetWaveCounter = 0 };
        return (frame with { Memory = memory, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = profile.Pools[0].Descriptor } }).WithState(state);
    }

    public static NativeBattleAquaFrame Advance(NativeBattleAquaFrame frame, NativeBattleAquaResources resources, NativeBattleAquaInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleAquaProfile profile = frame.Profile;
        if (resources.Effect != profile.Effect || frame.State.Effect != profile.Effect)
        {
            throw new InvalidDataException("The Aqua callback resources belong to another module.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("The Aqua root lost its captured action parameters.");
        NativeBattleAquaFrame current = frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            NativeBattleAquaState retained = current.State;
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current.WithState(retained with { PrimaryDrawCursor = odd ? retained.PrimaryBuffer : retained.AlternateBuffer, SecondaryDrawCursor = odd ? retained.PrimaryLimit : retained.AlternateLimit });
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = inputs.PackedCamera ?? NativeBattlePackedMatrix32.FromMatrix(inputs.Camera, inputs.CameraPadding18) } };
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
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = Add(state, 1, 0x808B20, 88, address).Hierarchy;
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
                    state = Done(state, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("The Aqua root leaves its eleven native phases.");
            }
            ushort survivors = 0;
            for (int pool = 1; pool < profile.Pools.Length; pool++)
            {
                NativeBattleEffectHierarchyStep advanced = state.Advance(pool, Child);
                state = advanced.Hierarchy;
                survivors = unchecked((ushort)(survivors + advanced.Survivors));
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
                case 0x808B20:
                    task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(), inputs.ReadPoseSource);
                    int actor = task.ReadByte(0x2D);
                    NativeBattlePoseSource poseSource = inputs.ReadPoseSource(actor);
                    NativeBattleSequenceState sequence = inputs.ReadModel(actor).SequenceState ?? throw new NotSupportedException("The Aqua target requires its current model sequence.");
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, poseSource, poseSource.Sample(sequence.Pose ?? throw new NotSupportedException("The Aqua target requires its actual pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("The Aqua target requires its actual pose frame."), sequence.PoseFlags109));
                    state = state.Write(address, task);
                    switch (Phase(task))
                    {
                        case 0:
                            current = current.WithState(InitializeShared(current.State, task, parameters, inputs));
                            state = Add(state, 2, 0x808DD0, 72, address).Hierarchy;
                            state = Next(state, address);
                            break;
                        case 1:
                            if (current.State.Word(0x48) == 1)
                            {
                                NativeBattleActionHeaderSlot selected = Header(task);
                                int first = selected.FirstContactIndex ?? throw new NotSupportedException("The Aqua target requires its captured contact pointer.");
                                for (int index = 0; index < selected.ReadByte(0x10); index++)
                                {
                                    Emit(ref state, new NativeBattleAquaContact(first + index));
                                }
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
                            throw new InvalidDataException("The Aqua target leaves its five native phases.");
                    }
                    return Finish(state, address);
                case 0x808DD0:
                    current = current.WithState(current.State.WithWord(0x46, current.State.Word(0x46) + 1));
                    if (current.State.Word(0x40) != current.State.Word(0x42))
                    {
                        current = current.WithState(current.State.WithWord(0x40, current.State.Word(0x42)).WithWord(0x46, 0));
                        NativeBattleAquaParticleStep born = NativeBattleAquaParticles.SpawnPhase1(current with { Effect = current.Effect with { Pools = state.Pools } }, state, address, inputs);
                        current = born.Frame;
                        state = born.Hierarchy;
                    }
                    if (current.State.Word(0x42) != current.State.Word(0x44))
                    {
                        current = current.WithState(current.State.WithWord(0x42, current.State.Word(0x44)));
                    }
                    task = state.Read(address);
                    switch (Phase(task))
                    {
                        case 0:
                            state = Add(state, 3, 0x808ED0, 340, address).Hierarchy;
                            if (profile.Effect == 72)
                            {
                                Emit(ref state, new NativeBattleAquaBindCaster(task.ReadByte(0x2C)));
                            }
                            state = state.Write(address, state.Read(address).WriteWord(0x44, profile.Effect == 55 ? (ushort)1 : (ushort)4));
                            state = Next(state, address);
                            break;
                        case 1:
                            task = state.Read(address).WriteWord(0x44, unchecked((ushort)(task.ReadWord(0x44) - 1)));
                            state = state.Write(address, task);
                            if (Word(task, 0x44) <= 0 && RequestPhase(1))
                            {
                                state = Next(state, address);
                            }
                            break;
                        case 2:
                            if (current.State.Word(0x40) >= 1)
                            {
                                Emit(ref state, new NativeBattleAquaSound(new(resources.Sound, 0, null) { Pan = 128 }));
                                state = Next(state, address);
                            }
                            break;
                        case 3:
                            if (current.State.Word(0x46) > profile.ContactThreshold)
                            {
                                current = current.WithState(current.State.WithWord(0x48, 1));
                                state = Next(state, address);
                            }
                            break;
                        case 4:
                            if (RequestPhase(2))
                            {
                                state = Next(state, address);
                            }
                            break;
                        case 5:
                            state = Done(state, address);
                            break;
                        case 6:
                            break;
                        default:
                            throw new InvalidDataException("The Aqua controller leaves its seven native phases.");
                    }
                    return Finish(state, address);
                case 0x808ED0:
                    switch (Phase(task))
                    {
                        case 0:
                            state = state.Write(address, task.WriteWord(0x1C, 0));
                            Emit(ref state, new NativeBattleAquaStage(0, true));
                            state = Next(state, address);
                            break;
                        case 1:
                            short increased = unchecked((short)(Word(task, 0x1C) + 512));
                            if (increased >= 1536)
                            {
                                increased = 1536;
                                state = Next(state, address);
                            }
                            state = state.Write(address, state.Read(address).WriteWord(0x1C, unchecked((ushort)increased)));
                            Emit(ref state, new NativeBattleAquaStage(increased, false));
                            break;
                        case 2:
                            if (Word(task, 0x24) > 40)
                            {
                                state = Next(state, address);
                            }
                            break;
                        case 3:
                            short decreased = unchecked((short)(Word(task, 0x1C) - 256));
                            if (decreased <= 0)
                            {
                                decreased = 0;
                                state = Done(state, address);
                            }
                            state = state.Write(address, state.Read(address).WriteWord(0x1C, unchecked((ushort)decreased)));
                            Emit(ref state, new NativeBattleAquaStage(decreased, false));
                            break;
                        case 4:
                            break;
                        default:
                            throw new InvalidDataException("The Aqua stage fade leaves its five native phases.");
                    }
                    return Finish(state, address);
                default:
                    NativeBattleAquaParticleStep particle = NativeBattleAquaParticles.Advance(current with { Effect = current.Effect with { Pools = state.Pools } }, state, address, resources, inputs);
                    current = particle.Frame;
                    return new(particle.Hierarchy, particle.Result);
            }
        }

        bool RequestPhase(short requested)
        {
            NativeBattleAquaState shared = current.State;
            if (shared.Word(0x44) == shared.Word(0x40) && shared.Word(0x40) == requested - 1)
            {
                current = current.WithState(shared.WithWord(0x44, requested));
                return true;
            }
            return false;
        }

        NativeBattleActionHeaderSlot Header(NativeBattleModulePoolTask task) => inputs.Packets.Headers[parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A))];
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();

        NativeBattleEffectHierarchyAppend Add(NativeBattleEffectHierarchy state, int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend child = state.Append8DC540(pool, profile.Callback(callback), size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            if (child.Address == 0)
            {
                throw new InvalidOperationException("The Aqua module exhausted an unguarded native task allocation.");
            }
            return child;
        }

        void Emit(ref NativeBattleEffectHierarchy state, NativeBattleAquaOperation operation)
        {
            current = inputs.Execute(current with { Effect = current.Effect with { Pools = state.Pools } }, operation);
            state = new(current.Effect.Pools);
        }
    }

    public static NativeBattleAquaState InitializeShared(NativeBattleAquaState retained, NativeBattleModulePoolTask task, NativeBattleEffectParameters parameters, NativeBattleAquaInputs inputs)
    {
        NativeBattleAquaState state = retained with { Shared84 = new byte[84].ToImmutableArray() };
        NativeBattleVector[] enemies = Enumerable.Range(3, 4).Select(inputs.ReadModel).Where(model => (model.Flags0 & 2) != 0).Select(model => model.Transform?.Position28 ?? throw new NotSupportedException("The Aqua enemy center requires its actual physical position.")).ToArray();
        short centerX = enemies.Length == 0 ? (short)0 : unchecked((short)((enemies.Min(point => point.X) + enemies.Max(point => point.X)) / 2));
        short centerZ = enemies.Length == 0 ? (short)0 : unchecked((short)((enemies.Min(point => point.Z) + enemies.Max(point => point.Z)) / 2));
        NativeBattleModelTransform caster = inputs.ReadModel(task.ReadByte(0x2C)).Transform ?? throw new NotSupportedException("The Aqua caster requires its actual physical position.");
        NativeBattleVector position = caster.Position28;
        state = state.WithWord(0, position.X).WithWord(2, position.Y).WithWord(4, position.Z).WithWord(6, caster.Vertical34).WithWord(8, centerX).WithWord(10, 0).WithWord(12, centerZ);
        short dx = unchecked((short)(centerX - position.X));
        short dz = unchecked((short)(centerZ - position.Z));
        int angle = NativePrecision53.RoundInteger(Math.Atan2(dx, dz) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        state = state.WithWord(0x4A, (angle - 0x800) & 0xFFF);
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A))];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("The Aqua distance requires its captured contact pointer.");
        int distance = 0;
        for (int index = 0; index < header.ReadByte(0x10); index++)
        {
            NativeBattleVector target = inputs.ReadModel(inputs.Packets.ReadContact(first + index).Primary.ActorIndex).Transform?.Position28 ?? throw new NotSupportedException("The Aqua distance requires each captured target position.");
            int x = target.X - position.X;
            int z = target.Z - position.Z;
            distance = Math.Max(distance, NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(x * x + z * z)));
        }
        return state.WithDword(0x3C, Math.Clamp(distance, 4096, 8192));

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
